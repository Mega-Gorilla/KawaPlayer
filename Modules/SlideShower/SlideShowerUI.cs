using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon.Common.Enums;
using Yamadev.YamaStream.UI;

namespace Yamadev.YamaStream.Modules.SlideShower
{
  [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
  public class SlideShowerUI : YamaPlayerListener
  {
    [SerializeField] private SlideShower _slideShower;
    [SerializeField, RegisterEvent(nameof(Toggle.onValueChanged), nameof(SlideModeOn))] private Toggle _slideModeOn;
    [SerializeField, RegisterEvent(nameof(Toggle.onValueChanged), nameof(SlideModeOff))] private Toggle _slideModeOff;
    [SerializeField, RegisterEvent(nameof(Toggle.onValueChanged), nameof(SetSlide1s))] private Toggle _slide1s;
    [SerializeField, RegisterEvent(nameof(Toggle.onValueChanged), nameof(SetSlide2s))] private Toggle _slide2s;
    [SerializeField, RegisterEvent(nameof(Toggle.onValueChanged), nameof(SetSlide3s))] private Toggle _slide3s;
    [SerializeField] private Text _slideModeTitleText;
    [SerializeField] private Text _slideModeDescriptionText;
    [SerializeField] private Text _slideModeOnText;
    [SerializeField] private Text _slideModeOffText;
    [SerializeField] private Text _slideSecondsTitleText;
    [SerializeField] private Text _slideSecondsDescriptionText;
    [SerializeField] private Text _slide1sText;
    [SerializeField] private Text _slide2sText;
    [SerializeField] private Text _slide3sText;
    private UIController _uiController;
    private Text _currentTimeText;
    private Text _totalDurationText;
    private Slider _progressSlider;
    private SliderHelper _progressSliderHelper;
    // What the page texts show, so they are written only when the page or the
    // count changes; the pages are shown with the video paused, so they rarely
    // do. While in slide mode this UI holds the progress of its UIController,
    // which then leaves the texts and the slider alone (issue #195).
    private bool _slideModeShown;
    private int _shownPage = int.MinValue;
    private int _shownPageCount = int.MinValue;
    // A page turn waiting for the end of the frame (see TakeSeek), and the
    // page a seek chose.
    private const int TurnNone = 0;
    private const int TurnToPage = 1;
    private const int TurnPrevious = 2;
    private const int TurnNext = 3;
    private int _pendingTurn = TurnNone;
    private int _pendingTurnPage;
    private bool _askingTurn;

    private void Start()
    {
      _uiController = GetComponentInParent<UIController>();
      if (!Utilities.IsValid(_uiController) || !Utilities.IsValid(_slideShower)) return;
      _slideShower.AddListener(this);
      _uiController.AddListener(this);

      _currentTimeText = (Text)_uiController.GetProgramVariable("_currentTimeText");
      _totalDurationText = (Text)_uiController.GetProgramVariable("_totalDurationText");
      _progressSlider = (Slider)_uiController.GetProgramVariable("_progressSlider");
      _progressSliderHelper = (SliderHelper)_uiController.GetProgramVariable("_progressSliderHelper");

      UpdateTranslation();
      UpdateDisplay();
    }


    private void LateUpdate()
    {
      if (_slideModeShown) UpdatePageDisplay();
    }

    private void UpdateDisplay()
    {
      UpdateSlideModeDisplay();
      UpdateSlideSecondsDisplay();
      UpdateProgressDisplay();
      _shownPage = int.MinValue;
      UpdatePageDisplay();
    }

    private void UpdateSlideModeDisplay()
    {
      if (!Utilities.IsValid(_slideShower)) return;

      if (Utilities.IsValid(_slideModeOn)) _slideModeOn.SetIsOnWithoutNotify(_slideShower.SlideMode);
      if (Utilities.IsValid(_slideModeOff)) _slideModeOff.SetIsOnWithoutNotify(!_slideShower.SlideMode);
    }

    private void UpdateSlideSecondsDisplay()
    {
      if (!Utilities.IsValid(_slideShower)) return;

      if (Utilities.IsValid(_slide1s)) _slide1s.SetIsOnWithoutNotify(_slideShower.SlideSeconds == 1);
      if (Utilities.IsValid(_slide2s)) _slide2s.SetIsOnWithoutNotify(_slideShower.SlideSeconds == 2);
      if (Utilities.IsValid(_slide3s)) _slide3s.SetIsOnWithoutNotify(_slideShower.SlideSeconds == 3);
    }

    private void UpdatePageDisplay()
    {
      if (!Utilities.IsValid(_slideShower) || !_slideShower.SlideMode) return;
      if (!Utilities.IsValid(_currentTimeText) || !Utilities.IsValid(_totalDurationText) || !Utilities.IsValid(_progressSlider)) return;
      int page = _slideShower.SlidePage;
      int pageCount = _slideShower.SlidePageCount;
      bool pageChanged = page != _shownPage || pageCount != _shownPageCount;
      // The slider can be off the page while the page is not: a drag let go of
      // and then cancelled, as a permission does, or a page SetSlidePage would
      // not move to. It goes back to the page, but not while the drag is still
      // waiting to be turned to.
      float sliderPage = Mathf.Clamp(page, _progressSlider.minValue, _progressSlider.maxValue);
      if (!pageChanged && (_progressSlider.value == sliderPage || _pendingTurn != TurnNone)) return;

      // Not while the slider is being dragged: that is the page being chosen.
      if ((bool)_uiController.GetProgramVariable("_progressDrag")) return;
      if (pageChanged)
      {
        _shownPage = page;
        _shownPageCount = pageCount;
        _currentTimeText.text = $"{page}";
        _totalDurationText.text = $"{pageCount}";
      }
      _progressSlider.SetValueWithoutNotify(page);
    }

    private void UpdateProgressDisplay()
    {
      if (!Utilities.IsValid(_slideShower) || !Utilities.IsValid(_progressSlider)) return;
      bool slideMode = _slideShower.SlideMode;
      _slideModeShown = slideMode;
      _progressSlider.wholeNumbers = slideMode;
      _progressSlider.minValue = slideMode && _slideShower.SlidePage > 0 ? 1 : 0;
      _progressSlider.maxValue = slideMode && _slideShower.SlidePageCount > 0 ? _slideShower.SlidePageCount : 1;
      // Out of slide mode, UIController shows the seek tooltip when it fits (not
      // for a live stream or a picture); letting go of the progress has it show
      // the time again at once.
      if (slideMode && Utilities.IsValid(_progressSliderHelper)) _progressSliderHelper.gameObject.SetActive(false);
      if (Utilities.IsValid(_uiController)) _uiController.ProgressHeld = slideMode;
    }

    private void UpdateTranslation()
    {
      if (!Utilities.IsValid(_uiController)) return;
      if (Utilities.IsValid(_slideModeTitleText)) _slideModeTitleText.text = _uiController.GetTranslation("module.slideshower.slideMode.title");
      if (Utilities.IsValid(_slideModeDescriptionText)) _slideModeDescriptionText.text = _uiController.GetTranslation("module.slideshower.slideMode.description");
      if (Utilities.IsValid(_slideModeOnText)) _slideModeOnText.text = _uiController.GetTranslation("module.slideshower.slideMode.on");
      if (Utilities.IsValid(_slideModeOffText)) _slideModeOffText.text = _uiController.GetTranslation("module.slideshower.slideMode.off");
      if (Utilities.IsValid(_slideSecondsTitleText)) _slideSecondsTitleText.text = _uiController.GetTranslation("module.slideshower.slideSeconds.title");
      if (Utilities.IsValid(_slideSecondsDescriptionText)) _slideSecondsDescriptionText.text = _uiController.GetTranslation("module.slideshower.slideSeconds.description");
      if (Utilities.IsValid(_slide1sText)) _slide1sText.text = _uiController.GetTranslation("module.slideshower.slideSeconds.1s");
      if (Utilities.IsValid(_slide2sText)) _slide2sText.text = _uiController.GetTranslation("module.slideshower.slideSeconds.2s");
      if (Utilities.IsValid(_slide3sText)) _slide3sText.text = _uiController.GetTranslation("module.slideshower.slideSeconds.3s");
    }

    // The slide mode and seconds reach everyone, so PermissionManagement may
    // refuse them, and the toggles go back (issue #211).
    private bool MayChangeSetting()
    {
      if (!Utilities.IsValid(_uiController) || _uiController.InvokeBeforeEvent("BeforeUserChangeSyncedSetting")) return true;
      UpdateSlideModeDisplay();
      UpdateSlideSecondsDisplay();
      return false;
    }

    public void SlideModeOn()
    {
      if (!Utilities.IsValid(_slideShower) || !Utilities.IsValid(_slideModeOn) || !_slideModeOn.isOn) return;
      if (!MayChangeSetting()) return;
      _slideShower.TakeOwnership();
      _slideShower.SlideMode = true;
    }

    public void SlideModeOff()
    {
      if (!Utilities.IsValid(_slideShower) || !Utilities.IsValid(_slideModeOff) || !_slideModeOff.isOn) return;
      if (!MayChangeSetting()) return;
      _slideShower.TakeOwnership();
      _slideShower.SlideMode = false;
    }

    public void SetSlideSeconds(int seconds)
    {
      if (!Utilities.IsValid(_slideShower)) return;
      if (!MayChangeSetting()) return;
      _slideShower.TakeOwnership();
      _slideShower.SlideSeconds = seconds;
    }

    public void SetSlide1s()
    {
      if (!Utilities.IsValid(_slide1s) || !_slide1s.isOn) return;
      SetSlideSeconds(1);
    }

    public void SetSlide2s()
    {
      if (!Utilities.IsValid(_slide2s) || !_slide2s.isOn) return;
      SetSlideSeconds(2);
    }

    public void SetSlide3s()
    {
      if (!Utilities.IsValid(_slide3s) || !_slide3s.isOn) return;
      SetSlideSeconds(3);
    }

    public void BeforeUserSetTime()
    {
      if (TakeSeek(TurnToPage)) _pendingTurnPage = (int)_progressSlider.value;
    }

    public void BeforeUserBackward() => TakeSeek(TurnPrevious);
    public void BeforeUserForward() => TakeSeek(TurnNext);

    // In slide mode a seek, back or forward turns the page instead, which
    // moves everyone's video, so it must pass the same checks. Cancelling the
    // seek here also keeps it from the listeners after this one, such as
    // PermissionManagement, in an order Start decides (issue #283). So the
    // turn waits for the end of the frame and asks every listener again, this
    // one letting it pass; InvokeBeforeEvent cannot be called from inside
    // itself.
    private bool TakeSeek(int turn)
    {
      if (_askingTurn || !Utilities.IsValid(_slideShower) || !_slideShower.SlideMode || !Utilities.IsValid(_uiController)) return false;
      _uiController.CancelCurrentAction();
      _pendingTurn = turn;
      SendCustomEventDelayedFrames(nameof(_TurnPage), 0, EventTiming.LateUpdate);
      return true;
    }

    public void _TurnPage()
    {
      int turn = _pendingTurn;
      _pendingTurn = TurnNone;
      if (turn == TurnNone || !Utilities.IsValid(_slideShower) || !_slideShower.SlideMode) return;

      _askingTurn = true;
      bool allowed = _uiController.InvokeBeforeEvent(turn == TurnToPage ? "BeforeUserSetTime" : turn == TurnPrevious ? "BeforeUserBackward" : "BeforeUserForward");
      _askingTurn = false;
      if (!allowed) return;

      _slideShower.TakeOwnership();
      if (turn == TurnToPage) _slideShower.SetSlidePage(_pendingTurnPage);
      else if (turn == TurnPrevious) _slideShower.PreviousSlide();
      else _slideShower.NextSlide();
    }

    public void AfterLanguageChanged() => UpdateTranslation();
    public void AfterSlideModeChanged() => UpdateDisplay();
    public void AfterSlideSecondsChanged() => UpdateDisplay();
    public override void AfterVideoReady() => SendCustomEventDelayedFrames(nameof(UpdateDisplay), 0, EventTiming.LateUpdate);
    public override void AfterVideoStarted() => SendCustomEventDelayedFrames(nameof(UpdateDisplay), 0, EventTiming.LateUpdate);
    public override void AfterVideoEnded() => SendCustomEventDelayedFrames(nameof(UpdateDisplay), 0, EventTiming.LateUpdate);
    public override void AfterVideoPlayed() => SendCustomEventDelayedFrames(nameof(UpdateDisplay), 0, EventTiming.LateUpdate);
    public override void AfterVideoPaused() => SendCustomEventDelayedFrames(nameof(UpdateDisplay), 0, EventTiming.LateUpdate);
    public override void AfterVideoStopped() => SendCustomEventDelayedFrames(nameof(UpdateDisplay), 0, EventTiming.LateUpdate);
    public override void AfterTrackLoaded() => SendCustomEventDelayedFrames(nameof(UpdateDisplay), 0, EventTiming.LateUpdate);
  }
}
