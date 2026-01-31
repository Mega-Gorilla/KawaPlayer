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
    [SerializeField] private PlayerHandler[] _videoPlayerHandlers = new PlayerHandler[0];
    [SerializeField, Range(0, 10)] private int _useFallbackAfterErrors = 1;
    [SerializeField] private string _timeFormat = @"hh\:mm\:ss";
    [SerializeField] private bool _isLocal;
    [SerializeField, Range(0, 10)] private int _maxErrorRetry = 5;
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(Loop))] private bool _loop;
    [UdonSynced, FieldChangeCallback(nameof(Speed))] private float _speed = 1f;
    [UdonSynced, FieldChangeCallback(nameof(Repeat))] private ulong _repeat;
    [UdonSynced] private byte _syncedState;
    [UdonSynced] private VideoPlayerType _playerType;
    [UdonSynced] private string _title = string.Empty;
    [UdonSynced] private VRCUrl _url = VRCUrl.Empty;
    private object[] _track;
    private PlayerHandler _handler;
    private YamaPlayerListener[] _listeners = new YamaPlayerListener[0];
    private int _errorRetryCount;
    private VRCUrl _retryTargetUrl = VRCUrl.Empty;
    private bool _reloading;
    private int _lastSetTimeFrame = 0;
    private float _lastLoadTime = 0f;

    private const float SAFETY_RETRY_INTERVAL = 5.1f;

    private void Start()
    {
      if (!Utilities.IsValid(_videoPlayerHandlers) || _videoPlayerHandlers.Length == 0)
      {
        PrintError($"Video player handlers are not assigned to {name}");
        return;
      }

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
    public bool IsError => Handler.IsError;
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
      if (!Utilities.IsValid(listener) || Array.IndexOf(_listeners, listener) >= 0) return;
      _listeners = _listeners.Add(listener);
    }

    public void SendCustomVideoEvent(string eventName)
    {
      int len = _listeners.Length;
      for (int i = 0; i < len; i++)
      {
        _listeners[i].SendCustomEvent(eventName);
      }
    }

    public PlayerHandler Handler
    {
      get
      {
        if (!Utilities.IsValid(_handler))
        {
          _handler = _videoPlayerHandlers[0];
        }
        return _handler;
      }
      set
      {
        _handler = value;
        if (Networking.IsOwner(gameObject) && !_isLocal) RequestSerialization();
        int len = _listeners.Length;
        for (int i = 0; i < len; i++)
        {
          var listener = _listeners[i];
          if (Utilities.IsValid(listener)) listener.AfterPlayerHandlerChanged(_handler.Type);
        }
        PrintLog($"Player handler changed to {_handler.Type.GetString()}.");
      }
    }

    [Obsolete("Use Handler instead")]
    public PlayerHandler VideoPlayerHandle => Handler;

    private void RegisterHandlerListeners()
    {
      int len = _videoPlayerHandlers.Length;
      for (int i = 0; i < len; i++)
      {
        var handler = _videoPlayerHandlers[i];
        if (Utilities.IsValid(handler)) handler.SetListener(this);
      }
    }

    private void SetupHandlers()
    {
      if (!Utilities.IsValid(_handler)) _handler = _videoPlayerHandlers[0];

      int len = _videoPlayerHandlers.Length;
      for (int i = 0; i < len; i++)
      {
        var handler = _videoPlayerHandlers[i];
        if (Utilities.IsValid(handler)) handler.Loop = _loop;
      }
    }

    public void SetPlayerType(VideoPlayerType playerType)
    {
      if (Utilities.IsValid(Handler) && Handler.Type == playerType) return;
      Stop();

      int len = _videoPlayerHandlers.Length;
      for (int i = 0; i < len; i++)
      {
        var handler = _videoPlayerHandlers[i];
        if (!Utilities.IsValid(handler)) continue;
        if (handler.Type == playerType)
        {
          Handler = handler;
          return;
        }
      }

      PrintError("Could not find player handler for player type: " + playerType.GetString());
    }

    public void Play(bool force = false)
    {
      if ((Stopped || IsPlaying) && !force) return;
      _syncedState = (byte)PlayerState.Playing;
      Handler.Play();
    }

    public void Pause(bool force = false)
    {
      if ((Stopped || Paused) && !force) return;
      _syncedState = (byte)PlayerState.Paused;
      Handler.Pause();
    }

    public void Stop(bool force = false)
    {
      if (Stopped && !IsError && !force) return;
      _syncedState = (byte)PlayerState.Idle;
      ClearPlaylistIndexes();
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
        int handlerLen = _videoPlayerHandlers.Length;
        for (int i = 0; i < handlerLen; i++) _videoPlayerHandlers[i].Loop = _loop;

        if (Networking.IsOwner(gameObject) && !_isLocal) RequestSerialization();
        int listenerLen = _listeners.Length;
        for (int i = 0; i < listenerLen; i++)
        {
          var listener = _listeners[i];
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
        int len = _listeners.Length;
        for (int i = 0; i < len; i++)
        {
          var listener = _listeners[i];
          if (Utilities.IsValid(listener)) listener.AfterSpeedChanged(value);
        }
        PrintLog($"Speed changed to {_speed:F2}x.");
      }
    }

    public void UpdateSpeed()
    {
      int len = _videoPlayerHandlers.Length;
      for (int i = 0; i < len; i++) _videoPlayerHandlers[i].Speed = _speed;
      if (!Stopped && Handler.Type == VideoPlayerType.AVProVideoPlayer && !Handler.UseFallbackHandler)
        SendCustomEventDelayedFrames(nameof(Reload), 0);
      UpdateAudioPitch();
    }

    public ulong Repeat
    {
      get => _repeat;
      set
      {
        _repeat = value;
        SendCustomEventDelayedFrames(nameof(CheckRepeat), 0);
        if (Networking.IsOwner(gameObject) && !_isLocal) RequestSerialization();
        int len = _listeners.Length;
        for (int i = 0; i < len; i++)
        {
          var listener = _listeners[i];
          if (Utilities.IsValid(listener)) listener.AfterRepeatChanged(value);
        }

        if (RepeatUtils.IsOn(_repeat)) PrintLog($"Repeat on, start: {RepeatUtils.GetStartTime(_repeat)}, end: {RepeatUtils.GetEndTime(_repeat)}.");
      }
    }

    public void CheckRepeat()
    {
      if (!IsPlaying || IsLive || !RepeatUtils.IsOn(_repeat)) return;

      var start = RepeatUtils.GetStartTime(_repeat);
      var end = RepeatUtils.GetEndTime(_repeat);
      if (Handler.Time > end || Handler.Time < start) SetTime(start);

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

      int len = _listeners.Length;
      for (int i = 0; i < len; i++)
      {
        var listener = _listeners[i];
        if (Utilities.IsValid(listener)) listener.AfterTimeChanged(time);
      }
      PrintLog($"{Handler.Type.GetString()}: Set video time: {time}.");
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
        int len = _listeners.Length;
        for (int i = 0; i < len; i++)
        {
          var listener = _listeners[i];
          if (Utilities.IsValid(listener)) listener.AfterTrackUpdated();
        }
      }
    }

    public void PlayTrack(object[] track)
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

      ClearPlaylistIndexes();

      _syncedState = (byte)PlayerState.Playing;
      LoadTrack(track);
    }

    private void LoadTrack(object[] track, bool isReload = false)
    {
      _autoForward = false;
      _reloading = isReload;
      Handler.Stop();

      var trackPlayerType = TrackUtils.GetPlayerType(track);
      SetPlayerType(trackPlayerType);

      if (!_reloading) Track = track;
      Handler.LoadUrl(TrackUtils.GetUrl(track));
      _lastLoadTime = Time.time;

      if (Networking.IsOwner(gameObject) && !_isLocal && !isReload)
      {
        RequestSerialization();
      }

      int len = _listeners.Length;
      for (int i = 0; i < len; i++)
      {
        var listener = _listeners[i];
        if (Utilities.IsValid(listener)) listener.AfterTrackLoaded();
      }
      PrintLog($"Load url: {TrackUtils.GetUrl(track)}.");
    }
  }
}
