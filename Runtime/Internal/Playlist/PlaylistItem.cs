
using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Yamadev.YamaStream
{
  [Serializable]
  public class PlaylistTrack
  {
    [FormerlySerializedAs("Mode")] public VideoPlayerType playerType;
    [FormerlySerializedAs("Title")] public string title;
    [FormerlySerializedAs("Url")] public string url;
  }

  public class PlaylistItem : MonoBehaviour
  {
    [FormerlySerializedAs("playListName")] public string playlistName;
    public PlaylistTrack[] tracks;

    [FormerlySerializedAs("YouTubePlayListID")] public string youtubePlaylistId;

    // Where a VHub-imported playlist came from (issue #90). Editor-only
    // metadata for re-importing: PlaylistBuildProcess does not copy it to the
    // Udon Playlist, so it is never available at runtime as a VRCUrl.
    public string vhubPlaylistUrl;
  }
}