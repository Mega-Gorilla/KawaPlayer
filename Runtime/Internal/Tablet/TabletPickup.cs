using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

namespace Yamadev.YamaStream.Tablet
{
  // The tablet's body: holding it, and putting it back where the world
  // placed it (issue #108, D1). It shares its object with the VRCPickup and
  // the optional VRCObjectSync, so it syncs nothing itself. The screen keeps
  // its own state on TabletScreen, on another object, so that pressing a
  // button on someone's tablet takes over the screen, never the tablet in
  // their hand.
  [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
  public class TabletPickup : YamaPlayerBehaviour
  {
    [SerializeField] private TabletScreen _screen;

    // In VR the tablet is grabbed by its side edges only, so pointing at
    // the screen never lifts it. Desktop has no hand to aim with and grabs
    // the whole body, which sits just behind the screen so a click on the
    // screen reaches the screen first. All of them belong on this object,
    // beside the VRCPickup: VRChat looks for the pickup on the object whose
    // collider was hit, never on its parents.
    //
    // The first collider on this object is none of these: one covering the
    // whole tablet, edges included, that is never enabled. VRChat appears to
    // place a VR hand's hold by the object's first collider, not by the one
    // the hand touched: with the left edge first, a right hand taking the
    // right edge ended up holding the left edge, the tablet hanging outside
    // it (issue #167). Keep it first, and keep it disabled, or pointing at
    // the screen would lift the tablet again.
    //
    // The VRCPickup's proximity is 0.03 m, so a VR hand takes the tablet only
    // where it touches an edge (issue #157).
    [SerializeField] private Collider[] _vrGrabColliders;
    [SerializeField] private Collider[] _desktopGrabColliders;

    // Seconds the tablet may lie untouched before it goes back. 0 never
    // returns it. Falling out of the world is left to the Respawn Height.
    [SerializeField, Min(0f)] private float _idleReturnSeconds = 0f;

    private VRCPickup _pickup;
    private VRCObjectSync _objectSync;
    private Vector3 _spawnPosition;
    private Quaternion _spawnRotation;
    private float _lastTouchTime;

    private void Start()
    {
      _pickup = GetComponent<VRCPickup>();
      _objectSync = GetComponent<VRCObjectSync>();
      _spawnPosition = transform.position;
      _spawnRotation = transform.rotation;
      _lastTouchTime = Time.time;

      SetCollidersEnabled(_vrGrabColliders, IsInVR);
      SetCollidersEnabled(_desktopGrabColliders, !IsInVR);

      if (_idleReturnSeconds > 0f) SendCustomEventDelayedSeconds(nameof(_CheckIdle), _idleReturnSeconds);
    }

    private bool IsHeld => Utilities.IsValid(_pickup) && _pickup.IsHeld;

    public string GetTranslation(string key) => Utilities.IsValid(_screen) ? _screen.GetTranslation(key) : string.Empty;

    public Font CurrentFont => Utilities.IsValid(_screen) ? _screen.CurrentFont : null;

    public override void OnPickup() => Touch();

    public override void OnDrop() => Touch();

    // Called on everyone when the screen changes, so a tablet in use does
    // not wander off however long it has been since anyone held it.
    public void Touch() => _lastTouchTime = Time.time;

    // A tablet in someone's hand stays there: this is for one left behind.
    public void ReturnToSpawn()
    {
      if (IsHeld) return;
      Touch();
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

    private void SetCollidersEnabled(Collider[] colliders, bool value)
    {
      if (!Utilities.IsValid(colliders)) return;
      foreach (var collider in colliders)
      {
        if (Utilities.IsValid(collider)) collider.enabled = value;
      }
    }
  }
}
