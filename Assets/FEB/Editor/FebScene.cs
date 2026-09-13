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
        loader.ConePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Infrastructure/Traffic Cone.prefab");
        loader.TitleLabel = Find<Text>("AutoDRIVE Simulator");
        loader.TitleLabel.text = "FEB Simulator";

        var auto = loader.gameObject.AddComponent<FebAutoStart>();
        auto.Cli = Object.FindObjectOfType<CLIManager>(true);
        auto.Connection = Find<SocketConnection>("Connection");
        auto.Driving = Object.FindObjectOfType<DrivingMode>(true);

        AddTrackButton(Find<DrivingMode>("Driving Mode").transform.parent);

        EditorSceneManager.SaveScene(scene, Output);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Output, true) };
        Debug.Log("FEB: wrote " + Output);
    }

    // Menu rows are anchored in a column; squeeze them by 10% and put a Track button on top.
    static void AddTrackButton(Transform menu)
    {
        foreach (RectTransform row in menu)
        {
            row.anchorMin = new Vector2(row.anchorMin.x, 0.025f + (row.anchorMin.y - 0.025f) * 0.9f);
            row.anchorMax = new Vector2(row.anchorMax.x, 0.025f + (row.anchorMax.y - 0.025f) * 0.9f);
        }
        var template = menu.Find("Camera Switch").gameObject;
        var row2 = Object.Instantiate(template, menu);
        row2.name = "Track";
        Object.DestroyImmediate(row2.GetComponent<CameraSwitch>());
        var rect = row2.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(rect.anchorMin.x, 0.835f);
        rect.anchorMax = new Vector2(rect.anchorMax.x, 0.925f);

        var label = row2.GetComponentInChildren<Text>();
        label.text = "Track";
        var trackMenu = row2.AddComponent<FebTrackMenu>();
        trackMenu.Label = label;
        var button = row2.GetComponent<Button>();
        while (button.onClick.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(button.onClick, 0);
        UnityEventTools.AddPersistentListener(button.onClick, new UnityAction(trackMenu.NextTrack));
    }

    static T Find<T>(string name) where T : Component
    {
        return Resources.FindObjectsOfTypeAll<T>().First(c => c.name == name && c.gameObject.scene.isLoaded);
    }
}
