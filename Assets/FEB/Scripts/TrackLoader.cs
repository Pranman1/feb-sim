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
    public float ConeHeight = 0.30f;            // metres; tall enough for the lidar plane (~0.1 m) to hit
    public float GridSpacing = 0.7f;            // lateral distance between the two grid columns
    public float GridStagger = 1.0f;            // longitudinal distance between grid rows (the car is 0.55 m long)
    public Text TitleLabel;                     // toolbar text, shows the track name
    public Socket Bridge;                       // the scene's Socket, whose per-vehicle arrays grow with --cars
    public DrivingMode Driving;
    public ResetManager ResetManager;           // car 0's reset manager; extra cars get their own
    public Sprite Decal;                        // replaces the sponsor decals on the cars (cosmetic only)

    public TrackData Track { get; private set; }
    public string Folder { get; private set; }

    const int RingSegments = 12;
    const float CheckpointHeight = 1.0f;
    const float CheckpointThickness = 0.05f;

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
        Debug.Log("FEB: loading track '" + Track.name + "' from " + Folder);

        var root = new GameObject("Track");
        root.transform.SetParent(transform, false);
        BuildWalls(root.transform);
        BuildCones(root.transform);
        var checkpoints = BuildCheckpoints(root.transform);
        SpawnExtraCars();
        ApplyDecals();
        PlaceVehicles(checkpoints);
        PlaceCameras(root.transform);
        if (TitleLabel != null) TitleLabel.text = "FEB Simulator  |  " + Track.name;
        var ghost = GetComponent<GhostLap>();
        if (ghost != null && Vehicles.Length > 0) ghost.Bind(Vehicles[0], Folder);
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
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
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
        for (int i = 0; i < n; i++)
        {
            Vector3 tangent = (centres[(i + 1) % n] - centres[(i - 1 + n) % n]).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
            for (int k = 0; k < RingSegments; k++)
            {
                float a = 2f * Mathf.PI * k / RingSegments;
                Vector3 dir = Mathf.Cos(a) * side + Mathf.Sin(a) * Vector3.up;
                vertices[i * RingSegments + k] = centres[i] + radius * dir;
                normals[i * RingSegments + k] = dir;
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
                triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
            }
        }
        var mesh = new Mesh { name = "Air duct" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.normals = normals;
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
        var mesh = ConeMesh(ConeHeight);
        var materials = new Dictionary<string, Material[]>();
        foreach (var cone in Track.cones)
        {
            if (!materials.TryGetValue(cone.color, out var pair))
                materials[cone.color] = pair = new[] { new Material(WallMaterial) { color = ConeColor(cone.color) },
                                                       new Material(WallMaterial) { color = StripeColor(cone.color) } };
            var go = new GameObject("Cone " + cone.color);
            go.transform.SetParent(parent, false);
            go.transform.position = TrackData.ToUnity(cone.x, cone.y);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = pair;
            var collider = go.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, ConeHeight / 2f, 0f);
            collider.height = ConeHeight;
            collider.radius = 0.3f * ConeHeight;
        }
    }

    // A truncated cone on a square base plate, apex up, origin at the ground. Submesh 0 is the
    // body colour, submesh 1 the stripe band (FSAE: blue/white, yellow/black).
    static Mesh ConeMesh(float height)
    {
        const int segments = 16;
        float bottom = 0.30f * height, top = 0.08f * height, plate = 0.45f * height, plateHeight = 0.05f * height;
        var vertices = new List<Vector3>();
        var body = new List<int>();
        var stripe = new List<int>();
        void Quad(List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = vertices.Count;
            vertices.AddRange(new[] { a, b, c, d });
            tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        // side: three bands, the middle one is the stripe
        float[] h = { plateHeight, 0.45f * height, 0.65f * height, height };
        float Radius(float y) => Mathf.Lerp(bottom, top, (y - plateHeight) / (height - plateHeight));
        for (int band = 0; band < 3; band++)
            for (int k = 0; k < segments; k++)
            {
                float a0 = 2f * Mathf.PI * k / segments, a1 = 2f * Mathf.PI * (k + 1) / segments;
                Vector3 r0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), r1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                float y0 = h[band], y1 = h[band + 1];
                Quad(band == 1 ? stripe : body, r0 * Radius(y0) + Vector3.up * y0, r0 * Radius(y1) + Vector3.up * y1,
                     r1 * Radius(y1) + Vector3.up * y1, r1 * Radius(y0) + Vector3.up * y0);
            }
        Vector3 p0 = new Vector3(-plate, 0f, -plate), p1 = new Vector3(plate, 0f, -plate), p2 = new Vector3(plate, 0f, plate), p3 = new Vector3(-plate, 0f, plate);
        Vector3 up = Vector3.up * plateHeight;
        Quad(body, p0 + up, p3 + up, p2 + up, p1 + up);                                  // plate top
        Quad(body, p0, p1, p1 + up, p0 + up); Quad(body, p1, p2, p2 + up, p1 + up);      // plate sides
        Quad(body, p2, p3, p3 + up, p2 + up); Quad(body, p3, p0, p0 + up, p3 + up);
        var mesh = new Mesh { name = "Cone", subMeshCount = 2 };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(body, 0);
        mesh.SetTriangles(stripe, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Color StripeColor(string name) { return name == "yellow" ? Color.black : Color.white; }

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
            if (!finish) list.Add(go.transform);
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
            foreach (var cam in clone.GetComponentsInChildren<Camera>(true))
                if (cam.targetTexture != null) cam.targetTexture = new RenderTexture(cam.targetTexture);

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

    void ApplyDecals()
    {
        if (Decal == null) return;
        foreach (var car in Vehicles)
            foreach (var image in car.GetComponentsInChildren<Image>(true))
                if (image.sprite != null) { image.sprite = Decal; image.preserveAspect = false; }
    }

    // ------------------------------------------------------------------ vehicles and cameras

    void PlaceVehicles(Transform[] checkpoints)
    {
        var pose = Track.spawn;
        Quaternion rotation = TrackData.ToUnityYaw(pose.yaw);
        for (int i = 0; i < Vehicles.Length; i++)
        {
            // two-column grid centred on the spawn pose, so every car stays inside the corridor:
            // column alternates left/right, each car half a row behind the previous one
            float lateral = (i % 2 == 0 ? -0.5f : 0.5f) * GridSpacing;
            float back = GridStagger * (i / 2) + 0.5f * GridStagger * (i % 2);
            Vector3 position = TrackData.ToUnity(pose.x, pose.y, 0.01f) + rotation * new Vector3(lateral, 0f, -back);
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
