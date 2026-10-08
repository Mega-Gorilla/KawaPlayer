using System;
using UnityEngine;
using VRC.SDK3.Components.Video;
using VRC.SDKBase;

namespace Yamadev.YamaStream
{
  // The last few playback errors, for the details a viewer can send in when
  // a video will not play (issue #168). Kept per viewer and never synced:
  // each viewer loads videos on their own, so their errors are their own.
  //
  // One failure is often several errors -- each retry, and the switch to the
  // fallback player -- so a single last error would hide how it went.
  //
  // Kept here rather than in a component beside the Controller: a second
  // UdonBehaviour on the Controller's object changes the network components
  // a world built with an earlier KawaPlayer recorded for it, and the SDK
  // then refuses to build that world.
  public partial class Controller
  {
    public const int ErrorLogCapacity = 5;

    // Every error since this player joined is numbered, so the details and
    // the log line of one error carry the same number.
    private int _errorCount;
    private string[] _errorTimes = new string[ErrorLogCapacity];
    private int[] _videoErrors = new int[ErrorLogCapacity];
    private int[] _errorPlayers = new int[ErrorLogCapacity];
    private int[] _errorAttempts = new int[ErrorLogCapacity];
    private int[] _errorNextPlayers = new int[ErrorLogCapacity];
    private string[] _errorDetails = new string[ErrorLogCapacity];
    private string[] _errorTitles = new string[ErrorLogCapacity];
    private string[] _errorUrls = new string[ErrorLogCapacity];
    private string[] _errorPlaylists = new string[ErrorLogCapacity];
    private int[] _errorTrackNumbers = new int[ErrorLogCapacity];

    // Taken before HandleErrorRetry, which may switch to the fallback handler
    // for the next try, so the entry describes the try that failed.
    private void RecordError(VideoError videoError)
    {
      var handler = ActiveHandler;
      _errorCount++;
      int slot = ErrorSlot(0);
      _errorTimes[slot] = DateTime.Now.ToString("HH:mm:ss");
      _videoErrors[slot] = (int)videoError;
      _errorPlayers[slot] = (int)handler.Type;
      _errorAttempts[slot] = _errorRetryCount;
      _errorDetails[slot] = handler.ErrorDetail;

      object[] track = Track;
      VRCUrl url = TrackUtils.GetUrl(track);
      _errorTitles[slot] = TrackUtils.GetTitle(track);
      _errorUrls[slot] = Utilities.IsValid(url) ? url.Get() : string.Empty;

      Playlist playlist = ActivePlaylist;
      bool fromPlaylist = Utilities.IsValid(playlist) && _playingTrackIndex >= 0;
      _errorPlaylists[slot] = fromPlaylist ? playlist.PlaylistName : string.Empty;
      _errorTrackNumbers[slot] = fromPlaylist ? _playingTrackIndex + 1 : 0;

      PrintLog($"Error #{_errorCount}: {handler.Type.GetString()}: Video error {videoError}.");
    }

    // After HandleErrorRetry: the retry count is above the failed try's only
    // when another try is coming, and the active handler is the one it uses.
    private void RecordErrorRetry()
    {
      int slot = ErrorSlot(0);
      _errorNextPlayers[slot] = _errorRetryCount > _errorAttempts[slot] ? (int)ActiveHandler.Type : -1;
    }

    private int ErrorSlot(int index) => (_errorCount - 1 - index) % ErrorLogCapacity;

    public int ErrorLogCount => Mathf.Min(_errorCount, ErrorLogCapacity);
    public int MaxErrorRetry => _maxErrorRetry;

    // index 0 is the newest error.
    public int GetErrorNumber(int index) => _errorCount - index;
    public string GetErrorTime(int index) => _errorTimes[ErrorSlot(index)];
    public VideoError GetVideoError(int index) => (VideoError)_videoErrors[ErrorSlot(index)];
    public VideoPlayerType GetErrorPlayer(int index) => (VideoPlayerType)_errorPlayers[ErrorSlot(index)];
    // 0 for the first try, n for the nth retry.
    public int GetErrorAttempt(int index) => _errorAttempts[ErrorSlot(index)];
    public bool GetErrorWillRetry(int index) => _errorNextPlayers[ErrorSlot(index)] >= 0;
    // The player the next try uses; only meaningful when GetErrorWillRetry.
    public VideoPlayerType GetErrorNextPlayer(int index) => (VideoPlayerType)Mathf.Max(0, _errorNextPlayers[ErrorSlot(index)]);
    // What the handler knows beyond the error type; empty when nothing.
    public string GetErrorDetail(int index) => _errorDetails[ErrorSlot(index)];
    public string GetErrorTitle(int index) => _errorTitles[ErrorSlot(index)];
    public string GetErrorUrl(int index) => _errorUrls[ErrorSlot(index)];
    // Empty when the track was not played from a playlist.
    public string GetErrorPlaylist(int index) => _errorPlaylists[ErrorSlot(index)];
    // 1-based; 0 when not from a playlist.
    public int GetErrorTrackNumber(int index) => _errorTrackNumbers[ErrorSlot(index)];
  }
}
