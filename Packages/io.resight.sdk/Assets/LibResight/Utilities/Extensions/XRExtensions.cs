using System;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections.LowLevel.Unsafe;

namespace Resight.Utilities.Extensions
{
    public static class XRExtensions
    {

        public static RSBuffer ToRSBuffer(this XRCpuImage image, int planeIndex)
        {
            var plane = image.GetPlane(planeIndex);

            IntPtr buf;
            unsafe {
                buf = (IntPtr)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(plane.data);
            }

            var res = new RSBuffer
            {
                buf = buf,
                width = plane.rowStride / plane.pixelStride,
                height = plane.data.Length / plane.rowStride,
                stride = plane.rowStride
            };

            switch (image.format) {
                case XRCpuImage.Format.IosYpCbCr420_8BiPlanarFullRange:
                    res.pixel_type.type = planeIndex == 0 ? 0 : 8;
                    break;
                case XRCpuImage.Format.DepthFloat32:
                    res.pixel_type.type = 5;
                    break;
                case XRCpuImage.Format.OneComponent8:
                    res.pixel_type.type = 0;
                    break;
                default:
                    Debug.LogError("ToRSBuffer: unknown format " + image.format.ToString());
                    res.pixel_type.type = 0;
                    break;
            }

            return res;
        }
    }
}