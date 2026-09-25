using System;
using System.IO;
using UnityEngine;

/*
    Track description produced by feb-racing/tools/track_build.py (track.json).
    Coordinates are in the map frame (x right, y up, metres), the same frame the
    devkit reports as ROS coordinates: ROS x = Unity z, ROS y = -Unity x.
*/

[Serializable] public class TrackPose { public float x; public float y; public float yaw; }
[Serializable] public class TrackCheckpoint { public float x; public float y; public float yaw; public float width; }
[Serializable] public class TrackWall { public float[] points; }
[Serializable] public class TrackCone { public float x; public float y; public string color; }

[Serializable]
public class TrackData
{
    public int version;
    public string name;
    public string direction;
    public int max_cars;        // 0 or missing = no limit
    public string category;     // feb, feb_cones, fsae, roboracer, f1, f1_cones (the track picker's groups)
    public int difficulty;      // 1..5
    public float length;
    public float[] centreline;
    public TrackCheckpoint[] checkpoints; // checkpoints[0] is the finish line
    public TrackPose spawn;
    public TrackWall[] walls;
    public float wall_diameter;
    public string wall_color;   // hex, e.g. "#9a9a9a"
    public TrackCone[] cones;

    public static TrackData Load(string folder)
    {
        return JsonUtility.FromJson<TrackData>(File.ReadAllText(Path.Combine(folder, "track.json")));
    }

    public static Vector3 ToUnity(float x, float y, float height = 0f) { return new Vector3(-y, height, x); }

    public static Quaternion ToUnityYaw(float yaw) { return Quaternion.Euler(0f, -yaw * Mathf.Rad2Deg, 0f); }
}
