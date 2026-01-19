using VRC.SDKBase;
using VRC.SDK3.Components.Video;
using UnityEngine;

namespace Yamadev.YamaStream
{
  public partial class Controller
  {
    public override void AfterVideoReady()
    {
      _errorRetryCount = 0;
      _retryTargetUrl = VRCUrl.Empty;
      if (State == PlayerState.Playing) Play(true);

      if (Networking.IsOwner(gameObject) && !_isLocal && !_reloading)
      {
        ResetSyncedVideoTime();
        RequestSerialization();
      }
      else EnsureVideoTime();

      foreach (YamaPlayerListener listener in EventListeners) listener.AfterVideoReady();
      PrintLog($"{_playerType.GetString()}: Video ready.");
      _reloading = false;
    }

    public override void AfterVideoStarted()
    {
      foreach (YamaPlayerListener listener in EventListeners) listener.AfterVideoStarted();
      PrintLog($"{_playerType.GetString()}: Video start.");
    }

    public override void AfterVideoLooped()
    {
      if (Networking.IsOwner(gameObject) && !_isLocal)
      {
        ResetSyncedVideoTime();
        RequestSerialization();
      }

      foreach (YamaPlayerListener listener in EventListeners) listener.AfterVideoLooped();
      PrintLog($"{_playerType.GetString()}: Video loop.");
    }

    public override void AfterVideoEnded()
    {
      if (Networking.IsOwner(gameObject) || _isLocal)
      {
        if (Utilities.IsValid(ActivePlaylist) && _forwardInterval >= 0)
        {
          _autoForward = true;
          SendCustomEventDelayedSeconds(nameof(AutoForward), _forwardInterval);
        }
        else
        {
          ClearPlaylistIndexes();
        }
        Stop();
      }

      foreach (YamaPlayerListener listener in EventListeners) listener.AfterVideoEnded();
      PrintLog($"{_playerType.GetString()}: Video end.");
    }

    public override void AfterVideoErrorOccurred(VideoError videoError)
    {
      PrintLog($"{_playerType.GetString()}: Video error {videoError}.");

      HandleErrorRetry(videoError);
      foreach (YamaPlayerListener listener in EventListeners) listener.AfterVideoErrorOccurred(videoError);
    }

    public void ErrorRetry()
    {
      var currentUrl = TrackUtils.GetUrl(Track);

      if (VRCUrl.IsNullOrEmpty(_retryTargetUrl) || !_retryTargetUrl.Equals(currentUrl))
      {
        _errorRetryCount = 0;
        _retryTargetUrl = VRCUrl.Empty;
        PrintLog("Retry cancelled: track has changed.");
        return;
      }

      if (IsPlaying || !currentUrl.IsValidUrl())
      {
        _retryTargetUrl = VRCUrl.Empty;
        return;
      }

      Handler.PlayUrl(currentUrl);
      foreach (YamaPlayerListener listener in EventListeners) listener.AfterVideoRetry();
    }

    private void HandleErrorRetry(VideoError videoError)
    {
      if (videoError == VideoError.AccessDenied)
      {
        PrintError("Access denied - no retry will be attempted");
        _errorRetryCount = 0;
        _retryTargetUrl = VRCUrl.Empty;
        return;
      }

      if (_errorRetryCount < _maxErrorRetry)
      {
        _errorRetryCount++;
        _retryTargetUrl = TrackUtils.GetUrl(Track);
        PrintLog($"Scheduling retry {_errorRetryCount}/{_maxErrorRetry} in {_retryAfterSeconds} seconds");
        SendCustomEventDelayedSeconds(nameof(ErrorRetry), _retryAfterSeconds);
      }
      else
      {
        _errorRetryCount = 0;
        _retryTargetUrl = VRCUrl.Empty;
        PrintError($"Maximum retry count ({_maxErrorRetry}) reached. Stopping retry attempts.");
      }
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
      foreach (YamaPlayerListener listener in EventListeners) listener.AfterOwnerChanged();
    }
  }
}