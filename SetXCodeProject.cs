#if UNITY_IOS
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEditor.iOS.Xcode;
using System.IO;

public class SetXCodeProject : MonoBehaviour
{
    [PostProcessBuild(1)]
    public static void AfterBuild(BuildTarget buildTarget, string path)
    {
        //build script for iOS to setting xcode in program
        if (buildTarget == BuildTarget.iOS)
        {
            try
            {
                string projPath = PBXProject.GetPBXProjectPath(path);
                PBXProject proj = new PBXProject();
                proj.ReadFromString(File.ReadAllText(projPath));
                string target = proj.TargetGuidByName("Unity-iPhone");

                //修改屬性
                proj.SetBuildProperty(target, "ENABLE_BITCODE", "NO");

                //add tbd
                string fileGuidLibz = proj.AddFile("usr/lib/libz.tbd", "Libraries/libz.tbd", PBXSourceTree.Sdk);
                proj.AddFileToBuild(target, fileGuidLibz);

                //add framework
                proj.AddFrameworkToProject(target, "Security.framework", false);
                proj.AddFrameworkToProject(target, "SystemConfiguration.framework", false);
                proj.AddFrameworkToProject(target, "iAD.framework", false);
                proj.AddFrameworkToProject(target, "AdSupport.framework", false);
                proj.AddFrameworkToProject(target, "AdServices.framework", false);
                proj.AddFrameworkToProject(target, "libsqlite3.0.tbd", false);
                proj.AddFrameworkToProject(target, "StoreKit.framework", false);
                proj.AddFrameworkToProject(target, "CoreTelephony.framework", false);
                proj.AddFrameworkToProject(target, "libresolv.tbd", false);
                proj.AddFrameworkToProject(target, "libc++.tbd", false);


                File.WriteAllText(projPath, proj.WriteToString());

                //2、修改Info.plist文件
                var plistPath = Path.Combine(path, "Info.plist");
                var plist = new PlistDocument();
                plist.ReadFromFile(plistPath);
                plist.root.SetString("NSPhotoLibraryUsageDescription", "photo privacy");

                File.WriteAllText(plistPath, plist.WriteToString());
            }
            catch (System.Exception e)
            {
                Debug.LogErrorFormat("setting xcod failed: {0}, {1}", e.Message, e.StackTrace);
            }
        }
    }
}

#endif
