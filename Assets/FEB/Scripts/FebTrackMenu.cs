using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/*
    Menu rows in the simulator's button style: "Track: <name>" cycles through the available
    tracks, "Cars: <n>" through 1..4 cars, "Look: Visual|Simple" between the dressed scene and
    the bare one (for weak machines, or taste). Each reloads the scene.
*/
public class FebTrackMenu : MonoBehaviour
{
    public Text TrackLabel;
    public Text CarsLabel;
    public Text LookLabel;
    const int MaxCars = 4;

    void Start()
    {
        TrackLabel.text = "Track: " + (FebLaunch.Track != null ? Path.GetFileName(FebLaunch.Track) : "?");
        CarsLabel.text = "Cars: " + FebLaunch.Cars;
        if (LookLabel != null) LookLabel.text = "Look: " + (FebLook.Visual ? "Visual" : "Simple");
    }

    public void NextLook()
    {
        FebLaunch.Look = FebLook.Visual ? "simple" : "visual";
        Reload(FebLaunch.Track, FebLaunch.Cars);
    }

    public void NextTrack()
    {
        var folders = TrackLibrary.Folders();
        if (folders.Count == 0) return;
        Reload(folders[(folders.IndexOf(FebLaunch.Track) + 1) % folders.Count], FebLaunch.Cars);
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
}
