using UnityEngine;

/*
    Simulated time and the real-time factor. Unity advances physics by at most
    Maximum Allowed Timestep (0.1 s) per rendered frame, so whenever a frame takes longer
    than that (GPU contention, a screen recorder, two cars) simulated time runs slower than
    the wall clock. The bridge stamps every message with SimTime so drivers integrate
    correctly through such stalls, and Factor tells everyone how far from real time the
    run was. Lap timers and collision counts are in simulated time.
*/
public class FebRealTime : MonoBehaviour
{
    public static double SimTime => Time.timeAsDouble;
    public static float Factor { get; private set; } = 1f;   // simulated seconds per wall second, smoothed over ~1 s

    double lastSim, lastWall;

    void Start()
    {
        lastSim = Time.timeAsDouble;
        lastWall = Time.realtimeSinceStartupAsDouble;
    }

    void Update()
    {
        double sim = Time.timeAsDouble, wall = Time.realtimeSinceStartupAsDouble;
        double dWall = wall - lastWall;
        if (dWall < 0.25) return;                       // measure over windows, not single frames
        float factor = (float)((sim - lastSim) / dWall);
        Factor = Mathf.Lerp(Factor, factor, 0.5f);
        lastSim = sim;
        lastWall = wall;
    }
}
