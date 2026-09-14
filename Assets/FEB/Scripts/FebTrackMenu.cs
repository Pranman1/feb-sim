using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/*
    Menu dropdowns: the track to load and the number of cars. Either choice reloads the scene.
*/
public class FebTrackMenu : MonoBehaviour
{
    public Dropdown TrackPicker;
    public Dropdown CarsPicker;
    const int MaxCars = 4;

    void Start()
    {
        var folders = TrackLibrary.Folders();
        TrackPicker.ClearOptions();
        TrackPicker.AddOptions(folders.Select(Path.GetFileName).ToList());
        TrackPicker.SetValueWithoutNotify(Mathf.Max(0, folders.IndexOf(FebLaunch.Track)));
        TrackPicker.onValueChanged.AddListener(index => Reload(folders[index], FebLaunch.Cars));

        CarsPicker.ClearOptions();
        CarsPicker.AddOptions(Enumerable.Range(1, MaxCars).Select(n => n == 1 ? "1 car" : n + " cars").ToList());
        CarsPicker.SetValueWithoutNotify(Mathf.Clamp(FebLaunch.Cars, 1, MaxCars) - 1);
        CarsPicker.onValueChanged.AddListener(index => Reload(FebLaunch.Track, index + 1));
    }

    static void Reload(string track, int cars)
    {
        if (track == FebLaunch.Track && cars == FebLaunch.Cars) return;
        FebLaunch.Track = track;
        FebLaunch.Cars = cars;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
