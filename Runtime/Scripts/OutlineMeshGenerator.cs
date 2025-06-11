using UnityEngine;
using System.Collections.Generic;
using System.IO;
using UnityEngine.XR;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace OutlineMesh
{
    [ExecuteAlways, RequireComponent(typeof(MeshFilter))]
    public class OutlineMeshGenerator : MonoBehaviour
    {
        public class MeshData
        {
            public List<int> Indices;
            public List<Vector3> Vertices;
            public List<Vector3> Normals;

            public MeshData(List<int> indices, List<Vector3> vertices, List<Vector3> normals)
            {
                Indices = indices;
                Vertices = vertices;
                Normals = normals;
            }
        }

        [SerializeField] private OutlineMeshSettings _meshInfo;
        [SerializeField] private List<MeshFilter> _meshFilters;
        [SerializeField] private Mesh _cachedMeshAsset;
        [SerializeField, HideInInspector] private MeshFilter _outlineMeshFilter;
        [SerializeField, HideInInspector] private long _regenerateMeshTimestampTicks;

        private Mesh _outlineMesh;

        public MeshFilter OutlineMeshFilter => _outlineMeshFilter;
        public OutlineMeshSettings MeshInfo => _meshInfo;

        protected void OnEnable()
        {
            if (_meshInfo == null)
            {
                return;
            }

            if (_meshInfo.RegenerateMeshTimestampTicks != _regenerateMeshTimestampTicks || !LoadMesh())
            {
                RegenerateMesh();
                _regenerateMeshTimestampTicks = _meshInfo.RegenerateMeshTimestampTicks;
            }
        }

        protected void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && _meshInfo != null && _meshInfo.RegenerateMeshTimestampTicks != _regenerateMeshTimestampTicks)
            {
                RegenerateMesh();
                _regenerateMeshTimestampTicks = _meshInfo.RegenerateMeshTimestampTicks;
            }
#endif
        }


        [ContextMenu("Load Cache If Exist")]
        public bool LoadMesh()
        {
            _outlineMeshFilter = _outlineMeshFilter ?? GetComponent<MeshFilter>();
            if (_meshFilters == null || _meshFilters.Count == 0 || _meshInfo == null)
                return false;

            if (_cachedMeshAsset != null)
            {
                _outlineMeshFilter.sharedMesh = _cachedMeshAsset;
                return true;
            }

            string relativePath = "Assets/" + _meshInfo.RelativePathToCacheFolder.Trim('/', '\\');
            string fileName = GenerateCacheFileName() + ".asset";
            string assetPath = Path.Combine(relativePath, fileName).Replace("\\", "/");

#if UNITY_EDITOR
            Mesh loaded = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (loaded != null)
            {
                _outlineMeshFilter.sharedMesh = loaded;
                _cachedMeshAsset = loaded;
                return true;
            }
#endif
            return false;
        }

        [ContextMenu("Regenerate Mesh And Cache")]
        public void RegenerateMesh()
        {
            _outlineMeshFilter = _outlineMeshFilter ?? GetComponent<MeshFilter>();

            if (_meshFilters == null
              || _meshFilters.Count == 0
              || _meshInfo == null)
            {
                return;
            }
            
            float threshold = _meshInfo.VerticesMergeDistanceThreshold;
            string relPath = "Assets/" + _meshInfo.RelativePathToCacheFolder.Trim('/', '\\');
            string absPath = Path.Combine(Application.dataPath, _meshInfo.RelativePathToCacheFolder.Trim('/', '\\'));
            if (!Directory.Exists(absPath))
                Directory.CreateDirectory(absPath);

#if UNITY_EDITOR
            if (_cachedMeshAsset != null)
            {
                string oldAssetPath = AssetDatabase.GetAssetPath(_cachedMeshAsset);
                if (!string.IsNullOrEmpty(oldAssetPath))
                {
                    AssetDatabase.DeleteAsset(oldAssetPath);
                    AssetDatabase.Refresh();
                }
                _cachedMeshAsset = null;
            }
#endif

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            int offset = 0;

            foreach (var mf in _meshFilters)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                var merged = MergeVertices(mf.sharedMesh, threshold);
                var t = mf.transform;
                for (int i = 0; i < merged.Vertices.Count; i++)
                {
                    Vector3 worldPos = t.TransformPoint(merged.Vertices[i]);
                    Vector3 localPos = transform.InverseTransformPoint(worldPos);
                    Vector3 worldNorm = t.TransformDirection(merged.Normals[i]).normalized;
                    vertices.Add(localPos + worldNorm * (_meshInfo.MeshWidth / 10f));
                    normals.Add(worldNorm);
                }
                foreach (int idx in merged.Indices)
                    triangles.Add(offset + idx);
                offset += merged.Vertices.Count;
            }

            if (_outlineMesh == null) _outlineMesh = new Mesh { name = GenerateCacheFileName() };
            else { _outlineMesh.Clear(); _outlineMesh.name = GenerateCacheFileName(); }

            _outlineMesh.SetVertices(vertices);
            _outlineMesh.SetNormals(normals);
            _outlineMesh.SetTriangles(triangles, 0);
            _outlineMesh.RecalculateBounds();

            _outlineMeshFilter.sharedMesh = _outlineMesh;

