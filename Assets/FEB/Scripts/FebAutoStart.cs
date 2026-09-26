using System.Collections;
using UnityEngine;

/*
    Applies the --connect and --mode launch options once the scene is up, so the
    launcher can start a fully wired simulator without anyone clicking the menu.
*/
public class FebAutoStart : MonoBehaviour
{
    public CLIManager Cli;                // holds the IP/port input fields
    public SocketConnection Connection;   // the menu's Connection button
    public DrivingMode Driving;           // the menu's Driving Mode button
    public CameraSwitch Cameras;          // the menu's camera button

    void Awake()
    {
        if (string.IsNullOrEmpty(FebLaunch.Connect)) return;
        string[] parts = FebLaunch.Connect.Split(':');
        Cli.IP.text = parts[0];
        if (parts.Length > 1) Cli.Port.text = parts[1];
    }

    IEnumerator Start()
    {
        yield return null; // DrivingMode.Start has now applied its default (autonomous)
        if (FebLaunch.Mode == "manual") Driving.ToggleDrivingMode();
        if (!string.IsNullOrEmpty(FebLaunch.Camera) && Cameras != null)
            for (int i = 0; i < Cameras.Cameras.Length && !Cameras.Label.text.Equals(FebLaunch.Camera, System.StringComparison.OrdinalIgnoreCase); i++)
                Cameras.NextCamera();
        // in batch mode CLIManager already enabled the socket
        if (!string.IsNullOrEmpty(FebLaunch.Connect) && !Application.isBatchMode)
        {
            Connection.ToggleSocketConnection();
            StartCoroutine(KeepTrying());
        }
    }

    // The devkit builds the stack's packages before its bridge listens, which can take minutes
    // on a laptop, and the socket gives up after one refused attempt. Try again every few
    // seconds until it is connected, for up to ten minutes, so nobody has to click.
    IEnumerator KeepTrying()
    {
        var socket = Connection.Socket.GetComponent<Socket>();
        float deadline = Time.realtimeSinceStartup + 600f;
        while (Time.realtimeSinceStartup < deadline)
        {
            yield return new WaitForSecondsRealtime(4f);
            if (socket == null || socket.ConnectionLabel == null || socket.ConnectionLabel.text == "Connected") yield break;
            Connection.ToggleSocketConnection();       // off
            yield return new WaitForSecondsRealtime(0.5f);
            Connection.ToggleSocketConnection();       // on again
        }
    }
}
