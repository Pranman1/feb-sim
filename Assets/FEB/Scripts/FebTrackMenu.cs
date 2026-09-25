using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/*
    Menu rows in the simulator's button style: "Track: <name>" opens the track picker, "Cars: <n>"
    cycles through 1..4 cars, "Look: Visual|Simple" switches between the dressed scene and the
    bare one. Each choice reloads the scene.

    The picker is a panel built in code on the scene's canvas, in the HUD's colours: a row of
    groups across the top (FEB, FEB cones, FSAE, RoboRacer, F1, F1 cones; a group only shows
    when it has tracks), and under it the group's tracks easiest first, each with its length
    and a five-dot difficulty. Click a track to drive it; Escape, the close button or a click
    outside puts the panel away.
*/
public class FebTrackMenu : MonoBehaviour
{
    public Text TrackLabel;
    public Text CarsLabel;
    public Text LookLabel;
    const int MaxCars = 4;

    static readonly string[] Groups = { "feb", "feb_cones", "fsae", "roboracer", "f1", "f1_cones" };
    static readonly string[] GroupNames = { "FEB", "FEB cones", "FSAE", "RoboRacer", "F1", "F1 cones" };
    static readonly Color Panel = new Color(0.05f, 0.07f, 0.11f, 1f);
    static readonly Color Dim = new Color(0.62f, 0.66f, 0.72f);
    static readonly Color RowHover = new Color(1f, 1f, 1f, 0.07f);
    static readonly Color ChipOff = new Color(1f, 1f, 1f, 0.08f);

    class Entry { public string folder, name, group; public float length; public int difficulty; }

    RectTransform overlay;
    string group;                    // the open group, remembered while the app runs
    static string lastGroup;

    void Start()
    {
        TrackLabel.text = "Track: " + CurrentName();
        CarsLabel.text = "Cars: " + FebLaunch.Cars;
        if (LookLabel != null) LookLabel.text = "Look: " + (FebLook.Visual ? "Visual" : "Simple");
    }

    /// Escape closes the panel. Lives on the overlay itself: the menu object this component sits
    /// on is inactive while the side menu is folded away, so its own Update would not run.
    class EscapeCloses : MonoBehaviour
    {
        public System.Action OnEscape;
        void Update() { if (Input.GetKeyDown(KeyCode.Escape)) OnEscape(); }
    }

    static string CurrentName()
    {
        if (string.IsNullOrEmpty(FebLaunch.Track)) return "?";
        try { return TrackData.Load(FebLaunch.Track).name; }
        catch { return Path.GetFileName(FebLaunch.Track); }
    }

    public void NextLook()
    {
        FebLaunch.Look = FebLook.Visual ? "simple" : "visual";
        Reload(FebLaunch.Track, FebLaunch.Cars);
    }

    public void NextCars()
    {
        var loader = FindObjectOfType<TrackLoader>();
        int limit = loader != null && loader.Track != null && loader.Track.max_cars > 0 ? Mathf.Min(MaxCars, loader.Track.max_cars) : MaxCars;
        if (limit <= 1) return;
        Reload(FebLaunch.Track, FebLaunch.Cars % limit + 1);
    }

