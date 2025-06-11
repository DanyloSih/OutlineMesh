using UnityEngine;
using System.Collections.Generic;

namespace AlienSlayerVR.Effects
{
    [CreateAssetMenu(
        fileName = nameof(OutlineMeshInfo),
        menuName = "OutlineMesh/" + nameof(OutlineMeshInfo))]
    public class OutlineMeshInfo : ScriptableObject
    {
        public float VerticesMergeDistanceThreshold;
        [Tooltip("\"Relative\" means that the path starts from the Assets folder, for example:\n" +
            "Your full path: D:\\Projects\\MyProject\\Assets\\SomeFolder\\Cash\n" +
            "Your relative path: SomeFolder\\Cash")]
        public string RelativePathToCashFolder;
    }

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

        public float Width;

        [SerializeField] private List<MeshFilter> _meshFilters;
        [SerializeField] private float _verticesMergeDistanceThreshold;

        private MeshFilter _outlineMeshFilter;
        private Mesh _outlineMesh;
        private MeshRenderer _outlineMeshRenderer;

        protected void Awake()
        {
            GenerateOutlineMesh();
        }

        [ContextMenu("Generate Outline Mesh")]
        public void GenerateOutlineMesh()
        {
            _outlineMeshFilter = _outlineMeshFilter ?? GetComponent<MeshFilter>();
            if (_meshFilters == null || _meshFilters.Count == 0)
            {
                return;
            }

            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<int> triangles = new List<int>();

            int vertexOffset = 0;

            foreach (MeshFilter mf in _meshFilters)
            {
                if (mf == null || mf.sharedMesh == null)
                {
                    continue;
                }

                Transform sourceTransform = mf.transform;
                MeshData mergedMesh = MergeVertices(mf.sharedMesh, _verticesMergeDistanceThreshold);

                for (int i = 0; i < mergedMesh.Vertices.Count; i++)
                {
                    Vector3 worldPos = sourceTransform.TransformPoint(mergedMesh.Vertices[i]);
                    Vector3 localPos = transform.InverseTransformPoint(worldPos);
                    Vector3 worldNormal = sourceTransform.TransformDirection(mergedMesh.Normals[i]).normalized;

                    Vector3 displaced = localPos + worldNormal * (Width / 10f);
                    vertices.Add(displaced);
                    normals.Add(worldNormal);
                }

                for (int i = 0; i < mergedMesh.Indices.Count; i++)
                {
                    triangles.Add(vertexOffset + mergedMesh.Indices[i]);
                }

                vertexOffset += mergedMesh.Vertices.Count;
            }

            if (_outlineMesh == null)
            {
                _outlineMesh = new Mesh();
                _outlineMesh.name = "OutlineMesh";
            }
            else
            {
                _outlineMesh.Clear();
            }

            _outlineMesh.SetVertices(vertices);
            _outlineMesh.SetNormals(normals);
            _outlineMesh.SetTriangles(triangles, 0);
            _outlineMesh.RecalculateBounds();

            _outlineMeshFilter.sharedMesh = _outlineMesh;
        }

        private MeshData MergeVertices(Mesh mesh, float threshold)
        {
            Vector3[] oldVertices = mesh.vertices;
            Vector3[] oldNormals = mesh.normals;
            int[] oldIndices = mesh.triangles;

            float inverseThreshold = 1.0f / threshold;

            Dictionary<Vector3Int, int> cellToIndex = new Dictionary<Vector3Int, int>();
            List<Vector3> positionSums = new List<Vector3>();
            List<Vector3> normalSums = new List<Vector3>();
            List<int> vertexCounts = new List<int>();
            int[] vertexMap = new int[oldVertices.Length];

            for (int i = 0; i < oldVertices.Length; i++)
            {
                Vector3 position = oldVertices[i];
                Vector3 normal = oldNormals.Length > 0 ? oldNormals[i] : Vector3.zero;

                Vector3Int cell = new Vector3Int(
                    Mathf.FloorToInt(position.x * inverseThreshold),
                    Mathf.FloorToInt(position.y * inverseThreshold),
                    Mathf.FloorToInt(position.z * inverseThreshold)
                );

                if (!cellToIndex.TryGetValue(cell, out int newIndex))
                {
                    newIndex = positionSums.Count;
                    cellToIndex[cell] = newIndex;
                    positionSums.Add(position);
                    normalSums.Add(normal);
                    vertexCounts.Add(1);
                }
                else
                {
                    positionSums[newIndex] += position;
                    normalSums[newIndex] += normal;
                    vertexCounts[newIndex]++;
                }

                vertexMap[i] = newIndex;
            }

            List<Vector3> newVertices = new List<Vector3>(positionSums.Count);
            List<Vector3> newNormals = new List<Vector3>(positionSums.Count);

            for (int i = 0; i < positionSums.Count; i++)
            {
                newVertices.Add(positionSums[i] / vertexCounts[i]);
                newNormals.Add(normalSums[i].normalized);
            }

            List<int> newIndices = new List<int>(oldIndices.Length);

            for (int i = 0; i < oldIndices.Length; i += 3)
            {
                int index0 = vertexMap[oldIndices[i]];
                int index1 = vertexMap[oldIndices[i + 1]];
                int index2 = vertexMap[oldIndices[i + 2]];

                if (index0 == index1 || index1 == index2 || index2 == index0)
                {
                    continue;
                }

                newIndices.Add(index0);
                newIndices.Add(index1);
                newIndices.Add(index2);
            }

            return new MeshData(newIndices, newVertices, newNormals);
        }
    } 
}
