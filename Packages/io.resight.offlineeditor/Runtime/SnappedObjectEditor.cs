#if UNITY_EDITOR

using UnityEngine;
using System.Collections;
using Unity.EditorCoroutines.Editor;

namespace Resight
{   
    [ExecuteInEditMode]
    public class SnappedObjectEditor : MonoBehaviour
    {
        public delegate void TransformUpdated(GameObject sender);
        public event TransformUpdated OnTransformUpdated;

        public bool DirtyRemote { get; set; } = true;

        private void Start()
        {
            EditorCoroutineUtility.StartCoroutine(SyncTransform(), this);
            hideFlags = HideFlags.HideAndDontSave;
        }

        private void OnDestroy()
        {
            //Debug.Log($"{name} destroyed");
        }

        private IEnumerator SyncTransform()
        {
            while (this != null)
            {   
                bool shouldSend = transform.hasChanged && !DirtyRemote;
                DirtyRemote = false;
                transform.hasChanged = false;

                if (shouldSend)
                    OnTransformUpdated?.Invoke(gameObject);

                yield return new EditorWaitForSeconds(0.25f);
            }
        }
    }
}

#endif