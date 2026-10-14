using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Yamadev.YamaStream
{
  public partial class Controller
  {
    // Every client checks its video against the shared time once a second,
    // and seeks only when it is 0.9 s of real time off, as VizVid does (issue
    // #235). The 0.3 s used before was less than what a seek itself costs, so
    // a client that seeked was off again by the next check. They were fields,
    // but no inspector showed them, and a value saved in a world would have
    // kept the old timing.
    private const float SYNC_INTERVAL = 1f;
    private const float SYNC_MARGIN = 0.9f;
    [UdonSynced] private float _syncedVideoTime = 0;
    // The server time (Networking.GetServerTimeInMilliseconds) when
    // _syncedVideoTime was taken, or 0 while there is none. GetNetworkDateTime,
    // used before, stays on the local clock on a client whose date sync fails,
    // and that client played off by its clock's error for good (issue #237).
    // The value wraps, so it is only compared through CalculateServerDeltaTime.
    // Its type changed with it, so VRChat keeps instances of older builds apart.
    [UdonSynced] private int _syncedServerTimeMs = 0;
    [UdonSynced] private int _trackVersion = 0;
    private int _appliedTrackVersion = 0;
    private float _lastSync = 0;
    // Where the video was at the last check, or where it was sent by a seek.
    private float _lastCheckedVideoTime = -1f;
    // The shared time last placed by: a new one is the owner's seek, pause or
    // play, and is followed exactly, while other synced changes are not.
    private int _appliedSyncedServerTimeMs = 0;
    private float _localDelay = 0;

    private void ApplySyncedState()
    {
      if (SyncedState == PlayerState.Idle)
      {
        if (Stopped && !IsError) return;
        StopLocal();
        if (IsError) AfterVideoStopped();
        return;
      }
      if (!ActiveHandler.IsReady) return;
      switch (SyncedState)
      {
        case PlayerState.Playing:
          if (IsPlaying) return;
          ActiveHandler.Play();
          break;
        case PlayerState.Paused:
          if (Paused) return;
          ActiveHandler.Pause();
          break;
      }
    }

    public float LocalDelay
    {
      get => _localDelay;
      set
      {
        _localDelay = value;
        EnsureVideoTime(true);
        int len = _listeners.Length;
        for (int i = 0; i < len; i++)
        {
          var listener = _listeners[i];
          if (Utilities.IsValid(listener)) listener.AfterLocalDelayChanged(value);
        }
      }
    }

    private void UpdateSyncedVideoTime(float time)
    {
      _syncedVideoTime = Mathf.Clamp(time - _localDelay, 0f, Duration);
      int now = Networking.GetServerTimeInMilliseconds();
      _syncedServerTimeMs = now == 0 ? 1 : now;
    }

    private void ResetSyncedVideoTime()
    {
      _syncedVideoTime = 0;
      _syncedServerTimeMs = 0;
    }

    private bool HasSyncedVideoTime => _syncedServerTimeMs != 0;

    // CalculateServerDeltaTime takes seconds, as GetServerTimeInSeconds gives.
    private float SecondsSinceSyncedVideoTime()
    {
      return (float)Networking.CalculateServerDeltaTime(Networking.GetServerTimeInSeconds(), _syncedServerTimeMs * 0.001);
    }

    private void EnsureVideoTime(bool force = false)
    {
      if (IsLive || Stopped || !HasSyncedVideoTime)
      {
        _lastSync = Time.time;
        return;
      }

      float offset = Paused ? 0 : SecondsSinceSyncedVideoTime() * Speed;
      float targetTime = Mathf.Clamp(_syncedVideoTime + offset + _localDelay, 0, Duration);
      if (force || Mathf.Abs(VideoTime - targetTime) / Mathf.Max(Speed, 0.1f) >= SYNC_MARGIN)
      {
        SeekLocal(targetTime);
        _lastCheckedVideoTime = targetTime;
      }

      _lastSync = Time.time;
    }

    // The check made once a second while playing. A video that has not moved
    // since the last check is buffering, or stuck at the place a seek sent it
    // to: seeking again would throw away what has loaded and leave it stuck.
    private void CheckVideoTime()
    {
      float videoTime = VideoTime;
      bool moving = videoTime != _lastCheckedVideoTime;
      _lastCheckedVideoTime = videoTime;
      if (!moving)
      {
        _lastSync = Time.time;
        return;
      }
      EnsureVideoTime();
    }

    public override void OnPreSerialization()
    {
      _title = TrackUtils.GetTitle(Track);
      _url = TrackUtils.GetUrl(Track);
      _trackExtension = TrackUtils.GetExtension(Track);
    }

    // The player the owner shares, or the one in use when the shared index
    // is not one of ours.
    private int SyncedHandlerIndex()
    {
      int handlerIndex = _handlerIndex;
      if (IsValidHandlerIndex(handlerIndex)) return handlerIndex;
      return Mathf.Max(Array.IndexOf(_videoPlayerHandlers, Handler), 0);
    }

    // The track the owner shares, as played on the given player.
    private object[] BuildSyncedTrack(int handlerIndex)
    {
      bool hasSyncedExtension = _trackExtension != null && _trackExtension.Length > 0;
      return hasSyncedExtension
        ? TrackUtils.NewTrackWithExtension(_videoPlayerHandlers[handlerIndex].Type, _title, _url, _trackExtension)
        : TrackUtils.NewTrack(_videoPlayerHandlers[handlerIndex].Type, _title, _url);
    }

    public override void OnDeserialization()
    {
      int handlerIndex = SyncedHandlerIndex();
      object[] track = BuildSyncedTrack(handlerIndex);

      int len = _listeners.Length;
      for (int i = 0; i < len; i++)
      {
        var listener = _listeners[i];
        if (Utilities.IsValid(listener)) listener.AfterTrackSynced();
      }

      bool trackChanged = _trackVersion != _appliedTrackVersion;
      _appliedTrackVersion = _trackVersion;

      // Switching the player tells the listeners; the same player now chosen,
      // or now switched to by a track, has to be told on its own.
      bool fromTrackChanged = _handlerFromTrack != _appliedHandlerFromTrack;
      _appliedHandlerFromTrack = _handlerFromTrack;
      bool handlerChanged = _videoPlayerHandlers[handlerIndex] != Handler;
      SwitchToHandlerIndex(handlerIndex);
      if (fromTrackChanged && !handlerChanged) NotifyPlayerHandlerChanged();
      if (SyncedState != PlayerState.Idle && trackChanged)
      {
        LoadTrackLocal(track, false);
      }

      ApplySyncedState();
      bool newSharedTime = _syncedServerTimeMs != _appliedSyncedServerTimeMs;
      _appliedSyncedServerTimeMs = _syncedServerTimeMs;
      EnsureVideoTime(newSharedTime);
    }
  }
}
