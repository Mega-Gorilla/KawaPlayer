
using System;
using UnityEngine;

namespace Yamadev.YamaStream
{
  [Serializable]
  public class PlaylistTrack
  {
    public VideoPlayerType Mode;
    public string Title;
    public string Url;
  }

  public class PlaylistItem : MonoBehaviour
  {
    public string playListName;
    public PlaylistTrack[] tracks;

    public string YouTubePlayListID;
  }
}