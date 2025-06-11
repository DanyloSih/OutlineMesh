using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OutlineMesh.Editor
{
    [CustomEditor(typeof(OutlineMeshSettings))]
    public class OutlineMeshSettingsEditor : UnityEditor.Editor
    {
        private SerializedProperty _thresholdProp;
        private SerializedProperty _widthProp;
        private SerializedProperty _pathProp;
        private SerializedProperty _regenerateTimestampProp;

        private void OnEnable()
        {
            _regenerateTimestampProp = serializedObject.FindProperty(OutlineMeshSettings.RegenerateTimestampTicksPropertyName);
            _thresholdProp = serializedObject.FindProperty(nameof(OutlineMeshSettings.VerticesMergeDistanceThreshold));
            _widthProp = serializedObject.FindProperty(nameof(OutlineMeshSettings.MeshWidth));
            _pathProp = serializedObject.FindProperty(nameof(OutlineMeshSettings.RelativePathToCacheFolder));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_widthProp);
            EditorGUILayout.PropertyField(_thresholdProp);
            EditorGUILayout.Space();

            string cacheRel = _pathProp.stringValue ?? string.Empty;
            string fullPath = Path.Combine(Application.dataPath, cacheRel);
            if (string.IsNullOrEmpty(cacheRel) || !Directory.Exists(fullPath))
            {
                EditorGUILayout.HelpBox(
                    "Cache path is invalid. Please select a valid folder inside the project's Assets directory.",
                    MessageType.Error);
            }

            EditorGUILayout.LabelField("Cache Folder", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(_pathProp, GUIContent.none, GUILayout.ExpandWidth(true));
            bool isBrowse = GUILayout.Button("Browse", GUILayout.MaxWidth(80));
            EditorGUILayout.EndHorizontal();

            if (isBrowse)
            {
                string initial = Directory.Exists(fullPath) ? fullPath : Application.dataPath;
                string selected = EditorUtility.OpenFolderPanel(
                    "Select Cache Folder", initial, string.Empty);
                if (!string.IsNullOrEmpty(selected))
                {
                    if (selected.StartsWith(Application.dataPath))
                    {
                        string rel = selected.Substring(Application.dataPath.Length + 1)
                            .Replace("\\", "/");
                        _pathProp.stringValue = rel;
                    }
                    else
                    {
                        EditorUtility.DisplayDialog(
                            "Invalid Folder",
                            "Please select a folder inside the project's Assets folder.",
                            "OK");
                    }
                }
            }

            if (GUILayout.Button($"Mark Linked Mesh Generators For Forced Generation"))
            {
                _regenerateTimestampProp.longValue = DateTime.Now.Ticks;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}