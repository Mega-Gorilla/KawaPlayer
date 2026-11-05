using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

namespace Yamadev.YamaStream.Tablet
{
  // The tablet's size, from 1 to 5 times the size it was made (issue #342).
  // TabletPickup changes it while its holder keeps the trigger down; this
  // keeps it and puts it on the tablet.
  //
  // VRChat moves a held pickup by its own transform, so the body (Body) keeps
  // its scale: what grows are its children, the parts listed here, and the
  // collider the tablet is grabbed by, both scaled about a point in the
  // body's space that stays where it is (the holding hand). A point x of the
  // tablet as it was made sits at _origin + x * _scale.
  //
  // The KawaPlayer app also turns over with the tablet (TabletScreen works
  // out how far, around the middle of its screen). This is the one script
  // that writes the app's place, putting the turn first and the size after
  // it, so neither undoes the other whichever comes first.
  //
  // The size is a property of the body, as its place is: it is shared when
  // the place is, when the body has a VRCObjectSync (Global Sync in the
  // inspector, on in the prefab), and each player's own otherwise. It sits
  // on its own object, under the body, so that it syncs by itself: the
  // body's own behaviour shares its object with the VRCObjectSync and cannot
  // sync by hand, and the screen (TabletScreen) changes owner with each
  // press. The app's turn is not shared: each player works it out from the
  // tablet as they see it.
  [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
  public class TabletSize : YamaPlayerBehaviour
  {
    private const float MinScale = 1f;
    private const float MaxScale = 5f;

    // While the size changes, it is sent at most this often; the last one is
    // sent when it stops.
    private const float SendInterval = 0.2f;

    // The body's children that grow with it and do not turn: the model and
    // the screen's canvas.
    [SerializeField] private Transform[] _parts = new Transform[0];
    // The KawaPlayer app, another child of the body, which grows and turns,
    // and its screen, the middle of which it turns around.
    [SerializeField] private Transform _app;
    [SerializeField] private Transform _appScreen;
    // The collider on the body the tablet is grabbed by.
    [SerializeField] private BoxCollider _grabCollider;

    [UdonSynced] private float _syncedScale = MinScale;
    [UdonSynced] private Vector3 _syncedOrigin = Vector3.zero;

    private float _scale = MinScale;
    private Vector3 _origin = Vector3.zero;
    private Quaternion _appTurn = Quaternion.identity;
    private Vector3[] _partPositions;
    private Vector3[] _partScales;
    private Vector3 _appPosition;
    private Quaternion _appRotation;
    private Vector3 _appScale;
    private Vector3 _appCenter;
    private Vector3 _colliderCenter;
    private Vector3 _colliderSize;
    private bool _shared;
    private bool _ready;
    private float _lastSendTime = float.NegativeInfinity;

    private void Start() => Prepare();

    // Remembers the parts as they were made. A size or a turn may come in
    // before Start, so whatever comes first does this; nothing else writes
    // these parts, so they are still as they were made then.
    private void Prepare()
    {
      if (_ready) return;
      _ready = true;
      _partPositions = new Vector3[_parts.Length];
      _partScales = new Vector3[_parts.Length];
      for (int i = 0; i < _parts.Length; i++)
      {
        if (!Utilities.IsValid(_parts[i])) continue;
        _partPositions[i] = _parts[i].localPosition;
        _partScales[i] = _parts[i].localScale;
      }
      if (Utilities.IsValid(_app))
      {
        _appPosition = _app.localPosition;
        _appRotation = _app.localRotation;
        _appScale = _app.localScale;
        _appCenter = Utilities.IsValid(_appScreen) && Utilities.IsValid(_app.parent)
          ? _app.parent.InverseTransformPoint(_appScreen.position)
          : _appPosition;
      }
      if (Utilities.IsValid(_grabCollider))
      {
        _colliderCenter = _grabCollider.center;
        _colliderSize = _grabCollider.size;
      }
      Transform body = transform.parent;
      _shared = Utilities.IsValid(body) && Utilities.IsValid(body.GetComponent<VRCObjectSync>());
    }

    public float Scale => _scale;

    // The middle of the tablet, in the body's space.
    public Vector3 Center
    {
      get
      {
        Prepare();
        return _origin + _colliderCenter * _scale;
      }
    }

    // The middle of the tablet at the size it was made, in the body's space.
    public Vector3 MadeCenter
    {
      get
      {
        Prepare();
        return _colliderCenter;
      }
    }

    // The nearest point of the tablet, in the body's space, to a point there.
    public Vector3 NearestPoint(Vector3 point)
    {
      Prepare();
      Vector3 half = _colliderSize * (_scale * 0.5f);
      Vector3 center = Center;
      return new Vector3(
        Mathf.Clamp(point.x, center.x - half.x, center.x + half.x),
        Mathf.Clamp(point.y, center.y - half.y, center.y + half.y),
        Mathf.Clamp(point.z, center.z - half.z, center.z + half.z));
    }

    // Sets the size, keeping a point given in the body's space where it is.
    // It is sent at most every SendInterval; FinishResize sends the last. At
    // a limit the size stays, and nothing is placed or sent again.
    public void ResizeAbout(float scale, Vector3 fixedPoint)
    {
      Prepare();
      scale = Mathf.Clamp(scale, MinScale, MaxScale);
      if (scale == _scale) return;
      Vector3 madePoint = (fixedPoint - _origin) / _scale;
      _origin = fixedPoint - madePoint * scale;
      _scale = scale;
      Apply();
      Send(false);
    }

    // Sends the size it ended at, once the resizing stops.
    public void FinishResize()
    {
      Prepare();
      Send(true);
    }

    // Back to the size it was made, as when the tablet goes back to its place.
    public void ResetSize()
    {
      Prepare();
      if (_scale == MinScale && _origin == Vector3.zero) return;
      _scale = MinScale;
      _origin = Vector3.zero;
      Apply();
      Send(true);
    }

    // Turns the KawaPlayer app by degrees around an axis given in the
    // world's space (TabletScreen's forward), around the middle of its
    // screen as it was made.
    public void TurnApp(float degrees, Vector3 axis)
    {
      Prepare();
      Transform body = transform.parent;
      if (Utilities.IsValid(body)) axis = body.InverseTransformDirection(axis);
      _appTurn = Quaternion.AngleAxis(degrees, axis);
      Apply();
    }

    private void Send(bool now)
    {
      if (!_shared || !IsLocalPlayerValid) return;
      if (!now && Time.time - _lastSendTime < SendInterval) return;
      _lastSendTime = Time.time;
      _syncedScale = _scale;
      _syncedOrigin = _origin;
      SyncVariables();
    }

    public override void OnDeserialization()
    {
      Prepare();
      if (!_shared) return;
      _scale = Mathf.Clamp(_syncedScale, MinScale, MaxScale);
      _origin = _syncedOrigin;
      Apply();
    }

    private void Apply()
    {
      for (int i = 0; i < _parts.Length; i++)
      {
        Transform part = _parts[i];
        if (!Utilities.IsValid(part)) continue;
        part.localPosition = _origin + _partPositions[i] * _scale;
        part.localScale = _partScales[i] * _scale;
      }
      if (Utilities.IsValid(_app))
      {
        Vector3 turned = _appCenter + _appTurn * (_appPosition - _appCenter);
        _app.localPosition = _origin + turned * _scale;
        _app.localRotation = _appTurn * _appRotation;
        _app.localScale = _appScale * _scale;
      }
      if (Utilities.IsValid(_grabCollider))
      {
        _grabCollider.center = _origin + _colliderCenter * _scale;
        _grabCollider.size = _colliderSize * _scale;
      }
    }
  }
}
