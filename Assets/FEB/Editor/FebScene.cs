using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/*
    Generates "FEB Racing.unity" from the upstream RoboRacer Sim Racing scene:
    the static tracks go, a TrackLoader comes in, the menu gets a Track button and
    the branding changes. Re-run after pulling upstream changes to the base scene.

        Unity -batchmode -quit -projectPath . -executeMethod FebScene.Create
*/
public static class FebScene
{
    const string Source = "Assets/Scenes/RoboRacer - Sim Racing.unity";
    public const string Output = "Assets/Scenes/FEB Racing.unity";

    [MenuItem("FEB/Create Racing Scene")]
    public static void Create()
    {
        var scene = EditorSceneManager.OpenScene(Source);
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == "Infrastructure" || go.name.EndsWith(" Track")) Object.DestroyImmediate(go);

        var loader = new GameObject("Track Loader").AddComponent<TrackLoader>();
        loader.Vehicles = Object.FindObjectsOfType<LapTimer>(true).Select(t => t.transform).ToArray();
        loader.TrackCamera = Find<FollowTarget>("Trackcam");
        loader.OverviewCamera = Find<Camera>("God's Eye").transform;
        loader.WallMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Air Duct/Air_Duct_White.mat");
        loader.TitleLabel = Find<Text>("AutoDRIVE Simulator");
        loader.TitleLabel.text = "FEBAUTO Sim";
        loader.Bridge = Find<Socket>("Socket");
        loader.Driving = Object.FindObjectOfType<DrivingMode>(true);
        loader.ResetManager = Object.FindObjectOfType<ResetManager>(true);
        loader.gameObject.AddComponent<GhostLap>().GhostMaterial =
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Transparent.mat");
        loader.gameObject.AddComponent<FebLook>();
        loader.gameObject.AddComponent<FebRealTime>();
        var hud = loader.gameObject.AddComponent<FebHud>();
        hud.Canvas = loader.TitleLabel.canvas.GetComponent<RectTransform>();
        hud.Font = loader.TitleLabel.font;
        hud.UpstreamPanel = Find<RectTransform>("Lap Times and Count").gameObject;

        var auto = loader.gameObject.AddComponent<FebAutoStart>();
        auto.Cli = Object.FindObjectOfType<CLIManager>(true);
        auto.Connection = Find<SocketConnection>("Connection");
        auto.Driving = Object.FindObjectOfType<DrivingMode>(true);
        auto.Cameras = Object.FindObjectOfType<CameraSwitch>(true);
        loader.DecalLeft = LoadSprite("Assets/FEB/Sprites/FEB Decal Left.png");
        loader.DecalRight = LoadSprite("Assets/FEB/Sprites/FEB Decal Right.png");

        AddTrackButton(Find<DrivingMode>("Driving Mode").transform.parent);

        EditorSceneManager.SaveScene(scene, Output);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Output, true) };
        Debug.Log("FEB: wrote " + Output);
    }

    // Menu rows keep the upstream button style. Scene Light goes (no use on a racetrack); Track and
    // Cars and Look rows come in under Driving Mode, and the button column is re-spaced to fit nine rows.
    static void AddTrackButton(Transform menu)
    {
        Object.DestroyImmediate(menu.Find("Scene Light").gameObject);
        var picker = menu.gameObject.AddComponent<FebTrackMenu>();
        var template = menu.Find("Camera Switch").gameObject;
        var track = CloneRow(template, "Track", picker.OpenTracks, "Assets/FEB/Sprites/Track Button.png");
        var cars = CloneRow(template, "Cars", picker.NextCars, "Assets/FEB/Sprites/Cars Button.png");
        var look = CloneRow(template, "Look", picker.NextLook, "Assets/FEB/Sprites/Look Button.png");
        picker.TrackLabel = track.GetComponentInChildren<Text>();
        picker.CarsLabel = cars.GetComponentInChildren<Text>();
        picker.LookLabel = look.GetComponentInChildren<Text>();

        string[] order = { "Connection", "Driving Mode", "Track", "Cars", "Look", "Camera Switch", "Rendering Quality", "Scene Reset", "Quit" };
        const float bottom = 0.025f, top = 0.725f;
        float height = (top - bottom) / order.Length;
        for (int i = 0; i < order.Length; i++)
        {
            var rect = menu.Find(order[i]).GetComponent<RectTransform>();
            float yMax = top - i * height;
            rect.anchorMin = new Vector2(rect.anchorMin.x, yMax - height);
            rect.anchorMax = new Vector2(rect.anchorMax.x, yMax);
        }
    }

    static GameObject CloneRow(GameObject template, string name, UnityAction onClick, string iconPath)
    {
        var row = Object.Instantiate(template, template.transform.parent);
        row.name = name;
        row.GetComponent<Image>().sprite = LoadSprite(iconPath);   // the row's own Image is its icon
        Object.DestroyImmediate(row.GetComponent<CameraSwitch>());
        var button = row.GetComponent<Button>();
        while (button.onClick.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(button.onClick, 0);
        UnityEventTools.AddPersistentListener(button.onClick, onClick);
        return row;
    }

    static Sprite LoadSprite(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static T Find<T>(string name) where T : Component
    {
        return Resources.FindObjectsOfTypeAll<T>().First(c => c.name == name && c.gameObject.scene.isLoaded);
    }
}
