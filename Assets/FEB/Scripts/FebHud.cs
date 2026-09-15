using UnityEngine;
using UnityEngine.UI;

/*
    A race HUD for car one, bottom centre: the running lap time large, then last lap,
    best lap, lap count, collisions and speed. Replaces the small upstream lap panel.
    Built in code on the scene's canvas, in both looks: clarity costs nothing.
*/
public class FebHud : MonoBehaviour
{
    public RectTransform Canvas;       // the "User Interface" canvas
    public Font Font;                  // the toolbar's font, so the HUD matches the rest
    public GameObject UpstreamPanel;   // "Lap Times and Count", hidden

    LapTimer timer;
    Rigidbody body;
    Transform car;
    FebStartLine startLine;
    int lapsAtStart;
    Text lapTime, lastLap, bestLap, lapCount, collisions, speed, realTime;

    public void Bind(Transform vehicle, FebStartLine line)
    {
        car = vehicle;
        startLine = line;
        timer = car.GetComponent<LapTimer>();
        body = car.GetComponent<Rigidbody>();
        lapsAtStart = timer.LapCount;
        if (UpstreamPanel != null) UpstreamPanel.SetActive(false);
        Build();
    }

    void Build()
    {
        var panel = Rect("FEB HUD", Canvas, new Vector2(0.30f, 0.02f), new Vector2(0.70f, 0.17f));
        panel.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.11f, 0.78f);
        Rect("Top line", panel, new Vector2(0f, 0.96f), new Vector2(1f, 1f)).gameObject.AddComponent<Image>().color = FebLook.Gold;

        lapTime = Label(panel, "Lap time", new Vector2(0.03f, 0.42f), new Vector2(0.55f, 0.94f), 46, Color.white, TextAnchor.MiddleLeft, "--");
        speed = Label(panel, "Speed", new Vector2(0.55f, 0.42f), new Vector2(0.97f, 0.94f), 30, Color.white, TextAnchor.MiddleRight);
        realTime = Label(panel, "Real time", new Vector2(0.75f, 0.02f), new Vector2(0.97f, 0.40f), 14, FebLook.Gold, TextAnchor.LowerRight);

        float[] x = { 0.03f, 0.27f, 0.51f, 0.75f };
        string[] names = { "LAST", "BEST", "LAP", "HITS" };
        var values = new Text[4];
        for (int i = 0; i < 4; i++)
        {
            Label(panel, names[i], new Vector2(x[i], 0.22f), new Vector2(x[i] + 0.22f, 0.40f), 14, FebLook.Gold, TextAnchor.LowerLeft, names[i]);
            values[i] = Label(panel, names[i] + " value", new Vector2(x[i], 0.02f), new Vector2(x[i] + 0.22f, 0.24f), 22, Color.white, TextAnchor.UpperLeft);
        }
        lastLap = values[0]; bestLap = values[1]; lapCount = values[2]; collisions = values[3];
    }

    void Update()
    {
        if (timer == null) return;
        // the clock starts at the car's first crossing of the line, not at scene load; once the
        // upstream timer has counted a lap it resets at the line itself and is used as is
        float? offset = startLine != null ? startLine.Offset(car) : 0f;
        if (offset == null) lapTime.text = "--";
        else lapTime.text = (timer.LapCount > lapsAtStart ? timer.LapTime : timer.LapTime - offset.Value).ToString("0.0") + " s";
        lastLap.text = Time(timer.LastLapTime);
        bestLap.text = Time(timer.BestLapTime);
        lapCount.text = timer.LapCount.ToString();
        collisions.text = timer.CollisionCount.ToString();
        speed.text = body != null ? body.velocity.magnitude.ToString("0.0") + " m/s" : "";
        float rtf = FebRealTime.Factor;
        realTime.text = "RTF " + rtf.ToString("0.00");
        realTime.color = rtf < 0.9f ? new Color(1f, 0.35f, 0.3f) : FebLook.Gold;   // red when simulated time falls behind
    }

    static string Time(float t) { return float.IsInfinity(t) ? "--" : t.ToString("0.00"); }

    static RectTransform Rect(string name, RectTransform parent, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    Text Label(RectTransform parent, string name, Vector2 min, Vector2 max, int size, Color color, TextAnchor anchor, string initial = "")
    {
        var text = Rect(name, parent, min, max).gameObject.AddComponent<Text>();
        text.font = Font;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.color = color;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.text = initial;
        return text;
    }
}
