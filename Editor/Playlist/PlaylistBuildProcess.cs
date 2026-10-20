using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UdonSharp;
using UdonSharpEditor;
using UnityEngine;
using VRC.SDKBase;

using Object = UnityEngine.Object;

namespace Yamadev.YamaStream.Editor
{
  public class PlaylistBuildProcess : IYamaPlayerBuildProcess
  {
    public int callbackOrder => -9000;

    public void Process()
    {
      var managers = Object.FindObjectsByType<PlaylistManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
      foreach (var manager in managers)
      {
        if (manager == null) continue;
        var playlistItems = manager.GetPlaylists();
        if (playlistItems == null || playlistItems.Count == 0)
        {
          Debug.LogWarning($"No playlists found in manager {manager.name}");
          continue;
        }

        var results = CreatePlaylistComponents(playlistItems);
      }
    }

    // A playlist imported from VHub plays its tracks on AVPro, as one loaded in
    // the world does (issue #166). VHub names Unity Video Player for them (mode
    // 0), which cannot play HLS and gets YouTube at 360p, and a playlist
    // imported before #166 still holds that; baking it as AVPro fixes such a
    // world once it is built again, without changing what the scene saved.
    // Each track's origin is not kept, so a track set to Unity by hand in such
    // a playlist plays on AVPro too: one meant for Unity belongs in a playlist
    // not imported from VHub, which the Playlist Editor says.
    internal static VideoPlayerType BakedPlayerType(PlaylistItem item, PlaylistTrack track)
    {
      bool fromVhub = !string.IsNullOrEmpty(item.vhubPlaylistUrl);
      return fromVhub && track.playerType == VideoPlayerType.UnityVideoPlayer
        ? VideoPlayerType.AVProVideoPlayer
        : track.playerType;
    }

    // U#'s own setup of a new behaviour (RunBehaviourSetup), found by name as
    // U# keeps it internal. The minimum SDK (3.10.2) has it with the same
    // signature; Tests/Editor checks it is there.
    private static readonly MethodInfo _runBehaviourSetup = typeof(UdonSharpEditorUtility).GetMethod(
      "RunBehaviourSetup", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(UdonSharpBehaviour) }, null);
    private static bool _warnedNoSetup;

    // AddUdonSharpComponent, without initializing the new UdonBehaviour there
    // and then, as it does in Play. The SDK initializes every UdonBehaviour of
    // a loaded scene after giving it networking, and throws on one already
    // initialized: that stopped it at the world's first playlist, and the rest
    // of the Controller ran without networking (issue #164). Left alone, the
    // playlist is initialized with the others, from what is set here. Outside
    // Play the two do the same, so a built world does not change.
    private static T AddUdonSharpComponentUninitialized<T>(GameObject gameObject) where T : UdonSharpBehaviour
    {
      if (_runBehaviourSetup == null)
      {
        if (!_warnedNoSetup) Debug.LogWarning("[KawaPlayer] UdonSharp's RunBehaviourSetup was not found, so the world's playlists are added as before, and in Play some of the player's UdonBehaviours run without networking (issue #164).");
        _warnedNoSetup = true;
        return gameObject.AddUdonSharpComponent<T>();
      }
      var behaviour = gameObject.AddComponent<T>();
      _runBehaviourSetup.Invoke(null, new object[] { behaviour });
      return behaviour;
    }

    private static List<Playlist> CreatePlaylistComponents(List<PlaylistItem> playlistItems)
    {
      var results = new List<Playlist>();
      foreach (var item in playlistItems)
      {
        if (item == null || !item.gameObject.activeSelf || item.tracks == null || item.tracks.Length == 0) continue;

        try
        {
          var udonPlaylist = AddUdonSharpComponentUninitialized<Playlist>(item.gameObject);

          udonPlaylist.SetProgramVariable("_playlistName", item.playlistName);

          var videoPlayerTypes = item.tracks.Select(track => BakedPlayerType(item, track)).ToArray();
          var titles = item.tracks.Select(track => track.title ?? string.Empty).ToArray();
          var urls = item.tracks.Select(track => new VRCUrl(track.url ?? string.Empty)).ToArray();

          udonPlaylist.SetProgramVariable("_videoPlayerTypes", videoPlayerTypes);
          udonPlaylist.SetProgramVariable("_titles", titles);
          udonPlaylist.SetProgramVariable("_urls", urls);

          results.Add(udonPlaylist);
        }
        catch (Exception ex)
        {
          Debug.LogError($"Failed to create playlist component for '{item.playlistName}': {ex.Message}");
        }
      }

      return results;
    }
  }
}
