using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;
using System.Diagnostics;
using System;

public class BuildPostProcess
{
    private static string assetsPath = "Packages/io.resight.sdk/";
    public static string[] externalIncludePaths = {
    };

    public static string[] externalNativeFiles = {
        Path.GetFullPath(assetsPath + "Assets/Plugins/iOS/data.bin")
    };

    public static string[] externalResources = {
    };

    public static string[] externalLibs = {
    };

    public static void ExecuteCommand(string pathToBuildProject, string cmd, string args)
    {
        Process p = new Process();

        ProcessStartInfo si = new ProcessStartInfo();
        si.UseShellExecute = false;
        si.RedirectStandardOutput = true;
        si.CreateNoWindow = true;
        si.FileName = cmd;
        si.Arguments = args;
        si.WorkingDirectory = pathToBuildProject;

        int exitCode = -1;
        string output = null;

        try
        {
            p.StartInfo = si;
            p.Start();

            output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
        }
        catch (Exception e)
        {
            UnityEngine.Debug.Log("Run Error " + e.ToString());
        }
        finally
        {
            exitCode = p.ExitCode;
            if (exitCode < 0) {
                UnityEngine.Debug.LogError("Failed: " + output);
            }
            p.Dispose();
            p = null;
        }
    }

    [PostProcessBuildAttribute]
    public static void OnPostprocessBuild(BuildTarget buildTarget, string pathToBuiltProject)
    {
        if (buildTarget == BuildTarget.iOS)
        {
            string projPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            PBXProject proj = new PBXProject();
            proj.ReadFromString(File.ReadAllText(projPath));

            string target = proj.GetUnityMainTargetGuid();

            foreach (var file in externalNativeFiles)
            {
                proj.AddFileToBuild(target, proj.AddFile(file, file));
            }

            string[] libraryPats = new string[externalLibs.Length];
            int i = 0;
            foreach (var file in externalLibs)
            {
                var baseName = Path.GetFileName(file);
                var pathName = Path.GetDirectoryName(file);
                proj.AddFileToBuild(target, proj.AddFile("../" + file, "../" + file));
                libraryPats[i++] = "$(SRCROOT)/../" + pathName;
            }

            proj.UpdateBuildProperty(target, "LIBRARY_SEARCH_PATHS", libraryPats, null);
            proj.SetBuildProperty(target, "SUPPORTS_MAC_DESIGNED_FOR_IPHONE_IPAD", "NO");

            string[] includePaths = new string[externalIncludePaths.Length];
            i = 0;
            foreach (var include in externalIncludePaths)
            {
                includePaths[i++] = "$(SRCROOT)/../" + include;
            }

            proj.UpdateBuildProperty(target, "HEADER_SEARCH_PATHS", includePaths, null);

            //proj.SetBuildProperty(target, "SWIFT_VERSION", "5.0");
            ////proj.SetBuildProperty(target, "SWIFT_OBJC_BRIDGING_HEADER", "../resight/Unity-iPhone-Bridging-Header.h");
            //proj.SetBuildProperty(target, "DEFINES_MODULE", "YES");
            ////proj.SetBuildProperty(target, "ENABLE_BITCODE", "NO");

            proj.SetBuildProperty(proj.ProjectGuid(), "DEFINES_MODULE", "YES");
            proj.SetBuildProperty(proj.ProjectGuid(), "SWIFT_VERSION", "5.0");
            proj.SetBuildProperty(proj.ProjectGuid(), "ENABLE_BITCODE", "NO");
            proj.SetBuildProperty(proj.ProjectGuid(), "CLANG_ENABLE_MODULES", "YES");
            proj.SetBuildProperty(proj.ProjectGuid(), "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");


            File.WriteAllText(projPath, proj.WriteToString());
        }
    }

    [PostProcessBuildAttribute]
    public static void CustomizePlist(BuildTarget buildTarget, string pathToBuiltProject)
    {
        if (buildTarget == BuildTarget.iOS) {
            // Get plist
            string plistPath = pathToBuiltProject + "/Info.plist";
            PlistDocument plist = new PlistDocument();
            plist.ReadFromString(File.ReadAllText(plistPath));

            // Get root
            PlistElementDict rootDict = plist.root;
       
            // Change value of CFBundleVersion in Xcode plist
            PlistElementArray bonjourServices = rootDict.CreateArray("NSBonjourServices");
            bonjourServices.AddString("_resight._tcp");
       
            // Write to file
            File.WriteAllText(plistPath, plist.WriteToString());
        }
    }
}
