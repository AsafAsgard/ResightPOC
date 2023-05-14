using UnityEngine;
using System;
using System.Collections.Generic;
using Resight.Utilities.Extensions;
using System.Runtime.InteropServices;
using UnityEngine.Assertions;
using System.Reflection;

namespace Resight.Persistence
{
    public class EntitiesManager : MonoBehaviour
    {
        public GameObject trackablesOrigin;
        private GameObject anchorDebugPrefab;

        private readonly Dictionary<ulong, RSAnchor> remoteAnchors_ = new();
        private readonly Dictionary<ulong, RSEntity> orphanEntities_ = new();
        private readonly Dictionary<ulong, GameObject> entities_ = new();

        public static EntitiesManager Instance { get; private set; }
        public static bool IsQuitting { get; private set; } = false;

        [RuntimeInitializeOnLoadMethod]
        static void RunOnStart()
        {
            Application.quitting += () => {
                LibResight.Log("Application is quitting");
                IsQuitting = true;
            };
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) {
                LibResight.Log("[LibResight] Only one instance of EntitiesManager is allowed");
                return;
            }

            Instance = this;

            LibResight.Instance.OnAnchor += OnAnchor;
            LibResight.Instance.OnEntityAdded += OnEntityAdded;
            LibResight.Instance.OnEntityRemoved += OnEntityRemoved;
            LibResight.Instance.OnEntityPoseUpdated += OnEntityPoseUpdated;
            LibResight.Instance.OnEntityDataUpdated += OnEntityDataUpdated;
            LibResight.Instance.OnStatus += OnStatus;

            if (trackablesOrigin == null)
            {
                trackablesOrigin = gameObject;
            }
        }

        private void OnStatus(EngineState status, IntPtr ctx)
        {
            if (status == EngineState.Init)
            {
                LibResight.Log("Status Init: Clearing anchors and entities");
                remoteAnchors_.Clear();
                orphanEntities_.Clear();
                foreach (var entity in entities_.Values)
                {
                    var isRegisteredField = typeof(SnappedObject).GetField("_isRegistered", BindingFlags.NonPublic | BindingFlags.Instance);
                    var isRemoteField = typeof(SnappedObject).GetField("_isRemote", BindingFlags.NonPublic | BindingFlags.Instance);
                    Assert.IsNotNull(isRegisteredField);
                    Assert.IsNotNull(isRemoteField);

                    isRegisteredField.SetValue(entity.GetComponent<SnappedObject>(), false);
                    isRemoteField.SetValue(entity.GetComponent<SnappedObject>(), false);

                    Destroy(entity);
                }

                entities_.Clear();
            }
        }

        // Public API
        public RSAnchor AddAnchor(ulong id, Matrix4x4 mat)
        {
            var anchor = RSCreateAnchor(id, mat.ToRSPose());
            CreateDebugAnchor(id, mat.ToRSPose());
            //remoteAnchors[anchor.id] = transform.gameObject;
            remoteAnchors_[id] = anchor;
            RSAddAnchor(anchor);

            return anchor;
        }

        public void AddEntity(GameObject go, RSAnchor anchor)
        {
            var entity = go.GetComponent<SnappedObject>();
            if (!entity) {
                LibResight.Log("[LibResight] AddEntity(): entity does not have a SnappedObject component");
                return;
            }

            if (entities_.ContainsKey(entity.Id)) {
                LibResight.Log("[LibResight] AddEntity(): could not add entity " + entity.Id + " - id already exist");
                return;
            }

            // attach to origin and add to managed entities
            go.transform.SetParent(trackablesOrigin.transform, true);
            go.transform.hasChanged = false;
            entities_[entity.Id] = go;
            LibResight.Log("[LibResight] AddEntity(): id=" + entity.Id + " prefabId=" + entity.PrefabId + ", anchor=" + anchor.id + ", GetInstanceID=" + go.GetInstanceID());
            
            // broadcast
            var data_len = entity.AuxData != null ? entity.AuxData.Length : 0;
            //var pose = go.transform.ToRSPose();
            var entityLocalPoseMat = anchor.pose.ToMatrix4x4().inverse * go.transform.ToRSPose().ToMatrix4x4();

            GCHandle gch = GCHandle.Alloc(entity.AuxData, GCHandleType.Pinned);
            RSAddEntity(anchor, entity.PrefabId, entityLocalPoseMat.ToRSPose(), gch.AddrOfPinnedObject(), data_len);
            gch.Free();
        }

