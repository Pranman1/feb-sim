using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/*
    Menu rows in the simulator's button style: "Track: <name>" cycles through the available
    tracks, "Cars: <n>" through 1..4 cars. Either reloads the scene.
*/
public class FebTrackMenu : MonoBehaviour
{
    public Text TrackLabel;
    public Text CarsLabel;
    const int MaxCars = 4;

    void Start()
    {
        TrackLabel.text = "Track: " + (FebLaunch.Track != null ? Path.GetFileName(FebLaunch.Track) : "?");
        CarsLabel.text = "Cars: " + FebLaunch.Cars;
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
