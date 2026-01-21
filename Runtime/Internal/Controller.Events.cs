using VRC.SDKBase;
using VRC.SDK3.Components.Video;

namespace Yamadev.YamaStream
{
  public partial class Controller
  {
    public override void AfterVideoPlayed()
    {
      if (Networking.IsOwner(gameObject) && !_isLocal && !_reloading)
      {
        UpdateSyncedVideoTime(VideoTime);
        RequestSerialization();
      }

      int len = _listeners.Length;
      for (int i = 0; i < len; i++)
      {
        var listener = _listeners[i];
        if (Utilities.IsValid(listener)) listener.AfterVideoPlayed();
      }
      PrintLog($"{_handler.Type.GetString()}: Video play.");
    }

    public override void AfterVideoPaused()
    {
      if (Networking.IsOwner(gameObject) && !_isLocal && !_reloading)
      {
        UpdateSyncedVideoTime(VideoTime);
        RequestSerialization();
      }

      int len = _listeners.Length;
      for (int i = 0; i < len; i++)
      {
        var listener = _listeners[i];
        if (Utilities.IsValid(listener)) listener.AfterVideoPaused();
      }
      PrintLog($"{_handler.Type.GetString()}: Video pause.");
    }

    public override void AfterVideoStopped()
    {
      _reloading = false;
      _errorRetryCount = 0;
      _retryTargetUrl = VRCUrl.Empty;
      _repeat = 0;
      Handler.UseFallbackHandler = false;

      if (Networking.IsOwner(gameObject) && !_isLocal)
      {
        if (!string.IsNullOrEmpty(TrackUtils.GetUrl(Track).Get())) _history.AddTrack(Track);
        Track = TrackUtils.CreateEmptyTrack();
        _syncedVideoTime = 0f;
        _networkDataTimeTicks = 0;
        RequestSerialization();
      }
      else
      {
        Track = TrackUtils.CreateEmptyTrack();
      }

      int len = _listeners.Length;
      for (int i = 0; i < len; i++)
      {
        var listener = _listeners[i];
        if (Utilities.IsValid(listener)) listener.AfterVideoStopped();
      }
      PrintLog($"{_handler.Type.GetString()}: Video stop.");
    }

    public override void AfterVideoReady()
    {
      _errorRetryCount = 0;
      _retryTargetUrl = VRCUrl.Empty;
      if (SyncedState == PlayerState.Playing) Play(true);
      if (SyncedState == PlayerState.Idle) Stop(true);

      if (Networking.IsOwner(gameObject) && !_isLocal && !_reloading)
      {
        ResetSyncedVideoTime();
        RequestSerialization();
      }
      else EnsureVideoTime();

      int len = _listeners.Length;
      for (int i = 0; i < len; i++)
      {
        var listener = _listeners[i];
        if (Utilities.IsValid(listener)) listener.AfterVideoReady();
      }
      PrintLog($"{_handler.Type.GetString()}: Video ready.");
      _reloading = false;
    }

    public override void AfterVideoStarted()
    {
      int len = _listeners.Length;
      for (int i = 0; i < len; i++)
      {
        var listener = _listeners[i];
        if (Utilities.IsValid(listener)) listener.AfterVideoStarted();
      }
      PrintLog($"{_handler.Type.GetString()}: Video start.");
    }

    public override void AfterVideoLooped()
    {
      if (Networking.IsOwner(gameObject) && !_isLocal)
      {
        ResetSyncedVideoTime();
        RequestSerialization();
      }

      int len = _listeners.Length;
      for (int i = 0; i < len; i++)
      {
        var listener = _listeners[i];
        if (Utilities.IsValid(listener)) listener.AfterVideoLooped();
      }
      PrintLog($"{_handler.Type.GetString()}: Video loop.");
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

      int len = _listeners.Length;
      for (int i = 0; i < len; i++)
      {
        var listener = _listeners[i];
        if (Utilities.IsValid(listener)) listener.AfterVideoEnded();
      }
      PrintLog($"{_handler.Type.GetString()}: Video end.");
    }

    public override void AfterVideoErrorOccurred(VideoError videoError)
    {
      PrintLog($"{_handler.Type.GetString()}: Video error {videoError}.");

      HandleErrorRetry(videoError);
      int len = _listeners.Length;
      for (int i = 0; i < len; i++)
      {
        var listener = _listeners[i];
        if (Utilities.IsValid(listener)) listener.AfterVideoErrorOccurred(videoError);
      }
    }

    private void HandleErrorRetry(VideoError videoError)
    {
      switch (videoError)
      {
        case VideoError.AccessDenied:
          PrintError("Access denied - no retry will be attempted");
          _errorRetryCount = 0;
          _retryTargetUrl = VRCUrl.Empty;
          return;
        case VideoError.InvalidURL:
          PrintError("Invalid URL - no retry will be attempted");
          _errorRetryCount = 0;
          _retryTargetUrl = VRCUrl.Empty;
          return;
        case VideoError.PlayerError:
          if (_errorRetryCount == 0)
          {
            if (_useFallbackHandler && Utilities.IsValid(Handler.FallbackHandler))
            {
              Handler.UseFallbackHandler = true;
              PrintLog($"Switching to fallback handler: {Handler.FallbackHandler.Type.GetString()}");
            }
          }
          else
          {
            Handler.UseFallbackHandler = false;
          }
          break;
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

      Handler.LoadUrl(currentUrl);
      int len = _listeners.Length;
      for (int i = 0; i < len; i++)
      {
        var listener = _listeners[i];
        if (Utilities.IsValid(listener)) listener.AfterVideoRetry();
      }
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
      int len = _listeners.Length;
      for (int i = 0; i < len; i++)
      {
        var listener = _listeners[i];
        if (Utilities.IsValid(listener)) listener.AfterOwnerChanged();
      }
    }
  }
}