using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Runtime.InteropServices;

namespace Resight.Mesh
{
    public class MeshManager : MonoBehaviour
    {
        [Tooltip("Prefab should include a MeshBlock component.")]
        [SerializeField] private GameObject meshBlockPrefab;

        [Space(10)]

        [Header("Physics")]
        [Tooltip("Disable the MeshBlock's collider, if exist.")]
        [SerializeField] private bool disableCollider = false;

        [Tooltip("Seconds to wait before updating the MeshCollider of an updated MeshBlock.")]
        [SerializeField] private float recalculateColliderInterval = 1.0f;

        public event Action<string> MeshExported;

        [SerializeField] AROcclusionManager occlusionManager;


        public enum RENDERING_OPTIONS
        {
            None = 0,
            Normals = 1,
            Colors = 2,
            Shadows = 3,
            ColorsAndShadows = 4,
            Custom = 5
        }

        [Header("Rendering")]
        [SerializeField] private RENDERING_OPTIONS renderingOptions_;

        public RENDERING_OPTIONS RenderingOptions {
            get => renderingOptions_;
            set {
                renderingOptions_ = value;
                ResetMeshBlocksMaterial();
            }   
        }

        [Header("Materials")]
        public Material materialNormals;
        public Material materialColors;
        public Material materialShadows;
        public Material materialColorsAndShadows;
        public Material materialCustom;

        private struct SubMesh
        {
            public GameObject anchor;
            public Dictionary<ulong, MeshBlock> meshBlocks;
        }
        private readonly Dictionary<ulong, SubMesh> meshes_ = new();

        private void Awake()
        {
            LibResight.Instance.OnStatus += OnStatus;
            LibResight.Instance.OnMeshBlockRemoved += OnMeshBlockRemoved;
            LibResight.Instance.OnMeshBlockUpdated += OnMeshBlockUpdated;
            LibResight.Instance.OnAnchor += OnAnchor;
            LibResight.Instance.OnMeshExported += OnMeshExported;

            if (!occlusionManager)
            {
                occlusionManager = FindObjectOfType<AROcclusionManager>();
            }
        }

        private void Start()
        {
            // The only way to access the depth texture in ARFoundation is to use the AROcclusionManager.
            // However, when it resides in the ARCamera, it will use the depth to occlude
            // and it interferes when you want to use a mesh (z fighting).
            // When AROcclusionManager is not attached to the camera, depth textures are available,
            // but depth-texture-occlusion will not be used.
            if (!occlusionManager)
            {
                occlusionManager = gameObject.AddComponent<AROcclusionManager>();
                occlusionManager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Fastest;
                occlusionManager.subsystem?.Start();
            }

            if (TryGetComponent(out LibResight libResight))
            {
                libResight.OcclusionManager = occlusionManager;
            }
        }

        private void OnMeshExported(IntPtr path)
        {
            // Suppoerted extensions: .gltf, .glb, .ply
            var file_path = Application.persistentDataPath + "/mesh.gltf";
            if (MeshExported != null)
            {
                MeshExported(file_path);
            }
        }

        public void ExportMesh()
        {
            var file_path = Application.persistentDataPath + "/mesh.gltf";
            LibResight.Log("ExportMesh: file_path=" + file_path);   
            RSMeshExport(file_path);
        }

        private void OnAnchor(RSAnchor anchor, int anchorType, byte weight, IntPtr userdata, IntPtr ctx)
        {
            if (!meshes_.TryGetValue(anchor.id, out SubMesh subMesh)) {
                subMesh = new SubMesh
                {
                    anchor = new GameObject { name = $"Anchor {anchor.id}" },
                    meshBlocks = new()
                };
                subMesh.anchor.transform.SetParent(transform, false);
                meshes_.Add(anchor.id, subMesh);
            }

            subMesh.anchor.transform.localPosition = new Vector3(anchor.pose.pos.x, anchor.pose.pos.y, -anchor.pose.pos.z);
            subMesh.anchor.transform.localRotation = new Quaternion(anchor.pose.rot.x, anchor.pose.rot.y, -anchor.pose.rot.z, -anchor.pose.rot.w);
        }

