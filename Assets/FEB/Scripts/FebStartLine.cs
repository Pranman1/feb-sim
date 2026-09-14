using System.Collections.Generic;
using UnityEngine;

/*
    Sits on the finish-line trigger and remembers when each car first crossed it, so the
    HUD can start its clock at the line rather than at scene load. The upstream LapTimer,
    which scoring reads, is untouched: its first counted lap still ends at the line after
    a full lap of checkpoints.
*/
public class FebStartLine : MonoBehaviour
{
    readonly Dictionary<Transform, float> firstCrossing = new Dictionary<Transform, float>();   // car root -> LapTimer.LapTime then

    void OnTriggerEnter(Collider other)
    {
        var timer = other.GetComponentInParent<LapTimer>();
        if (timer != null && !firstCrossing.ContainsKey(timer.transform)) firstCrossing[timer.transform] = timer.LapTime;
    }

    // LapTimer.LapTime at the car's first crossing, or null if it has not crossed yet.
    public float? Offset(Transform car)
    {
        return firstCrossing.TryGetValue(car, out var t) ? t : (float?)null;
    }
}
