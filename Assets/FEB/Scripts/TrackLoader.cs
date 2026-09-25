using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/*
    Builds the racetrack at runtime from a track folder (track.json): air-duct walls,
    checkpoints, finish line, cones and the floor, then places the cars on the spawn
    pose and points the overview cameras at the track. Runs in Awake so that
    LapTimer/ResetManager see the finished track in their Start.
*/
public class TrackLoader : MonoBehaviour
{
    public const string RacetrackName = "Racetrack"; // LapTimer counts collisions with objects of this name

    public string DefaultTrack = "loop";        // shipped track used when no --track is given
    public Transform[] Vehicles;                // car roots (LapTimer + VehicleController), index 0 = roboracer_1
    public FollowTarget TrackCamera;            // top-down "Trackcam" follower
    public Transform OverviewCamera;            // "God's Eye" camera, aimed at the car by TrackTarget
    public Material WallMaterial;               // HDRP lit material; also tinted for the cones
    public float SmallConeHeight = 0.178f;      // metres: the 7 inch cones that mark the blue/yellow boundaries
    public float BigConeHeight = 0.305f;        // metres: the 12 inch orange cones at the start line
    public float GridStagger = 2.5f;            // distance between cars on the single-file grid: outside the lidar bubble of the car behind
    public Text TitleLabel;                     // toolbar text, shows the track name
    public Socket Bridge;                       // the scene's Socket, whose per-vehicle arrays grow with --cars
    public DrivingMode Driving;
    public ResetManager ResetManager;           // car 0's reset manager; extra cars get their own
    public Sprite DecalLeft;                    // rear panel, driver's left: the FEB mark (cosmetic only)
    public Sprite DecalRight;                   // rear panel, driver's right: "FEB AUTO"

    public TrackData Track { get; private set; }
    public string Folder { get; private set; }
    public List<MeshRenderer> WallRenderers { get; } = new List<MeshRenderer>();   // for the Visual look's banding
    public FebStartLine StartLine { get; private set; }                              // first-crossing times for the HUD

    const int RingSegments = 12;
    const float CheckpointHeight = 1.0f;
    const float CheckpointThickness = 0.05f;

    void Start()
    {
        if (FebLaunch.Picker) StartCoroutine(OpenPicker());
    }

    /// --picker: open the track picker once the scene is up (screenshots, docs). The picker
    /// component sits on the folded side menu, which is inactive, so it is driven from here.
    System.Collections.IEnumerator OpenPicker()
    {
        yield return null;
        var menu = FindObjectOfType<FebTrackMenu>(true);
        if (menu != null) menu.OpenTracks();
    }

    void Awake()
    {
        Folder = TrackLibrary.Resolve(FebLaunch.Track ?? DefaultTrack);
        if (Folder == null)
        {
            Debug.LogError("FEB: no track found for '" + (FebLaunch.Track ?? DefaultTrack) + "'");
            return;
        }
        FebLaunch.Track = Folder;
        Track = TrackData.Load(Folder);
        if (Track.max_cars > 0 && FebLaunch.Cars > Track.max_cars) FebLaunch.Cars = Track.max_cars;
        Debug.Log("FEB: loading track '" + Track.name + "' from " + Folder + " with " + FebLaunch.Cars + " car(s)");

        var root = new GameObject("Track");
        root.transform.SetParent(transform, false);
        BuildWalls(root.transform);
        BuildCones(root.transform);
        var checkpoints = BuildCheckpoints(root.transform);
        SpawnExtraCars();
        ApplyDecals();
        PlaceVehicles(checkpoints);
        PlaceCameras(root.transform);
        if (TitleLabel != null) TitleLabel.text = "FEBAUTO Sim  |  " + Track.name;
        var ghost = GetComponent<GhostLap>();
        if (ghost != null && Vehicles.Length > 0) ghost.Bind(Vehicles[0], Folder);
        if (StartLine != null) foreach (var car in Vehicles) StartLine.Watch(car);
        var hud = GetComponent<FebHud>();
        if (hud != null && Vehicles.Length > 0) hud.Bind(Vehicles[0], StartLine);
        var look = GetComponent<FebLook>();
        if (look != null) look.Apply(this);
    }

