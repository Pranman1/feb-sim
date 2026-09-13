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
        // in batch mode CLIManager already enabled the socket
        if (!string.IsNullOrEmpty(FebLaunch.Connect) && !Application.isBatchMode) Connection.ToggleSocketConnection();
    }
}
