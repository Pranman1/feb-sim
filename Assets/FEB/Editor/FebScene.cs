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
        loader.TitleLabel.text = "FEB Simulator";
        loader.Bridge = Find<Socket>("Socket");
        loader.Driving = Object.FindObjectOfType<DrivingMode>(true);
        loader.ResetManager = Object.FindObjectOfType<ResetManager>(true);
        loader.gameObject.AddComponent<GhostLap>().GhostMaterial =
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Transparent.mat");

        var auto = loader.gameObject.AddComponent<FebAutoStart>();
        auto.Cli = Object.FindObjectOfType<CLIManager>(true);
        auto.Connection = Find<SocketConnection>("Connection");
        auto.Driving = Object.FindObjectOfType<DrivingMode>(true);
        auto.Cameras = Object.FindObjectOfType<CameraSwitch>(true);
        loader.Decal = LoadSprite("Assets/FEB/Sprites/FEB Decal.png");

        AddTrackButton(Find<DrivingMode>("Driving Mode").transform.parent);

        EditorSceneManager.SaveScene(scene, Output);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Output, true) };
        Debug.Log("FEB: wrote " + Output);
    }

    // The Scene Light row is of no use on a racetrack; track and car-count dropdowns take its place.
    static void AddTrackButton(Transform menu)
    {
        var old = menu.Find("Scene Light");
        var rect = old.GetComponent<RectTransform>();
        float top = rect.anchorMax.y, bottom = rect.anchorMin.y, mid = 0.5f * (top + bottom);
        var picker = menu.gameObject.AddComponent<FebTrackMenu>();
        picker.TrackPicker = MakeDropdown(menu, "Track", mid + 0.005f, top - 0.005f);
        picker.CarsPicker = MakeDropdown(menu, "Cars", bottom + 0.005f, mid - 0.005f);
        Object.DestroyImmediate(old.gameObject);
    }

    static Dropdown MakeDropdown(Transform menu, string name, float yMin, float yMax)
    {
        var go = DefaultControls.CreateDropdown(new DefaultControls.Resources());
        go.name = name;
        go.transform.SetParent(menu, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0.1f, yMin);
        r.anchorMax = new Vector2(0.9f, yMax);
        r.offsetMin = r.offsetMax = Vector2.zero;
        go.GetComponentInChildren<Text>().fontSize = 20;
        return go.GetComponent<Dropdown>();
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
