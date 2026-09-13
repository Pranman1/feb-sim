using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/*
    Player builds for the three desktop platforms, run from the command line:

        Unity -batchmode -quit -projectPath . -executeMethod FebBuild.Linux -logFile -

    Shipped tracks are copied from FEB_TRACKS (default ../feb-racing/tracks) into
    StreamingAssets before every build. Keep secret competition tracks outside that folder.
*/
public static class FebBuild
{
    static readonly string[] Scenes = { FebScene.Output };
    const string ShippedTracks = "Assets/StreamingAssets/Tracks";

    public static void Linux() { Build(BuildTarget.StandaloneLinux64, "Builds/linux/FEB Simulator.x86_64"); }
    public static void Mac() { Build(BuildTarget.StandaloneOSX, "Builds/mac/FEB Simulator.app"); }
    public static void Windows() { Build(BuildTarget.StandaloneWindows64, "Builds/windows/FEB Simulator.exe"); }

    static void Build(BuildTarget target, string path)
    {
        SyncTracks();
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FEB/Sprites/FEB Icon.png");
        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });

        BuildReport report = BuildPipeline.BuildPlayer(Scenes, path, target, BuildOptions.None);
        Debug.Log("FEB: build " + target + " " + report.summary.result + " (" + report.summary.totalSize / 1048576 + " MB) -> " + path);
        if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    static void SyncTracks()
    {
        string source = Environment.GetEnvironmentVariable("FEB_TRACKS") ?? "../feb-racing/tracks";
        if (Directory.Exists(ShippedTracks)) Directory.Delete(ShippedTracks, true);
        foreach (string folder in Directory.GetDirectories(source))
        {
            if (!File.Exists(Path.Combine(folder, "track.json"))) continue;
            string dest = Path.Combine(ShippedTracks, Path.GetFileName(folder));
            Directory.CreateDirectory(dest);
            foreach (string file in new[] { "track.json", "preview.png" })
                if (File.Exists(Path.Combine(folder, file))) File.Copy(Path.Combine(folder, file), Path.Combine(dest, file));
        }
        AssetDatabase.Refresh();
    }
}
