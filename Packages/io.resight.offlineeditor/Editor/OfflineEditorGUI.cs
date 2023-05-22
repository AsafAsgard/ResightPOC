#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;

namespace Resight
{
    public class OfflineEditorWindow : EditorWindow
    {
        private LibResight libResight;
        private OfflineEditorLogic offlineEditorLogic;

        private int selectedSpaceIdx_ = -1;
        private string[] spacesArray_ = new string[0];

        private const string MenuPrefix = "Resight/";
        [MenuItem(MenuPrefix + "Offline Editor")]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow(typeof(OfflineEditorWindow), false, "Offline Editor", true);
        }

        private bool TryInit()
        {
            if (offlineEditorLogic)
                return true;

            if (!libResight)
                libResight = FindObjectOfType<LibResight>();
            
            if (libResight)
            {
                if (!libResight.TryGetComponent(out offlineEditorLogic))
                {
                    offlineEditorLogic = libResight.gameObject.AddComponent<OfflineEditorLogic>();
                }

                offlineEditorLogic.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable | HideFlags.DontSave;
                return true;
            }

            return false;
        }

        void OnGUI()
        {
            if (!TryInit())
            {
                EditorGUILayout.HelpBox("Missing LibResight in the scene", MessageType.Warning);
                return;
            }

            GUILayout.Space(10);

            if (!offlineEditorLogic.IsConnected)
            {
                offlineEditorLogic.Devkey = libResight.devKey;
                offlineEditorLogic.Namespace = libResight.nameSpace;
            }
            
            if (string.IsNullOrWhiteSpace(offlineEditorLogic.Devkey))
            {
                EditorGUILayout.HelpBox("Missing developer key", MessageType.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(offlineEditorLogic.Namespace))
            {
                EditorGUILayout.HelpBox($"Missing namespace", MessageType.Warning);
                return;
            }

            // disconneted -> connect
            if (!offlineEditorLogic.IsConnected)
            {
                if (GUILayout.Button("Connect Space Editor"))
                {
                    offlineEditorLogic.Connect();
                }

                return;
            }

            // connected -> disconnect
            if (GUILayout.Button("Disconnect Space Editor"))
            {
                offlineEditorLogic.Disconnect();
                return;
            }

            if (offlineEditorLogic.IsSpacesUpdated || selectedSpaceIdx_ == -1)
            {
                spacesArray_ = offlineEditorLogic.GetSpacesArray();
                for (int i = 0; i < spacesArray_.Length; i++)
                {
                    if (offlineEditorLogic.ActiveSpaceId == ulong.Parse(spacesArray_[i].Split(" - ")[0]))
                    {
                        selectedSpaceIdx_ = i;
                        break;
                    }
                }

                if (selectedSpaceIdx_ <= 0 || selectedSpaceIdx_ >= spacesArray_.Length)
                {
                    selectedSpaceIdx_ = 0;
                }
            }

            if (spacesArray_.Length == 0)
            {
                EditorGUILayout.HelpBox($"Waiting for data...", MessageType.Info);
                return;
            }

            // spaces dropdown & auto-download toggle
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            selectedSpaceIdx_ = EditorGUILayout.Popup("Selected Space", selectedSpaceIdx_, spacesArray_);
            offlineEditorLogic.ActiveSpaceId = ulong.Parse(spacesArray_[selectedSpaceIdx_].Split(" - ")[0]);
            offlineEditorLogic.AutoDownloadScans = GUILayout.Toggle(offlineEditorLogic.AutoDownloadScans, new GUIContent("↺", "Auto-Download Scans"), "Button", GUILayout.MaxWidth(20));
            GUILayout.EndHorizontal();

            // show/hide scans/entities in hierarchy
            GUILayout.Space(10);
            offlineEditorLogic.ShowScansInHierarchy = GUILayout.Toggle(offlineEditorLogic.ShowScansInHierarchy, "Show scans in hierarchy");
            offlineEditorLogic.ShowEntitiesInHierarchy = GUILayout.Toggle(offlineEditorLogic.ShowEntitiesInHierarchy, "Show entities in hierarchy");
        }
        
        void OnHierarchyChange()
        {
            // as it does not return objects with HideFlags.DontSave set,
            // we can use it to detect new objects created by the user
            var all = FindObjectsOfType<Persistence.SnappedObject>();
            foreach (var obj in all)
            {
                // note: undo changes in unity might cause false-positives in FindObjectsOfType
                //       that's why we also check for existence of SnappedObjectEditor and its Id
                if (!obj.TryGetComponent(out SnappedObjectEditor snappedObjectEditor))
                {
                    var snappedObject = obj.GetComponent<Persistence.SnappedObject>();
                    if (snappedObject.Id == 0)
                        offlineEditorLogic.RegisterObject(snappedObject);
                }
            }
        }
    }
}

#endif