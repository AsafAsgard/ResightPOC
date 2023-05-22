#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using Firebase.Database;
using Firebase.Extensions;
using Firebase.Storage;
using Firebase;
using Rho.Entities.Proto;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System;
using Unity.EditorCoroutines.Editor;

namespace Resight
{   
    [ExecuteInEditMode]
    public class OfflineEditorLogic : MonoBehaviour
    {
        public string Namespace { get; set; }
        public string Devkey { get; set; }

        public bool IsConnected { get; private set; } = false;
        public bool IsSpacesUpdated { get; private set; } = false;

        public GameObject rootGameObject;

        public ulong ActiveSpaceId
        {
            get => activeSpaceId_;
            set
            {
                if (activeSpaceId_ != value)
                {
                    SetActiveSpace(value);
                }
            }
        }

        public bool AutoDownloadScans
        {
            get => autoDownloadScans_;
            set
            {
                if (autoDownloadScans_ != value)
                {
                    autoDownloadScans_ = value;
                    RefreshAllEntities();
                }
            }
        }

        public bool ShowScansInHierarchy
        {
            get => showScansInHierarchy_;
            set
            {
                if (showScansInHierarchy_ != value)
                {
                    showScansInHierarchy_ = value;
                    RefreshAllEntities();
                }
            }
        }

        public bool ShowEntitiesInHierarchy
        {
            get => showEntitiesInHierarchy_;
            set
            {
                if (showEntitiesInHierarchy_ != value)
                {
                    showEntitiesInHierarchy_ = value;
                    RefreshAllEntities();
                }
            }
        }


        [Serializable]
        private struct UserAnchorJson
        {
            public long _rnd;
            public long parent;
            public long[] pose;
        }

        [Serializable]
        private struct UserEntityJson
        {
            public long _rnd;
            public string user_id;
            public long[] pose;
            public long version;
            public long size;
            public bool deleted;
        }

        private class SpaceInfo
        {
            public ulong spaceId;
            public ulong lastSessionId;
            public VisibleNodes visibleNodes;
        }

        private class UserEntity
        {
            public ulong id;
            public ulong sessionId;
            public ulong version;
            public string user_id; // can use a non empty string to debug unknown prefabs
            public Matrix4x4 anchor_pose = Matrix4x4.identity;
            public Matrix4x4 entity_pose = Matrix4x4.identity;
            public GameObject go;
            public bool isMeshAnchor;
            public bool isDownloading;
        }

        private string ScansFolder { get; set; } = "ResightScans";

        private Queue<Action> actionQueue_ = new();

        private FirebaseApp secondaryApp = null;
        private FirebaseDatabase secondaryDatabase = null;
        private FirebaseStorage secondaryStorage = null;
        private DatabaseReference entitiesReference = null;
        private DatabaseReference anchorsReference = null;
        private StorageReference scansStorageReference = null;
        private Material scanMaterial;

        private readonly Dictionary<ulong, SpaceInfo> spaces_ = new();
        private readonly Dictionary<ulong, Matrix4x4> nodes_ = new();
        private readonly Dictionary<ulong, UserEntity> entities_ = new();

        private ulong activeSpaceId_ = 0;
        private bool showScansInHierarchy_ = false;
        private bool showEntitiesInHierarchy_ = true;
        private bool autoDownloadScans_ = true;

        private string applicationDataPath_;

        void OnEnable()
        {
            Debug.Log("OnEnable");
            applicationDataPath_ = Application.dataPath;

            // not ready yet
            //scanMaterial = AssetDatabase.LoadAssetAtPath<Material>("Packages/io.resight.sdk/Assets/LibResight/Mesh/MeshBlockColorsAndShadows.mat");

            EditorCoroutineUtility.StartCoroutine(MainThreadExecuter(), this);
        }

        void EnqueueAction(Action action)
        {
            lock (actionQueue_)
            {
                actionQueue_.Enqueue(action);
            }
        }

        IEnumerator MainThreadExecuter()
        {
            var waiter = new EditorWaitForSeconds(0.5f);
            while (this != null)
            {
                while (actionQueue_.Count > 0)
                {
                    Action action;
                    lock (actionQueue_)
                    {
                        action = actionQueue_.Dequeue();
                    }

                    action();
                }
                yield return waiter;
            }
        }