        public void RemoveEntity(GameObject go, RSAnchor anchor)
        {
            var entity = go.GetComponent<SnappedObject>();
            if (!entity) {
                LibResight.Log("[LibResight] RemoveEntity(): entity does not have a SnappedObject component");
                return;
            }

            if (anchor.id == 0UL)
            {
                anchor = remoteAnchors_[entity.Id];
            }

            if (entities_.ContainsKey(entity.Id))
            {
                entities_.Remove(entity.Id);
            }

            LibResight.Log("[LibResight] RemoveEntity(): id=" + anchor.id + " prefabId=" + entity.PrefabId);

            RSRemoveEntity(anchor);
        }

        public void UpdateEntityPose(GameObject go, RSAnchor anchor)
        {
            var entity = go.GetComponent<SnappedObject>();
            if (!entity) {
                LibResight.Log("[LibResight] UpdateEntityPose(): entity does not have a SnappedObject component");
                return;
            }

            //LibResight.Log("[LibResight] UpdateEntityPose() (local): Id=" + entity.Id + ", anchor=" + anchor.id + ", GetInstanceID=" + go.GetInstanceID() + ", pos=" + go.transform.position + ", instanceId=" + entity.Id);

            //TODO: eliminate anchor's pose from local pose and send the 
            if (anchor.id == 0UL)
            {
                anchor = remoteAnchors_[entity.Id];
            }
            var entityLocalPoseMat = anchor.pose.ToMatrix4x4().inverse * go.transform.ToRSPose().ToMatrix4x4();

            RSUpdateEntityPose(anchor, entityLocalPoseMat.ToRSPose());
        }

        public void UpdateEntityData(GameObject go, RSAnchor anchor)
        {
            var entity = go.GetComponent<SnappedObject>();
            if (!entity) {
                LibResight.Log("[LibResight] UpdateEntityData(): entity does not have a SnappedObject component");
                return;
            }

            var data_len = entity.AuxData != null ? entity.AuxData.Length : 0;
            LibResight.Log("[LibResight] UpdateEntityData() (local): Id=" + entity.Id + ", data_len=" + data_len);

            var id = entity.PrefabId;

            if (anchor.id == 0UL)
            {
                anchor = remoteAnchors_[entity.Id];
            }

            GCHandle gch = GCHandle.Alloc(entity.AuxData, GCHandleType.Pinned);
            RSUpdateEntityData(anchor, gch.AddrOfPinnedObject(), data_len);
            gch.Free();
        }

        private void CreateDebugAnchor(ulong id, RSPose pose = new RSPose())
        {
            if (anchorDebugPrefab == null)
            {
                return;
            }
            var anchorDebug = Instantiate(anchorDebugPrefab);
            anchorDebug.name = "" + id;
            pose.ToTransform(anchorDebug.transform);
        }

