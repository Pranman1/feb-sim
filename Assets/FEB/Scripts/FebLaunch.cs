using System;
using UnityEngine;

/*
    Command-line options of the FEB Simulator (all optional):

      --track <name|folder>      track to load (default: first shipped track)
      --tracks <folder>          extra folder of track folders (the launcher passes the repo's tracks/)
      --connect <host:port>      connect to the devkit bridge on start
      --mode <manual|autonomous> driving mode on start (default: autonomous)
      --lidar-hz <rate>          lidar scan rate override
      --cars <n>                 number of cars (head-to-head)
      --camera <name>            starting camera, e.g. "God's Eye", Trackcam, "Driver's Eye"
      --look <visual|simple>     dressed scene or the bare one (default: last choice, else visual)

    Unity's own flags (-batchmode, -nographics, -screen-width ...) still apply.
*/
public static class FebLaunch
{
    public static string Track = Get("--track");        // also set by the in-app menu
    public static string Tracks = Get("--tracks");
    public static string Connect = Get("--connect");
    public static string Mode = Get("--mode");
    public static string Camera = Get("--camera");
    public static float LidarHz = float.TryParse(Get("--lidar-hz"), out var hz) ? hz : 0f;
    public static int Cars = int.TryParse(Get("--cars"), out var n) ? Math.Max(1, n) : 1;   // also set by the in-app menu

    const string LookKey = "feb.look";
    static string look;
    public static string Look
    {
        get { return look ??= Get("--look") ?? PlayerPrefs.GetString(LookKey, "visual"); }
        set { look = value; PlayerPrefs.SetString(LookKey, value); PlayerPrefs.Save(); }
    }

    static string Get(string flag)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        return null;
    }
}
