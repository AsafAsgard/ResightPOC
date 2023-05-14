using System;
using System.IO;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Resight.Utilities;
using Resight.Utilities.Extensions;

namespace Resight
{
    // Native delegates
    public delegate void RSOnStatus(EngineState status, IntPtr ctx);
    public delegate void RSOnAnchor(RSAnchor anchor, int anchorType, byte weight, IntPtr userdata, IntPtr ctx);
    public delegate void RSOnEntity(RSEntity entity, IntPtr ctx);
    public delegate void RSOnMeshBlock(RSMeshBlockEvent block, IntPtr ctx);
    public delegate void RSOnMeshExported(IntPtr path);

    public enum EngineState
    {
        Uninitialized = -1,
        Init = 0,
        Mapping,
        Stopping,
        Stopped
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RSVec3f
    {
        public float x;
        public float y;
        public float z;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RSVec4f
    {
        public float x;
        public float y;
        public float z;
        public float w;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RSPose
    {
        public RSVec3f pos;
        public RSVec4f rot;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RSAnchor
    {
        public ulong id;
        public ulong parentId;
        public RSPose pose;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RSEntity
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string id;
        public ulong parentId;
        public RSPose pose;
        public ulong dtSize;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 512)]
        public byte[] data;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RSMeshBlockEvent
    {
        public ulong anchor_id;
        public ulong block_id;
        public RSVec3f block_position;
        public RSVec3f block_size;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RSMeshBlockVertexElement
    {
        public RSVec3f position;
        public RSVec3f normal;
        public RSVec4f color;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct RSMeshBlockTriangle
    {
        public fixed uint vertices[3];
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RSConfiguration
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string dataPath;

        [MarshalAs(UnmanagedType.LPStr)]
        public string libPath;

        [MarshalAs(UnmanagedType.LPStr)]
        public string developerKey;

        [MarshalAs(UnmanagedType.LPStr)]
        public string ns;

        public RSOnStatus OnStatus;
        public RSOnAnchor OnAnchor;

        public RSOnEntity OnEntityAdded;
        public RSOnEntity OnEntityRemoved;
        public RSOnEntity OnEntityPoseUpdated;
        public RSOnEntity OnEntityDataUpdated;

        public RSOnMeshBlock OnMeshBlockRemoved;
        public RSOnMeshBlock OnMeshBlockUpdated;

        public RSOnMeshExported OnMeshExported;

        public IntPtr ctx;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RSLightEstimate
    {
        public float ambient_intensity;
        public float ambient_color_temperature;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RSPixelType
    {
        public int type;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct RSCamera
    {
        public RSPose pose;
        public fixed float intrinsics[4];
        public fixed float image_resolution[2];
        public float exposure_duration;
        public float exposure_offset;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RSBuffer
    {
        public IntPtr buf;
        public int width;
        public int height;
        public int stride;
        public RSPixelType pixel_type;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RSFrameEvent
    {
        public ulong frame_ts;
        public int tracking_state;
        public RSCamera camera;
        public RSBuffer yplane;
        public RSBuffer uvplane;
        public RSBuffer depth_plane;
        public RSBuffer depth_confidence_plane;
        public RSBuffer humen_stencil;
        public RSBuffer humen_depth;
        public RSLightEstimate light_estimate;
        public IntPtr arframe;
    }

    [DefaultExecutionOrder(ARUpdateOrder.k_Session + 4)]
    public class LibResight : MonoBehaviour
    {
        public string devKey;
        public string nameSpace;
        [SerializeField] ARCameraManager cameraManager;

        public event RSOnStatus OnStatus;
        public event RSOnAnchor OnAnchor;
        public event RSOnEntity OnEntityAdded;
        public event RSOnEntity OnEntityRemoved;
        public event RSOnEntity OnEntityPoseUpdated;
        public event RSOnEntity OnEntityDataUpdated;
        public event RSOnMeshBlock OnMeshBlockRemoved;
        public event RSOnMeshBlock OnMeshBlockUpdated;
        public event RSOnMeshExported OnMeshExported;

        public static LibResight Instance { get; private set; }
        public static EngineState State { get; private set; } = EngineState.Uninitialized;

        private bool running_ = false;

        private AROcclusionManager occlusionManager_;
        public AROcclusionManager OcclusionManager
        {
            get => occlusionManager_;
            set
            {
                occlusionManager_ = value;
            }
        }

        void Awake()
        {
            Log("[LibResight] Awake()");

            if (Instance != null && Instance != this) {
                Log("[LibResight] Only one instance of LibResight is allowed");
                return;
            }

            Instance = this;

            //Application.targetFrameRate = 60;
        }

        void Start()
        {
            Log("[LibResight] Start()");
            running_ = true;
            Init();
        }

        void Update()
        {
            RSTick(iOSTimeNow());
        }

        private void Init()
        {
            Log("[LibResight] Init()");
            MainQueue.Reset();

            var conf = new RSConfiguration();
            var timestamp = iOSTimeNow();
            var dataPath = Application.persistentDataPath;
            var dirInf = new DirectoryInfo(dataPath);
            if (!dirInf.Exists) {
                dirInf.Create();
            }

            conf.dataPath = dataPath;
            conf.libPath = Application.dataPath + "/..";
            conf.developerKey = devKey;
            conf.ns = nameSpace;
            conf.OnStatus = OnStatus_;
            conf.OnAnchor = OnAnchor_;
            conf.OnEntityAdded = OnEntityAdded_;
            conf.OnEntityRemoved = OnEntityRemoved_;
            conf.OnEntityPoseUpdated = OnEntityPoseUpdated_;
            conf.OnEntityDataUpdated = OnEntityDataUpdated_;
            conf.OnMeshBlockRemoved = OnMeshBlockRemoved_;
            conf.OnMeshBlockUpdated = OnMeshBlockUpdated_;
            conf.OnMeshExported = OnMeshExported_;
            
            conf.ctx = (IntPtr)0;

            Log("[LibResight] dataPath=" + conf.dataPath + " libPath=" + conf.libPath);

            RSInitialize(timestamp, conf);
            iOSLocationServicesStart();
        }

        void OnEnable()
        {
            Log($"[LibResight] OnEnable()");

            if (cameraManager != null) {
                cameraManager.frameReceived += OnARFrame;
            }
            Application.logMessageReceived += HandleLog;
        }

        void OnDisable()
        {
            Log($"[LibResight] OnDisable()");

            if (cameraManager != null) {
                cameraManager.frameReceived -= OnARFrame;
            }

            Application.logMessageReceived -= HandleLog;
        }

        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            if (type == LogType.Exception)
            {
                Halt("[LibResight] [E] " + logString + " " + stackTrace);
            }
        }

        void OnApplicationPause(bool paused)
        {
            Log($"[LibResight] OnApplicationPause({paused})");

            if (State == EngineState.Init) {
                return;
            }

            if (paused) {
                Log("[LibResight] Stopping ResightSDK...");
                RSStop(iOSTimeNow());
                iOSLocationServicesStop();
            } else {
                if (!running_) {
                    Init();
                }
            }
        }

        unsafe void OnARFrame(ARCameraFrameEventArgs events)
        {
            if (!cameraManager) { return; }

            switch (Screen.orientation) {
                case ScreenOrientation.Portrait:
                    break;
                case ScreenOrientation.LandscapeLeft:
                    break;
                default:
                    Log("[LibResight] ReSight only supports left-landscape/portrait orientation. Ignoring frames...");
                    return;
            }

            switch (ARSession.state) {
                case ARSessionState.SessionTracking:
                    break;
                default:
                    return;
            }

            switch (State) {
                case EngineState.Uninitialized:
                    Log("[LibResight] ResightSDK hasn't been initialized. Ignoring frame");
                    return;
            }

            if (!cameraManager.TryGetIntrinsics(out XRCameraIntrinsics intrinsics)) {
                Log("[LibResight] Couldn't get camera intrinsics. Ignoring frame");
                return;
            }

            var frameEvent = new RSFrameEvent
            {
                frame_ts = iOSTimeNow(),
                tracking_state = 1
            };

            frameEvent.camera.pose = cameraManager.transform.ToRSPose(false);
            frameEvent.camera.intrinsics[0] = intrinsics.focalLength.x;
            frameEvent.camera.intrinsics[1] = intrinsics.focalLength.y;
            frameEvent.camera.intrinsics[2] = intrinsics.principalPoint.x;
            frameEvent.camera.intrinsics[3] = intrinsics.principalPoint.y;
            frameEvent.camera.exposure_duration = (float)(events.exposureDuration ?? 0);
            frameEvent.camera.exposure_offset = events.exposureOffset ?? 0;
            frameEvent.light_estimate.ambient_intensity = events.lightEstimation.averageIntensityInLumens ?? 0;
            frameEvent.light_estimate.ambient_color_temperature = events.lightEstimation.averageColorTemperature ?? 0;

            var yuv = new XRCpuImage();
            var depth = new XRCpuImage();
            var depth_confidence = new XRCpuImage();
            var human_stencil = new XRCpuImage();
            var human_depth = new XRCpuImage();
            var cameraParams = new XRCameraParams
            {
                zNear = Camera.main.nearClipPlane,
                zFar = Camera.main.farClipPlane,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                screenOrientation = Screen.orientation
            };
            XRCameraFrame xrFrame = default;
            GCHandle handle = default;

            if (cameraManager.subsystem.TryGetLatestFrame(cameraParams, out xrFrame))
            {
                handle = GCHandle.Alloc(xrFrame, GCHandleType.Pinned);
                frameEvent.arframe = xrFrame.nativePtr;
            }

            if (cameraManager.TryAcquireLatestCpuImage(out yuv)) {
                frameEvent.yplane = yuv.ToRSBuffer(0);
                frameEvent.uvplane = yuv.ToRSBuffer(1);
            }
            else {
                Log("[LibResight] Couldn't get image of frame. Ignoring frame");
                return;
            }

            frameEvent.camera.image_resolution[0] = yuv.width;
            frameEvent.camera.image_resolution[1] = yuv.height;

            if (occlusionManager_ && occlusionManager_.enabled && occlusionManager_.TryAcquireEnvironmentDepthCpuImage(out depth)) {
                frameEvent.depth_plane = depth.ToRSBuffer(0);
            }

            if (occlusionManager_ && occlusionManager_.enabled && occlusionManager_.TryAcquireEnvironmentDepthConfidenceCpuImage(out depth_confidence)) {
                frameEvent.depth_confidence_plane = depth_confidence.ToRSBuffer(0);
            }

            if (occlusionManager_ && occlusionManager_.enabled && occlusionManager_.TryAcquireHumanStencilCpuImage(out human_stencil))
            {
                frameEvent.humen_stencil = human_stencil.ToRSBuffer(0);
            }

            if (occlusionManager_ && occlusionManager_.enabled && occlusionManager_.TryAcquireHumanDepthCpuImage(out human_depth))
            {
                frameEvent.humen_depth = human_depth.ToRSBuffer(0);
            }


            RSOnFrame(iOSTimeNow(), frameEvent);

            yuv.Dispose();
            depth.Dispose();
            depth_confidence.Dispose();
            human_stencil.Dispose();
            human_depth.Dispose();
            handle.Free();
        }

        public static void Log(string fmt, params object[] args)
        {
            var s = string.Format("[Unity]: " + fmt, args);
            RSLog(s);
        }

        public static void Halt(string fmt, params object[] args)
        {
            var s = string.Format("[Unity]: " + fmt, args);
            RSHalt(s);
        }

        [MonoPInvokeCallback(typeof(RSOnStatus))]
        static void OnStatus_(EngineState status, IntPtr ctx)
        {
            Log($"[LibResight] OnStatus_(): {status}");

            if (status == EngineState.Stopped) {
                Log("[LibResight] OnStatus_(): Stopped -> tearing down");
                RSTearDown();
                Instance.running_ = false;
            }

            MainQueue.Enqueue(() => {
                State = status;
                Log($"[LibResight] OnStatus(): {State}");

                if (status == EngineState.Mapping)
                {
                    Log("Stopping location services to lower power consumption");
                    iOSLocationServicesStop();
                }

                Instance.OnStatus?.Invoke(status, ctx);
            });
        }

        [MonoPInvokeCallback(typeof(RSOnAnchor))]
        static void OnAnchor_(RSAnchor anchor, int anchorType, byte weight, IntPtr userdata, IntPtr ctx)
        {
            MainQueue.Enqueue(() => {
                Instance.OnAnchor?.Invoke(anchor, anchorType, weight, userdata, ctx);
            });
        }

        [MonoPInvokeCallback(typeof(RSOnEntity))]
        static void OnEntityAdded_(RSEntity entity, IntPtr ctx)
        {
            MainQueue.Enqueue(() => {
                Instance.OnEntityAdded?.Invoke(entity, ctx);
            });
        }

        [MonoPInvokeCallback(typeof(RSOnEntity))]
        static void OnEntityRemoved_(RSEntity entity, IntPtr ctx)
        {
            MainQueue.Enqueue(() => {
                Instance.OnEntityRemoved?.Invoke(entity, ctx);
            });
        }

        [MonoPInvokeCallback(typeof(RSOnEntity))]
        static void OnEntityPoseUpdated_(RSEntity entity, IntPtr ctx)
        {
            MainQueue.Enqueue(() => {
                Instance.OnEntityPoseUpdated?.Invoke(entity, ctx);
            });
        }

        [MonoPInvokeCallback(typeof(RSOnEntity))]
        static void OnEntityDataUpdated_(RSEntity entity, IntPtr ctx)
        {
            MainQueue.Enqueue(() => {
                Instance.OnEntityDataUpdated?.Invoke(entity, ctx);
            });
        }

        [MonoPInvokeCallback(typeof(RSOnMeshBlock))]
        static void OnMeshBlockRemoved_(RSMeshBlockEvent block, IntPtr ctx)
        {
            MainQueue.Enqueue(() => {
                Instance.OnMeshBlockRemoved?.Invoke(block, ctx);
            });
        }

        [MonoPInvokeCallback(typeof(RSOnMeshBlock))]
        static void OnMeshBlockUpdated_(RSMeshBlockEvent block, IntPtr ctx)
        {
            MainQueue.Enqueue(() => {
                Instance.OnMeshBlockUpdated?.Invoke(block, ctx);
            });
        }

        [MonoPInvokeCallback(typeof(RSOnMeshExported))]
        static void OnMeshExported_(IntPtr path)
        {
            MainQueue.Enqueue(() => {
                if (path == IntPtr.Zero)
                {
                    return;
                }
                
                Instance.OnMeshExported?.Invoke(path);
            });
        }


#if UNITY_IOS && !UNITY_EDITOR
        // Native functions
        [DllImport("__Internal")]
        private static extern void RSInitialize(ulong ts, RSConfiguration conf);

        [DllImport("__Internal")]
        private static extern void RSTearDown();

        [DllImport("__Internal")]
        private static extern void RSStop(ulong ts);

        [DllImport("__Internal")]
        private static extern EngineState RSGetState();

        [DllImport("__Internal")]
        private static extern void RSTick(ulong ts);

        [DllImport("__Internal")]
        private static extern void RSOnFrame(ulong ts, RSFrameEvent frameEvent);

        [DllImport("__Internal")]
        private static extern void RSLog([MarshalAs(UnmanagedType.LPStr)] string fmt);

        [DllImport("__Internal")]
        private static extern void RSHalt([MarshalAs(UnmanagedType.LPStr)] string fmt);

        [DllImport("__Internal")]
        private static extern void iOSLocationServicesStart();

        [DllImport("__Internal")]
        private static extern void iOSLocationServicesStop();

        [DllImport("__Internal")]
        private static extern ulong iOSTimeNow();
#else
        private static void RSInitialize(ulong ts, RSConfiguration conf) {}

        private static void RSTearDown() {}

        private static void RSStop(ulong ts) {}

        private static EngineState RSGetState() {return EngineState.Mapping; }

        private static void RSTick(ulong ts) {}

        private static void RSOnFrame(ulong ts, RSFrameEvent frameEvent) {}

        private static void iOSLocationServicesStart() {}

        private static void iOSLocationServicesStop() {}

        private static ulong iOSTimeNow() {return 0;}

        private static void RSLog(string fmt) { }

        private static void RSHalt(string fmt) { }

#endif
    }


} // namespace Resight
