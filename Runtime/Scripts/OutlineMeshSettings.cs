using System;
using UnityEngine;

namespace OutlineMesh
{
    [CreateAssetMenu(
        fileName = nameof(OutlineMeshSettings),
        menuName = "OutlineMesh/" + nameof(OutlineMeshSettings))]
    public class OutlineMeshSettings : ScriptableObject
    {
        public static readonly string RegenerateTimestampTicksPropertyName = nameof(_regenerateTimestampTicks);

        [Min(0.0001f)] public float VerticesMergeDistanceThreshold = 0.0001f;
        [Min(0.0001f)] public float MeshWidth = 0.02f;
        [Tooltip("\"Relative\" means that the path starts from the Assets folder, for example:\n" +
            "Your full path: D:\\Projects\\MyProject\\Assets\\SomeFolder\\Cache\n" +
            "Your relative path: SomeFolder\\Cache")]
        public string RelativePathToCacheFolder;

        [SerializeField, HideInInspector] private long _regenerateTimestampTicks;

        public long RegenerateMeshTimestampTicks => _regenerateTimestampTicks;
    }
}
