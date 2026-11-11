using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon.Common;
using static VRC.SDKBase.VRCPlayerApi;

namespace Yamadev.YamaStream.UI
{
  public partial class UIController
  {
    private const int DEFAULT_LAYER = 0; // こっちはビームが出る
    private const int UI_LAYER = 5; // こっちはビームが出ない、ロック時はこっち

    // The lock comes off with this many presses on the screen, all within
    // _unlockClickWindow seconds of the first (issue #341).
    private const int UnlockClicks = 3;

    [Header("UI Lock - Components")]
    [SerializeField] private RectTransform _lockDetectionRect;
    // Shows the presses made so far, a filled dot for each ("●●○").
    [SerializeField] private Text _unlockProgressText;
    [SerializeField] private float _unlockClickWindow = 1.5f;

    private bool _locked = false;
    private int _unlockClicks = 0;
    private float _unlockFirstClickTime = 0f;
    private bool _pointerInside = false;
    private bool _rightHand = true;
    // Whether each hand is holding a press that began on the screen while
    // its controls showed (#340).
    private bool _screenPressedRight;
    private bool _screenPressedLeft;
    private RaycastHit[] _rayHits = new RaycastHit[TrackingUtils.MAX_RAY_HITS];

    public override void PostLateUpdate()
    {
      if (!Utilities.IsValid(_lockDetectionRect) || !_locked) return;

      bool pointerInside = IsPointingAtScreen();
      if (pointerInside != _pointerInside)
      {
        if (pointerInside) _systemUIAnimator.SetTrigger("ShowLockMessage");
        else _systemUIAnimator.SetTrigger("HideLockMessage");
        _pointerInside = pointerInside;
      }

      // Pointing away, or the time for the presses running out, counts
      // them again.
      if (_unlockClicks > 0 && (!pointerInside || Time.time - _unlockFirstClickTime > _unlockClickWindow)) ResetUnlockClicks();
    }

    // A press while locked and pointing at the screen: the first starts the
    // time, and the third within it takes the lock off.
    private void CountUnlockClick()
    {
      if (!IsPointingAtScreen()) return;
      if (_unlockClicks == 0 || Time.time - _unlockFirstClickTime > _unlockClickWindow)
      {
        _unlockClicks = 0;
        _unlockFirstClickTime = Time.time;
      }
      _unlockClicks++;
      if (_unlockClicks < UnlockClicks)
      {
        string dots = "";
        for (int i = 0; i < UnlockClicks; i++) dots += i < _unlockClicks ? "●" : "○";
        _unlockProgressText.text = dots;
        return;
      }
      ResetUnlockClicks();
      UnlockUI();
      _pointerInside = false;
      _systemUIAnimator.SetTrigger("HideLockMessage");
    }

    private void ResetUnlockClicks()
    {
      _unlockClicks = 0;
      _unlockProgressText.text = "";
    }

    public void LockUI()
    {
      if (!Utilities.IsValid(_lockDetectionRect) || !Utilities.IsValid(_userUIAnimator)) return;
      if (!Utilities.IsValid(_systemUIAnimator) || !Utilities.IsValid(_unlockProgressText)) return;
      _lockDetectionRect.gameObject.layer = UI_LAYER;
      _userUIAnimator.SetTrigger("ToggleUI");
      ResetUnlockClicks();
      _locked = true;
    }

    public void UnlockUI()
    {
      if (!Utilities.IsValid(_lockDetectionRect) || !Utilities.IsValid(_userUIAnimator)) return;
      _lockDetectionRect.gameObject.layer = DEFAULT_LAYER;
      _userUIAnimator.SetTrigger("ToggleUI");
      RefreshShownControls();
      SwitchOffHiddenListPage();
      OpenListWithControls();
      TouchControls();
      _locked = false;
    }

    public override void InputUse(bool value, UdonInputEventArgs args)
    {
      _rightHand = args.handType == HandType.RIGHT;

      if (value)
      {
        if (_locked)
        {
          CountUnlockClick();
          return;
        }
        // A press on the screen keeps the controls up while it is held (a
        // drag of the seek bar or a slider) and counts their time again when
        // it is let go (#340).
        if (Utilities.IsValid(_userUIAnimator) && IsUserUIState("ToggleUI", "UIOn") && IsPointingAtScreen())
        {
          if (_rightHand) _screenPressedRight = true;
          else _screenPressedLeft = true;
          TouchControls();
        }
      }
      else
      {
        bool wasPressed = _rightHand ? _screenPressedRight : _screenPressedLeft;
        if (_rightHand) _screenPressedRight = false;
        else _screenPressedLeft = false;
        if (wasPressed) TouchShownControls();
      }
    }

    // A URL field's entry ended (entered or cancelled; its onEndEdit). Its
    // text input popup takes VRChat's input while it is open, so the press
    // that opened it may never be let go to Udon: no press on the screen is
    // held now. The controls' hiding waited while it was open, so their time
    // counts again (#340).
    private void UrlEntryEnded()
    {
      _screenPressedRight = false;
      _screenPressedLeft = false;
      TouchShownControls();
    }

    // Whether the local player points at this screen (its canvas, the lock's
    // detection rect) with the hand that last pressed, or the head on
    // desktop. The lock reads it every frame, and a press reads it to keep
    // the controls up while it is held (#340).
    private bool IsPointingAtScreen()
    {
      if (!Utilities.IsValid(_lockDetectionRect)) return false;
      var trackingDataType = IsInVR ? (_rightHand ? TrackingDataType.RightHand : TrackingDataType.LeftHand) : TrackingDataType.Head;
      Vector3 point = TrackingUtils.GetMousePosition(LocalPlayer, trackingDataType, _rayHits);
      if (point == Vector3.zero) return false;
      return _lockDetectionRect.rect.Contains(_lockDetectionRect.InverseTransformPoint(point));
    }

    public override void InputGrab(bool value, UdonInputEventArgs args)
    {
      _rightHand = args.handType == HandType.RIGHT;
    }
  }
}