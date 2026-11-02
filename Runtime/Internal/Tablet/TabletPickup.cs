using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace Yamadev.YamaStream.Tablet
{
  // The tablet's body: holding it, and putting it back where the world
  // placed it (issue #108, D1). It shares its object with the VRCPickup and
  // the optional VRCObjectSync, so it syncs nothing itself. The screen keeps
  // its own state on TabletScreen, on another object, so that pressing a
  // button on someone's tablet takes over the screen, never the tablet in
  // their hand.
  //
  // The tablet is grabbed by one collider covering the whole body, in VR and
  // on desktop alike. It sits just behind the screen, so a click or a laser
  // on the screen reaches the screen first, and it belongs on this object,
  // beside the VRCPickup: VRChat looks for the pickup on the object whose
  // collider was hit, never on its parents. Keep it the only one. VRChat
  // places a VR hand's hold by the object's first collider, not by the one
  // the hand touched: with a collider for each side edge, the left one
  // first, a right hand taking the right edge ended up holding the left edge
  // (issue #167, confirmed in VR by swapping the two). The VRCPickup's
  // proximity is 0.03 m, so a VR hand takes the tablet only where it touches
  // it, and pointing at the screen from away lifts nothing (issues #150,
  // #157).
  //
  // Its holder can resize it (issue #342): with the trigger held (the
  // pickup's Use, which reaches only the holder), the holding hand's stick
  // up or down, or on desktop the mouse wheel with the left button held.
  // TabletSize keeps the size. The holding hand stays where it is, and the
  // player does not walk meanwhile, as the left stick walks.
  [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
  public class TabletPickup : YamaPlayerBehaviour
  {
    [SerializeField] private TabletScreen _screen;

    // Seconds the tablet may lie untouched before it goes back. 0 never
    // returns it. Falling out of the world is left to the Respawn Height.
    [SerializeField, Min(0f)] private float _idleReturnSeconds = 0f;

    // The tablet's size, on its own object under this one.
    [SerializeField] private TabletSize _size;

    // A stick pushed all the way doubles the size in this many seconds.
    private const float DoublingSeconds = 1f;
    // A stick nearer the middle than this does nothing.
    private const float StickDeadZone = 0.15f;
    // Each notch of the mouse wheel (0.1 of its axis) changes the size by
    // a tenth: log2(1.1) doublings.
    private const float WheelNotch = 0.1f;
    private const float WheelNotchDoublings = 0.1375035f;

    private VRCPickup _pickup;
    private VRCObjectSync _objectSync;
    private Vector3 _spawnPosition;
    private Quaternion _spawnRotation;
    private float _lastTouchTime;
    private bool _resizing;
    private bool _resizeLoopRunning;
    private Vector3 _resizePoint;
    private float _lookVertical;
    private float _moveVertical;

    private void Start()
    {
      _pickup = GetComponent<VRCPickup>();
      _objectSync = GetComponent<VRCObjectSync>();
      _spawnPosition = transform.position;
      _spawnRotation = transform.rotation;
      _lastTouchTime = Time.time;

      if (_idleReturnSeconds > 0f) SendCustomEventDelayedSeconds(nameof(_CheckIdle), _idleReturnSeconds);
    }

    private bool IsHeld => Utilities.IsValid(_pickup) && _pickup.IsHeld;

    public string GetTranslation(string key) => Utilities.IsValid(_screen) ? _screen.GetTranslation(key) : string.Empty;

    public Font CurrentFont => Utilities.IsValid(_screen) ? _screen.CurrentFont : null;

    public override void OnPickup() => Touch();

    public override void OnDrop()
    {
      StopResizing();
      Touch();
    }

    public override void OnPickupUseDown()
    {
      if (!Utilities.IsValid(_size) || !IsLocalPlayerValid || _resizing) return;
      _resizing = true;
      _resizePoint = HoldingPoint();
      LocalPlayer.Immobilize(true);
      if (_resizeLoopRunning) return;
      _resizeLoopRunning = true;
      SendCustomEventDelayedFrames(nameof(_ResizeFrame), 1);
    }

    public override void OnPickupUseUp() => StopResizing();

    // The right stick (look) and the left one (move), up and down.
    public override void InputLookVertical(float value, UdonInputEventArgs args) => _lookVertical = value;

    public override void InputMoveVertical(float value, UdonInputEventArgs args) => _moveVertical = value;

    // Runs each frame while resizing, and stops itself once it is over.
    public void _ResizeFrame()
    {
      if (!_resizing)
      {
        _resizeLoopRunning = false;
        return;
      }
      float doublings;
      if (IsInVR)
      {
        float stick = _pickup.currentHand == VRC_Pickup.PickupHand.Left ? _moveVertical : _lookVertical;
        doublings = Mathf.Abs(stick) < StickDeadZone ? 0f : stick * Time.deltaTime / DoublingSeconds;
      }
      else
      {
        doublings = Input.GetAxis("Mouse ScrollWheel") / WheelNotch * WheelNotchDoublings;
      }
      if (doublings != 0f)
      {
        _size.ResizeAbout(_size.Scale * Mathf.Pow(2f, doublings), _resizePoint, false);
        Touch();
      }
      SendCustomEventDelayedFrames(nameof(_ResizeFrame), 1);
    }

    private void StopResizing()
    {
      if (!_resizing) return;
      _resizing = false;
      if (IsLocalPlayerValid) LocalPlayer.Immobilize(false);
      // Sends the size it ended at.
      _size.ResizeAbout(_size.Scale, _resizePoint, true);
    }

    // The point that stays where it is while the tablet grows, in this
    // object's space: in VR the nearest point of the tablet to the holding
    // hand, so the edge in the hand stays in it; on desktop, where the tablet
    // is held before the eyes, its middle.
    private Vector3 HoldingPoint()
    {
      if (!IsInVR) return _size.Center;
      VRCPlayerApi.TrackingDataType hand = _pickup.currentHand == VRC_Pickup.PickupHand.Left
        ? VRCPlayerApi.TrackingDataType.LeftHand
        : VRCPlayerApi.TrackingDataType.RightHand;
      return _size.NearestPoint(transform.InverseTransformPoint(LocalPlayer.GetTrackingData(hand).position));
    }

    // Called on everyone when the screen changes, so a tablet in use does
    // not wander off however long it has been since anyone held it.
    public void Touch() => _lastTouchTime = Time.time;

    // A tablet in someone's hand stays there: this is for one left behind.
    public void ReturnToSpawn()
    {
      if (IsHeld) return;
      Touch();
      if (Utilities.IsValid(_size)) _size.ResetSize();
      if (!IsAwayFromSpawn()) return;

      // Without global sync, every player has their own tablet and only
      // their own one comes back.
      if (!Utilities.IsValid(_objectSync))
      {
        transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
        return;
      }
      TakeOwnership();
      _objectSync.Respawn();
    }

    // Every player keeps this timer, so it carries on when the owner leaves;
    // only the owner moves a synced tablet. It waits out exactly the time
    // left, so it runs about once per idle period instead of polling.
    public void _CheckIdle()
    {
      float remaining = _idleReturnSeconds - (Time.time - _lastTouchTime);
      if (remaining > 0f)
      {
        SendCustomEventDelayedSeconds(nameof(_CheckIdle), remaining);
        return;
      }
      if (!Utilities.IsValid(_objectSync) || IsObjectOwner) ReturnToSpawn();
      SendCustomEventDelayedSeconds(nameof(_CheckIdle), _idleReturnSeconds);
    }

    private bool IsAwayFromSpawn()
    {
      return (transform.position - _spawnPosition).sqrMagnitude > 0.0001f
        || Quaternion.Angle(transform.rotation, _spawnRotation) > 1f;
    }
  }
}