    // ------------------------------------------------------------------ walls

    void BuildWalls(Transform parent)
    {
        float radius = Track.wall_diameter / 2f;
        var material = new Material(WallMaterial);
        if (ColorUtility.TryParseHtmlString(Track.wall_color ?? "#9a9a9a", out var color)) material.color = color;
        foreach (var wall in Track.walls)
        {
            var go = new GameObject(RacetrackName);
            go.transform.SetParent(parent, false);
            var mesh = TubeMesh(wall.points, radius);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            WallRenderers.Add(renderer);
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
    }

    // Closed tube (air duct) swept along a closed polyline lying on the floor.
    static Mesh TubeMesh(float[] xy, float radius)
    {
        int n = xy.Length / 2;
        var centres = new Vector3[n];
        for (int i = 0; i < n; i++) centres[i] = TrackData.ToUnity(xy[2 * i], xy[2 * i + 1], radius);

        var vertices = new Vector3[n * RingSegments];
        var normals = new Vector3[n * RingSegments];
        var uv = new Vector2[n * RingSegments];          // u: metres along the duct, v: around it
        float along = 0f;
        for (int i = 0; i < n; i++)
        {
            Vector3 tangent = (centres[(i + 1) % n] - centres[(i - 1 + n) % n]).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
            if (i > 0) along += Vector3.Distance(centres[i], centres[i - 1]);
            for (int k = 0; k < RingSegments; k++)
            {
                float a = 2f * Mathf.PI * k / RingSegments;
                Vector3 dir = Mathf.Cos(a) * side + Mathf.Sin(a) * Vector3.up;
                vertices[i * RingSegments + k] = centres[i] + radius * dir;
                normals[i * RingSegments + k] = dir;
                uv[i * RingSegments + k] = new Vector2(along, (float)k / RingSegments);
            }
        }
        var triangles = new int[n * RingSegments * 6];
        int t = 0;
        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            for (int k = 0; k < RingSegments; k++)
            {
                int k1 = (k + 1) % RingSegments;
                int a = i * RingSegments + k, b = i * RingSegments + k1;
                int c = next * RingSegments + k, d = next * RingSegments + k1;
                triangles[t++] = a; triangles[t++] = b; triangles[t++] = c;   // outside faces front (clockwise from outside)
                triangles[t++] = b; triangles[t++] = d; triangles[t++] = c;
            }
        }
        var mesh = new Mesh { name = "Air duct" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    // ------------------------------------------------------------------ cones

    // Cones are generated, not loaded: the upstream cone asset is a SketchUp file that
    // only imports on Windows/macOS editors.
    void BuildCones(Transform parent)
    {
        if (Track.cones == null || Track.cones.Length == 0) return;
        var meshes = new Dictionary<string, Mesh>();
        var materials = new Dictionary<string, Material[]>();
        foreach (var cone in Track.cones)
        {
            float height = ConeHeight(cone.color);
            if (!meshes.TryGetValue(cone.color, out var mesh))
                meshes[cone.color] = mesh = ConeMesh(height, cone.color == "orange");
            if (!materials.TryGetValue(cone.color, out var pair))
                materials[cone.color] = pair = new[] { new Material(WallMaterial) { color = ConeColor(cone.color) },
                                                       new Material(WallMaterial) { color = StripeColor(cone.color) } };
            var go = new GameObject("Cone " + cone.color);
            go.transform.SetParent(parent, false);
            go.transform.position = TrackData.ToUnity(cone.x, cone.y);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = pair;
            var collider = go.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, height / 2f, 0f);
            collider.height = height;
            collider.radius = 0.3f * height;
        }
    }

    float ConeHeight(string colour) { return colour == "orange" ? BigConeHeight : SmallConeHeight; }

    // A truncated cone on a square base plate, apex up, origin at the ground, proportioned like
    // the real thing: a 7 inch sports cone has a 4.3 inch body at the base, a 1.2 inch top and a
    // 5.5 inch plate; the 12 inch start cone is the same shape scaled, with a taller plate. Submesh 0
    // is the body colour, submesh 1 the stripe band (FSAE: blue/white, yellow/black, orange/white).
    static Mesh ConeMesh(float height, bool big)
    {
        const int segments = 40;
        float bottom = 0.31f * height, top = 0.085f * height, plate = 0.39f * height, plateHeight = (big ? 0.045f : 0.035f) * height;
        var vertices = new List<Vector3>();
        var body = new List<int>();
        var stripe = new List<int>();
        void Quad(List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = vertices.Count;
            vertices.AddRange(new[] { a, b, c, d });
            tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        // side in bands; the stripe is one band (two on the big cone, as on the real start cones)
        float[] h = big ? new[] { plateHeight, 0.35f * height, 0.48f * height, 0.60f * height, 0.73f * height, height }
                        : new[] { plateHeight, 0.45f * height, 0.65f * height, height };
        bool[] isStripe = big ? new[] { false, true, false, true, false } : new[] { false, true, false };
        float Radius(float y) => Mathf.Lerp(bottom, top, (y - plateHeight) / (height - plateHeight));
        for (int band = 0; band < h.Length - 1; band++)
            for (int k = 0; k < segments; k++)
            {
                float a0 = 2f * Mathf.PI * k / segments, a1 = 2f * Mathf.PI * (k + 1) / segments;
                Vector3 r0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), r1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                float y0 = h[band], y1 = h[band + 1];
                Quad(isStripe[band] ? stripe : body, r0 * Radius(y0) + Vector3.up * y0, r0 * Radius(y1) + Vector3.up * y1,
                     r1 * Radius(y1) + Vector3.up * y1, r1 * Radius(y0) + Vector3.up * y0);
            }
        // flat top
        {
            int centre = vertices.Count;
            vertices.Add(Vector3.up * height);
            for (int k = 0; k < segments; k++)
            {
                float a0 = 2f * Mathf.PI * k / segments, a1 = 2f * Mathf.PI * (k + 1) / segments;
                int i = vertices.Count;
                vertices.Add(new Vector3(Mathf.Cos(a0) * top, height, Mathf.Sin(a0) * top));
                vertices.Add(new Vector3(Mathf.Cos(a1) * top, height, Mathf.Sin(a1) * top));
                body.AddRange(new[] { centre, i + 1, i });
            }
        }
        Vector3 p0 = new Vector3(-plate, 0f, -plate), p1 = new Vector3(plate, 0f, -plate), p2 = new Vector3(plate, 0f, plate), p3 = new Vector3(-plate, 0f, plate);
        Vector3 up = Vector3.up * plateHeight;
        Quad(body, p0 + up, p3 + up, p2 + up, p1 + up);                                  // plate top
        Quad(body, p0, p1, p1 + up, p0 + up); Quad(body, p1, p2, p2 + up, p1 + up);      // plate sides
        Quad(body, p2, p3, p3 + up, p2 + up); Quad(body, p3, p0, p0 + up, p3 + up);
        var mesh = new Mesh { name = big ? "Big cone" : "Cone", subMeshCount = 2 };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(body, 0);
        mesh.SetTriangles(stripe, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Color StripeColor(string name) { return name == "yellow" ? Color.black : Color.white; }   // orange start cones: white bands

    static Color ConeColor(string name)
    {
        switch (name)
        {
            case "blue": return new Color(0.05f, 0.25f, 0.85f);
            case "yellow": return new Color(1.0f, 0.85f, 0.0f);
            default: return new Color(1.0f, 0.35f, 0.0f);
        }
    }

    // ------------------------------------------------------------------ checkpoints

    // Returns the checkpoint transforms in lap order (finish line excluded), as LapTimer expects.
    Transform[] BuildCheckpoints(Transform parent)
    {
        var list = new List<Transform>();
        for (int i = 0; i < Track.checkpoints.Length; i++)
        {
            var cp = Track.checkpoints[i];
            bool finish = i == 0;
            var go = new GameObject(finish ? "Finish Line" : "Checkpoint " + (i - 1));
            go.transform.SetParent(parent, false);
            go.transform.position = TrackData.ToUnity(cp.x, cp.y, CheckpointHeight / 2f);
            go.transform.rotation = TrackData.ToUnityYaw(cp.yaw);
            go.tag = finish ? "Finish Line A" : "Checkpoint";
            go.layer = LayerMask.NameToLayer(finish ? "Finish Line A" : "Checkpoint");
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(cp.width, CheckpointHeight, CheckpointThickness);
            if (finish) { StartLine = go.AddComponent<FebStartLine>(); StartLine.HalfWidth = cp.width / 2f; }
            else list.Add(go.transform);
        }
        return list.ToArray();
    }

    // ------------------------------------------------------------------ extra cars (head-to-head)

    // Clones car 0 for --cars N and registers each clone with the bridge (as V2, V3, ...),
    // the driving-mode toggle and its own reset manager, exactly like the upstream H2H scene.
    void SpawnExtraCars()
    {
        if (FebLaunch.Cars <= 1 || Vehicles.Length == 0 || Bridge == null) return;
        var original = Vehicles[0].gameObject;
        var all = Vehicles.ToList();
        for (int i = Vehicles.Length; i < FebLaunch.Cars; i++)
        {
            var clone = Instantiate(original, original.transform.parent);
            clone.name = "RoboRacer " + (i + 1);
            // sensor cameras render to their own texture; viewing cameras (Driver's Eye) belong to car 0 only
            foreach (var cam in clone.GetComponentsInChildren<Camera>(true))
                if (cam.targetTexture != null) cam.targetTexture = new RenderTexture(cam.targetTexture);
                else cam.gameObject.SetActive(false);
            foreach (var listener in clone.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;

            var reset = ResetManager.gameObject.AddComponent<ResetManager>();
            reset.Vehicles = new[] { clone.transform };
            reset.VehicleRigidBodies = new[] { clone.GetComponent<Rigidbody>() };
            reset.CoSimManagers = new[] { clone.GetComponent<CoSimManager>() };
            reset.LeftWheelEncoders = new[] { Twin(ResetManager.LeftWheelEncoders[0], clone) };
            reset.RightWheelEncoders = new[] { Twin(ResetManager.RightWheelEncoders[0], clone) };
            reset.LapTimers = new[] { clone.GetComponent<LapTimer>() };

            Bridge.ResetManagers = Append(Bridge.ResetManagers, reset);
            Bridge.VehicleRigidBodies = Append(Bridge.VehicleRigidBodies, clone.GetComponent<Rigidbody>());
            Bridge.VehicleControllers = Append(Bridge.VehicleControllers, clone.GetComponent<VehicleController>());
            Bridge.LeftWheelEncoders = Append(Bridge.LeftWheelEncoders, Twin(Bridge.LeftWheelEncoders[0], clone));
            Bridge.RightWheelEncoders = Append(Bridge.RightWheelEncoders, Twin(Bridge.RightWheelEncoders[0], clone));
            Bridge.PositioningSystems = Append(Bridge.PositioningSystems, Twin(Bridge.PositioningSystems[0], clone));
            Bridge.InertialMeasurementUnits = Append(Bridge.InertialMeasurementUnits, Twin(Bridge.InertialMeasurementUnits[0], clone));
            Bridge.LIDARUnits = Append(Bridge.LIDARUnits, Twin(Bridge.LIDARUnits[0], clone));
            Bridge.FrontCameras = Append(Bridge.FrontCameras, Twin(Bridge.FrontCameras[0], clone));
            Bridge.LapTimers = Append(Bridge.LapTimers, clone.GetComponent<LapTimer>());
            clone.GetComponent<VehicleController>().DrivingMode = 1;   // extra cars are only ever driven by a bridge
            all.Add(clone.transform);
        }
        Vehicles = all.ToArray();
    }

    // The clone's counterpart of a component of car 0 (same position in the hierarchy).
    T Twin<T>(T component, GameObject clone) where T : Component
    {
        int index = Array.IndexOf(Vehicles[0].GetComponentsInChildren<T>(true), component);
        return clone.GetComponentsInChildren<T>(true)[index];
    }

    static T[] Append<T>(T[] array, T item) { return (array ?? new T[0]).Concat(new[] { item }).ToArray(); }

    // The car's rear panel carries two sponsor images side by side; they become the FEB mark
    // (driver's left) and "FEB AUTO" (driver's right). Inactive decal variants stay inactive.
    void ApplyDecals()
    {
        if (DecalLeft == null || DecalRight == null) return;
        foreach (var car in Vehicles)
            foreach (var image in car.GetComponentsInChildren<Image>())
            {
                if (image.sprite == null) continue;
                bool left = car.InverseTransformPoint(image.rectTransform.position).x < 0f;
                image.sprite = left ? DecalLeft : DecalRight;
                image.preserveAspect = true;
            }
    }

    // ------------------------------------------------------------------ vehicles and cameras

    void PlaceVehicles(Transform[] checkpoints)
    {
        var pose = Track.spawn;
        Quaternion rotation = TrackData.ToUnityYaw(pose.yaw);
        for (int i = 0; i < Vehicles.Length; i++)
        {
            // single-file grid on the centreline, car 0 in front: a car beside another in a 2.2 m
            // corridor sits inside its neighbour's lidar safety bubble and both steer into the walls
            // at the start; in line, the car behind simply follows (RoboRacer head-to-head starts the same way)
            Vector3 position = TrackData.ToUnity(pose.x, pose.y, 0.01f) + rotation * new Vector3(0f, 0f, -GridStagger * i);
            Vehicles[i].SetPositionAndRotation(position, rotation);
            var body = Vehicles[i].GetComponent<Rigidbody>();   // interpolated bodies ignore a bare transform move
            if (body != null) { body.position = position; body.rotation = rotation; }
            var timer = Vehicles[i].GetComponent<LapTimer>();
            if (timer == null) continue;
            timer.Checkpoints = checkpoints;
            timer.RacetrackName = RacetrackName;
            if (FebLaunch.LidarHz > 0f)
                foreach (var lidar in Vehicles[i].GetComponentsInChildren<LIDAR>(true)) lidar.ScanRate = FebLaunch.LidarHz;
        }
        Physics.SyncTransforms();
    }

    void PlaceCameras(Transform parent)
    {
        var bounds = new Bounds(TrackData.ToUnity(Track.centreline[0], Track.centreline[1]), Vector3.zero);
        for (int i = 0; i < Track.centreline.Length; i += 2)
            bounds.Encapsulate(TrackData.ToUnity(Track.centreline[i], Track.centreline[i + 1]));
        float extent = Mathf.Max(bounds.size.x, bounds.size.z) + 2f * Track.wall_diameter + 2f;

        var centre = new GameObject("Track Centre");
        centre.transform.SetParent(parent, false);
        centre.transform.position = bounds.center;
        if (TrackCamera != null)
        {
            TrackCamera.Target = centre;
            TrackCamera.ZoomOutLimit = 3f * extent;
            TrackCamera.Follower.transform.position = bounds.center + Vector3.up * extent;
        }
        if (OverviewCamera != null)
            OverviewCamera.position = bounds.center + new Vector3(0f, 0.4f * extent, -0.6f * extent);
    }
}