    static void Reload(string track, int cars)
    {
        FebLaunch.Track = track;
        FebLaunch.Cars = cars;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ------------------------------------------------------------------ the picker

    public void OpenTracks()
    {
        if (overlay != null) { Close(); return; }
        var entries = Entries();
        if (entries.Count == 0) return;
        var current = entries.FirstOrDefault(e => e.folder == FebLaunch.Track);
        group = lastGroup ?? (current != null ? current.group : entries[0].group);
        if (entries.All(e => e.group != group)) group = entries[0].group;
        Build(entries);
    }

    void Close()
    {
        if (overlay != null) Destroy(overlay.gameObject);
        overlay = null;
    }

    static List<Entry> Entries()
    {
        var list = new List<Entry>();
        foreach (var folder in TrackLibrary.Folders())
        {
            try
            {
                var t = TrackData.Load(folder);
                list.Add(new Entry {
                    folder = folder, name = string.IsNullOrEmpty(t.name) ? Path.GetFileName(folder) : t.name,
                    group = string.IsNullOrEmpty(t.category) ? (t.cones != null && t.cones.Length > 0 ? "feb_cones" : "feb") : t.category,
                    length = t.length, difficulty = Mathf.Clamp(t.difficulty, 1, 5) });
            }
            catch { }
        }
        return list.OrderBy(e => e.difficulty).ThenBy(e => e.length).ToList();
    }

    void Build(List<Entry> entries)
    {
        var canvas = GetComponentInParent<Canvas>(true).rootCanvas.GetComponent<RectTransform>();
        var font = TrackLabel.font;
        overlay = Rect("FEB Tracks", canvas, Vector2.zero, Vector2.one);
        overlay.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
        overlay.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        overlay.gameObject.AddComponent<EscapeCloses>().OnEscape = Close;

        var panel = Rect("Panel", overlay, new Vector2(0.30f, 0.17f), new Vector2(0.70f, 0.87f));
        panel.gameObject.AddComponent<Image>().color = Panel;
        Rect("Top line", panel, new Vector2(0f, 0.992f), new Vector2(1f, 1f)).gameObject.AddComponent<Image>().color = FebLook.Gold;
        Label(panel, font, "Title", new Vector2(0.04f, 0.90f), new Vector2(0.6f, 0.98f), 16, FebLook.Gold, TextAnchor.MiddleLeft, "TRACKS");
        var close = TextButton(panel, font, "Close", new Vector2(0.92f, 0.905f), new Vector2(0.98f, 0.975f), "X", 16, Color.white, new Color(0f, 0f, 0f, 0f));
        close.onClick.AddListener(Close);

        // groups across the top: only the ones with tracks
        var groups = Groups.Where(g => entries.Any(e => e.group == g)).ToList();
        foreach (var g in entries.Select(e => e.group).Distinct().Where(g => !Groups.Contains(g))) groups.Add(g);
        float x = 0.04f, step = 0.92f / groups.Count, pad = 0.006f;
        foreach (var g in groups)
        {
            bool on = g == group;
            int idx = System.Array.IndexOf(Groups, g);
            string label = idx >= 0 ? GroupNames[idx] : g;
            var chip = TextButton(panel, font, "Group " + g, new Vector2(x + pad, 0.80f), new Vector2(x + step - pad, 0.88f), label, 15,
                                  on ? Color.black : Color.white, on ? FebLook.Gold : ChipOff);
            string chosen = g;
            chip.onClick.AddListener(() => { group = lastGroup = chosen; Close(); Build(Entries()); });
            x += step;
        }

        // the tracks of the open group, one row each, scrolling when there are many
        var viewport = Rect("Viewport", panel, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.77f));
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = Rect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f));
        content.pivot = new Vector2(0.5f, 1f);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.spacing = 2f;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        foreach (var e in entries.Where(e => e.group == group))
        {
            bool current = e.folder == FebLaunch.Track;
            var row = Rect("Track " + e.name, content, Vector2.zero, Vector2.one);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;
            var bg = row.gameObject.AddComponent<Image>();
            bg.color = current ? new Color(1f, 1f, 1f, 0.05f) : new Color(1f, 1f, 1f, 0f);
            var button = row.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            button.colors = colors;
            button.targetGraphic = bg;
            var hover = row.gameObject.AddComponent<Image>();   // never used: the Button tints bg
            Destroy(hover);
            string folder = e.folder;
            button.onClick.AddListener(() => Reload(folder, FebLaunch.Cars));
            Label(row, font, "Name", new Vector2(0.02f, 0f), new Vector2(0.62f, 1f), 19, current ? FebLook.Gold : Color.white, TextAnchor.MiddleLeft, e.name);
            Label(row, font, "Length", new Vector2(0.62f, 0f), new Vector2(0.80f, 1f), 15, Dim, TextAnchor.MiddleRight, e.length.ToString("0") + " m");
            for (int i = 0; i < 5; i++)
            {
                var dot = Rect("Dot " + i, row, new Vector2(0.84f + i * 0.03f, 0.36f), new Vector2(0.84f + i * 0.03f + 0.018f, 0.64f));
                dot.gameObject.AddComponent<Image>().color = i < e.difficulty ? FebLook.Gold : new Color(1f, 1f, 1f, 0.15f);
            }
        }
    }

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

    static Text Label(RectTransform parent, Font font, string name, Vector2 min, Vector2 max, int size, Color color, TextAnchor anchor, string value)
    {
        var text = Rect(name, parent, min, max).gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.color = color;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.text = value;
        return text;
    }

    static Button TextButton(RectTransform parent, Font font, string name, Vector2 min, Vector2 max, string value, int size, Color textColor, Color back)
    {
        var rect = Rect(name, parent, min, max);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = back;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        Label(rect, font, "Label", Vector2.zero, Vector2.one, size, textColor, TextAnchor.MiddleCenter, value);
        return button;
    }
}