        private void OnAnchor(RSAnchor anchor, int anchorType, byte weight, IntPtr userdata, IntPtr ctx)
        {
            if (!remoteAnchors_.TryGetValue(anchor.id, out RSAnchor lastUpdate)) {
                // new anchor
                //LibResight.Log("[LibResight] HandleOnAnchor(): New remote anchor: anchor.id=" + anchor.id);
                remoteAnchors_.Add(anchor.id, anchor);

                CreateDebugAnchor(anchor.id, anchor.pose);

                if (orphanEntities_.TryGetValue(anchor.id, out RSEntity entity)) {
                    LibResight.Log("[LibResight] HandleOnAnchor(): Reattaching entity " + entity.parentId + " to anchor " + anchor.id);
                    OnEntityAdded(entity, ctx);
                    orphanEntities_.Remove(anchor.id);
                }

                return;
            }

            if (anchorDebugPrefab != null)
            {
                var anchorGo = GameObject.Find("" + anchor.id);
                anchor.pose.ToTransform(anchorGo.transform);
            }
        
            // Update entity's pose based on anchor's pose
            GameObject entityObj;
            if (entities_.TryGetValue(anchor.id, out entityObj)) {
                var lastAnchorPose = lastUpdate.pose.ToMatrix4x4();
                var newAnchorPose = anchor.pose.ToMatrix4x4();
                var deltaAnchorChange = newAnchorPose * lastAnchorPose.inverse;

                UpdateEntityPose(deltaAnchorChange, entityObj);

                LibResight.Log("[LibResight] HandleOnAnchor(): anchor.id=" + anchor.id
                        + ", entity.pos=" + entityObj.transform.position + ", entity.rot=" + entityObj.transform.rotation
                        + ", entity.localPos=" + entityObj.transform.localPosition + ", entity.localRot=" + entityObj.transform.localRotation);
            }

            // update anchor in remoteAnchors (b/c it's a struct, we need to re-add it to the list)
            remoteAnchors_.Remove(anchor.id);
            remoteAnchors_.Add(anchor.id, anchor);

            LibResight.Log("[LibResight] HandleOnAnchor(): Anchor_id: " + anchor.id + " pose: " + anchor.pose.pos.x + "," + anchor.pose.pos.y + "," + anchor.pose.pos.z + "\n");
        }

        private void OnEntityAdded(RSEntity entity, IntPtr ctx)
        {
            LibResight.Log("[LibResight] OnEntityAdded(): entity.id=" + entity.id + ", entity.parentId=" + entity.parentId + ", time=" + Time.time);

            if (HandleOrphanEntity(entity))
            {
                return;
            }

            if (entities_.ContainsKey(entity.parentId)) {
                LibResight.Log("[LibResight] OnEntityAdded(): ignored, entity already exist");
                return;
            }

            
            if (entity.id == "") {
                LibResight.Log("[LibResight] OnEntityAdded(): ignored, entity has an empty key");
                return;
            }

            var entityPrefab = Resources.Load(entity.id, typeof(GameObject));
            if (entityPrefab == null) {
                LibResight.Log($"[LibResight] OnEntityAdded(): could not find entity.id: {entity.id}");
                return;
            }

            var entityObj = Instantiate(entityPrefab) as GameObject;
            entityObj.transform.SetParent(trackablesOrigin.transform);
            if (!entityObj.TryGetComponent(out SnappedObject snappedObject)) {
                snappedObject = entityObj.AddComponent<SnappedObject>();
            }
            snappedObject.Id = entity.parentId;
            snappedObject.AuxData = entity.data;

            entities_[entity.parentId] = entityObj;

            UpdateEntityPose(entity);

            //LibResight.Log("[LibResight] HandleOnEntityAdded(): entity.id=" + entity.id
                //+ ", entityObj.pos=" + entityObj.transform.position + ", entityObj.rot=" + entityObj.transform.rotation
                //+ ", entityObj.localPos=" + entityObj.transform.localPosition + ", entityObj.localRot=" + entityObj.transform.localRotation
                //+ ", parent.pos=" + entityObj.transform.position + ", parent.rot=" + entityObj.transform.rotation);
        }

        private void OnEntityRemoved(RSEntity entity, IntPtr ctx)
        {
            LibResight.Log("[LibResight] OnEntityRemoved(): entity.parentId=" + entity.parentId + ", time=" + Time.time);

            if (entities_.ContainsKey(entity.parentId)) {
                var entityObj = entities_[entity.parentId];
                Destroy(entityObj);
                entities_.Remove(entity.parentId);
            } 
            else if (orphanEntities_.ContainsKey(entity.parentId)) {
                // remove orphan
                orphanEntities_.Remove(entity.parentId);
            }
        }

        private void OnEntityPoseUpdated(RSEntity entity, IntPtr ctx)
        {
            LibResight.Log("[LibResight] OnEntityPoseUpdated(): entity.parentId=" + entity.parentId + ", time=" + Time.time);
            if (entities_.ContainsKey(entity.parentId)) {
                UpdateEntityPose(entity);
            }
            else if (orphanEntities_.ContainsKey(entity.parentId)) {
                // update orhpan's pose
                var updatedEntity = orphanEntities_[entity.parentId];
                updatedEntity.pose = entity.pose;
                orphanEntities_[entity.parentId] = updatedEntity;
            }
            else {
                LibResight.Log("[LibResight] OnEntityPoseUpdated(): Entity was not found. Adding it.");
                OnEntityAdded(entity, ctx);
            }
        }