        public void Connect()
        {
            if (IsConnected)
                return;

            IsConnected = true;

            Firebase.AppOptions secondaryAppOptions = new Firebase.AppOptions
            {
                ApiKey = "AIzaSyCzKyOQfRXAQxb9aVWIX9S4MJdjOQUaS54",
                AppId = "1:1071127650929:ios:72418ce37b1ce27d",
                ProjectId = "resightcloud-9ef64",
                DatabaseUrl = new Uri("https://resightcloud-9ef64.firebaseio.com"),
                StorageBucket = "resightcloud-9ef64.appspot.com"
            };

            secondaryApp = FirebaseApp.Create(secondaryAppOptions, "Secondary");

            byte[] decodedBytes = Convert.FromBase64String(Devkey);
            string decodedText = Encoding.UTF8.GetString(decodedBytes);
            var devParams = decodedText.Split(',');

            var secondaryAuth = Firebase.Auth.FirebaseAuth.GetAuth(secondaryApp);
            Firebase.Auth.FirebaseUser user = secondaryAuth.CurrentUser;
            if (user == null || user.Email != devParams[0])
            {
                secondaryAuth.SignInWithEmailAndPasswordAsync(devParams[0], devParams[1]).ContinueWith(task =>
                {
                    if (task.IsCompletedSuccessfully)
                    {
                        RegisterServerEvents(task.Result.UserId);
                    } else
                    {
                        IsConnected = false;
                    }
                });
            } else
            {
                EnqueueAction(() => { RegisterServerEvents(user.UserId); });
            }
        }

        public void Disconnect()
        {
            SetActiveSpace(0); // will clear nodes and destroy gameobjects

            anchorsReference = null;
            entitiesReference = null;
            scansStorageReference = null;
            secondaryDatabase = null;
            secondaryStorage = null;

            if (secondaryApp != null)
            {
                secondaryApp.Dispose();
                secondaryApp = null;
            }

            spaces_.Clear();
            entities_.Clear();
            IsConnected = false;
        }

        private ulong GenerateRandomId()
        {
            var random = new System.Random();
            var longBytes = new byte[8];
            random.NextBytes(longBytes);
            return BitConverter.ToUInt64(longBytes, 0); 
        }

        private unsafe ulong AsUInt64(long value)
        {
            return *(ulong*)&value;            
        }

        private unsafe long AsInt64(ulong value)
        {
            return *(long*)&value;
        }

        public void RegisterObject(Persistence.SnappedObject snappedObject)
        {
            Debug.Assert(IsConnected);
            Debug.Assert(activeSpaceId_ != 0);
            Debug.Assert(snappedObject.Id == 0);
            Debug.Assert(snappedObject.GetComponent<SnappedObjectEditor>() == null);

            var entity = new UserEntity();
            var id = GenerateRandomId();

            Debug.Log($"[Resight] Registering new entity: {id} @ {snappedObject.PrefabId}");

            snappedObject.Id = id;
            entity.id = id;
            entity.sessionId = spaces_[activeSpaceId_].spaceId;
            entity.user_id = snappedObject.PrefabId;
            entity.anchor_pose = Matrix4x4.identity;
            entity.isMeshAnchor = false;
            entity.go = snappedObject.gameObject;
            entity.go.hideFlags = HideFlags.DontSave;

            if (rootGameObject != null)
                entity.go.transform.SetParent(rootGameObject.transform, false);

            var snappedObjectEditor = entity.go.AddComponent<SnappedObjectEditor>();
            snappedObjectEditor.hideFlags = HideFlags.HideAndDontSave | HideFlags.HideInInspector;
            snappedObjectEditor.DirtyRemote = false;
            snappedObjectEditor.OnTransformUpdated += SnappedObject_OnTransformUpdated;
            entities_.Add(entity.id, entity);

            // now push the update
            var userAnchor = new UserAnchorJson
            {
                _rnd = AsInt64(GenerateRandomId()),
                parent = AsInt64(entity.sessionId),
                pose = EncodePose(entity.entity_pose.GetPosition(), entity.entity_pose.rotation)
            };
            anchorsReference.Child(id.ToString()).SetRawJsonValueAsync( JsonUtility.ToJson(userAnchor) );

            // will push the entity update
            SnappedObject_OnTransformUpdated(entity.go);
        }

