using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/*
    Finds track folders. Tracks ship inside the app (StreamingAssets/Tracks) and
    members or organisers can drop extra ones into ~/.feb-sim/tracks without a rebuild.
*/
public static class TrackLibrary
{
    public static string UserTracksDir
    {
        get { return Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".feb-sim", "tracks"); }
    }

    public static List<string> Folders()
    {
        var roots = new[] { Path.Combine(Application.streamingAssetsPath, "Tracks"), UserTracksDir };
        return roots.Where(Directory.Exists)
                    .SelectMany(Directory.GetDirectories)
                    .Where(d => File.Exists(Path.Combine(d, "track.json")))
                    .OrderBy(Path.GetFileName)
                    .ToList();
    }

    // A folder path, or the folder name of a shipped/user track. Null if nothing matches.
    public static string Resolve(string nameOrPath)
    {
        if (string.IsNullOrEmpty(nameOrPath)) return Folders().FirstOrDefault();
        if (File.Exists(Path.Combine(nameOrPath, "track.json"))) return Path.GetFullPath(nameOrPath);
        return Folders().FirstOrDefault(d => Path.GetFileName(d) == nameOrPath);
    }
}
