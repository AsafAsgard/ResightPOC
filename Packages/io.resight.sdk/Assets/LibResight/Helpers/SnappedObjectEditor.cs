using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

#if UNITY_2021_2_OR_NEWER
using UnityEditor.SceneManagement;
#else
using UnityEditor.Experimental.SceneManagement;
#endif

namespace Resight.Persistence
{
    [CustomEditor(typeof(SnappedObject))]
    internal sealed class SnapedObjectEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (!Application.isPlaying) {
                var snappedObj = (SnappedObject)target;

                UpdateInstanceId(snappedObj);
            }
            base.OnInspectorGUI();
        }

        private bool IsPrefabInstance(SnappedObject snappedObj)
        {
            return PrefabStageUtility.GetCurrentPrefabStage() == null &&
              PrefabUtility.IsPartOfPrefabInstance(snappedObj) &&
              !PrefabUtility.IsPrefabAssetMissing(snappedObj);
        }

        private void UpdateInstanceId(SnappedObject snappedObj)
        {
            // We want to first ensure we are selecting an instance, not a prefab.
            if (!IsPrefabInstance(snappedObj)) {
                PopulatePrefabId();
                return;
            }
            //if (snappedObj.Id == 0UL)
            //{
            //    GenerateInstanceId();
            //}
        }

        private void PopulatePrefabId()
        {
            var prefabIdField = serializedObject.FindProperty("_prefabId");

            if (prefabIdField.stringValue == "") {
                prefabIdField.stringValue = serializedObject.targetObject.name;
                serializedObject.ApplyModifiedProperties();
            }
        }

        private void GenerateInstanceId()
        {
            var rawIdField = serializedObject.FindProperty("_instanceId");
            rawIdField.longValue = SnappedObject.GenerateId();
            serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif

