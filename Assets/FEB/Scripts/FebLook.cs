using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/*
    The "Visual" look: everything cosmetic that makes the scene feel like a race track
    rather than a physics test. Nothing here has a collider or touches the car's physics,
    so "Simple" (the bare scene) and "Visual" score identically.

      ground   the floor tinted, a dark asphalt ribbon along the corridor with white edge lines
      walls    white air ducts with FEB blue bands (UV texture on the tube mesh)
      lines    a chequered start/finish line, gold marks at every checkpoint
      sky      a gradient sky and a warmer, lower sun
      livery   the deck (wing and rear panel) wrapped blue with gold edge streaks, chassis plate and
               crash members gold, the rear decals on a white plate

    Selected with --look visual|simple, or the menu's Look button; remembered between runs.
*/
public class FebLook : MonoBehaviour
{
    public static readonly Color Blue = new Color(0.110f, 0.275f, 0.529f);   // #1C4687
    public static readonly Color Gold = new Color(0.992f, 0.710f, 0.082f);   // #FDB515
    static readonly Color Asphalt = new Color(0.16f, 0.17f, 0.19f);

    public float BandPeriod = 1.25f;          // m of duct per blue band
    public float BandWidth = 0.25f;           // m, the blue part of a period
    public float EdgeLine = 0.04f;            // m, the white line along each edge of the asphalt
    public float RoadLift = 0.004f;           // m above the floor: no z-fighting, no bump the car can feel

    public static bool Visual => FebLaunch.Look != "simple";

    public void Apply(TrackLoader loader)
    {
        if (!Visual) return;
        var track = loader.Track;
        var root = new GameObject("Look");
        root.transform.SetParent(transform, false);
        TintFloor();
        Road(root.transform, track, loader.WallMaterial);
        Lines(root.transform, track, loader.WallMaterial);
        BandWalls(loader);
        Sky();
        foreach (var car in loader.Vehicles) Livery(car);
    }

    // ------------------------------------------------------------------ ground

    static void TintFloor()
    {
        var floor = GameObject.Find("Floor");
        var renderer = floor != null ? floor.GetComponent<MeshRenderer>() : null;
        if (renderer == null) return;
        var m = renderer.material;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.55f, 0.58f, 0.64f));
    }

    // Asphalt ribbon along the centreline, as wide as the corridor (nearest wall or cone),
    // plus a white line along each edge.
    void Road(Transform parent, TrackData track, Material template)
    {
        int n = track.centreline.Length / 2;
        if (n < 3) return;
        var centre = new Vector3[n];
        var width = new float[n];
        var obstacles = Obstacles(track);
        for (int i = 0; i < n; i++)
        {
            centre[i] = TrackData.ToUnity(track.centreline[2 * i], track.centreline[2 * i + 1], RoadLift);
            float nearest = float.MaxValue;
            foreach (var o in obstacles) nearest = Mathf.Min(nearest, (o - centre[i]).sqrMagnitude);
            width[i] = obstacles.Count > 0 ? Mathf.Max(0.3f, Mathf.Sqrt(nearest) - track.wall_diameter / 2f) : 1.0f;
        }
        // smooth the width a little so wall joints do not scallop the edge
        var smooth = new float[n];
        for (int i = 0; i < n; i++) smooth[i] = (width[(i - 1 + n) % n] + width[i] + width[(i + 1) % n]) / 3f;

        var asphalt = new Material(template) { name = "Asphalt" };
        SetColor(asphalt, Asphalt);
        if (asphalt.HasProperty("_BaseColorMap")) { asphalt.SetTexture("_BaseColorMap", AsphaltTexture()); asphalt.SetTextureScale("_BaseColorMap", new Vector2(0.5f, 0.5f)); }
        var white = new Material(template) { name = "Edge line" };
        SetColor(white, new Color(0.92f, 0.92f, 0.92f));

        Strip(parent, "Asphalt", centre, i => -smooth[i], i => smooth[i], asphalt, 0f);
        Strip(parent, "Edge left", centre, i => smooth[i] - EdgeLine, i => smooth[i], white, 0.001f);
        Strip(parent, "Edge right", centre, i => -smooth[i], i => -smooth[i] + EdgeLine, white, 0.001f);
    }

    static List<Vector3> Obstacles(TrackData track)
    {
        var list = new List<Vector3>();
        if (track.walls != null)
            foreach (var wall in track.walls)
                for (int i = 0; i + 1 < wall.points.Length; i += 2) list.Add(TrackData.ToUnity(wall.points[i], wall.points[i + 1], 0f));
        if (list.Count == 0 && track.cones != null)
            foreach (var cone in track.cones) list.Add(TrackData.ToUnity(cone.x, cone.y, 0f));
        return list;
    }

    // A closed strip between two lateral offsets of the centreline (positive = driver's left).
    static void Strip(Transform parent, string name, Vector3[] centre, System.Func<int, float> inner, System.Func<int, float> outer, Material material, float lift)
    {
        int n = centre.Length;
        var vertices = new Vector3[2 * n];
        var uv = new Vector2[2 * n];
        float along = 0f;
        for (int i = 0; i < n; i++)
        {
            Vector3 tangent = (centre[(i + 1) % n] - centre[(i - 1 + n) % n]).normalized;
            Vector3 left = Vector3.Cross(tangent, Vector3.up).normalized;
            if (i > 0) along += Vector3.Distance(centre[i], centre[i - 1]);
            vertices[2 * i] = centre[i] + left * inner(i) + Vector3.up * lift;
            vertices[2 * i + 1] = centre[i] + left * outer(i) + Vector3.up * lift;
            uv[2 * i] = new Vector2(along, 0f);
            uv[2 * i + 1] = new Vector2(along, 1f);
        }
        var triangles = new int[6 * n];
        for (int i = 0; i < n; i++)
        {
            int a = 2 * i, b = 2 * i + 1, c = 2 * ((i + 1) % n), d = 2 * ((i + 1) % n) + 1;
            triangles[6 * i] = a; triangles[6 * i + 1] = b; triangles[6 * i + 2] = c;   // clockwise seen from above
            triangles[6 * i + 3] = b; triangles[6 * i + 4] = d; triangles[6 * i + 5] = c;
        }
        var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    static Texture2D AsphaltTexture()
    {
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Asphalt", wrapMode = TextureWrapMode.Repeat };
        var pixels = new Color32[size * size];
        var rng = new System.Random(7);
        for (int i = 0; i < pixels.Length; i++)
        {
            float g = 0.82f + 0.18f * (float)rng.NextDouble();          // fine grain
            g *= 0.9f + 0.1f * Mathf.PerlinNoise((i % size) / 23f, (i / size) / 23f);  // slow patches
            byte v = (byte)(255f * Mathf.Clamp01(g));
            pixels[i] = new Color32(v, v, v, 255);
        }
        tex.SetPixels32(pixels);
        tex.Apply(true);
        return tex;
    }

    // ------------------------------------------------------------------ lines

    void Lines(Transform parent, TrackData track, Material template)
    {
        if (track.checkpoints == null || track.checkpoints.Length == 0) return;
        var chequer = new Material(template) { name = "Chequer" };
        SetColor(chequer, Color.white);
        if (chequer.HasProperty("_BaseColorMap")) chequer.SetTexture("_BaseColorMap", ChequerTexture());
        var gold = new Material(template) { name = "Gold" };
        SetColor(gold, Gold);

        for (int i = 0; i < track.checkpoints.Length; i++)
        {
            var cp = track.checkpoints[i];
            var pose = TrackData.ToUnity(cp.x, cp.y, RoadLift + 0.002f);
            var rotation = TrackData.ToUnityYaw(cp.yaw);
            if (i == 0)
                Quad(parent, "Start line", pose, rotation, cp.width, 0.35f, chequer);
            else
                foreach (float side in new[] { -1f, 1f })
                    Quad(parent, "Checkpoint mark", pose + rotation * new Vector3(side * (cp.width / 2f - 0.10f), 0f, 0f), rotation, 0.12f, 0.12f, gold);
        }
    }

    // A flat rectangle on the floor: x = width (across the track), z = length (along it).
    static void Quad(Transform parent, string name, Vector3 centre, Quaternion rotation, float width, float length, Material material)
    {
        var mesh = new Mesh { name = name };
        mesh.vertices = new[] { new Vector3(-width / 2f, 0f, -length / 2f), new Vector3(-width / 2f, 0f, length / 2f),
                                new Vector3(width / 2f, 0f, length / 2f), new Vector3(width / 2f, 0f, -length / 2f) };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(width / 0.35f * 2f, 1f), new Vector2(width / 0.35f * 2f, 0f) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(centre, rotation);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    static Texture2D ChequerTexture()
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "Chequer", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        tex.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(20, 20, 20, 255), new Color32(20, 20, 20, 255), new Color32(255, 255, 255, 255) });
        tex.Apply();
        return tex;
    }

    // ------------------------------------------------------------------ walls

    void BandWalls(TrackLoader loader)
    {
        if (loader.WallRenderers.Count == 0) return;
        var material = new Material(loader.WallMaterial) { name = "Banded duct" };
        SetColor(material, Color.white);
        if (material.HasProperty("_BaseColorMap"))
        {
            material.SetTexture("_BaseColorMap", BandTexture());
            material.SetTextureScale("_BaseColorMap", new Vector2(1f / BandPeriod, 1f));   // u is metres along the duct
        }
        foreach (var renderer in loader.WallRenderers) renderer.sharedMaterial = material;
    }

    Texture2D BandTexture()
    {
        const int w = 128;
        var tex = new Texture2D(w, 4, TextureFormat.RGBA32, false) { name = "Band", wrapMode = TextureWrapMode.Repeat };
        int blue = Mathf.RoundToInt(w * BandWidth / BandPeriod);
        var pixels = new Color32[w * 4];
        for (int x = 0; x < w; x++)
        {
            Color32 c = x < blue ? (Color32)Blue : new Color32(225, 225, 228, 255);
            for (int y = 0; y < 4; y++) pixels[y * w + x] = c;
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    // ------------------------------------------------------------------ sky and sun

    static void Sky()
    {
        var go = new GameObject("FEB Sky");
        var volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.sharedProfile = profile;
        var environment = profile.Add<VisualEnvironment>(false);
        environment.skyType.Override((int)SkyType.Gradient);
        var sky = profile.Add<GradientSky>(false);
        sky.top.Override(new Color(0.20f, 0.42f, 0.78f));
        sky.middle.Override(new Color(0.62f, 0.76f, 0.92f));
        sky.bottom.Override(new Color(0.84f, 0.86f, 0.88f));
        sky.gradientDiffusion.Override(1.2f);
        sky.exposure.Override(0f);
        sky.multiplier.Override(1f);

        foreach (var light in FindObjectsOfType<Light>())
            if (light.type == LightType.Directional)
            {
                light.color = new Color(1.0f, 0.96f, 0.88f);
                light.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            }
    }

    // ------------------------------------------------------------------ livery

    // Colours on material instances: the prefab and its physics stay untouched. The body panels
    // are the large parts (deck, wing, bulkheads); the crash members and chassis plate go gold.
    static void Livery(Transform car)
    {
        foreach (var renderer in car.GetComponentsInChildren<MeshRenderer>())
        {
            string part = renderer.gameObject.name;
            if (part.StartsWith("Wheel") || part.Contains("Tire") || part.Contains("Tyre")) continue;
            var size = renderer.bounds.size;
            float extent = Mathf.Max(size.x, size.y, size.z);
            Color? color = null;
            if (part.StartsWith("Platform Deck") || part.StartsWith("Rear Shock Tower")) { Wrap(renderer, car); continue; }   // the deck, and the wing with its endplates and rear panel
            if (part.StartsWith("Chassis") || part.Contains("Crash Member") || part.Contains("Bumper")) color = Gold;
            else if (part.Contains("Bulkhead") || extent > 0.20f) color = Blue;
            if (color == null) continue;
            var materials = renderer.materials;      // instances
            foreach (var m in materials) SetColor(m, color.Value);
            renderer.materials = materials;
        }
        DecalPlate(car);
    }

    // The deck and the rear tower (wing, endplates, rear panel) are CAD meshes without texture coordinates. Give the
    // renderer's own copy of the mesh planar coordinates from the car's axes (u across, v along)
    // and a wrap texture: blue with gold streaks along the outer edges and the trailing edge.
    // Only the rendered copy changes; colliders and the prefab asset are untouched.
    static void Wrap(MeshRenderer renderer, Transform car)
    {
        var filter = renderer.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return;
        var mesh = filter.mesh;                     // instance for this renderer
        var vertices = mesh.vertices;
        var local = new Vector3[vertices.Length];
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        for (int i = 0; i < vertices.Length; i++)
        {
            local[i] = car.InverseTransformPoint(renderer.transform.TransformPoint(vertices[i]));
            min = Vector3.Min(min, local[i]);
            max = Vector3.Max(max, local[i]);
        }
        var uv = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
            uv[i] = new Vector2(Mathf.InverseLerp(min.x, max.x, local[i].x), Mathf.InverseLerp(min.z, max.z, local[i].z));
        mesh.uv = uv;

        var materials = renderer.materials;
        foreach (var m in materials)
        {
            SetColor(m, Color.white);
            if (m.HasProperty("_BaseColorMap")) { m.SetTexture("_BaseColorMap", WrapTexture()); m.SetTextureScale("_BaseColorMap", Vector2.one); }
        }
        renderer.materials = materials;
    }

    static Texture2D WrapTexture()
    {
        const int size = 512;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Deck wrap", wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        Color32 blue = Blue, gold = Gold;
        for (int y = 0; y < size; y++)
        {
            float v = (float)y / size;                          // 0 = rear edge, 1 = nose
            float streak = 0.045f + 0.075f * (1f - v);          // edge streaks widen toward the rear
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size;                      // 0 = driver's right edge, 1 = left edge
                bool edge = u < streak || u > 1f - streak;
                bool trailing = v < 0.035f;
                pixels[y * size + x] = edge || trailing ? gold : blue;
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(true);
        return tex;
    }

    // The rear decals sit on the (now blue) deck: give them a white plate so both colours of the mark read.
    static void DecalPlate(Transform car)
    {
        foreach (var canvas in car.GetComponentsInChildren<Canvas>())
        {
            if (canvas.GetComponentInChildren<UnityEngine.UI.Image>() == null) continue;
            var plate = new GameObject("Decal plate", typeof(RectTransform));
            var rect = plate.GetComponent<RectTransform>();
            rect.SetParent(canvas.transform, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = plate.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.96f, 0.96f, 0.96f, 1f);
            image.raycastTarget = false;
        }
    }

    static void SetColor(Material m, Color c)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        else m.color = c;
    }
}