        private void OnEntityDataUpdated(RSEntity entity, IntPtr ctx)
        {
            LibResight.Log("[LibResight] OnEntityDataUpdated(): entity.parentId=" + entity.parentId + ", time=" + Time.time);
            if (entities_.ContainsKey(entity.parentId)) {
                var entityObj = entities_[entity.parentId];
            }
            else if (orphanEntities_.ContainsKey(entity.parentId)) {
                var updatedEntity = orphanEntities_[entity.parentId];
                updatedEntity.data = entity.data;
                updatedEntity.dtSize = entity.dtSize;
                orphanEntities_[entity.parentId] = updatedEntity;
            }
            else {
                LibResight.Log("[LibResight] OnEntityDataUpdated(): Entity was not found. Adding it.");
                OnEntityAdded(entity, ctx);
            }
        }

        private void UpdateEntityPose(Matrix4x4 anchorMat, GameObject entity)
        {
            var entityMat = entity.transform.ToRSPose().ToMatrix4x4();
            var entityPoseMat = anchorMat * entityMat;
            entityPoseMat.ToRSPose().ToTransform(entity.transform);

            // update transform in SnappedObject to avoid feedback loop
            entity.GetComponent<SnappedObject>().SetTransform(entity.transform);
        }

        private bool HandleOrphanEntity(RSEntity entity)
        {
            bool isOrphan = !remoteAnchors_.ContainsKey(entity.parentId);
            if (isOrphan) {
                LibResight.Log("[LibResight] HandleOrphanEntity(): saving orphan entity: entity.parentId=" + entity.parentId + ", entity.id=" + entity.id);
                orphanEntities_[entity.parentId] = entity;
            }

            return isOrphan;
        }

        private void UpdateEntityPose(RSEntity entity)
        {
            var anchor = remoteAnchors_[entity.parentId];
            var entityObj = entities_[entity.parentId];
            Matrix4x4 anchorMat = anchor.pose.ToMatrix4x4();
            Matrix4x4 entityMat = entity.pose.ToMatrix4x4();

            var entityPoseMat = anchorMat * entityMat;

            LibResight.Log("[LibResight] target position: pos=" + entityPoseMat.GetColumn(3));
            entityPoseMat.ToRSPose().ToTransform(entityObj.transform);

            // update transform in SnappedObject to avoid feedback loop
            entityObj.GetComponent<SnappedObject>().SetTransform(entityObj.transform);
        }


#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void RSAddAnchor(RSAnchor anchor);

        [DllImport("__Internal")]
        private static extern RSAnchor RSCreateAnchor(ulong id, RSPose pose);

        [DllImport("__Internal")]
        private static extern int RSAddEntity(RSAnchor anchor, [MarshalAs(UnmanagedType.LPStr)] string key, RSPose pose, IntPtr data, int size);

        [DllImport("__Internal")]
        private static extern void RSRemoveEntity(RSAnchor anchor);

        [DllImport("__Internal")]
        private static extern void RSUpdateEntityPose(RSAnchor anchor, RSPose pose);

        [DllImport("__Internal")]
        private static extern void RSUpdateEntityData(RSAnchor parent, IntPtr data, int size);
#else
        private static void RSAddAnchor(RSAnchor anchor) {}

        private static RSAnchor RSCreateAnchor(ulong id, RSPose pose) {return new RSAnchor();}

        private static int RSAddEntity(RSAnchor anchor, [MarshalAs(UnmanagedType.LPStr)] string key, RSPose pose, IntPtr data, int size) {return 0;}

        private static void RSRemoveEntity(RSAnchor anchor) { }

        private static void RSUpdateEntityPose(RSAnchor anchor, RSPose pose) {}

        private static void RSUpdateEntityData(RSAnchor parent, IntPtr data, int size) {}
#endif
    }
}
