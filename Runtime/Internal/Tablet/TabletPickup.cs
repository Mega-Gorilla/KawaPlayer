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
  //
  // With a stand (issue #349), the tablet's place is on the stand: the build
  // puts the body on the stand's rest (_dock, the chosen design's), so that
  // is where it starts and goes back to. Let go near the rest, it slides
  // onto it and back to its own size, so it has somewhere to be put down.
  // Only the holder, who owns it, moves it, and VRCObjectSync, when the
  // world syncs the tablet's place, takes it to the others.
  [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
  public class TabletPickup : YamaPlayerBehaviour
  {
    [SerializeField] private TabletScreen _screen;

    // Seconds the tablet may lie untouched before it goes back. 0 never
    // returns it. Falling out of the world is left to the Respawn Height.
    [SerializeField, Min(0f)] private float _idleReturnSeconds = 0f;

    // The tablet's size, on its own object under this one.
    [SerializeField] private TabletSize _size;

    // Where the tablet rests on its stand, or none without a stand.
    [SerializeField] private Transform _dock;

    // Let go this near the rest (metres, between the tablet's middle and the
    // rest's), the tablet slides onto it, in this many seconds.
    private const float DockReach = 0.3f;
    private const float DockSeconds = 0.25f;

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
    private bool _docking;
    private bool _dockLoopRunning;
    private float _dockStartTime;
    private Vector3 _dockFromPosition;
    private Quaternion _dockFromRotation;

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

    public override void OnPickup()
    {
      _docking = false;
      Touch();
    }

    public override void OnDrop()
    {
      StopResizing();
      Touch();
      if (!IsNearDock()) return;
      if (Utilities.IsValid(_size)) _size.ResetSize();
      _dockFromPosition = transform.position;
      _dockFromRotation = transform.rotation;
      _dockStartTime = Time.time;
      _docking = true;
      if (_dockLoopRunning) return;
      _dockLoopRunning = true;
      SendCustomEventDelayedFrames(nameof(_DockFrame), 1);
    }

    // Let go by its holder, who owns it, near a stand's rest: the middle of
    // the tablet as it shows (a resize moves it off the body's origin)
    // against where its middle will be on the rest.
    private bool IsNearDock()
    {
      if (!Utilities.IsValid(_dock) || !_dock.gameObject.activeInHierarchy) return false;
      if (Utilities.IsValid(_objectSync) && !IsObjectOwner) return false;
      Vector3 middle = transform.position, rest = _dock.position;
      if (Utilities.IsValid(_size))
      {
        middle += transform.rotation * _size.Center;
        rest += _dock.rotation * _size.MadeCenter;
      }
      return (middle - rest).sqrMagnitude <= DockReach * DockReach;
    }

    // Runs each frame while the tablet slides onto the stand, and stops
    // itself once it is there, picked up again, or taken by someone else
    // (who becomes its owner, and whose pickup reaches only them).
    public void _DockFrame()
    {
      if (!_docking || (Utilities.IsValid(_objectSync) && !IsObjectOwner))
      {
        _docking = false;
        _dockLoopRunning = false;
        return;
      }
      float t = Mathf.Clamp01((Time.time - _dockStartTime) / DockSeconds);
      float eased = t * t * (3f - 2f * t);
      transform.SetPositionAndRotation(
        Vector3.Lerp(_dockFromPosition, _dock.position, eased),
        Quaternion.Slerp(_dockFromRotation, _dock.rotation, eased));
      if (t >= 1f)
      {
        _docking = false;
        _dockLoopRunning = false;
        Touch();
        return;
      }
      SendCustomEventDelayedFrames(nameof(_DockFrame), 1);
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
        _size.ResizeAbout(_size.Scale * Mathf.Pow(2f, doublings), _resizePoint);
        Touch();
      }
      SendCustomEventDelayedFrames(nameof(_ResizeFrame), 1);
    }

    private void StopResizing()
    {
      if (!_resizing) return;
      _resizing = false;
      if (IsLocalPlayerValid) LocalPlayer.Immobilize(false);
      _size.FinishResize();
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
      _docking = false;
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
