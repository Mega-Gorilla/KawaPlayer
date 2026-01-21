using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Yamadev.YamaStream
{
  public enum PlayerState
  {
    Idle,
    Playing,
    Paused,
  }

  [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
  [DefaultExecutionOrder(-1000)]
  [DisallowMultipleComponent]
  public partial class Controller : YamaPlayerListener
  {
    [SerializeField, HideInInspector] private string _version;
    [SerializeField] private PlayerHandler[] _videoPlayerHandlers;
    [SerializeField] private string _timeFormat = @"hh\:mm\:ss";
    [SerializeField] private bool _isLocal;
    [SerializeField, Range(5f, 10f)] private float _retryAfterSeconds = 5.1f;
    [SerializeField, Range(0, 10)] private int _maxErrorRetry = 5;
    [SerializeField, UdonSynced] private VideoPlayerType _playerType;
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(Loop))] private bool _loop;
    [UdonSynced] private byte _syncedState;
    [UdonSynced, FieldChangeCallback(nameof(Speed))] private float _speed = 1f;
    [UdonSynced, FieldChangeCallback(nameof(Repeat))] private ulong _repeat;
    [UdonSynced] private string _title = string.Empty;
    [UdonSynced] private VRCUrl _url = VRCUrl.Empty;
    private object[] _track;
    private PlayerHandler _handler;
    private YamaPlayerListener[] _listeners = new YamaPlayerListener[0];
    private int _errorRetryCount;
    private VRCUrl _retryTargetUrl = VRCUrl.Empty;
    private bool _reloading;
    private int _lastSetTimeFrame = 0;

    private void Start()
    {
      EnsurePlayerHandler();
      SetupHandlers();
      ReadPlaylists();

      InitializeScreen();
      InitializeAudio();

      RegisterHandlerListeners();
    }

    private void Update()
    {
      if (IsPlaying && Time.time - _lastSync > _syncFrequency)
      {
        EnsureVideoTime();
      }
    }

    public string Version => _version;
    public string TimeFormat => _timeFormat;
    public bool IsLocal => _isLocal;
    public PlayerState SyncedState => (PlayerState)_syncedState;
    public PlayerState State => Handler.IsStopped ? PlayerState.Idle : Handler.IsPaused ? PlayerState.Paused : Handler.IsPlaying ? PlayerState.Playing : PlayerState.Idle;
    public bool Paused => Handler.IsPaused;
    public bool Stopped => Handler.IsStopped;
    public bool IsPlaying => Handler.IsPlaying;
    public float Duration => Handler.Duration;
    public string FormatedDuration => TimeSpan.FromSeconds(Duration).ToString(_timeFormat);
    public float VideoTime => Handler.Time;
    public string FormatedVideoTime => TimeSpan.FromSeconds(VideoTime).ToString(_timeFormat);
    public bool IsLoading => Handler.IsLoading;
    public bool IsLive => float.IsInfinity(Duration);

    public YamaPlayerListener[] EventListeners
    {
      get => _listeners;
      set => _listeners = value;
    }

    public void AddListener(YamaPlayerListener listener)
    {
      if (!Utilities.IsValid(listener) || Array.IndexOf(EventListeners, listener) >= 0) return;
      EventListeners = EventListeners.Add(listener);
    }

    public void SendCustomVideoEvent(string eventName)
    {
      foreach (YamaPlayerListener listener in EventListeners)
      {
        listener.SendCustomEvent(eventName);
      }
    }

    public PlayerHandler Handler
    {
      get
      {
        if (!Utilities.IsValid(_handler)) EnsurePlayerHandler();
        return _handler;
      }
    }

    public VideoPlayerType PlayerType
    {
      get => _playerType;
      set
      {
        if (_playerType == value) return;
        _playerType = value;
        EnsurePlayerType();

        if (Networking.IsOwner(gameObject) && !_isLocal)
        {
          RequestSerialization();
        }
      }
    }

    private void RegisterHandlerListeners()
    {
      foreach (PlayerHandler handler in _videoPlayerHandlers)
      {
        if (Utilities.IsValid(handler)) handler.SetListener(this);
      }
    }

    private void EnsurePlayerHandler()
    {
      foreach (PlayerHandler handler in _videoPlayerHandlers)
      {
        if (!Utilities.IsValid(handler)) continue;
        if (handler.Type == _playerType)
        {
          _handler = handler;
          return;
        }
      }
      _handler = null;
      PrintError("No player handler found.");
    }

    private void SetupHandlers()
    {
      foreach (PlayerHandler handler in _videoPlayerHandlers)
      {
        if (Utilities.IsValid(handler)) handler.Loop = _loop;
      }
    }

    private void EnsurePlayerType()
    {
      if (Utilities.IsValid(Handler) && Handler.Type == _playerType) return;
      Stop();

      var oldPlayerType = Handler.Type;

      EnsurePlayerHandler();
      foreach (YamaPlayerListener listener in EventListeners)
      {
        if (Utilities.IsValid(listener)) listener.AfterPlayerHandlerChanged(_playerType);
      }
      PrintLog($"Video player changed from {oldPlayerType.GetString()} to {_playerType.GetString()}.");
    }

    public void Play(bool force = false)
    {
      if ((Stopped || IsPlaying) && !force) return;
      _syncedState = (byte)PlayerState.Playing;
      Handler.Play();

      SendCustomEventDelayedFrames(nameof(CheckRepeat), 0);
    }

    public void Pause(bool force = false)
    {
      if ((Stopped || Paused) && !force) return;
      _syncedState = (byte)PlayerState.Paused;
      Handler.Pause();
    }

    public void Stop(bool force = false)
    {
      if (Stopped && !force) return;
      _syncedState = (byte)PlayerState.Idle;
      Handler.Stop();
    }

    public void Reload()
    {
      if (!Stopped && !IsLoading) LoadTrack(Track, true);
    }

    public bool Loop
    {
      get => _loop;
      set
      {
        _loop = value;
        foreach (PlayerHandler handler in _videoPlayerHandlers) handler.Loop = _loop;

        if (Networking.IsOwner(gameObject) && !_isLocal) RequestSerialization();
        foreach (YamaPlayerListener listener in EventListeners)
        {
          if (Utilities.IsValid(listener)) listener.AfterLoopChanged(value);
        }
        PrintLog($"Loop changed to {_loop}.");
      }
    }

    public float Speed
    {
      get => _speed;
      set
      {
        _speed = value;
        UpdateSpeed();
        if (Networking.IsOwner(gameObject) && !_isLocal)
        {
          UpdateSyncedVideoTime(VideoTime);
          RequestSerialization();
        }
        foreach (YamaPlayerListener listener in EventListeners) if (Utilities.IsValid(listener)) listener.AfterSpeedChanged(value);
        PrintLog($"Speed changed to {_speed:F2}x.");
      }
    }

    public void UpdateSpeed()
    {
      foreach (PlayerHandler handler in _videoPlayerHandlers) handler.Speed = _speed;
      if (!Stopped && _playerType == VideoPlayerType.AVProVideoPlayer)
        SendCustomEventDelayedFrames(nameof(Reload), 0);
      UpdateAudioPitch();
    }

    public RepeatStatus RepeatStatus
    {
      get => RepeatStatus.New(_repeat);
      set => Repeat = value.GetPackedData();
    }

    private ulong Repeat
    {
      get => _repeat;
      set
      {
        _repeat = value;
        SendCustomEventDelayedFrames(nameof(CheckRepeat), 0);
        if (Networking.IsOwner(gameObject) && !_isLocal) RequestSerialization();
        foreach (YamaPlayerListener listener in EventListeners)
        {
          if (Utilities.IsValid(listener)) listener.AfterRepeatChanged(value);
        }
        RepeatStatus status = RepeatStatus.New(_repeat);
        if (status.IsOn()) PrintLog($"Repeat on, start: {status.GetStartTime()}, end: {status.GetEndTime()}.");
      }
    }

    public void CheckRepeat()
    {
      RepeatStatus status = RepeatStatus.New(_repeat);
      if (!IsPlaying || !status.IsOn()) return;
      if (Handler.Time > status.GetEndTime() || Handler.Time < status.GetStartTime()) SetTime(status.GetStartTime());
      SendCustomEventDelayedSeconds(nameof(CheckRepeat), 0.5f);
    }

    public void SetTime(float time)
    {
      if (IsLive || Time.frameCount == _lastSetTimeFrame) return;
      _lastSetTimeFrame = Time.frameCount;

      Handler.Time = time;
      if (Networking.IsOwner(gameObject) && !_isLocal)
      {
        UpdateSyncedVideoTime(time);
        RequestSerialization();
      }

      foreach (YamaPlayerListener listener in EventListeners)
      {
        if (Utilities.IsValid(listener)) listener.AfterTimeChanged(time);
      }
      PrintLog($"{_playerType.GetString()}: Set video time: {time}.");
    }


    public object[] Track
    {
      get
      {
        if (!Utilities.IsValid(_track))
        {
          _track = TrackUtils.CreateEmptyTrack();
        }
        return _track;
      }
      set
      {
        _track = value;
        foreach (YamaPlayerListener listener in EventListeners)
        {
          if (Utilities.IsValid(listener)) listener.AfterTrackUpdated();
        }
      }
    }

    public void PlayTrack(object[] track, bool direct = true)
    {
      if (!Utilities.IsValid(track)) return;

      var url = TrackUtils.GetUrl(track);
      if (!url.IsValidUrl())
      {
        PrintError($"URL {url.Get()} is not valid.");
        return;
      }

      if (IsPlaying && (Networking.IsOwner(gameObject) || _isLocal))
      {
        Stop();
      }

      if (direct) ClearPlaylistIndexes();
      _syncedState = (byte)PlayerState.Playing;
      LoadTrack(track);
    }

    private void LoadTrack(object[] track, bool isReload = false)
    {
      _autoForward = false;
      _reloading = isReload;
      if (!_reloading) Handler.Stop();

      var currentPlayerType = TrackUtils.GetPlayerType(track);
      if (!isReload && PlayerType != currentPlayerType)
      {
        var currentStatus = _syncedState;
        var oldPlayerType = _playerType;

        _playerType = currentPlayerType;
        EnsurePlayerHandler();
        foreach (YamaPlayerListener listener in EventListeners)
        {
          if (Utilities.IsValid(listener)) listener.AfterPlayerHandlerChanged(_playerType);
        }
        PrintLog($"Video player changed from {oldPlayerType.GetString()} to {_playerType.GetString()}.");

        _syncedState = currentStatus;
      }
      if (!_reloading) Track = track;
      Handler.LoadUrl(TrackUtils.GetUrl(track));

      if (Networking.IsOwner(gameObject) && !_isLocal && !isReload)
      {
        RequestSerialization();
      }
      foreach (YamaPlayerListener listener in EventListeners)
      {
        if (Utilities.IsValid(listener)) listener.AfterTrackLoaded();
      }
      PrintLog($"Load url: {TrackUtils.GetUrl(track)}.");
    }
  }
}
