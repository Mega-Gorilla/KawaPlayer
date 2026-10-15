using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

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
    [SerializeField] private bool _allowAutoSwitchHandler = true;
    [SerializeField, Range(0, 10)] private int _useFallbackAfterErrors = 1;
    [SerializeField] private string _timeFormat = @"hh\:mm\:ss";
    [SerializeField] private bool _isLocal;
    [SerializeField, Range(0, 10)] private int _maxErrorRetry = 5;
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(Loop))] private bool _loop;
    [UdonSynced, FieldChangeCallback(nameof(Speed))] private float _speed = 1f;
    [UdonSynced, FieldChangeCallback(nameof(Repeat))] private ulong _repeat;
    [UdonSynced] private byte _syncedState;
    [UdonSynced] private int _handlerIndex;
    [UdonSynced] private string _title = string.Empty;
    [UdonSynced] private VRCUrl _url = VRCUrl.Empty;
    [UdonSynced] private byte[] _trackExtension = new byte[0];
    // Whether the player in use was switched to by a track -- a picture from
    // the tablet, a URL played on the player picked for it in the URL dialog,
    // or a track from a playlist or the queue -- rather than chosen in the
    // settings (issue #182). A URL entered next does not keep the image
    // viewer a picture left behind.
    [UdonSynced] private bool _handlerFromTrack;
    private bool _appliedHandlerFromTrack;
    private object[] _track;
    private PlayerHandler _handler;
    private bool _useFallback;
    private YamaPlayerListener[] _listeners = new YamaPlayerListener[0];
    private int _errorRetryCount;
    private object[] _retryTargetTrack;
    private bool _reloading;
    private int _lastSetTimeFrame = 0;
    private float _lastLoadTime = 0f;
    private bool _checkRepeatRunning = false;
    private string _uiLanguage = string.Empty;

    private const float SAFETY_RETRY_INTERVAL = 5.1f;
    // A reload while loading waits this long, so a load that is only slow is
    // not restarted: normal loads took up to 8.1 s in VRChat, resolving the
    // URL alone 3 to 5 s (issue #232). An error can be reloaded at once. The
    // global reload button waits as long after a send (UIController).
    public const float RELOAD_WAIT_WHILE_LOADING = 15f;
    // _RunPendingReload is scheduled: one at a time.
    private bool _reloadPending;
    // When a reload was last asked for; a load begun since covers it.
    private float _reloadRequestedAt;
    // A change of speed or resolution came while loading: reload once ready.
    private bool _reloadAfterLoad;

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
      if (IsPlaying && Time.time - _lastSync > SYNC_INTERVAL)
      {
        CheckVideoTime();
      }
    }

    public string Version => _version;
    public string TimeFormat => _timeFormat;
    public bool IsLocal => _isLocal;
    public PlayerState SyncedState => (PlayerState)_syncedState;
    public bool IsLoading => ActiveHandler.IsLoading;
    public bool Paused => ActiveHandler.IsPaused;
    public bool Stopped => ActiveHandler.IsStopped;
    public bool IsPlaying => ActiveHandler.IsPlaying;
    public bool IsError => ActiveHandler.IsError;
    public float Duration => ActiveHandler.Duration;
    public float VideoTime => ActiveHandler.Time;
    public bool IsLive => float.IsInfinity(Duration) || float.IsNaN(Duration);
    public string FormatedDuration => IsLive ? string.Empty : TimeSpan.FromSeconds(Duration).ToString(_timeFormat);
    public string FormatedVideoTime => IsLive ? string.Empty : TimeSpan.FromSeconds(VideoTime).ToString(_timeFormat);

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

    // The language the local player last chose on any UI of this player.
    // Every UI follows it, so the screen and a tablet never show two
    // languages (issue #159). It is the player's own and is not synced.
    public string UILanguage => _uiLanguage;

    public void SetUILanguage(string language)
    {
      if (string.IsNullOrEmpty(language) || language == _uiLanguage) return;
      _uiLanguage = language;
      SendCustomVideoEvent("_OnUILanguageChanged");
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
        SetUseFallback(false);
        _handler = value;
        _handlerIndex = Array.IndexOf(_videoPlayerHandlers, value);
        if (Networking.IsOwner(gameObject) && !_isLocal) RequestSerialization();
        NotifyPlayerHandlerChanged();
        PrintLog($"Player handler changed to {_handler.Type.GetString()}.");
      }
    }

    public bool HandlerFromTrack => _handlerFromTrack;

    // Also told when the same player stops or starts counting as switched to
    // by a track: which player a UI offers for the next URL follows that too.
    private void NotifyPlayerHandlerChanged()
    {
      int len = _listeners.Length;
      for (int i = 0; i < len; i++)
      {
        var listener = _listeners[i];
        if (Utilities.IsValid(listener)) listener.AfterPlayerHandlerChanged(_handler.Type);
      }
    }

    [Obsolete("Use Handler instead")]
    public PlayerHandler VideoPlayerHandle => Handler;

    public PlayerHandler ActiveHandler
    {
      get
      {
        if (_useFallback && Utilities.IsValid(Handler.FallbackHandler)) return Handler.FallbackHandler;
        return Handler;
      }
    }

    private void SetUseFallback(bool value)
    {
      if (_useFallback == value) return;
      if (value)
      {
        if (!Utilities.IsValid(Handler.FallbackHandler)) return;
        _useFallback = true;
        return;
      }
      _useFallback = false;
      var fallback = Handler.FallbackHandler;
      if (Utilities.IsValid(fallback) && !fallback.IsStopped) fallback.Stop();
    }

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

    public bool AllowAutoSwitchHandler
    {
      get => _allowAutoSwitchHandler;
      set => _allowAutoSwitchHandler = value;
    }

    // Both choose the player rather than switch to it for a track: it stays
    // for the URLs entered next, even when a track had switched to it
    // already.
    public void SetPlayerType(VideoPlayerType playerType)
    {
      if (Utilities.IsValid(Handler) && Handler.Type == playerType)
      {
        KeepHandler();
        return;
      }
      Stop();
      SwitchToChosenHandler(FindHandlerIndexByType(playerType));
    }

    public void SetPlayerHandler(int index)
    {
      if (index >= 0 && index < _videoPlayerHandlers.Length && _videoPlayerHandlers[index] == Handler)
      {
        KeepHandler();
        return;
      }
      Stop();
      SwitchToChosenHandler(index);
    }

    private void KeepHandler()
    {
      if (!_handlerFromTrack) return;
      SetHandlerFromTrack(false);
      if (Networking.IsOwner(gameObject) && !_isLocal) RequestSerialization();
      NotifyPlayerHandlerChanged();
    }

    // Cleared before switching, so the listeners told of the switch see it.
    private void SwitchToChosenHandler(int index)
    {
      if (IsValidHandlerIndex(index)) SetHandlerFromTrack(false);
      SwitchToHandlerIndex(index);
    }

    // The applied copy follows every change made here, so OnDeserialization
    // notices only one that came over the network.
    private void SetHandlerFromTrack(bool value)
    {
      _handlerFromTrack = value;
      _appliedHandlerFromTrack = value;
    }

    private bool IsValidHandlerIndex(int index)
    {
      return index >= 0 && index < _videoPlayerHandlers.Length && Utilities.IsValid(_videoPlayerHandlers[index]);
    }

    private bool SwitchToHandlerIndex(int index)
    {
      if (!IsValidHandlerIndex(index))
      {
        PrintError($"Cannot switch handler: invalid handler index {index}.");
        return false;
      }

      var next = _videoPlayerHandlers[index];
      _handlerIndex = index;
      if (next == _handler) return true;

      StopLocal();
      Handler = next;
      return true;
    }

    private int FindHandlerIndexByType(VideoPlayerType playerType)
    {
      int len = _videoPlayerHandlers.Length;
      for (int i = 0; i < len; i++)
      {
        var handler = _videoPlayerHandlers[i];
        if (Utilities.IsValid(handler) && handler.Type == playerType) return i;
      }
      return -1;
    }

    public int FindHandlerIndexForUrl(VRCUrl url)
    {
      return FindHandlerIndexForTrack(TrackUtils.NewTrack(0, string.Empty, url));
    }

    public int FindHandlerIndexForTrack(object[] track)
    {
      int len = _videoPlayerHandlers.Length;
      for (int i = 0; i < len; i++)
      {
        var handler = _videoPlayerHandlers[i];
        if (Utilities.IsValid(handler) && handler.IsValidTrack(track)) return i;
      }
      return -1;
    }

    private int ResolveHandlerIndexForTrack(object[] track)
    {
      int declared = FindHandlerIndexByType(TrackUtils.GetPlayerType(track));
      if (declared >= 0 && _videoPlayerHandlers[declared].IsValidTrack(track)) return declared;
      if (_allowAutoSwitchHandler) return FindHandlerIndexForTrack(track);
      return -1;
    }

    public void Play(bool force = false)
    {
      if ((Stopped || IsPlaying) && !force) return;
      _syncedState = (byte)PlayerState.Playing;
      ActiveHandler.Play();
    }

    public void Pause(bool force = false)
    {
      if ((Stopped || Paused) && !force) return;
      _syncedState = (byte)PlayerState.Paused;
      ActiveHandler.Pause();
    }

    public void Stop(bool force = false)
    {
      _autoForward = false;
      _reloading = false;
      if (Stopped && !IsError && !force) return;
      _syncedState = (byte)PlayerState.Idle;
      ClearPlaylistIndexes();
      StopLocal();
      if (IsError) AfterVideoStopped();
    }

    private void StopLocal()
    {
      _autoForward = false;
      ActiveHandler.Stop();
    }

    // Whether reloading would do anything now: the video is shared as playing
    // or paused, and this player is not in a load that may still finish.
    public bool CanReload => SyncedState != PlayerState.Idle && !(IsLoading && Time.time - _lastLoadTime < RELOAD_WAIT_WHILE_LOADING);

    // Reloads this player's video, and only this player's: it also brings
    // back a video that stopped on an error or is stuck loading, which it did
    // nothing for before (issue #232), and nothing is sent.
    public void Reload()
    {
      if (!CanReload) return;
      RequestReload();
    }

    // The reload a change of speed or of the maximum resolution needs on
    // AVPro. A load going on is not restarted: the video is reloaded once it
    // is ready, so the change is not lost.
    public void ReloadForSettings()
    {
      if (Stopped) return;
      if (IsLoading)
      {
        _reloadAfterLoad = true;
        return;
      }
      RequestReload();
    }

    // Whether a reload for everyone would do anything: the video is shared as
    // playing or paused. Each player then judges for itself, as for its own
    // reload, so a load of theirs still under way is left to finish.
    public bool CanGlobalReload => SyncedState != PlayerState.Idle;

    // Every player reloads its own video, as if each had pressed reload: for
    // a live event, where a stream is not placed by the clock and players fall
    // behind, each by their own amount (issue #236). Only the press is sent,
    // and nothing synced changes. A local player has no one to send it to.
    public void GlobalReload()
    {
      if (!CanGlobalReload) return;
      if (_isLocal)
      {
        Reload();
        return;
      }
      SendCustomNetworkEvent(NetworkEventTarget.All, nameof(OnGlobalReload));
    }

    // Run on every player by GlobalReload. Its name has no leading
    // underscore, as Udon lets no network event call such a method.
    public void OnGlobalReload() => Reload();

    // A load starting now takes the speed and resolution as they are, so a
    // reload waiting for the load before it to be ready is no longer needed:
    // an error's retry would otherwise be loaded again once ready.
    private void MarkLoadStarted()
    {
      _lastLoadTime = Time.time;
      _reloadAfterLoad = false;
    }

    // Asks for a reload. It runs once this Controller's last load is 5.1 s
    // old, as VRChat turns away a second load within 5 s, and requests made
    // while it waits make one reload, of whatever is shared when it runs.
    private void RequestReload()
    {
      _reloadRequestedAt = Time.time;
      RunRequestedReload();
    }

    public void _RunPendingReload()
    {
      _reloadPending = false;
      RunRequestedReload();
    }

    // A load begun after the last request -- a retry after an error, due at
    // the same 5.1 s, or a new track -- already does what the reload would,
    // so the request is dropped. A request made after that load still runs,
    // 5.1 s after it: the wait is worked out again whenever it ends, as a
    // load may have begun meanwhile.
    private void RunRequestedReload()
    {
      if (_lastLoadTime >= _reloadRequestedAt || SyncedState == PlayerState.Idle) return;
      float wait = _lastLoadTime + SAFETY_RETRY_INTERVAL - Time.time;
      if (wait > 0)
      {
        if (!_reloadPending)
        {
          _reloadPending = true;
          SendCustomEventDelayedSeconds(nameof(_RunPendingReload), wait);
        }
        return;
      }
      ReloadNow();
    }

    // A non-owner reloads the track it was sent. The owner, and a local
    // player, reload their own: what the owner shares is made from it only
    // when sent, so the synced copy can lag behind, and is empty when local.
    // The player stays as it is (the synced one for a non-owner), so a reload
    // never changes what is shared.
    private void ReloadNow()
    {
      _errorRetryCount = 0;
      _retryTargetTrack = null;

      object[] track;
      int handlerIndex;
      if (Networking.IsOwner(gameObject) || _isLocal)
      {
        track = Track;
        handlerIndex = Array.IndexOf(_videoPlayerHandlers, Handler);
      }
      else
      {
        handlerIndex = SyncedHandlerIndex();
        track = BuildSyncedTrack(handlerIndex);
        Track = track;
      }
      VRCUrl url = TrackUtils.GetUrl(track);
      if (!Utilities.IsValid(url) || string.IsNullOrEmpty(url.Get())) return;

      // Marked as a reload before anything stops: the fallback player, or the
      // one switched from, reports a stop, which otherwise reads as the owner
      // stopping the video, clearing the track and the shared time.
      _reloading = true;
      SetUseFallback(false);
      if (IsValidHandlerIndex(handlerIndex) && _videoPlayerHandlers[handlerIndex] != Handler) SwitchToHandlerIndex(handlerIndex);
      LoadTrackLocal(track, true);
    }

    public bool Loop
    {
      get => _loop;
      set
      {
        _loop = value;
        int handlerLen = _videoPlayerHandlers.Length;
        for (int i = 0; i < handlerLen; i++)
        {
          var handler = _videoPlayerHandlers[i];
          if (Utilities.IsValid(handler)) handler.Loop = _loop;
        }

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
      for (int i = 0; i < len; i++)
      {
        var handler = _videoPlayerHandlers[i];
        if (Utilities.IsValid(handler)) handler.Speed = _speed;
      }
      if (!Stopped && ActiveHandler.Type == VideoPlayerType.AVProVideoPlayer)
        SendCustomEventDelayedFrames(nameof(ReloadForSettings), 0);
      UpdateAudioPitch();
    }

    public ulong Repeat
    {
      get => _repeat;
      set
      {
        _repeat = value;
        CheckRepeat();
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
      if (_checkRepeatRunning) return;
      _checkRepeatRunning = true;
      SendCustomEventDelayedFrames(nameof(_CheckRepeat), 0);
    }

    public void _CheckRepeat()
    {
      if (!RepeatUtils.IsOn(_repeat) || IsLive || Stopped)
      {
        _checkRepeatRunning = false;
        return;
      }

      if (IsPlaying)
      {
        var start = RepeatUtils.GetStartTime(_repeat);
        var end = RepeatUtils.GetEndTime(_repeat);
        if (ActiveHandler.Time > end || ActiveHandler.Time < start) SetTime(start);
      }

      SendCustomEventDelayedSeconds(nameof(_CheckRepeat), 0.5f);
    }

    public void SetTime(float time)
    {
      if (IsLive || Time.frameCount == _lastSetTimeFrame) return;
      _lastSetTimeFrame = Time.frameCount;

      ActiveHandler.Time = time;
      if (Networking.IsOwner(gameObject) && !_isLocal)
      {
        UpdateSyncedVideoTime(time);
        RequestSerialization();
      }

      NotifyTimeChanged(time);
      PrintLog($"{ActiveHandler.Type.GetString()}: Set video time: {time}.");
    }

    // Places this player's video by the shared time, without sharing a time:
    // following the shared time is not a seek by the owner. Going through
    // SetTime made the owner send everyone the time they already had, each
    // time its own video drifted or reloaded (issue #235).
    private void SeekLocal(float time)
    {
      if (IsLive || Time.frameCount == _lastSetTimeFrame) return;
      _lastSetTimeFrame = Time.frameCount;

      ActiveHandler.Time = time;
      NotifyTimeChanged(time);
      PrintLog($"{ActiveHandler.Type.GetString()}: Follow the shared time: {time}.");
    }

    private void NotifyTimeChanged(float time)
    {
      int len = _listeners.Length;
      for (int i = 0; i < len; i++)
      {
        var listener = _listeners[i];
        if (Utilities.IsValid(listener)) listener.AfterTimeChanged(time);
      }
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

      if (ResolveHandlerIndexForTrack(track) < 0)
      {
        PrintError($"Track is not playable: url={TrackUtils.GetUrl(track).Get()}, extension={TrackUtils.GetExtensionString(track)}.");
        return;
      }

      if (IsPlaying && (Networking.IsOwner(gameObject) || _isLocal))
      {
        Stop();
      }

      ClearPlaylistIndexes();

      _syncedState = (byte)PlayerState.Playing;
      ResolveAndLoadTrack(track);
    }

    private void ResolveAndLoadTrack(object[] track, bool isReload = false)
    {
      _reloading = isReload;

      int index = ResolveHandlerIndexForTrack(track);
      if (index >= 0)
      {
        // Only a switch counts: a track on the player chosen leaves it chosen.
        if (_videoPlayerHandlers[index] != Handler) SetHandlerFromTrack(true);
        SwitchToHandlerIndex(index);
      }

      LoadTrackLocal(track, isReload);

      if (Networking.IsOwner(gameObject) && !_isLocal && !isReload)
      {
        _trackVersion++;
        _appliedTrackVersion = _trackVersion;
        // A new track goes out with no shared time, which its owner starts
        // when it is ready (AfterVideoReady). Stopping the last track clears
        // it too, but a track that ended in an error was stopped without.
        ResetSyncedVideoTime();
        RequestSerialization();
      }
    }

    private void LoadTrackLocal(object[] track, bool isReload)
    {
      _reloading = isReload;
      StopLocal();

      if (!isReload) Track = track;
      ActiveHandler.LoadTrack(track);
      MarkLoadStarted();

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