        private void RegisterServerEvents(string userId)
        {
            secondaryDatabase = FirebaseDatabase.GetInstance(secondaryApp);
            secondaryStorage = FirebaseStorage.GetInstance(secondaryApp);
            secondaryDatabase.LogLevel = LogLevel.Error;
            secondaryStorage.LogLevel = LogLevel.Error;

            var remotePath = $"users/{userId}/{Namespace}";
            scansStorageReference = secondaryStorage.GetReference($"{remotePath}/meshes");

            var spacesReference = secondaryDatabase.GetReference($"{remotePath}/userdata/spaces");

            spacesReference.ChildAdded += (sender, e) => { EnqueueAction(() => { DatabaseSpaceUpdate(e.Snapshot); }); };
            spacesReference.ChildChanged += (sender, e) => { EnqueueAction(() => { DatabaseSpaceUpdate(e.Snapshot); }); };

            anchorsReference = secondaryDatabase.GetReference($"{remotePath}/userdata/anchors");
            anchorsReference.ChildAdded += (sender, e) => { EnqueueAction(() => { DatabaseAnchorUpdate(e.Snapshot); }); };
            anchorsReference.ChildChanged += (sender, e) => { EnqueueAction(() => { DatabaseAnchorUpdate(e.Snapshot); }); };

            entitiesReference = secondaryDatabase.GetReference($"{remotePath}/userdata/entities");
            entitiesReference.ChildAdded += (sender, e) => { EnqueueAction(() => { DatabaseEntityUpdate(e.Snapshot); }); };
            entitiesReference.ChildChanged += (sender, e) => { EnqueueAction(() => { DatabaseEntityUpdate(e.Snapshot); }); };
        }


        private unsafe Matrix4x4 DecodePose(long[] pose)
        {
            fixed (long* p = pose)
            {
                Quaternion q = new((float)*(double*)(p + 0), (float)*(double*)(p + 1), -(float)*(double*)(p + 2), -(float)*(double*)(p + 3));
                Vector3 t = new((float)*(double*)(p + 4), (float)*(double*)(p + 5), -(float)*(double*)(p + 6));
                return Matrix4x4.TRS(t, q, Vector3.one);
            }
        }

        private unsafe long[] EncodePose(Vector3 t, Quaternion q)
        {
            long[] result = new long[7];
            fixed (long* p = result)
            {
                *(double*)(p + 0) = q.x;
                *(double*)(p + 1) = q.y;
                *(double*)(p + 2) = -q.z;
                *(double*)(p + 3) = -q.w;
                *(double*)(p + 4) = t.x;
                *(double*)(p + 5) = t.y;
                *(double*)(p + 6) = -t.z;
            }
            return result;
        }

        private Matrix4x4 ExtractPose(Pose3 pose)
        {
            Quaternion q = new(pose.Qx, pose.Qy, pose.Qz, pose.Qw);
            Vector3 t = new(pose.X, pose.Y, pose.Z);
            return Matrix4x4.TRS(t, q, Vector3.one);
        }

        public string[] GetSpacesArray()
        {
            List<SpaceInfo> sortedSpaces = new();
            sortedSpaces.AddRange(spaces_.Values);
            sortedSpaces.Sort((x, y) => {
                return y.visibleNodes.Nodes.Count.CompareTo(x.visibleNodes.Nodes.Count);
            });

            var spacesArray = new string[spaces_.Count];
            int i = 0;
            foreach (var space in sortedSpaces)
            {
                int sessionsCount = space.visibleNodes.Nodes.Count;
                spacesArray[i++] = $"{space.spaceId} - {sessionsCount} anchors";
            }

            IsSpacesUpdated = false;
            return spacesArray;
        }

        private void SetActiveSpace(ulong spaceId)
        {
            activeSpaceId_ = spaceId;

            nodes_.Clear();
            if (activeSpaceId_ != 0 && spaces_.ContainsKey(activeSpaceId_))
            {
                Debug.Log($"[Resight] SetActiveSpace: {spaceId}");
                foreach (var node in spaces_[activeSpaceId_].visibleNodes.Nodes)
                {
                    nodes_[node.Id] = ExtractPose(node.Pose);
                }
            }

            RefreshAllEntities();
        }

        private void DatabaseSpaceUpdate(DataSnapshot snapshot)
        {
            ulong spaceId = ulong.Parse(snapshot.Key);
            if (!spaces_.TryGetValue(spaceId, out SpaceInfo spaceInfo))
            {
                spaceInfo = new SpaceInfo();
                spaces_.Add(spaceId, spaceInfo);
            }

            bool betterSession = false;
            foreach (var user in snapshot.Children)
            {
                ulong sessionId = ulong.Parse(user.Key);
                if (sessionId > spaceInfo.lastSessionId)
                {
                    byte[] decodedBytes = Convert.FromBase64String((string)user.Value);
                    var visibleNodes = VisibleNodes.Parser.ParseFrom(decodedBytes);
                    spaceInfo.spaceId = spaceId;
                    spaceInfo.lastSessionId = sessionId;
                    spaceInfo.visibleNodes = visibleNodes;
                    betterSession = true;
                }
            }

            if (spaceId == activeSpaceId_ && betterSession)
            {
                SetActiveSpace(activeSpaceId_);
            }

            IsSpacesUpdated = true;
        }

