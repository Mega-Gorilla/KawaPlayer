using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components.Video;
using VRC.SDKBase;

namespace Yamadev.YamaStream
{
  // The last few playback errors on one player, for the details a viewer
  // can send in when a video will not play (issue #168). It sits beside the
  // Controller. Kept per viewer and never synced: each viewer loads videos
  // on their own, so their errors are their own.
  //
  // One failure is often several errors -- each retry, and the switch to the
  // fallback player -- so a single last error would hide how it went.
  [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
  public class PlaybackErrorLog : YamaPlayerListener
  {
    public const int Capacity = 5;

    private Controller _controller;
    private int _count;
    private int[] _numbers = new int[Capacity];
    private string[] _times = new string[Capacity];
    private int[] _errors = new int[Capacity];
    private int[] _players = new int[Capacity];
    private int[] _attempts = new int[Capacity];
    private int[] _nextPlayers = new int[Capacity];
    private string[] _details = new string[Capacity];
    private string[] _titles = new string[Capacity];
    private string[] _urls = new string[Capacity];
    private string[] _playlists = new string[Capacity];
    private int[] _trackNumbers = new int[Capacity];

    private void Start()
    {
      _controller = GetComponent<Controller>();
      if (!Utilities.IsValid(_controller))
      {
        PrintError($"PlaybackErrorLog is not beside a Controller: {gameObject.name}");
        return;
      }
      _controller.AddListener(this);
    }

    // The Controller has already taken down the failed try and scheduled the
    // next one when listeners hear of the error.
    public override void AfterVideoErrorOccurred(VideoError videoError)
    {
      if (!Utilities.IsValid(_controller)) return;
      int slot = _count % Capacity;
      _count++;

      _numbers[slot] = _controller.ErrorNumber;
      _times[slot] = DateTime.Now.ToString("HH:mm:ss");
      _errors[slot] = (int)videoError;
      _players[slot] = (int)_controller.LastErrorPlayerType;
      _attempts[slot] = _controller.LastErrorAttempt;
      _nextPlayers[slot] = _controller.ErrorRetryCount > _controller.LastErrorAttempt ? (int)_controller.ActiveHandler.Type : -1;
      _details[slot] = _controller.LastErrorDetail;

      object[] track = _controller.Track;
      VRCUrl url = TrackUtils.GetUrl(track);
      _titles[slot] = TrackUtils.GetTitle(track);
      _urls[slot] = Utilities.IsValid(url) ? url.Get() : string.Empty;

      Playlist playlist = _controller.ActivePlaylist;
      bool fromPlaylist = Utilities.IsValid(playlist) && _controller.PlayingTrackIndex >= 0;
      _playlists[slot] = fromPlaylist ? playlist.PlaylistName : string.Empty;
      _trackNumbers[slot] = fromPlaylist ? _controller.PlayingTrackIndex + 1 : 0;
    }

    public int EntryCount => Mathf.Min(_count, Capacity);

    // index 0 is the newest entry.
    private int Slot(int index) => (_count - 1 - index) % Capacity;

    public int GetNumber(int index) => _numbers[Slot(index)];
    public string GetTime(int index) => _times[Slot(index)];
    public VideoError GetError(int index) => (VideoError)_errors[Slot(index)];
    public VideoPlayerType GetPlayer(int index) => (VideoPlayerType)_players[Slot(index)];
    // 0 for the first try, n for the nth retry.
    public int GetAttempt(int index) => _attempts[Slot(index)];
    public bool WillRetry(int index) => _nextPlayers[Slot(index)] >= 0;
    // The player the next try uses; only meaningful when WillRetry.
    public VideoPlayerType GetNextPlayer(int index) => (VideoPlayerType)Mathf.Max(0, _nextPlayers[Slot(index)]);
    public string GetDetail(int index) => _details[Slot(index)];
    public string GetTitle(int index) => _titles[Slot(index)];
    public string GetUrl(int index) => _urls[Slot(index)];
    // Empty when the track was not played from a playlist.
    public string GetPlaylist(int index) => _playlists[Slot(index)];
    // 1-based; 0 when not from a playlist.
    public int GetTrackNumber(int index) => _trackNumbers[Slot(index)];

    public int MaxRetry => Utilities.IsValid(_controller) ? _controller.MaxErrorRetry : 0;
  }
}
