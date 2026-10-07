using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Yamadev.YamaStream.Tablet
{
  // The visitors app's record (issue #153): everyone who has been in the
  // instance and the log of arrivals and departures, synced so a late joiner
  // sees it all (issue #108, D3). Every tablet carries one; the build keeps
  // one, moves it out of its tablet and switches the others off, so however
  // many tablets a world has, the record is kept and the photos are taken
  // once.
  //
  // A player writes their own arrival, since only their client knows their
  // platform and input; the owner writes departures. Photos are not synced:
  // each client takes its own of whoever is in the instance.
  [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
  public class TabletVisitorRecorder : YamaPlayerBehaviour
  {
    // When the visitors fill up, the one who left longest ago makes room,
    // and their log entries go with them.
    private const int MaxVisitors = 500;
    private const int MaxLogEntries = 1000;

    // A visitor's state: here or not, instance owner or not, and from bit 2
    // the number of visits.
    private const int StatePresent = 1;
    private const int StateOwner = 2;
    private const int VisitShift = 2;

    // A log entry: arrival or departure, from bit 1 the visitor's number,
    // from bit 16 their environment.
    private const int EntryArrival = 1;
    private const int EntryVisitorShift = 1;
    private const int EntryVisitorMask = 0x7FFF;
    private const int EntryEnvironmentShift = 16;

    // An environment: in VR or not, from bit 1 the last input method
    // (VRCInputMethod), from bit 8 the platform.
    public const int EnvironmentVR = 1;
    public const int EnvironmentInputShift = 1;
    public const int EnvironmentInputMask = 0x7F;
    public const int EnvironmentPlatformShift = 8;
    public const int PlatformPC = 0;
    public const int PlatformAndroid = 1;
    public const int PlatformIOS = 2;

    private const long TicksPerSecond = 10000000L;
    // An arrival is written a few seconds after joining: the input method is
    // known by then, and players joining together do not all take the
    // record at once.
    private const float ArrivalDelayMin = 3f;
    private const float ArrivalDelayMax = 5f;
    private const float ArrivalRetryDelay = 1f;
    // During OnPlayerLeft the leaving player is still listed.
    private const float DepartureCheckDelay = 3f;
    // At most one photo per interval. The first waits for the avatar to load;
    // after that each is retaken every few minutes, avatars change.
    private const float PhotoInterval = 2f;
    private const int FirstPhotoDelay = 5;
    private const float PhotoRefresh = 300f;
    // The layers an avatar is drawn on: others' on Player; your own, with its
    // head, only on MirrorReflection (on PlayerLocal the head is hidden).
    private const int PlayerLayerMask = 1 << 9;
    private const int MirrorReflectionLayerMask = 1 << 18;

    [Tooltip("Set by the build: every visitors app in the world.")]
    [SerializeField] private TabletVisitorsApp[] _apps = new TabletVisitorsApp[0];

    [Header("Photos")]
    [Tooltip("Off, no photos are taken and every visitor shows the stand-in picture. Set from the tablet's inspector.")]
    [SerializeField] private bool _photosEnabled = true;
    [SerializeField] private Camera _photoCamera;
    // One texture holds every photo, side by side: 8 x 8 photos of 128 px
    // take 4 MB, with 2 MB more for the depth Android needs to draw them.
    [SerializeField] private int _photoSize = 128;
    [SerializeField] private int _photoColumns = 8;
    [SerializeField] private int _photoRows = 8;

    // Times are seconds from _startTicks, on the server's clock, which every
    // client shares.
    [UdonSynced] private long _startTicks;
    [UdonSynced] private string[] _names = new string[0];
    [UdonSynced] private int[] _states = new int[0];
    [UdonSynced] private int[] _environments = new int[0];
    [UdonSynced] private int[] _arrivals = new int[0];
    [UdonSynced] private int[] _departures = new int[0];
    // Oldest first.
    [UdonSynced] private int[] _logTimes = new int[0];
    [UdonSynced] private int[] _logEntries = new int[0];

    private bool _arrivalScheduled;
    private bool _departureCheckScheduled;

    private RenderTexture _photoTexture;
    // Per photo: whose it is, and when it was taken (Time.time).
    private string[] _photoNames = new string[0];
    private float[] _photoTimes = new float[0];

    private void Start()
    {
      ScheduleArrival();
      if (_photosEnabled) StartPhotos();
    }

    public override void OnDeserialization()
    {
      // An arrival written at the same moment as someone else's can be lost:
      // write it again.
      if (!IsHere()) ScheduleArrival();
      NotifyApps();
    }

    #region Record

    public long StartTicks => _startTicks;
    public int VisitorCount => _names.Length;
    public string GetName(int visitor) => _names[visitor];
    public bool IsPresent(int visitor) => (_states[visitor] & StatePresent) != 0;
    public bool WasInstanceOwner(int visitor) => (_states[visitor] & StateOwner) != 0;
    public int GetVisits(int visitor) => _states[visitor] >> VisitShift;
    public int GetEnvironment(int visitor) => _environments[visitor];
    public int GetArrival(int visitor) => _arrivals[visitor];
    // Their last departure; -1 if they never left.
    public int GetDeparture(int visitor) => _departures[visitor];

    public int LogCount => _logTimes.Length;
    public int GetLogTime(int entry) => _logTimes[entry];
    public bool IsLogArrival(int entry) => (_logEntries[entry] & EntryArrival) != 0;
    public int GetLogVisitor(int entry) => (_logEntries[entry] >> EntryVisitorShift) & EntryVisitorMask;
    public int GetLogEnvironment(int entry) => _logEntries[entry] >> EntryEnvironmentShift;

    private void ScheduleArrival()
    {
      if (_arrivalScheduled || !IsLocalPlayerValid) return;
      _arrivalScheduled = true;
      SendCustomEventDelayedSeconds(nameof(_Arrive), UnityEngine.Random.Range(ArrivalDelayMin, ArrivalDelayMax));
    }

    public void _Arrive()
    {
      _arrivalScheduled = false;
      if (!IsLocalPlayerValid || IsHere()) return;
      // Not before this client has the record: an arrival written over an
      // empty one would take the record and lose everyone who has left. The
      // delay above does not promise that; a settled network does, every
      // object's state having arrived by then.
      if (!Networking.IsNetworkSettled)
      {
        _arrivalScheduled = true;
        SendCustomEventDelayedSeconds(nameof(_Arrive), ArrivalRetryDelay);
        return;
      }
      TakeOwnership();

      string name = LocalPlayer.displayName;
      int visitor = FindVisitor(name);
      if (visitor < 0) visitor = AddVisitor(name);
      if (visitor < 0) return;

      int now = Now();
      int environment = LocalEnvironment();
      int visits = GetVisits(visitor) + 1;
      _states[visitor] = (visits << VisitShift) | StatePresent | (IsInstanceOwner ? StateOwner : 0);
      _environments[visitor] = environment;
      _arrivals[visitor] = now;
      _departures[visitor] = -1;
      AddLogEntry(now, true, visitor, environment);
      RequestSerialization();
      NotifyApps();
      // The record is ours now, departures included.
      ScheduleDepartureCheck();
    }

    private bool IsHere()
    {
      if (!IsLocalPlayerValid) return false;
      int visitor = FindVisitor(LocalPlayer.displayName);
      return visitor >= 0 && IsPresent(visitor);
    }

    public override void OnPlayerLeft(VRCPlayerApi player)
    {
      if (IsObjectOwner) ScheduleDepartureCheck();
    }

    // Whoever the record passes to checks it: someone may have left while
    // the last owner was leaving.
    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
      if (Utilities.IsValid(player) && player.isLocal) ScheduleDepartureCheck();
    }

    private void ScheduleDepartureCheck()
    {
      if (_departureCheckScheduled) return;
      _departureCheckScheduled = true;
      SendCustomEventDelayedSeconds(nameof(_CheckDepartures), DepartureCheckDelay);
    }

    public void _CheckDepartures()
    {
      _departureCheckScheduled = false;
      if (!IsObjectOwner) return;

      VRCPlayerApi[] players = GetPlayers();
      int now = -1;
      for (int i = 0; i < _names.Length; i++)
      {
        if (!IsPresent(i) || IsInInstance(players, _names[i])) continue;
        if (now < 0) now = Now();
        _states[i] &= ~StatePresent;
        _departures[i] = now;
        AddLogEntry(now, false, i, _environments[i]);
      }
      if (now < 0) return;
      RequestSerialization();
      NotifyApps();
    }

    private int Now()
    {
      long ticks = Networking.GetNetworkDateTime().Ticks;
      if (_startTicks == 0) _startTicks = ticks;
      // A client whose clock is a moment behind the first one's would go
      // below zero.
      return Mathf.Max(0, (int)((ticks - _startTicks) / TicksPerSecond));
    }

    private int FindVisitor(string name)
    {
      for (int i = 0; i < _names.Length; i++)
      {
        if (_names[i] == name) return i;
      }
      return -1;
    }

    private int AddVisitor(string name)
    {
      if (_names.Length < MaxVisitors)
      {
        _names = _names.Add(name);
        _states = _states.Add(0);
        _environments = _environments.Add(0);
        _arrivals = _arrivals.Add(0);
        _departures = _departures.Add(-1);
        return _names.Length - 1;
      }

      int oldest = -1;
      for (int i = 0; i < _names.Length; i++)
      {
        if (IsPresent(i)) continue;
        if (oldest < 0 || _departures[i] < _departures[oldest]) oldest = i;
      }
      if (oldest < 0) return -1;
      RemoveLogEntriesOf(oldest);
      _names[oldest] = name;
      _states[oldest] = 0;
      _environments[oldest] = 0;
      _arrivals[oldest] = 0;
      _departures[oldest] = -1;
      return oldest;
    }

    private void AddLogEntry(int time, bool arrival, int visitor, int environment)
    {
      if (_logTimes.Length >= MaxLogEntries)
      {
        _logTimes = _logTimes.Remove(0);
        _logEntries = _logEntries.Remove(0);
      }
      _logTimes = _logTimes.Add(time);
      _logEntries = _logEntries.Add((arrival ? EntryArrival : 0) | (visitor << EntryVisitorShift) | (environment << EntryEnvironmentShift));
    }

    private void RemoveLogEntriesOf(int visitor)
    {
      int kept = 0;
      for (int i = 0; i < _logEntries.Length; i++)
      {
        if (GetLogVisitor(i) == visitor) continue;
        _logTimes[kept] = _logTimes[i];
        _logEntries[kept] = _logEntries[i];
        kept++;
      }
      int[] times = new int[kept];
      int[] entries = new int[kept];
      Array.Copy(_logTimes, times, kept);
      Array.Copy(_logEntries, entries, kept);
      _logTimes = times;
      _logEntries = entries;
    }

    // The world is built once per platform, so the platform is known when it
    // is compiled.
    private int LocalEnvironment()
    {
      int platform = PlatformPC;
#if UNITY_ANDROID
      platform = PlatformAndroid;
#elif UNITY_IOS
      platform = PlatformIOS;
#endif
      int input = (int)InputManager.GetLastUsedInputMethod();
      return (IsInVR ? EnvironmentVR : 0)
        | ((input & EnvironmentInputMask) << EnvironmentInputShift)
        | (platform << EnvironmentPlatformShift);
    }

    private VRCPlayerApi[] GetPlayers()
    {
      VRCPlayerApi[] players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
      VRCPlayerApi.GetPlayers(players);
      return players;
    }

    private bool IsInInstance(VRCPlayerApi[] players, string name)
    {
      foreach (VRCPlayerApi player in players)
      {
        if (Utilities.IsValid(player) && player.displayName == name) return true;
      }
      return false;
    }

    private void NotifyApps()
    {
      foreach (TabletVisitorsApp app in _apps)
      {
        if (Utilities.IsValid(app)) app.OnVisitorsChanged();
      }
    }

    #endregion

    #region Photos

    public Texture PhotoTexture => _photoTexture;

    // -1 when there is no photo of them.
    public int FindPhoto(string name)
    {
      for (int i = 0; i < _photoNames.Length; i++)
      {
        if (_photoNames[i] == name) return i;
      }
      return -1;
    }

    public Rect GetPhotoRect(int photo)
    {
      float width = 1f / _photoColumns;
      float height = 1f / _photoRows;
      return new Rect(photo % _photoColumns * width, photo / _photoColumns * height, width, height);
    }

    private void StartPhotos()
    {
      if (!Utilities.IsValid(_photoCamera) || _photoColumns <= 0 || _photoRows <= 0) return;
      _photoTexture = new RenderTexture(_photoSize * _photoColumns, _photoSize * _photoRows, 16, RenderTextureFormat.ARGB32);
      _photoTexture.Create();
      _photoCamera.enabled = false;
      _photoCamera.targetTexture = _photoTexture;
      int count = _photoColumns * _photoRows;
      _photoNames = new string[count].Populate(string.Empty);
      _photoTimes = new float[count];
      SendCustomEventDelayedSeconds(nameof(_TakeNextPhoto), PhotoInterval);
    }

    // Takes the photo most wanted: of someone who has none yet, or else the
    // oldest one past its time.
    public void _TakeNextPhoto()
    {
      SendCustomEventDelayedSeconds(nameof(_TakeNextPhoto), PhotoInterval);
      if (_startTicks == 0) return;

      int now = Now();
      VRCPlayerApi target = null;
      int targetPhoto = -1;
      float oldest = float.MaxValue;
      foreach (VRCPlayerApi player in GetPlayers())
      {
        if (!Utilities.IsValid(player)) continue;
        int visitor = FindVisitor(player.displayName);
        if (visitor < 0 || now - _arrivals[visitor] < FirstPhotoDelay) continue;
        int photo = FindPhoto(player.displayName);
        float taken = photo < 0 ? float.MinValue : _photoTimes[photo];
        if (photo >= 0 && Time.time - taken < PhotoRefresh) continue;
        if (taken >= oldest) continue;
        oldest = taken;
        target = player;
        targetPhoto = photo;
      }
      if (!Utilities.IsValid(target)) return;

      if (targetPhoto < 0) targetPhoto = FreePhoto(GetPlayers());
      TakePhoto(target, targetPhoto);
      _photoNames[targetPhoto] = target.displayName;
      _photoTimes[targetPhoto] = Time.time;
      foreach (TabletVisitorsApp app in _apps)
      {
        if (Utilities.IsValid(app)) app.OnPhotosChanged();
      }
    }

    // A new avatar: retake soon, once it has had a moment to appear.
    public override void OnAvatarChanged(VRCPlayerApi player)
    {
      if (!Utilities.IsValid(player)) return;
      int photo = FindPhoto(player.displayName);
      if (photo >= 0) _photoTimes[photo] = Time.time - PhotoRefresh + FirstPhotoDelay;
    }

    // An empty place, or else the oldest photo of someone who has left, or
    // else the oldest photo.
    private int FreePhoto(VRCPlayerApi[] players)
    {
      int oldestGone = -1;
      int oldest = 0;
      for (int i = 0; i < _photoNames.Length; i++)
      {
        if (string.IsNullOrEmpty(_photoNames[i])) return i;
        if (_photoTimes[i] < _photoTimes[oldest]) oldest = i;
        if (IsInInstance(players, _photoNames[i])) continue;
        if (oldestGone < 0 || _photoTimes[i] < _photoTimes[oldestGone]) oldestGone = i;
      }
      return oldestGone >= 0 ? oldestGone : oldest;
    }

    // Head and shoulders, from in front. The clip planes keep to a short
    // depth around the head, so whoever stands behind stays out of it.
    private void TakePhoto(VRCPlayerApi player, int photo)
    {
      float eyeHeight = player.GetAvatarEyeHeightAsMeters();
      float scale = Mathf.Clamp(eyeHeight, 0.2f, 10f) / 1.6f;
      Vector3 head = player.GetBonePosition(HumanBodyBones.Head);
      if (head == Vector3.zero) head = player.GetPosition() + Vector3.up * eyeHeight * 0.95f;
      Vector3 forward = Facing(player);
      Vector3 target = head + Vector3.up * (0.08f * scale);
      float distance = 0.6f * scale;

      _photoCamera.transform.SetPositionAndRotation(target + forward * distance, Quaternion.LookRotation(-forward, Vector3.up));
      _photoCamera.nearClipPlane = Mathf.Max(0.01f, distance - 0.3f * scale);
      _photoCamera.farClipPlane = distance + 0.35f * scale;
      _photoCamera.cullingMask = player.isLocal ? MirrorReflectionLayerMask : PlayerLayerMask;
      _photoCamera.rect = GetPhotoRect(photo);
      _photoCamera.Render();
    }

    // Which way the body faces, from the shoulders; for an avatar without
    // them (Generic), the player's own direction.
    private Vector3 Facing(VRCPlayerApi player)
    {
      Vector3 left = player.GetBonePosition(HumanBodyBones.LeftUpperArm);
      Vector3 right = player.GetBonePosition(HumanBodyBones.RightUpperArm);
      Vector3 forward = left != Vector3.zero && right != Vector3.zero
        ? Vector3.Cross(right - left, Vector3.up)
        : player.GetRotation() * Vector3.forward;
      forward.y = 0f;
      return forward.sqrMagnitude > 0.000001f ? forward.normalized : Vector3.forward;
    }

    #endregion
  }
}