        private void DatabaseAnchorUpdate(DataSnapshot snapshot)
        {
            //Debug.Log(snapshot.Key + " --- " + snapshot.GetRawJsonValue());
            UserAnchorJson userAnchor = JsonUtility.FromJson<UserAnchorJson>(snapshot.GetRawJsonValue());
            ulong id = ulong.Parse(snapshot.Key); // anchor.id == entity.id
            if (!entities_.TryGetValue(id, out UserEntity entity))
            {
                entity = new UserEntity();
                entities_.Add(id, entity);
            }
            entity.id = id;
            entity.sessionId = AsUInt64(userAnchor.parent);
            entity.anchor_pose = DecodePose(userAnchor.pose);
            RefreshEntity(entity); 
        }

        private void DatabaseEntityUpdate(DataSnapshot snapshot)
        {
            //Debug.Log(snapshot.Key + " --- " + snapshot.GetRawJsonValue());
            UserEntityJson userEntity = JsonUtility.FromJson<UserEntityJson>(snapshot.GetRawJsonValue());
            ulong id = ulong.Parse(snapshot.Key);
            if (userEntity.deleted)
                return;

            if (!entities_.TryGetValue(id, out UserEntity entity))
            {
                Debug.LogWarning("Could not find anchor for entity");
                entity = new UserEntity();
                entities_.Add(id, entity);
            } else
            {
                // discard event if it has an older version
                if (AsUInt64(userEntity.version) <= entity.version)
                    return;
            }
            if (entity.user_id != userEntity.user_id)
            {
                if (entity.go)
                {
                    DestroyImmediate(entity.go);
                    entity.go = null;
                }
            }
            entity.id = id;
            entity.user_id = userEntity.user_id;
            entity.entity_pose = DecodePose(userEntity.pose);
            entity.version = AsUInt64(userEntity.version);
            entity.isMeshAnchor = entity.user_id.StartsWith("MeshAnchor");
            RefreshEntity(entity);
        }

        private void RefreshAllEntities()
        {
            foreach (var entity in entities_.Values)
            {
                RefreshEntity(entity);
            }
        }

        private bool TryConstruct(UserEntity entity)
        {
            if (string.IsNullOrEmpty(entity.user_id))
                return false;

            GameObject prefab;

            if (entity.isMeshAnchor) {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/{ScansFolder}/{entity.user_id}.gltf");
            } else {
                prefab = Resources.Load<GameObject>(entity.user_id);
            }

            if (prefab == null)
            {
                if (entity.isMeshAnchor)
                {
                    if (AutoDownloadScans && !entity.isDownloading)
                    {
                        EditorCoroutineUtility.StartCoroutine(DownloadScan(entity, 6 * 10, 10), this);
                    }
                } else
                {
                    Debug.LogWarning($"[Resight] Could not find asset: {entity.user_id}");
                }

                return false;
            }

            entity.go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            entity.go.hideFlags = HideFlags.DontSave;

            if (rootGameObject != null)
                entity.go.transform.SetParent(rootGameObject.transform, false);

            var snappedObjectEditor = entity.go.AddComponent<SnappedObjectEditor>();
            snappedObjectEditor.hideFlags = HideFlags.HideAndDontSave | HideFlags.HideInInspector; 

            // special handling for scans
            if (entity.isMeshAnchor)
            {
                var child = entity.go.transform.GetChild(0).gameObject;
                child.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                child.hideFlags |= HideFlags.DontSave | HideFlags.NotEditable;
                if (scanMaterial)
                {
                    child.GetComponent<MeshRenderer>().sharedMaterial = scanMaterial;
                }
            }
            else
            {
                if (entity.go.TryGetComponent(out Persistence.SnappedObject snappedObject))
                {
                    snappedObject.Id = entity.id;
                }

                // make sure the entity has a SnappedObject component
                // this will help identify changes in the scene that need
                // to be sent out
                snappedObjectEditor.OnTransformUpdated += SnappedObject_OnTransformUpdated;
            }

            return true;
        }

