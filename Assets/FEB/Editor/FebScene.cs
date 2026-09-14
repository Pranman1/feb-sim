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
        loader.Decal = LoadSprite("Assets/FEB/Sprites/FEB Logo.png");

        AddTrackButton(Find<DrivingMode>("Driving Mode").transform.parent);

        EditorSceneManager.SaveScene(scene, Output);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Output, true) };
        Debug.Log("FEB: wrote " + Output);
    }

    // The Scene Light row is of no use on a racetrack; it becomes the Track button.
    static void AddTrackButton(Transform menu)
    {
        var row = menu.Find("Scene Light").gameObject;
        row.name = "Track";
        Object.DestroyImmediate(row.GetComponent<SceneLighting>());
        var label = row.GetComponentInChildren<Text>();
        label.text = "Track";
        var trackMenu = row.AddComponent<FebTrackMenu>();
        trackMenu.Label = label;
        var button = row.GetComponent<Button>();
        while (button.onClick.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(button.onClick, 0);
        UnityEventTools.AddPersistentListener(button.onClick, new UnityAction(trackMenu.NextTrack));
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
