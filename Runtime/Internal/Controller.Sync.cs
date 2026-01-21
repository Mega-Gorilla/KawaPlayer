using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Yamadev.YamaStream
{
  public partial class Controller
  {
    [SerializeField, Range(1f, 10f)] private float _syncFrequency = 5.0f;
    [SerializeField, Range(0f, 1f)] private float _syncMargin = 0.3f;
    [UdonSynced] private float _syncedVideoTime = 0;
    [UdonSynced] private long _networkDataTimeTicks = 0;
    private float _lastSync = 0;
    private float _localDelay = 0;

    private void ApplySyncedState()
    {
      if (!Handler.IsReady) return;
      switch (SyncedState)
      {
        case PlayerState.Idle:
          if (Stopped) return;
          Handler.Stop();
          break;
        case PlayerState.Playing:
          if (IsPlaying) return;
          Handler.Play();
          break;
        case PlayerState.Paused:
          if (Paused) return;
          Handler.Pause();
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
      _networkDataTimeTicks = Networking.GetNetworkDateTime().Ticks;
    }

    private void ResetSyncedVideoTime()
    {
      _syncedVideoTime = 0;
      _networkDataTimeTicks = 0;
    }

    private void EnsureVideoTime(bool force = false)
    {
      if (IsLive || Stopped || _networkDataTimeTicks == 0) return;

      float offset = Paused ? 0 : (float)(Networking.GetNetworkDateTime().Ticks - _networkDataTimeTicks) / TimeSpan.TicksPerSecond * Speed;
      float targetTime = Mathf.Clamp(_syncedVideoTime + offset + _localDelay, 0, Duration);
      if (force || Mathf.Abs(VideoTime - targetTime) >= _syncMargin)
      {
        SetTime(targetTime);
      }

      _lastSync = Time.time;
    }

    public override void OnPreSerialization()
    {
      _playerType = TrackUtils.GetPlayerType(Track);
      _title = TrackUtils.GetTitle(Track);
      _url = TrackUtils.GetUrl(Track);
    }

    public override void OnDeserialization()
    {
      object[] track;
      if (Utilities.IsValid(ActivePlaylist) && _playingTrackIndex >= 0 && _playingTrackIndex < ActivePlaylist.TrackCount)
      {
        track = ActivePlaylist.GetTrack(_playingTrackIndex);
      }
      else
      {
        track = TrackUtils.NewTrack(_playerType, _title, _url);
      }
      int len = _listeners.Length;
      for (int i = 0; i < len; i++)
      {
        var listener = _listeners[i];
        if (Utilities.IsValid(listener)) listener.AfterTrackSynced();
      }

      if (_syncedState != (byte)PlayerState.Idle && TrackUtils.GetUrl(track).Get() != TrackUtils.GetUrl(Track).Get())
      {
        LoadTrack(track);
      }
      else
      {
        SetPlayerType(_playerType);
      }

      ApplySyncedState();
      EnsureVideoTime();
    }
  }
}
