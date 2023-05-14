
using UnityEditor;

using UnityEngine;

[InitializeOnLoad]
public class BuildPreProcess
{
   static BuildPreProcess()
    {
        PlayerSettings.allowUnsafeCode = true;
        PlayerSettings.SetArchitecture(BuildTargetGroup.iOS, 1); //ARM64
        PlayerSettings.iOS.targetOSVersionString = "14.0";
        PlayerSettings.iOS.cameraUsageDescription = "Resight AR Engine";
        PlayerSettings.iOS.locationUsageDescription = "Resight AR Engine";

        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
    }
}
