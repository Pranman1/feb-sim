using System;

/*
    Command-line options of the FEB Simulator (all optional):

      --track <name|folder>      track to load (default: first shipped track)
      --connect <host:port>      connect to the devkit bridge on start
      --mode <manual|autonomous> driving mode on start (default: autonomous)
      --lidar-hz <rate>          lidar scan rate override
      --cars <n>                 number of cars (head-to-head)
      --camera <name>            starting camera, e.g. "God's Eye", Trackcam, "Driver's Eye"

    Unity's own flags (-batchmode, -nographics, -screen-width ...) still apply.
*/
public static class FebLaunch
{
    public static string Track = Get("--track");        // also set by the in-app track menu
    public static string Connect = Get("--connect");
    public static string Mode = Get("--mode");
    public static string Camera = Get("--camera");
    public static float LidarHz = float.TryParse(Get("--lidar-hz"), out var hz) ? hz : 0f;
    public static int Cars = int.TryParse(Get("--cars"), out var n) ? Math.Max(1, n) : 1;

    static string Get(string flag)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        return null;
    }
}
