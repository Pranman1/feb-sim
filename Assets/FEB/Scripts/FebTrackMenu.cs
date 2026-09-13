using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/*
    Menu button that cycles through the available tracks and reloads the scene.
*/
public class FebTrackMenu : MonoBehaviour
{
    public Text Label;

    void Start()
    {
        Label.text = FebLaunch.Track != null ? Path.GetFileName(FebLaunch.Track) : "Track";
    }

    public void NextTrack()
    {
        var folders = TrackLibrary.Folders();
        if (folders.Count == 0) return;
        int index = (folders.IndexOf(FebLaunch.Track) + 1) % folders.Count;
        FebLaunch.Track = folders[index];
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
