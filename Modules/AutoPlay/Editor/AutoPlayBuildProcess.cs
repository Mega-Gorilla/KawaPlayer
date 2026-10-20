using System;
using System.Linq;
using UnityEngine;
using Yamadev.YamaStream.Editor;
using Object = UnityEngine.Object;

namespace Yamadev.YamaStream.Modules.AutoPlay.Editor
{
  // The inspector numbers From Playlist's playlists among every PlaylistItem
  // under the player's Playlists, those switched off or without tracks too,
  // while the world numbers only the ones PlaylistBuildProcess made Playlists
  // of. Each one left out before the chosen one moved the world's number onto
  // the next playlist (issue #209). The number is turned into the world's
  // here, on the scene being built, so the saved scene keeps the inspector's.
  // A chosen playlist that is not in the world plays nothing, and the build
  // log says so. After PlaylistBuildProcess, which makes the Playlists, and
  // YamaPlayerModuleBuildProcess, which leaves out modules switched off.
  internal class AutoPlayBuildProcess : IYamaPlayerBuildProcess
  {
    public int callbackOrder => -2900;

    public void Process()
    {
      foreach (var autoPlay in Object.FindObjectsByType<AutoPlay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
      {
        if (autoPlay == null || !YamaPlayerModuleBuildProcess.IsModuleEnabled(autoPlay)) continue;
        if ((AutoPlayMode)autoPlay.GetProgramVariable("_autoPlayMode") != AutoPlayMode.FromPlaylist) continue;
        var controller = autoPlay.GetComponentInParent<Controller>(true);
        var player = autoPlay.GetComponentInParent<YamaPlayer>(true);
        if (controller == null || player == null || player.PlaylistManager == null) continue;

        var items = player.PlaylistManager.GetPlaylists();
        int chosen = (int)autoPlay.GetProgramVariable("_playlistIndex");
        var item = chosen >= 0 && chosen < items.Count ? items[chosen] : null;
        var playlist = item != null ? item.GetComponent<Playlist>() : null;
        int index = playlist != null ? Array.IndexOf(WorldPlaylists(controller), playlist) : -1;
        autoPlay.SetProgramVariable("_playlistIndex", index);
        if (index >= 0) continue;

        var name = item != null && !string.IsNullOrEmpty(item.playlistName) ? item.playlistName : $"Playlist {chosen + 1}";
        Debug.LogWarning($"[KawaPlayer] The AutoPlay module of {YamaPlayerModuleBuildProcess.GetPath(player.transform)} plays the playlist \"{name}\" on join, but that playlist is not in the world (switched off, without tracks, or gone), so nothing plays. Choose another playlist in the module, or switch the playlist on and give it tracks.", autoPlay.gameObject);
      }
    }

    // The Playlists the Controller reads at Start (Controller.ReadPlaylists):
    // those on active objects under it, in Hierarchy order. The player itself
    // may be switched off and on later, so only the objects below the
    // Controller count.
    private static Playlist[] WorldPlaylists(Controller controller)
    {
      return controller.GetComponentsInChildren<Playlist>(true)
        .Where(playlist => IsActiveBelow(playlist.transform, controller.transform))
        .ToArray();
    }

    private static bool IsActiveBelow(Transform transform, Transform root)
    {
      for (; transform != null && transform != root; transform = transform.parent)
      {
        if (!transform.gameObject.activeSelf) return false;
      }
      return true;
    }
  }
}
