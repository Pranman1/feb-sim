using System.Collections.Generic;
using UnityEngine;

/*
    Sits on the finish line and remembers when each car first drove across it (from behind
    the line to ahead of it, within its width), so the HUD can start its clock at the line
    rather than at scene load. Geometry, not the trigger: the grid is so close to the line
    that a car can spawn touching the trigger. The upstream LapTimer, which scoring reads,
    is untouched: its first counted lap still ends at the line after a full lap of checkpoints.
*/
public class FebStartLine : MonoBehaviour
{
    public float HalfWidth = 1f;

    readonly List<(Transform car, LapTimer timer)> cars = new List<(Transform, LapTimer)>();
    readonly Dictionary<Transform, float> lastAlong = new Dictionary<Transform, float>();
    readonly Dictionary<Transform, float> firstCrossing = new Dictionary<Transform, float>();   // car -> LapTimer.LapTime then

    public void Watch(Transform car)
    {
        var timer = car.GetComponent<LapTimer>();
        if (timer != null) cars.Add((car, timer));
    }

    void FixedUpdate()
    {
        foreach (var (car, timer) in cars)
        {
            if (firstCrossing.ContainsKey(car)) continue;
            Vector3 local = transform.InverseTransformPoint(car.position);   // z: along the driving direction
            bool had = lastAlong.TryGetValue(car, out float before);
            lastAlong[car] = local.z;
            if (had && before < 0f && local.z >= 0f && Mathf.Abs(local.x) <= HalfWidth) firstCrossing[car] = timer.LapTime;
        }
    }

    // LapTimer.LapTime at the car's first crossing, or null if it has not crossed yet.
    public float? Offset(Transform car)
    {
        return firstCrossing.TryGetValue(car, out var t) ? t : (float?)null;
    }
}
