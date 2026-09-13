using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/*
    Records the car's pose during each lap and replays the best lap as a translucent
    ghost, so members can see where they gain and lose time. The ghost is saved per
    track under ~/.feb-sim/ghosts and survives restarts. It has no colliders and
    lives on the sensor-visualisation layer, so the lidar never sees it.
*/
public class GhostLap : MonoBehaviour
{
    public Material GhostMaterial;      // a transparent material (Assets/Materials/Transparent)
    public Color Tint = new Color(0.99f, 0.71f, 0.08f, 0.35f);
    public float SampleInterval = 0.05f;

    [Serializable]
    class Recording
    {
        public float lapTime;
        public float[] t;
        public float[] x, y, z, qx, qy, qz, qw;
    }

    Transform car;
    LapTimer timer;
    string file;
    Recording best;
    readonly List<(float t, Vector3 p, Quaternion q)> current = new List<(float, Vector3, Quaternion)>();
    GameObject ghost;
    int lastLapCount;
    float lastSample;

    public void Bind(Transform vehicle, string trackFolder)
    {
        car = vehicle;
        timer = vehicle.GetComponent<LapTimer>();
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".feb-sim", "ghosts");
        Directory.CreateDirectory(dir);
        file = Path.Combine(dir, Path.GetFileName(trackFolder) + ".json");
        if (File.Exists(file)) best = JsonUtility.FromJson<Recording>(File.ReadAllText(file));
        ghost = BuildGhost();
        ghost.SetActive(best != null);
        lastLapCount = timer.LapCount;
    }

    void FixedUpdate()
    {
        if (car == null) return;
        if (timer.LapCount != lastLapCount)
        {
            lastLapCount = timer.LapCount;
            if (best == null || timer.LastLapTime < best.lapTime) SaveBest(timer.LastLapTime);
            current.Clear();
        }
        if (timer.LapTime - lastSample >= SampleInterval)
        {
            lastSample = timer.LapTime;
            current.Add((timer.LapTime, car.position, car.rotation));
        }
        if (best != null) Replay(timer.LapTime);
    }

    void SaveBest(float lapTime)
    {
        int n = current.Count;
        if (n < 2) return;
        best = new Recording
        {
            lapTime = lapTime, t = new float[n], x = new float[n], y = new float[n], z = new float[n],
            qx = new float[n], qy = new float[n], qz = new float[n], qw = new float[n]
        };
        for (int i = 0; i < n; i++)
        {
            var s = current[i];
            best.t[i] = s.t; best.x[i] = s.p.x; best.y[i] = s.p.y; best.z[i] = s.p.z;
            best.qx[i] = s.q.x; best.qy[i] = s.q.y; best.qz[i] = s.q.z; best.qw[i] = s.q.w;
        }
        File.WriteAllText(file, JsonUtility.ToJson(best));
        ghost.SetActive(true);
        Debug.Log("FEB: new ghost lap " + lapTime.ToString("F2") + " s saved to " + file);
    }

    void Replay(float lapTime)
    {
        int n = best.t.Length;
        if (lapTime >= best.t[n - 1]) { ghost.SetActive(false); return; }
        ghost.SetActive(true);
        int i = Array.BinarySearch(best.t, lapTime);
        if (i < 0) i = ~i;
        i = Mathf.Clamp(i, 1, n - 1);
        float u = Mathf.InverseLerp(best.t[i - 1], best.t[i], lapTime);
        ghost.transform.position = Vector3.Lerp(new Vector3(best.x[i - 1], best.y[i - 1], best.z[i - 1]), new Vector3(best.x[i], best.y[i], best.z[i]), u);
        ghost.transform.rotation = Quaternion.Slerp(new Quaternion(best.qx[i - 1], best.qy[i - 1], best.qz[i - 1], best.qw[i - 1]),
                                                    new Quaternion(best.qx[i], best.qy[i], best.qz[i], best.qw[i]), u);
    }

    // A visual-only copy of the car: its meshes, one translucent material, no physics.
    GameObject BuildGhost()
    {
        var root = new GameObject("Ghost");
        root.transform.SetParent(transform, false);
        var material = new Material(GhostMaterial) { color = Tint };
        int layer = LayerMask.NameToLayer("SensorVisualization");
        foreach (var source in car.GetComponentsInChildren<MeshRenderer>())
        {
            var filter = source.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            var part = new GameObject(source.name);
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = car.InverseTransformPoint(source.transform.position);
            part.transform.localRotation = Quaternion.Inverse(car.rotation) * source.transform.rotation;
            part.transform.localScale = source.transform.lossyScale;
            part.layer = layer;
            part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        return root;
    }
}