        private void SetMeshBlockMaterial(MeshBlock meshBlock)
        {
            if (meshBlock.TryGetComponent(out MeshRenderer meshRenderer))
            {
                switch (RenderingOptions)
                {
                    case RENDERING_OPTIONS.None:
                        meshRenderer.enabled = false;
                        break;
                    case RENDERING_OPTIONS.Normals:
                        meshRenderer.sharedMaterial = materialNormals;
                        break;
                    case RENDERING_OPTIONS.Colors:
                        meshRenderer.sharedMaterial = materialColors;
                        break;
                    case RENDERING_OPTIONS.Shadows:
                        meshRenderer.sharedMaterial = materialShadows;
                        break;
                    case RENDERING_OPTIONS.ColorsAndShadows:
                        meshRenderer.sharedMaterial = materialColorsAndShadows;
                        break;
                    case RENDERING_OPTIONS.Custom:
                        meshRenderer.sharedMaterial = materialCustom;
                        break;
                }
            }
        }

        private void ResetMeshBlocksMaterial()
        {
            foreach (var mesh in meshes_.Values)
            {
                foreach (var meshBlock in mesh.meshBlocks.Values)
                {
                    SetMeshBlockMaterial(meshBlock);
                }
            }
        }

        private void OnStatus(EngineState status, IntPtr ctx)
        {
            if (status == EngineState.Init) {
                foreach (var mesh in meshes_.Values)
                {
                    foreach (var meshBlock in mesh.meshBlocks.Values)
                    {
                        Destroy(meshBlock.gameObject);
                    }
                }
                meshes_.Clear();
            }
        }

        private MeshBlock AddMeshBlock(RSMeshBlockEvent block, SubMesh subMesh)
        {
            var go = Instantiate(meshBlockPrefab, subMesh.anchor.transform, false);
            go.name = $"MeshBlock {block.block_id}";

            var pos = block.block_position;

            go.transform.localPosition = new Vector3(pos.x, pos.y, pos.z);

            if (!go.TryGetComponent(out MeshBlock meshBlock)) {
                meshBlock = go.AddComponent<MeshBlock>();
            }

            meshBlock.AnchorId = block.anchor_id;
            meshBlock.BlockId = block.block_id;
            meshBlock.Bounds = new Bounds(
                new Vector3(
                    0.5f * block.block_size.x,
                    0.5f * block.block_size.y,
                    0.5f * block.block_size.z),
                new Vector3(
                    block.block_size.x,
                    block.block_size.y,
                    block.block_size.z)
                );

            meshBlock.recalculateColliderInterval = recalculateColliderInterval;
            SetMeshBlockMaterial(meshBlock);

            if (disableCollider && go.TryGetComponent(out MeshCollider meshCollider)) {
                meshCollider.enabled = false;
            }

            subMesh.meshBlocks[block.block_id] = meshBlock;

            return meshBlock;
        }

        private void OnMeshBlockRemoved(RSMeshBlockEvent block, IntPtr ctx)
        {
            if (meshes_.TryGetValue(block.anchor_id, out SubMesh subMesh))
            {
                subMesh.meshBlocks.Remove(block.block_id);
            }
        }

        private void OnMeshBlockUpdated(RSMeshBlockEvent block, IntPtr ctx)
        {
            if (meshes_.TryGetValue(block.anchor_id, out SubMesh subMesh))
            {
                if (subMesh.meshBlocks.TryGetValue(block.block_id, out MeshBlock meshBlock))
                {
                    meshBlock.UpdateData();
                }
                else
                {
                    meshBlock = AddMeshBlock(block, subMesh);
                    meshBlock.UpdateData();
                }
            }
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void RSMeshExport([MarshalAs(UnmanagedType.LPStr)] string filePath);
#else
        private static void RSMeshExport([MarshalAs(UnmanagedType.LPStr)] string filePath) { }
#endif
    }
}
