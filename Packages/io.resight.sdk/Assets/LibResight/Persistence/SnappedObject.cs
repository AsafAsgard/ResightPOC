using UnityEngine;
using System.Collections;
using System;
using Resight.Utilities.Extensions;

namespace Resight.Persistence
{

    public class SnappedObject : MonoBehaviour
    {
        [SerializeField] float minPositionChange = 1e-3f;
        [SerializeField] float minRotationChange = 1e-2f;

        public bool IsRemote
        {
            get => _isRemote;
            private set
            {
                _isRemote = value;
            }
        }

        /// <summary>
        /// An Id that represents the instance of the object, shared between all peers in the session
        /// </summary>
#if UNITY_EDITOR
        [ReadOnly]
#endif
        [SerializeField]
        private long _instanceId = 0L;

        private bool _isRegistered = false;

        private bool _isRemote = false;

        private RSAnchor _anchor;

        private Vector3 _lastPosition;

        private Quaternion _lastRotation;

        /// <summary>
        /// An Id that represents the prefab, shared between all builds of the scene
        /// </summary>
#if UNITY_EDITOR
        [ReadOnly]
#endif
        [SerializeField]
        private string _prefabId = "";

        private byte[] _auxData;
        public byte[] AuxData
        {
            get { return _auxData; }
            set
            {
                _auxData = value;
                if (IsRegistered()) {
                    EntitiesManager.Instance.UpdateEntityData(gameObject, _anchor);
                }
            }
        }

        public ulong Id
        {
            get { return (ulong)_instanceId; }
            set { _instanceId = (long)value; }
        }

        public string PrefabId
        {
            get { return _prefabId; }
        }

        public void SetTransform(Transform transform)
        {
            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
        }

        private bool IsRegistered()
        {
            return _isRegistered || _isRemote;
        }

        private bool TransformHasChanged()
        {
            return (transform.position - _lastPosition).magnitude > minPositionChange ||
                (Quaternion.Angle(transform.rotation, _lastRotation) > minRotationChange);
        }

        private void Register()
        {
            var aux_len = _auxData != null ? _auxData.Length : 0;
            LibResight.Log("[LibResight] SnappedObject::Register(): instanceId=" + Id + " prefabId=" + PrefabId + ", AugData=" + aux_len);
            var anchor = EntitiesManager.Instance.AddAnchor(Id, transform.ToRSPose().ToMatrix4x4());
            EntitiesManager.Instance.AddEntity(gameObject, anchor);
            if (transform == null || gameObject == null) {
                LibResight.Log("NULL");
                return;
            }
            _anchor = anchor;
            _isRegistered = true;
            SetTransform(transform);
        }

        private void OnStatus(EngineState state, IntPtr ctx)
        {
            if (state == EngineState.Mapping && !_isRegistered) {
                Register();

                LibResight.Instance.OnStatus -= OnStatus;
            }
        }

        // Use this for initialization
        void Start()
        {
            if (_instanceId == 0L) {
                _instanceId = GenerateId();

                if (LibResight.State == EngineState.Mapping) {
                    Register();
                    return;
                }

                LibResight.Instance.OnStatus += OnStatus;
            } else {
                _isRemote = true;
                SetTransform(transform);
            }
        }

        // Update is called once per frame
        void LateUpdate()
        {
            if (IsRegistered() && TransformHasChanged()) {
                SetTransform(transform);
                EntitiesManager.Instance.UpdateEntityPose(gameObject, _anchor);
            }
        }

        void OnDestroy()
        {
            LibResight.Instance.OnStatus -= OnStatus;
            // We only remove entities that were explicitly destroyed by the user
            if (!EntitiesManager.IsQuitting && IsRegistered()) {
                EntitiesManager.Instance.RemoveEntity(gameObject, _anchor);
            }
        }

        internal static long GenerateId()
        {
            var random = new System.Random();
            var longBytes = new byte[8];
            random.NextBytes(longBytes);
            return BitConverter.ToInt64(longBytes, 0);
        }
    }
}
