using UnityEngine;
using System.Collections;
using Resight;

namespace Resight.Utilities.Extensions
{
    internal static class TransformExtensions
    {
        private static Quaternion ToQuaternion(this RSVec4f rot)
        {
            Quaternion res = new Quaternion(rot.x, rot.y, -rot.z, -rot.w);

            return res;
        }

        private static RSVec4f ToRSRotation(this Quaternion quat, bool ignoreOrientation = true)
        {
            Quaternion rotationQuat = Quaternion.identity;

            if (!ignoreOrientation)
            {
                switch (Screen.orientation)
                {
                    case ScreenOrientation.Portrait:
                        rotationQuat = Quaternion.AngleAxis(-90.0f, Vector3.forward);
                        break;
                    case ScreenOrientation.LandscapeLeft:
                        break;
                    default:
                        break;
                }
            }
            
            var resightQuat = quat * rotationQuat;

            return new RSVec4f
            {
                x = resightQuat.x,
                y = resightQuat.y,
                z = -resightQuat.z,
                w = -resightQuat.w
            };
        }

        public static RSPose ToRSPose(this Matrix4x4 matrix)
        {
            var position = matrix.GetColumn(3);
            var rotation = Quaternion.LookRotation(matrix.GetColumn(2), matrix.GetColumn(1));
            return new RSPose
            {
                pos = new RSVec3f
                {
                    x = position.x,
                    y = position.y,
                    z = position.z
                },
                rot = new RSVec4f
                {
                    x = rotation.x,
                    y = rotation.y,
                    z = rotation.z,
                    w = rotation.w
                }
            };
        }

        public static RSPose ToRSPose(this Transform transform, bool ignoreOrientation = true)
        {
            return new RSPose
            {
                pos = new RSVec3f
                {
                    x = transform.position.x,
                    y = transform.position.y,
                    z = -transform.position.z
                },
                rot = transform.rotation.ToRSRotation(ignoreOrientation)
            };
        }

        public static void ToTransform(this RSPose pose, Transform transform)
        {
            transform.position = new Vector3
            {
                x = pose.pos.x,
                y = pose.pos.y,
                z = -pose.pos.z
            };

            transform.rotation = pose.rot.ToQuaternion();
        }

        public static Matrix4x4 ToMatrix4x4(this RSPose pose)
        {
            var position = new Vector3(pose.pos.x, pose.pos.y, pose.pos.z);
            var rotation = new Quaternion(pose.rot.x, pose.rot.y, pose.rot.z, pose.rot.w);

            var mat = Matrix4x4.identity;
            mat.SetTRS(position,
                       rotation,
                       Vector3.one);
            return mat;
        }

    } // ResightExtensions
}
