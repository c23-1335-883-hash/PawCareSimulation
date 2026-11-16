using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Builds the Android release APK the same way every time:
//   Builds/PawCare-v<version>/PawCare-v<version>.apk
// The version comes from Player Settings > Version. Use the menu item, or run
// Unity with: -batchmode -quit -buildTarget Android -executeMethod PawCareBuild.BuildAndroid
public static class PawCareBuild
{
    [MenuItem("PawCare/Build Android APK")]
    public static void BuildAndroid()
    {
        string root = Path.GetDirectoryName(Application.dataPath);
        string name = "PawCare-v" + PlayerSettings.bundleVersion;
        string folder = Path.Combine(root, "Builds", name);
        Directory.CreateDirectory(folder);
        string resultFile = Path.Combine(root, "Builds", "build_result.txt");

        string text;
        try
        {
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = Path.Combine(folder, name + ".apk"),
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });
            BuildSummary s = report.summary;
            text = "RESULT " + s.result + "\nversion " + PlayerSettings.bundleVersion + " (code " + PlayerSettings.Android.bundleVersionCode + ")"
                + "\noutput " + s.outputPath + "\nerrors " + s.totalErrors + " warnings " + s.totalWarnings
                + "\ntime " + s.totalTime + "\nfinished " + DateTime.Now;
        }
        catch (Exception e)
        {
            text = "RESULT Exception\n" + e;
        }
        File.WriteAllText(resultFile, text);
        Debug.Log("PawCareBuild: " + text);
    }
}