#if UNITY_EDITOR
            string fileName = GenerateCacheFileName() + ".asset";
            string assetPath = Path.Combine(relPath, fileName).Replace("\\", "/");
            var assetMesh = Object.Instantiate(_outlineMesh);
            assetMesh.name = _outlineMesh.name;
            AssetDatabase.CreateAsset(assetMesh, assetPath);
            AssetDatabase.Refresh();
            _cachedMeshAsset = assetMesh;
#endif
        }

        private string GenerateCacheFileName()
        {
            var key = string.Join("_", _meshFilters.ConvertAll(m => m.sharedMesh != null ? m.sharedMesh.name : "null")).Replace(' ', '_');
            return $"{gameObject.name}_{key}";
        }

        private MeshData MergeVertices(Mesh mesh, float threshold)
        {
            var oldV = mesh.vertices;
            var oldN = mesh.normals;
            var oldI = mesh.triangles;
            float inv = 1f / threshold;
            var cellIndex = new Dictionary<Vector3Int, int>();
            var posSum = new List<Vector3>();
            var normSum = new List<Vector3>();
            var count = new List<int>();
            int[] map = new int[oldV.Length];

            for (int i = 0; i < oldV.Length; i++)
            {
                var p = oldV[i];
                var n = oldN.Length > 0 ? oldN[i] : Vector3.zero;
                var cell = new Vector3Int(
                    Mathf.FloorToInt(p.x * inv),
                    Mathf.FloorToInt(p.y * inv),
                    Mathf.FloorToInt(p.z * inv));
                if (!cellIndex.TryGetValue(cell, out int idx))
                {
                    idx = posSum.Count;
                    cellIndex[cell] = idx;
                    posSum.Add(p);
                    normSum.Add(n);
                    count.Add(1);
                }
                else
                {
                    posSum[idx] += p;
                    normSum[idx] += n;
                    count[idx]++;
                }
                map[i] = idx;
            }

            var verts = new List<Vector3>(posSum.Count);
            var norms = new List<Vector3>(posSum.Count);
            for (int i = 0; i < posSum.Count; i++)
            {
                verts.Add(posSum[i] / count[i]);
                norms.Add(normSum[i].normalized);
            }

            var idxs = new List<int>();
            for (int i = 0; i < oldI.Length; i += 3)
            {
                int a = map[oldI[i]];
                int b = map[oldI[i + 1]];
                int c = map[oldI[i + 2]];
                if (a == b || b == c || c == a) continue;
                idxs.AddRange(new[] { a, b, c });
            }

            return new MeshData(idxs, verts, norms);
        }
    }
}