        private void RefreshEntity(UserEntity entity)
        {
            //Debug.Log($"RefreshEntity id={entity.id} user_id={entity.user_id} parent={entity.sessionId}");
            // check that the entity is attached to a visible node
            if (!nodes_.TryGetValue(entity.sessionId, out Matrix4x4 node_pose))
            {
                //Debug.Log($"Could not find node for parent {entity.sessionId}");
                // destroy dangling gameobject for invisible entities
                if (entity.go != null)
                {
                    //DestroyImmediate(entity.go.GetComponent<SnappedObjectEditor>());
                    DestroyImmediate(entity.go);
                }
                return;
            }

            bool isMeshAnchor = entity.isMeshAnchor;

            // try to instanciate the entity's prefab
            if (entity.go == null && !TryConstruct(entity))
                return;

            // update the entity's pose
            var temp = node_pose * entity.anchor_pose * entity.entity_pose;
            Quaternion q = temp.rotation;
            Vector3 t = temp.GetPosition();
            entity.go.transform.SetLocalPositionAndRotation(t, q);
            entity.go.GetComponent<SnappedObjectEditor>().DirtyRemote = true;

            // update hideFlags
            bool isShown = isMeshAnchor ? showScansInHierarchy_ : showEntitiesInHierarchy_;
            bool isEditable = !isMeshAnchor || (isMeshAnchor && showScansInHierarchy_);

            if (isShown)
                entity.go.hideFlags &= ~HideFlags.HideInHierarchy;
            else
                entity.go.hideFlags |= HideFlags.HideInHierarchy;

            if (isEditable)
                entity.go.hideFlags &= ~HideFlags.NotEditable;
            else
                entity.go.hideFlags |= HideFlags.NotEditable;
        }

        private void SnappedObject_OnTransformUpdated(GameObject sender)
        {
            var snappedObject = sender.GetComponent<Persistence.SnappedObject>();
            Debug.Assert(entities_.ContainsKey(snappedObject.Id));

            UserEntity entity = entities_[snappedObject.Id];
            Debug.Assert(nodes_.ContainsKey(entity.sessionId));

            entity.version += 1;
            entity.entity_pose = entity.anchor_pose.inverse * nodes_[entity.sessionId].inverse * snappedObject.transform.localToWorldMatrix;
            Quaternion q = entity.entity_pose.rotation;
            Vector3 t = entity.entity_pose.GetPosition();

            var userEntity = new UserEntityJson
            {
                _rnd = AsInt64(GenerateRandomId()),
                user_id = entity.user_id,
                pose = EncodePose(t, q),
                version = AsInt64(entity.version),
                size = 0
            };
            entitiesReference.Child(entity.id.ToString()).SetRawJsonValueAsync(JsonUtility.ToJson(userEntity));
        }

        private IEnumerator DownloadScan(UserEntity entity, uint retryCount, float waitSeconds)
        {
            var waiter = new EditorWaitForSeconds(waitSeconds);
            entity.isDownloading = true;
            while (retryCount-- > 0)
            {
                var t1 = Task.Run(async () => await TryDownloadScan(entity));
                yield return new WaitUntil(() => t1.IsCompleted);

                if (t1.Result)
                {
                    RefreshEntity(entity);
                    entity.isDownloading = false;
                    yield break;
                }

                //Debug.Log($"[Resight] Failed to download: {entity.user_id} ({retryCount} retries left)");
                yield return waiter;
            }
            entity.isDownloading = false;
        }

        private async Task<bool> TryDownloadScan(UserEntity entity)
        {
            //Debug.LogWarning($"TryDownloadScan: {entity.user_id}");
            try
            {
                if (scansStorageReference == null)
                    return false;

                if (!Directory.Exists($"Assets/{ScansFolder}"))
                {
                    Directory.CreateDirectory($"Assets/{ScansFolder}");
                    AssetDatabase.Refresh();
                }

                string filename = $"{entity.user_id}.gltf";

                var storageMetaData = await scansStorageReference.Child(filename).GetMetadataAsync();
                string absLocalFilePath = $"{applicationDataPath_}/{ScansFolder}/{filename}";
                string relLocalFilePath = $"Assets/{ScansFolder}/{filename}";

                if (File.Exists(absLocalFilePath))
                {
                    var remoteBytes = storageMetaData.SizeBytes;
                    var localBytes = new FileInfo(absLocalFilePath).Length;
                    if (remoteBytes == localBytes)
                        return true;
                }

                Debug.Log($"[Resight] Downloading: {filename}");
                var localDownloadFilePath = $"{Uri.UriSchemeFile}://{absLocalFilePath}";

                await scansStorageReference.Child(filename).GetFileAsync(localDownloadFilePath).ContinueWithOnMainThread(task =>
                {
                    Debug.Log($"[Resight] Importing: {filename}");
                    AssetDatabase.ImportAsset(relLocalFilePath, ImportAssetOptions.ForceSynchronousImport);
                    Debug.Log($"[Resight] Imported: {filename}");
                });

                return true;
            }
            catch (Exception)
            {
                //Debug.LogWarning($"Failed to download scan {entity.user_id}");
                return false;
            }
        }
    }
}

#endif
