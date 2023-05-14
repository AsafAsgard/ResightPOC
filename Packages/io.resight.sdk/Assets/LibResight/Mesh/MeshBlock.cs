using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;
using Unity.Collections;
using Unity.Jobs;
using System;
using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.InteropServices;

namespace Resight.Mesh
{
    /// <summary>
    /// Represents a block in space that is part of the reconstructed mesh
    /// * A MeshFilter will be added if it's missing
    /// * A MeshCollider must exist if you want to enable collisions with the mesh
    /// * A MeshRenderer must exist if you want to enable mesh rendering
    /// * See the MeshBlockMaterial properties for more settings
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public class MeshBlock : MonoBehaviour
    {
        public float recalculateColliderInterval = 0.1f;

        public ulong AnchorId
        {
            get => anchor_id_;
            set
            {
                anchor_id_ = value;
            }
        }

        public ulong BlockId
        {
            get => block_id_;
            set
            {
                block_id_ = value;
            }
        }

        public Bounds Bounds
        {
            get => bounds_;
            set
            {
                bounds_ = value;
                meshFilter_.sharedMesh.bounds = value;
            }
        }

        private ulong anchor_id_;
        private ulong block_id_;
        private Bounds bounds_;
        private MeshFilter meshFilter_;
        private MeshCollider meshCollider_;

        private bool recalculateCollider_ = false;
        private float dtCollider_ = 0f;

        void Awake()
        {
            meshFilter_ = gameObject.GetComponent<MeshFilter>();
            //if (meshFilter_ == null) {
            //    meshFilter_ = gameObject.AddComponent<MeshFilter>();
            //}

            meshCollider_ = gameObject.GetComponent<MeshCollider>();

            var mesh = new UnityEngine.Mesh();
            mesh.MarkDynamic();
            meshFilter_.sharedMesh = mesh;
        }

        private void LateUpdate()
        {
            if (recalculateCollider_) {
                dtCollider_ += Time.deltaTime;
                if (dtCollider_ > recalculateColliderInterval) {
                    meshCollider_.sharedMesh = null;
                    meshCollider_.sharedMesh = meshFilter_.sharedMesh;
                    recalculateCollider_ = false;
                    dtCollider_ = 0f;
                }
            }
        }

        public void UpdateData()
        {
            if (LibResight.State != EngineState.Mapping) return;

            int vertices_count = -1;
            int triangles_count = -1;

            RSMeshBlock_FetchBegin(anchor_id_, block_id_, ref vertices_count, ref triangles_count);
            if (vertices_count <= 0 || triangles_count <= 0) {
                return;
            }

            var indices_count = triangles_count * 3;
            var dataArray = UnityEngine.Mesh.AllocateWritableMeshData(1);
            var data = dataArray[0];

            int vertexAttributeCount = 3;
            var vertexAttributes = new NativeArray<VertexAttributeDescriptor>(
                vertexAttributeCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory
            );

            vertexAttributes[0] = new VertexAttributeDescriptor(VertexAttribute.Position);
            vertexAttributes[1] = new VertexAttributeDescriptor(VertexAttribute.Normal);
            vertexAttributes[2] = new VertexAttributeDescriptor(VertexAttribute.Color, dimension: 4);

            data.SetVertexBufferParams(vertices_count, vertexAttributes);
            data.SetIndexBufferParams(indices_count, IndexFormat.UInt32);

            unsafe {
                var vertices_ptr = (IntPtr)data.GetVertexData<RSMeshBlockVertexElement>().GetUnsafePtr();
                var triangles_ptr = (IntPtr)data.GetIndexData<uint>().GetUnsafePtr();

                RSMeshBlock_FetchEnd(anchor_id_, block_id_, vertices_ptr, triangles_ptr);
            }

            MeshUpdateFlags meshUpdateFlags =
                MeshUpdateFlags.DontNotifyMeshUsers
                | MeshUpdateFlags.DontRecalculateBounds
                | MeshUpdateFlags.DontResetBoneBounds
                | MeshUpdateFlags.DontValidateIndices;

            var subMeshDescriptor = new SubMeshDescriptor(0, indices_count, MeshTopology.Triangles)
            {
                bounds = Bounds,
                firstVertex = 0,
                vertexCount = indices_count
            };

            data.subMeshCount = 1;
            data.SetSubMesh(0, subMeshDescriptor, meshUpdateFlags);

            UnityEngine.Mesh.ApplyAndDisposeWritableMeshData(dataArray, meshFilter_.sharedMesh, meshUpdateFlags);
            vertexAttributes.Dispose();

            if (meshCollider_ && meshCollider_.enabled) {
                recalculateCollider_ = true;
            }
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void RSMeshBlock_FetchBegin(UInt64 anchor_id, UInt64 block_id, ref int vertices_count, ref int triangles_count);

        [DllImport("__Internal")]
        private static extern void RSMeshBlock_FetchEnd(UInt64 anchor_id, UInt64 block_id, IntPtr vertex_data, IntPtr index_data);
#else
        private static void RSMeshBlock_FetchBegin(UInt64 anchor_id, UInt64 block_id, ref int vertices_count, ref int triangles_count) { }

        private static void RSMeshBlock_FetchEnd(UInt64 anchor_id, UInt64 block_id, IntPtr vertex_data, IntPtr index_data) { }
#endif
    }

}