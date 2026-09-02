using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDK3.Components.Video;
using VRC.SDKBase;

namespace Yamadev.YamaStream.UI
{
  [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
  public partial class UIController : YamaPlayerListener
  {
    #region Serialized Fields

    [Header("Core")]
    [SerializeField] private Controller _controller;
    [SerializeField, HideInInspector] private Color _primaryColor = new Color(240f / 256f, 98f / 256f, 146f / 256f, 1.0f);
    [SerializeField, HideInInspector] private Color _secondaryColor = new Color(248f / 256f, 187f / 256f, 208f / 256f, 31f / 256f);

    [Header("Appearance - Idle Screen")]
    [SerializeField] private Image _idleScreenImage;
    [SerializeField] private Sprite _idleScreenSprite;

    [Header("Animator")]
    [SerializeField] private Animator _userUIAnimator;
    [SerializeField] private Animator _systemUIAnimator;

    [Header("Modal Dialog")]
    [SerializeField] private Modal _modalDialog;
    [SerializeField] private ToggleGroup _modalPlayerSelectorGroup;
    [SerializeField] private Toggle _modalUnityPlayerToggle;
    [SerializeField] private Toggle _modalAVProPlayerToggle;
    [SerializeField] private Toggle _modalImageViewerToggle;

    [Header("URL Interceptor")]
    // Wired at build time by PlaylistLoaderBuildProcess when the module is
    // present; see the contract in PlayUrlField.
    [SerializeField] private UdonSharpBehaviour _urlInterceptor;
    // Line shown in the (otherwise empty) track info bar while nothing is
    // loaded: the player name and version, optionally followed by what the
    // URL input accepts. The trailing sentence's key is supplied by an
    // interceptor module's build process (issue #84); empty means the
    // branding shows on its own.
    [SerializeField] private Text _urlHintText;
    [SerializeField, HideInInspector] private string _urlHintKey;

    [Header("Track Info Display (Top Bar)")]
    [SerializeField, RegisterEvent(nameof(VRCUrlInputField.onEndEdit), nameof(PlayUrlTop))] private VRCUrlInputField _urlInputFieldTop;
    [SerializeField] private Text _trackTitleText;
    [SerializeField] private Text _trackUrlText;

    [Header("Playback Control Buttons (Left Side)")]
    [SerializeField, RegisterEvent(nameof(VRCUrlInputField.onEndEdit), nameof(PlayUrl))] private VRCUrlInputField _urlInputField;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(Loop))] private Button _loopOnButton;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(LoopOff))] private Button _loopOffButton;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(SetShuffle))] private Button _shufflePlayButton;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(SetShuffleOff))] private Button _shufflePlayOffButton;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(Reload))] private Button _reloadButton;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(LockUI))] private Button _lockButton;

    [Header("Progress Display (Bottom Bar)")]
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(Backward))] private Button _backwardButton;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(Play))] private Button _playButton;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(Pause))] private Button _pauseButton;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(Stop))] private Button _stopButton;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(Forward))] private Button _forwardButton;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(Mute))] private Button _muteButton;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(Unmute))] private Button _unmuteButton;
    [SerializeField, RegisterEvent(nameof(Slider.onValueChanged), nameof(SetVolume))] private Slider _volumeSlider;
    [SerializeField] private SliderHelper _volumeSliderHelper;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(SetVolumeByHelper))] private Button _volumeTooltipButton;
    [SerializeField] private Text _volumeTooltipText;
    [SerializeField]
    [RegisterEventTrigger(EventTriggerType.BeginDrag, nameof(BeginProgressDrag))]
    [RegisterEventTrigger(EventTriggerType.EndDrag, nameof(SetTime))]
    private Slider _progressSlider;
    [SerializeField] private Text _currentTimeText;
    [SerializeField] private Text _totalDurationText;
    [SerializeField] private SliderHelper _progressSliderHelper;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(SetTimeByHelper))] private Button _progressTooltipButton;
    [SerializeField] private Text _progressTooltipText;

    [Header("Settings - Playback")]
    [SerializeField, RegisterEvent(nameof(Toggle.onValueChanged), nameof(SetUnityPlayer))] private Toggle _unityPlayerToggle;
    [SerializeField, RegisterEvent(nameof(Toggle.onValueChanged), nameof(SetAVProPlayer))] private Toggle _avProPlayerToggle;
    [SerializeField, RegisterEvent(nameof(Toggle.onValueChanged), nameof(SetImageViewer))] private Toggle _imageViewerToggle;
    [SerializeField, RegisterEventTrigger(EventTriggerType.EndDrag, nameof(SetSpeed))] private Slider _speedSlider;
    [SerializeField] private Text _speedValueText;
    [SerializeField] private RangeSlider _repeatRangeSlider;
    [SerializeField, RegisterEvent(nameof(Toggle.onValueChanged), nameof(RepeatOff))] private Toggle _repeatOffToggle;
    [SerializeField, RegisterEvent(nameof(Toggle.onValueChanged), nameof(Repeat))] private Toggle _repeatOnToggle;
    [SerializeField] private Text _repeatStartTimeText;
    [SerializeField] private Text _repeatEndTimeText;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(Subtract100ms))] private Button _localDelaySubtract100msButton;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(Subtract50ms))] private Button _localDelaySubtract50msButton;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(Add50ms))] private Button _localDelayAdd50msButton;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(Add100ms))] private Button _localDelayAdd100msButton;
    [SerializeField] private Text _localDelayValueText;

    [Header("Settings - Audio/Video")]
    [SerializeField, RegisterEvent(nameof(Toggle.onValueChanged), nameof(SetMirrorFlipOn))] private Toggle _mirrorFlipOnToggle;
    [SerializeField, RegisterEvent(nameof(Toggle.onValueChanged), nameof(SetMirrorFlipOff))] private Toggle _mirrorFlipOffToggle;
    [SerializeField, RegisterEvent(nameof(Toggle.onValueChanged), nameof(SetBrightness))] private Slider _brightnessSlider;
    [SerializeField] private Text _brightnessValueText;
    [SerializeField, RegisterEvent(nameof(Toggle.onValueChanged), nameof(SetMaxResolution360))] private Toggle _maxResolution360Toggle;
    [SerializeField, RegisterEvent(nameof(Toggle.onValueChanged), nameof(SetMaxResolution480))] private Toggle _maxResolution480Toggle;
    [SerializeField, RegisterEvent(nameof(Toggle.onValueChanged), nameof(SetMaxResolution720))] private Toggle _maxResolution720Toggle;
    [SerializeField, RegisterEvent(nameof(Toggle.onValueChanged), nameof(SetMaxResolution1080))] private Toggle _maxResolution1080Toggle;
    [SerializeField, RegisterEvent(nameof(Toggle.onValueChanged), nameof(SetMaxResolution2160))] private Toggle _maxResolution2160Toggle;
    [SerializeField, RegisterEvent(nameof(Toggle.onValueChanged), nameof(SetMaxResolution4320))] private Toggle _maxResolution4320Toggle;

    [Header("Loading & Message")]
    [SerializeField] private GameObject _loadingIndicator;
    [SerializeField] private Text _statusMessageText;

    [Header("Version Info")]
    [SerializeField] private TextAsset _updateLogTextAsset;
    [SerializeField] private Text _versionDisplayText;
    [SerializeField] private Text _updateLogText;

    #endregion

    private UdonSharpBehaviour[] _listeners = new UdonSharpBehaviour[0];
    private bool _progressDrag;
    private bool _actionCancelled;

    private void Start()
    {
      if (!_controller)
      {
        PrintError($"Controller is not assigned to UIController:{name}");
        return;
      }
      _controller.AddListener(this);

      InitializeTranslation();
      UpdateTranslation();
      GeneratePlaylistView();

      UpdateUI();

      GenerateVersionView();
      UpdateIdleScreenView();

      if (Utilities.IsValid(_repeatRangeSlider))
      {
        _repeatRangeSlider.SetUp(this, nameof(SetRepeatStart), nameof(SetRepeatEnd));
      }
      if (Utilities.IsValid(_userUIAnimator) && _defaultPlaylistOpen) _userUIAnimator.SetTrigger("TogglePlaylist");
    }

    private void Update()
    {
      if (!Utilities.IsValid(_controller)) return;
      if (Utilities.IsValid(_volumeSliderHelper) && Utilities.IsValid(_volumeTooltipText))
      {
        _volumeTooltipText.text = $"{Mathf.Ceil(_volumeSliderHelper.Percent * 100)}%";
      }
      if (!_controller.Stopped && !_controller.IsLoading) UpdateProgressView();
    }

    public void AddListener(UdonSharpBehaviour listener)
    {
      if (!Utilities.IsValid(listener) || Array.IndexOf(_listeners, listener) >= 0) return;
      _listeners = _listeners.Add(listener);
    }

    public bool InvokeBeforeEvent(string eventName)
    {
      _actionCancelled = false;
      int len = _listeners.Length;
      for (int i = 0; i < len; i++)
      {
        var listener = _listeners[i];
        if (Utilities.IsValid(listener)) listener.SendCustomEvent(eventName);
        if (_actionCancelled) break;
      }
      return !_actionCancelled;
    }

    public void CancelCurrentAction() => _actionCancelled = true;

    // Whether this UI can put a dialog up at all, and whether one is
    // already waiting on an answer. A caller that falls back to acting
    // without asking has to tell those apart: doing so because a question
    // is on screen would go ahead with something nobody agreed to.
    // The URL the open play-or-queue question is about, and the field it
    // came from. Settled when the question goes up; see PlayUrlField.
    private VRCUrl _pendingPlayUrl;
    private VRCUrlInputField _pendingPlayUrlField;

    public bool HasModalDialog => Utilities.IsValid(_modalDialog);
    public bool IsModalBusy => Utilities.IsValid(_modalDialog) && _modalDialog.IsAwaitingAnswer;

    public void ShowMessage(string title, string message, string closeText = null)
    {
      if (!Utilities.IsValid(_modalDialog)) return;
      _modalDialog.Show(title, message, string.IsNullOrEmpty(closeText) ? GetTranslation("button.close") : closeText, "", this, null, null);
    }

    public void SetUnityPlayer()
    {
      if (_controller.Handler.Type == VideoPlayerType.UnityVideoPlayer) return;
      if (!Utilities.IsValid(_modalDialog) || (_controller.Stopped && !_controller.IsLoading))
      {
        SetUnityPlayerInternal();
        return;
      }
      // Refused while another question is up. The toggle already moved to
      // the player that will not be selected after all, so put it back --
      // the same thing cancelling does.
      if (!_modalDialog.TryShow(
        GetTranslation("msg.confirmChangePlayer"),
        GetTranslation("msg.confirmChangePlayerDetail"),
        GetTranslation("button.cancel"),
        GetTranslation("button.continue"),
        this,
        nameof(UpdatePlayerSelector), // close event
        nameof(SetUnityPlayerInternal)))
        UpdatePlayerSelector();
    }

    public void SetUnityPlayerInternal()
    {
      if (!InvokeBeforeEvent("BeforeUserChangePlayerHandler"))
      {
        UpdateUI();
        return;
      }
      _controller.TakeOwnership();
      _controller.SetPlayerType(VideoPlayerType.UnityVideoPlayer);
    }

    public void SetAVProPlayer()
    {
      if (_controller.Handler.Type == VideoPlayerType.AVProVideoPlayer) return;
      if (!Utilities.IsValid(_modalDialog) || (_controller.Stopped && !_controller.IsLoading))
      {
        SetAVProPlayerInternal();
        return;
      }
      if (!_modalDialog.TryShow(
        GetTranslation("msg.confirmChangePlayer"),
        GetTranslation("msg.confirmChangePlayerDetail"),
        GetTranslation("button.cancel"),
        GetTranslation("button.continue"),
        this,
        nameof(UpdatePlayerSelector), // close event
        nameof(SetAVProPlayerInternal)))
        UpdatePlayerSelector();
    }

    public void SetAVProPlayerInternal()
    {
      if (!InvokeBeforeEvent("BeforeUserChangePlayerHandler"))
      {
        UpdateUI();
        return;
      }
      _controller.TakeOwnership();
      _controller.SetPlayerType(VideoPlayerType.AVProVideoPlayer);
    }

    public void SetImageViewer()
    {
      if (_controller.Handler.Type == VideoPlayerType.ImageViewer) return;
      if (!Utilities.IsValid(_modalDialog) || (_controller.Stopped && !_controller.IsLoading))
      {
        SetImageViewerInternal();
        return;
      }
      if (!_modalDialog.TryShow(
        GetTranslation("msg.confirmChangePlayer"),
        GetTranslation("msg.confirmChangePlayerDetail"),
        GetTranslation("button.cancel"),
        GetTranslation("button.continue"),
        this,
        nameof(UpdatePlayerSelector), // close event
        nameof(SetImageViewerInternal)))
        UpdatePlayerSelector();
    }

    public void SetImageViewerInternal()
    {
      if (!InvokeBeforeEvent("BeforeUserChangePlayerHandler"))
      {
        UpdateUI();
        return;
      }
      _controller.TakeOwnership();
      _controller.SetPlayerType(VideoPlayerType.ImageViewer);
    }

    public void PlayUrl() => PlayUrlField(_urlInputField);

    public void PlayUrlTop() => PlayUrlField(_urlInputFieldTop);

    private void PlayUrlField(VRCUrlInputField urlInputField)
    {
      if (!Utilities.IsValid(urlInputField)) return;

      // Optional URL interceptor (issue #82): a module (PlaylistLoader) can
      // claim the entered URL before the video path sees it. Wired by the
      // module's build process; unassigned means the hook is inert. This is a
      // dedicated slot rather than a Before event because _actionCancelled is
      // a single shared flag — the interceptor itself invokes
      // InvokeBeforeEvent("BeforeUserLoadPlaylist"), and nesting that inside
      // another InvokeBeforeEvent would race the flag.
      if (Utilities.IsValid(_urlInterceptor))
      {
        _urlInterceptor.SetProgramVariable("interceptUrl", urlInputField.GetUrl());
        // Pass the calling UIController so a shared interceptor talks back
        // to the panel the URL was entered on, not a fixed one.
        _urlInterceptor.SetProgramVariable("interceptSource", this);
        _urlInterceptor.SetProgramVariable("interceptHandled", false);
        _urlInterceptor.SendCustomEvent("OnUrlSubmitted");
        if ((bool)_urlInterceptor.GetProgramVariable("interceptHandled"))
        {
          urlInputField.SetUrl(VRCUrl.Empty);
          return;
        }
      }

      if (_controller.FindHandlerIndexForUrl(urlInputField.GetUrl()) < 0)
      {
        urlInputField.SetUrl(VRCUrl.Empty);
        return;
      }

      if (!Utilities.IsValid(_modalDialog) || (_controller.Stopped && !_controller.IsLoading))
      {
        PlayUrlInternal(_controller.Handler.Type, urlInputField.GetUrl());
        urlInputField.SetUrl(VRCUrl.Empty);
        return;
      }

      // Nothing below may run while another question is up. The selector
      // lives inside the dialog, so switching it on would change what that
      // question looks like, and switching it back would hide part of it.
      if (_modalDialog.IsAwaitingAnswer) return;

      ShowVideoPlayerSelector();
      if (!_modalDialog.TryShow(
        GetTranslation("label.playUrl"),
        GetTranslation("msg.confirmPlayUrlDetail"),
        GetTranslation("button.cancel"),
        GetTranslation("button.confirmAddQueue"),
        GetTranslation("button.confirmPlayUrl"),
        this,
        nameof(CancelPlayUrl),
        nameof(AddUrlToQueueEvent),
        nameof(PlayUrlEvent)))
      {
        HideVideoPlayerSelector();
        return;
      }

      // What the question is about, settled now. The buttons used to read
      // the field when pressed, which was harmless while entering a second
      // URL replaced the dialog. It no longer does, so the field can hold
      // something else by the time an answer arrives -- and the answer would
      // have applied to that instead of to what was asked about.
      _pendingPlayUrl = urlInputField.GetUrl();
      _pendingPlayUrlField = urlInputField;
    }

    // Answered no, or closed. Nothing is played, and the question is let go
    // of so the next one is about its own URL.
    public void CancelPlayUrl()
    {
      _pendingPlayUrl = null;
      _pendingPlayUrlField = null;
      HideVideoPlayerSelector();
    }

    public void ShowVideoPlayerSelector()
    {
      if (!Utilities.IsValid(_modalPlayerSelectorGroup)) return;

      if (Utilities.IsValid(_modalUnityPlayerToggle))
      {
        _modalUnityPlayerToggle.isOn = _controller.Handler.Type == VideoPlayerType.UnityVideoPlayer;
      }
      if (Utilities.IsValid(_modalAVProPlayerToggle))
      {
        _modalAVProPlayerToggle.isOn = _controller.Handler.Type == VideoPlayerType.AVProVideoPlayer;
      }
      if (Utilities.IsValid(_modalImageViewerToggle))
      {
        _modalImageViewerToggle.isOn = _controller.Handler.Type == VideoPlayerType.ImageViewer;
      }

      _modalPlayerSelectorGroup.gameObject.SetActive(true);
    }

    public void HideVideoPlayerSelector()
    {
      if (Utilities.IsValid(_modalPlayerSelectorGroup))
      {
        _modalPlayerSelectorGroup.gameObject.SetActive(false);
      }
    }

    public VideoPlayerType GetVideoPlayerSelectorValue()
    {
      if (Utilities.IsValid(_modalUnityPlayerToggle) && _modalUnityPlayerToggle.isOn) return VideoPlayerType.UnityVideoPlayer;
      if (Utilities.IsValid(_modalAVProPlayerToggle) && _modalAVProPlayerToggle.isOn) return VideoPlayerType.AVProVideoPlayer;
      if (Utilities.IsValid(_modalImageViewerToggle) && _modalImageViewerToggle.isOn) return VideoPlayerType.ImageViewer;
      return _controller.Handler.Type;
    }

    // The question carries which field it came from, so one handler answers
    // for both. The Top names are no longer used from here; they stay
    // because they are public and something outside may still send them.
    public void AddUrlToQueueEvent() => AddPendingUrlToQueue();

    public void AddUrlTopToQueueEvent() => AddPendingUrlToQueue();

    private void AddPendingUrlToQueue()
    {
      var url = _pendingPlayUrl;
      var field = _pendingPlayUrlField;
      _pendingPlayUrl = null;
      _pendingPlayUrlField = null;
      HideVideoPlayerSelector();
      if (!Utilities.IsValid(url)) return;

      AddUrlToQueueEventInternal(GetVideoPlayerSelectorValue(), url);
      if (Utilities.IsValid(field)) field.SetUrl(VRCUrl.Empty);
    }

    public void AddUrlToQueueEventInternal(VideoPlayerType playerType, VRCUrl url)
    {
      if (!InvokeBeforeEvent("BeforeUserAddTrackToQueue"))
      {
        UpdateUI();
        return;
      }

      _controller.TakeOwnership();
      _controller.Queue.AddTrack(TrackUtils.NewTrack(playerType, "", url));
    }

    public void PlayUrlEvent() => PlayPendingUrl();

    public void PlayUrlTopEvent() => PlayPendingUrl();

    private void PlayPendingUrl()
    {
      var url = _pendingPlayUrl;
      var field = _pendingPlayUrlField;
      _pendingPlayUrl = null;
      _pendingPlayUrlField = null;
      HideVideoPlayerSelector();
      if (!Utilities.IsValid(url)) return;

      PlayUrlInternal(GetVideoPlayerSelectorValue(), url);
      if (Utilities.IsValid(field)) field.SetUrl(VRCUrl.Empty);
    }

    private void PlayUrlInternal(VideoPlayerType playerType, VRCUrl url)
    {
      if (!InvokeBeforeEvent("BeforeUserPlayTrack"))
      {
        UpdateUI();
        return;
      }

      _controller.TakeOwnership();
      _controller.PlayTrack(TrackUtils.NewTrack(playerType, "", url));
    }

    public void Play()
    {
      if (!InvokeBeforeEvent("BeforeUserPlayTrack"))
      {
        UpdateUI();
        return;
      }

      _controller.TakeOwnership();
      _controller.Play();
    }

    public void Pause()
    {
      if (!InvokeBeforeEvent("BeforeUserPauseVideo"))
      {
        UpdateUI();
        return;
      }

      _controller.TakeOwnership();
      _controller.Pause();
    }

    public void Stop()
    {
      if (!InvokeBeforeEvent("BeforeUserStopVideo"))
      {
        UpdateUI();
        return;
      }

      _controller.TakeOwnership();
      _controller.Stop();
    }

    public void BeginProgressDrag() => _progressDrag = true;

    public void SetTime()
    {
      _progressDrag = false;
      if (!_progressSlider || _controller.Stopped) return;
      if (!InvokeBeforeEvent("BeforeUserSetTime"))
      {
        UpdateUI();
        return;
      }

      _controller.TakeOwnership();
      _controller.SetTime(_controller.Duration * _progressSlider.value);
    }

    public void SetTimeByHelper()
    {
      if (!_progressSliderHelper) return;
      if (!InvokeBeforeEvent("BeforeUserSetTime"))
      {
        UpdateUI();
        return;
      }

      _controller.TakeOwnership();
      _controller.SetTime(_controller.Duration * _progressSliderHelper.Percent);
    }

    public void Loop()
    {
      if (!InvokeBeforeEvent("BeforeUserChangeLoop"))
      {
        UpdateUI();
        return;
      }

      _controller.TakeOwnership();
      _controller.Loop = true;
    }

    public void LoopOff()
    {
      if (!InvokeBeforeEvent("BeforeUserChangeLoop"))
      {
        UpdateUI();
        return;
      }

      _controller.TakeOwnership();
      _controller.Loop = false;
    }

    public void Reload()
    {
      if (!InvokeBeforeEvent("BeforeUserReloadVideo"))
      {
        UpdateUI();
        return;
      }

      _controller.Reload();
    }

    public void Repeat()
    {
      if (!Utilities.IsValid(_repeatOnToggle) || !_repeatOnToggle.isOn) return;
      SetRepeat(true);
    }

    public void RepeatOff()
    {
      if (!Utilities.IsValid(_repeatOffToggle) || !_repeatOffToggle.isOn) return;
      SetRepeat(false);
    }

    public void SetRepeat(bool on)
    {
      if (!InvokeBeforeEvent("BeforeUserChangeRepeat"))
      {
        UpdateUI();
        return;
      }

      if (!Utilities.IsValid(_repeatRangeSlider) || !_repeatRangeSlider.IsReady) return;

      var repeat = RepeatUtils.NewRepeatStatus(_controller.Repeat);
      if (!on)
      {
        _controller.TakeOwnership();
        RepeatUtils.TurnOff(repeat);
        _controller.Repeat = RepeatUtils.GetPackedData(repeat);
        return;
      }

      var startTime = Mathf.Clamp(_controller.Duration * _repeatRangeSlider.LeftValue, 0f, _controller.Duration);
      var endTime = Mathf.Clamp(_controller.Duration * _repeatRangeSlider.RightValue, 0f, _controller.Duration);

      repeat = RepeatUtils.NewRepeatStatus(true, startTime, endTime);
      _controller.TakeOwnership();
      _controller.Repeat = RepeatUtils.GetPackedData(repeat);
    }

    public void SetRepeatStart()
    {
      if (!Utilities.IsValid(_repeatRangeSlider) || !_repeatRangeSlider.IsReady || _controller.Stopped || _controller.IsLive) return;

      if (RepeatUtils.IsOn(_controller.Repeat))
      {
        _repeatRangeSlider.SetLeftValueWithoutNotify(RepeatUtils.GetStartTime(_controller.Repeat) / _controller.Duration);
        return;
      }

      if (Utilities.IsValid(_repeatStartTimeText))
      {
        var value = Mathf.Clamp(_controller.Duration * _repeatRangeSlider.LeftValue, 0f, _controller.Duration);
        _repeatStartTimeText.text = $"{GetTranslation("label.start")}(A): {TimeSpan.FromSeconds(value).ToString(_controller.TimeFormat)}";
      }
    }

    public void SetRepeatEnd()
    {
      if (!Utilities.IsValid(_repeatRangeSlider) || !_repeatRangeSlider.IsReady || _controller.Stopped || _controller.IsLive) return;

      if (RepeatUtils.IsOn(_controller.Repeat))
      {
        _repeatRangeSlider.SetRightValueWithoutNotify(RepeatUtils.GetEndTime(_controller.Repeat) / _controller.Duration);
        return;
      }

      if (Utilities.IsValid(_repeatEndTimeText))
      {
        var value = Mathf.Clamp(_controller.Duration * _repeatRangeSlider.RightValue, 0f, _controller.Duration);
        _repeatEndTimeText.text = $"{GetTranslation("label.end")}(B): {TimeSpan.FromSeconds(value).ToString(_controller.TimeFormat)}";
      }
    }

    public void SetShuffle()
    {
      if (!InvokeBeforeEvent("BeforeUserChangeShufflePlay"))
      {
        UpdateUI();
        return;
      }

      _controller.TakeOwnership();
      _controller.ShufflePlay = true;
    }

    public void SetShuffleOff()
    {
      if (!InvokeBeforeEvent("BeforeUserChangeShufflePlay"))
      {
        UpdateUI();
        return;
      }

      _controller.TakeOwnership();
      _controller.ShufflePlay = false;
    }

    public void Backward()
    {
      if (!InvokeBeforeEvent("BeforeUserBackward"))
      {
        UpdateUI();
        return;
      }

      _controller.TakeOwnership();
      _controller.Backward();
    }

    public void Forward()
    {
      if (!InvokeBeforeEvent("BeforeUserForward"))
      {
        UpdateUI();
        return;
      }

      _controller.TakeOwnership();
      _controller.Forward();
    }

    public void SetSpeed()
    {
      if (!Utilities.IsValid(_speedSlider)) return;
      if (_controller.SyncedState != PlayerState.Idle && (_controller.Stopped || _controller.IsLoading))
      {
        UpdateUI();
        return;
      }
      if (!InvokeBeforeEvent("BeforeUserChangeSpeed"))
      {
        UpdateUI();
        return;
      }

      _controller.TakeOwnership();
      _controller.Speed = _speedSlider.value / 20f;
    }

    public void Mute()
    {
      if (!InvokeBeforeEvent("BeforeUserChangeMute"))
      {
        UpdateUI();
        return;
      }
      _controller.Mute = true;
    }

    public void Unmute()
    {
      if (!InvokeBeforeEvent("BeforeUserChangeMute"))
      {
        UpdateUI();
        return;
      }
      _controller.Mute = false;
    }

    public void SetVolume()
    {
      if (!Utilities.IsValid(_volumeSlider)) return;
      if (!InvokeBeforeEvent("BeforeUserChangeVolume"))
      {
        UpdateUI();
        return;
      }

      _controller.Volume = _volumeSlider.value;
    }

    public void SetVolumeByHelper()
    {
      if (!Utilities.IsValid(_volumeSliderHelper)) return;
      if (!InvokeBeforeEvent("BeforeUserChangeVolume"))
      {
        UpdateUI();
        return;
      }

      _controller.Volume = _volumeSliderHelper.Percent;
    }

    public void Subtract50ms() => SetLocalDelay(_controller.LocalDelay - 0.05f);
    public void Subtract100ms() => SetLocalDelay(_controller.LocalDelay - 0.1f);
    public void Add50ms() => SetLocalDelay(_controller.LocalDelay + 0.05f);
    public void Add100ms() => SetLocalDelay(_controller.LocalDelay + 0.1f);

    private void SetLocalDelay(float value)
    {
      if (!InvokeBeforeEvent("BeforeUserChangeLocalDelay"))
      {
        UpdateUI();
        return;
      }
      _controller.LocalDelay = value;
    }

    public void SetBrightness()
    {
      if (!Utilities.IsValid(_brightnessSlider)) return;
      if (!InvokeBeforeEvent("BeforeUserChangeBrightness"))
      {
        UpdateUI();
        return;
      }

      _controller.Brightness = _brightnessSlider.value;
    }

    public void SetMirrorFlipOn()
    {
      if (!Utilities.IsValid(_mirrorFlipOnToggle) || !_mirrorFlipOnToggle.isOn) return;
      if (!InvokeBeforeEvent("BeforeUserChangeMirrorFlip"))
      {
        UpdateUI();
        return;
      }

      _controller.MirrorFlip = true;
    }
    public void SetMirrorFlipOff()
    {
      if (!Utilities.IsValid(_mirrorFlipOffToggle) || !_mirrorFlipOffToggle.isOn) return;
      if (!InvokeBeforeEvent("BeforeUserChangeMirrorFlip"))
      {
        UpdateUI();
        return;
      }

      _controller.MirrorFlip = false;
    }

    public void SetMaxResolution360() => SetMaxResolution(360);
    public void SetMaxResolution480() => SetMaxResolution(480);
    public void SetMaxResolution720() => SetMaxResolution(720);
    public void SetMaxResolution1080() => SetMaxResolution(1080);
    public void SetMaxResolution2160() => SetMaxResolution(2160);
    public void SetMaxResolution4320() => SetMaxResolution(4320);
    private void SetMaxResolution(int value)
    {
      if (!InvokeBeforeEvent("BeforeUserChangeMaxResolution"))
      {
        UpdateUI();
        return;
      }
      _controller.MaxResolution = value;
    }

    public void UpdateUI()
    {
      UpdatePlayerSelector();
      UpdateProgressView();
      UpdatePlaybackView();
      UpdateScreenView();
      UpdateTrackView();
      UpdateLoadingView();
      UpdateAudioView();
      if (Utilities.IsValid(_idleScreenImage) && Utilities.IsValid(_idleScreenSprite))
      {
        _idleScreenImage.gameObject.SetActive(_controller.Stopped || _controller.IsLoading);
      }
    }

    private void UpdateProgressView()
    {
      if (Utilities.IsValid(_currentTimeText))
      {
        _currentTimeText.text = _controller.FormatedVideoTime;
      }

      if (Utilities.IsValid(_totalDurationText))
      {
        _totalDurationText.text = _controller.Handler.Type == VideoPlayerType.ImageViewer ? "Image" : _controller.IsLive ? "Live" : _controller.FormatedDuration;
      }

      if (Utilities.IsValid(_progressSlider) && !_progressDrag)
      {
        var progressValue = _controller.IsLive ? 1f : Mathf.Clamp(_controller.Duration == 0f ? 0f : _controller.VideoTime / _controller.Duration, 0f, 1f);
        _progressSlider.SetValueWithoutNotify(progressValue);
      }

      UpdateTooltip();
    }

    private void UpdateTooltip()
    {
      if (!Utilities.IsValid(_progressSliderHelper) || !Utilities.IsValid(_progressTooltipText)) return;

      var showTooltip = !_controller.Stopped && !_controller.IsLive && _controller.Handler.Type != VideoPlayerType.ImageViewer;
      _progressSliderHelper.gameObject.SetActive(showTooltip);

      if (!showTooltip) return;

      var seekSeconds = _controller.Duration * _progressSliderHelper.Percent;
      var tooltipText = float.IsInfinity(seekSeconds) || float.IsNaN(seekSeconds) ? "Live" : TimeSpan.FromSeconds(seekSeconds).ToString(_controller.TimeFormat);

      _progressTooltipText.text = tooltipText;
    }

    private void UpdatePlaybackView()
    {
      var repeat = RepeatUtils.NewRepeatStatus(_controller.Repeat);
      if (Utilities.IsValid(_playButton)) _playButton.gameObject.SetActive(!_controller.IsPlaying);
      if (Utilities.IsValid(_pauseButton)) _pauseButton.gameObject.SetActive(_controller.IsPlaying);
      if (Utilities.IsValid(_loopOnButton)) _loopOnButton.gameObject.SetActive(!_controller.Loop);
      if (Utilities.IsValid(_loopOffButton)) _loopOffButton.gameObject.SetActive(_controller.Loop);
      if (Utilities.IsValid(_speedSlider)) _speedSlider.SetValueWithoutNotify((float)Math.Round(_controller.Speed * 20));
      if (Utilities.IsValid(_speedValueText)) _speedValueText.text = $"{_controller.Speed:F2}x";
      if (Utilities.IsValid(_repeatOffToggle)) _repeatOffToggle.SetIsOnWithoutNotify(!RepeatUtils.IsOn(_controller.Repeat));
      if (Utilities.IsValid(_repeatOnToggle)) _repeatOnToggle.SetIsOnWithoutNotify(RepeatUtils.IsOn(_controller.Repeat));
      if (Utilities.IsValid(_repeatRangeSlider) && Utilities.IsValid(_repeatStartTimeText) && Utilities.IsValid(_repeatEndTimeText))
      {
        if (!_controller.IsPlaying || RepeatUtils.IsOn(_controller.Repeat))
        {
          string notSetText = GetTranslation("label.notSet");
          string startText, endText;
          if (!_controller.IsPlaying)
          {
            startText = notSetText;
            endText = notSetText;
          }
          else
          {
            startText = RepeatUtils.GetStartTime(_controller.Repeat) == 0 ? notSetText : TimeSpan.FromSeconds(RepeatUtils.GetStartTime(_controller.Repeat)).ToString(_controller.TimeFormat);
            endText = RepeatUtils.GetEndTime(_controller.Repeat) >= _controller.Duration || _controller.IsLive ? notSetText : TimeSpan.FromSeconds(RepeatUtils.GetEndTime(_controller.Repeat)).ToString(_controller.TimeFormat);
          }
          _repeatStartTimeText.text = $"{GetTranslation("label.start")}(A): {startText}";
          _repeatEndTimeText.text = $"{GetTranslation("label.end")}(B): {endText}";

          _repeatRangeSlider.SetLeftValueWithoutNotify(_controller.IsLive || !_controller.IsPlaying ? 0f : Mathf.Clamp(RepeatUtils.GetStartTime(_controller.Repeat) / _controller.Duration, 0f, 1f));
          _repeatRangeSlider.SetRightValueWithoutNotify(_controller.IsLive || !_controller.IsPlaying ? 1f : Mathf.Clamp(RepeatUtils.GetEndTime(_controller.Repeat) / _controller.Duration, 0f, 1f));
        }
      }
      if (Utilities.IsValid(_localDelayValueText)) _localDelayValueText.text = (Mathf.Round(_controller.LocalDelay * 100) / 100).ToString();
      if (Utilities.IsValid(_shufflePlayButton)) _shufflePlayButton.gameObject.SetActive(!_controller.ShufflePlay);
      if (Utilities.IsValid(_shufflePlayOffButton)) _shufflePlayOffButton.gameObject.SetActive(_controller.ShufflePlay);
    }

    private void UpdateTrackView()
    {
      var track = _controller.Track;
      var title = TrackUtils.GetTitle(track);
      var url = TrackUtils.GetUrl(track).Get();
      if (Utilities.IsValid(_trackTitleText)) _trackTitleText.text = !string.IsNullOrEmpty(title) ? title : url;
      if (Utilities.IsValid(_trackUrlText)) _trackUrlText.text = !string.IsNullOrEmpty(title) ? url : string.Empty;
      UpdateUrlHintView(string.IsNullOrEmpty(title) && string.IsNullOrEmpty(url));
    }

    private void UpdateUrlHintView(bool trackInfoEmpty)
    {
      if (!Utilities.IsValid(_urlHintText)) return;
      string hint = string.Empty;
      if (trackInfoEmpty)
      {
        // The branding always shows while idle, matching the version string
        // in the info panel so it never goes stale. An interceptor module
        // appends what its URL input accepts; a missing key resolves to an
        // empty string, so an unwired module or a stale key leaves the
        // branding alone instead of adding a blank tail.
        hint = $"KawaPlayer v{_controller.Version}";
        if (!string.IsNullOrEmpty(_urlHintKey))
        {
          string accepts = GetTranslation(_urlHintKey);
          if (!string.IsNullOrEmpty(accepts)) hint = $"{hint} / {accepts}";
        }
      }
      _urlHintText.text = hint;
      _urlHintText.gameObject.SetActive(!string.IsNullOrEmpty(hint));
    }

    private void UpdateAudioView()
    {
      if (Utilities.IsValid(_muteButton)) _muteButton.gameObject.SetActive(!_controller.Mute);
      if (Utilities.IsValid(_unmuteButton)) _unmuteButton.gameObject.SetActive(_controller.Mute);
      if (Utilities.IsValid(_volumeSlider)) _volumeSlider.SetValueWithoutNotify(_controller.Volume);
    }

    private void UpdateScreenView()
    {
      if (Utilities.IsValid(_mirrorFlipOnToggle)) _mirrorFlipOnToggle.SetIsOnWithoutNotify(_controller.MirrorFlip);
      if (Utilities.IsValid(_mirrorFlipOffToggle)) _mirrorFlipOffToggle.SetIsOnWithoutNotify(!_controller.MirrorFlip);
      if (Utilities.IsValid(_maxResolution360Toggle)) _maxResolution360Toggle.SetIsOnWithoutNotify(_controller.MaxResolution == 360);
      if (Utilities.IsValid(_maxResolution480Toggle)) _maxResolution480Toggle.SetIsOnWithoutNotify(_controller.MaxResolution == 480);
      if (Utilities.IsValid(_maxResolution720Toggle)) _maxResolution720Toggle.SetIsOnWithoutNotify(_controller.MaxResolution == 720);
      if (Utilities.IsValid(_maxResolution1080Toggle)) _maxResolution1080Toggle.SetIsOnWithoutNotify(_controller.MaxResolution == 1080);
      if (Utilities.IsValid(_maxResolution2160Toggle)) _maxResolution2160Toggle.SetIsOnWithoutNotify(_controller.MaxResolution == 2160);
      if (Utilities.IsValid(_maxResolution4320Toggle)) _maxResolution4320Toggle.SetIsOnWithoutNotify(_controller.MaxResolution == 4320);
      if (Utilities.IsValid(_brightnessSlider)) _brightnessSlider.SetValueWithoutNotify(_controller.Brightness);
      if (Utilities.IsValid(_brightnessValueText)) _brightnessValueText.text = $"{Mathf.Ceil(_controller.Brightness * 100)}%";
    }

    private void UpdateErrorView(VideoError videoError)
    {
      if (Utilities.IsValid(_loadingIndicator)) _loadingIndicator.SetActive(true);
      if (Utilities.IsValid(_userUIAnimator)) _userUIAnimator.SetBool("Loading", false);
      if (!_statusMessageText) return;
      string message;
      switch (videoError)
      {
        case VideoError.Unknown:
          message = GetTranslation("error.unknown");
          break;
        case VideoError.InvalidURL:
          message = GetTranslation("error.invalidUrl");
          break;
        case VideoError.AccessDenied:
          message = GetTranslation("error.accessDenied");
          break;
        case VideoError.RateLimited:
          message = GetTranslation("error.rateLimited");
          break;
        case VideoError.PlayerError:
          message = GetTranslation("error.playerError");
          break;
        default:
          return;
      }
      if (ShouldShowYouTubeHint(videoError))
      {
        // InvalidURL gets its own wording: a YouTube URL that passed the host
        // check is syntactically valid, so the failure is in resolution (video
        // missing/private, or a yt-dlp failure - the editor resolver maps
        // empty yt-dlp output to InvalidURL). Retrying is not suggested there
        // because the video may simply not exist.
        string hintKey = videoError == VideoError.InvalidURL ? "error.youtubeHintInvalidUrl" : "error.youtubeHint";
        string hint = GetTranslation(hintKey);
        if (!string.IsNullOrEmpty(hint)) message = $"{message}\n{hint}";
      }
      _statusMessageText.text = message;
    }

    private bool ShouldShowYouTubeHint(VideoError videoError)
    {
      // Unknown, InvalidURL and PlayerError: YouTube playback failures
      // observed on the real client arrived as PlayerError (issue #80), while
      // the editor resolver maps them to InvalidURL. PlayerError alone cannot
      // tell where the failure happened (resolver, YouTube, AVPro, network),
      // but the hint only states the observed result and a retry suggestion,
      // so it stays accurate. AccessDenied already carries an actionable
      // base message (Allow Untrusted URLs) and RateLimited is VRChat's 5s
      // limit, so those get no hint. Detection is additive: direct YouTube
      // URLs match by host, and PlaylistLoader tracks (VHub redirect URLs)
      // match by the provider carried in the track extension when the server
      // supplies one (issue #72).
      switch (videoError)
      {
        case VideoError.Unknown:
        case VideoError.InvalidURL:
        case VideoError.PlayerError:
          var track = _controller.Track;
          return UrlUtils.IsYouTubeUrl(TrackUtils.GetUrl(track).Get())
              || TrackProviderUtils.IsYouTube(track);
        default:
          return false;
      }
    }

    private void UpdateLoadingView()
    {
      if (Utilities.IsValid(_loadingIndicator)) _loadingIndicator.SetActive(_controller.IsLoading);
      if (Utilities.IsValid(_userUIAnimator)) _userUIAnimator.SetBool("Loading", _controller.IsLoading);
      if (Utilities.IsValid(_statusMessageText)) _statusMessageText.text = GetTranslation("msg.videoLoading");
    }

    private void GenerateVersionView()
    {
      if (Utilities.IsValid(_versionDisplayText)) _versionDisplayText.text = $"KawaPlayer v{_controller.Version}";
      if (Utilities.IsValid(_updateLogText) && Utilities.IsValid(_updateLogTextAsset)) _updateLogText.text = _updateLogTextAsset.text;
    }

    private void UpdateIdleScreenView()
    {
      if (Utilities.IsValid(_idleScreenImage) && Utilities.IsValid(_idleScreenSprite)) _idleScreenImage.sprite = _idleScreenSprite;
    }

    public void UpdatePlayerSelector()
    {
      if (Utilities.IsValid(_unityPlayerToggle)) _unityPlayerToggle.SetIsOnWithoutNotify(_controller.Handler.Type == VideoPlayerType.UnityVideoPlayer);
      if (Utilities.IsValid(_avProPlayerToggle)) _avProPlayerToggle.SetIsOnWithoutNotify(_controller.Handler.Type == VideoPlayerType.AVProVideoPlayer);
      if (Utilities.IsValid(_imageViewerToggle)) _imageViewerToggle.SetIsOnWithoutNotify(_controller.Handler.Type == VideoPlayerType.ImageViewer);
    }

    #region Event Handlers

    public override void AfterVideoReady() => UpdateUI();
    public override void AfterVideoStarted() => UpdateUI();
    public override void AfterVideoEnded() => UpdateUI();
    public override void AfterVideoPlayed() => UpdateUI();
    public override void AfterVideoPaused() => UpdateUI();
    public override void AfterVideoStopped()
    {
      UpdateUI();
      GeneratePlaylistTracks();
    }
    public override void AfterVideoErrorOccurred(VideoError videoError) => UpdateErrorView(videoError);
    public override void AfterPlayerHandlerChanged(VideoPlayerType playerType) => UpdateUI();
    public override void AfterTimeChanged(float time) => UpdateProgressView();
    public override void AfterLoopChanged(bool loop) => UpdatePlaybackView();
    public override void AfterRepeatChanged(ulong repeat) => UpdatePlaybackView();
    public override void AfterSpeedChanged(float speed) => UpdatePlaybackView();
    public override void AfterLocalDelayChanged(float localDelay) => UpdatePlaybackView();
    public override void AfterShufflePlayChanged(bool shufflePlay) => UpdatePlaybackView();
    public override void AfterTrackLoaded()
    {
      UpdateUI();
      GeneratePlaylistTracks();
    }
    public override void AfterTrackUpdated() => UpdateTrackView();
    public override void AfterVideoRetry() => UpdateLoadingView();
    public override void AfterQueueUpdated()
    {
      if (Utilities.IsValid(_queueTabToggle) && _queueTabToggle.isOn) GeneratePlaylistTracks();
    }
    public override void AfterHistoryUpdated()
    {
      if (Utilities.IsValid(_historyTabToggle) && _historyTabToggle.isOn) GeneratePlaylistTracks();
    }
    public override void AfterPlaylistsUpdated()
    {
      // A dynamic slot going from empty to filled (issue #88) changes both
      // the playlist list and, when that slot is the one on screen, the
      // track list under it. Going the other way drops the selection.
      ClearSelectionIfEmptied();
      GeneratePlaylistView();
      GeneratePlaylistTracks();
    }
    public override void AfterVolumeChanged(float volume) => UpdateAudioView();
    public override void AfterMuteChanged(bool mute) => UpdateAudioView();
    public override void AfterMaxResolutionChanged(int maxResolution) => UpdateScreenView();
    public override void AfterMirrorFlipChanged(bool mirrorFlip) => UpdateScreenView();
    public override void AfterBrightnessChanged(float brightness) => UpdateScreenView();

    #endregion
  }
}
