using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using Yamadev.YamaStream.UI;

namespace Yamadev.YamaStream.Tablet
{
  // What the tablet shows: the home screen or one app. The open app is
  // synced, so everyone looking at the tablet sees the same screen, a late
  // joiner included (issue #108, D6), and so is the URL of the image app's
  // picture (D2). Whoever presses a button takes the screen over; what
  // happens inside an app stays with each player.
  //
  // The screen also turns with the tablet (issue #183): held upright, the
  // display is turned a quarter and the apps take their portrait layout;
  // upside down, it is turned half. Each player works this out from the
  // tablet's own orientation, which everyone sees alike.
  [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
  public class TabletScreen : YamaPlayerBehaviour
  {
    // The synced app numbers. The home screen is not an app.
    private const int Home = -1;
    private const int VersionApp = 0;
    private const int KawaPlayerApp = 1;
    private const int ImageApp = 2;
    private const int SettingsApp = 3;
    private const int VisitorsApp = 4;

    private const float DistanceCheckInterval = 0.5f;
    private const float OrientationCheckInterval = 0.2f;
    // How far past the last quarter turn the tablet must go before the
    // screen follows, so it does not flip back and forth around 45 degrees.
    private const float TurnThreshold = 55f;
    // Lying flatter than this, which way is up says nothing.
    private const float FlatLimit = 0.8f;
    // How many pictures the image app's list holds (issue #181).
    private const int ImageListSize = 5;

    [SerializeField] private UIController _uiController;
    [SerializeField] private TabletPickup _tablet;
    [SerializeField] private TabletImageApp _imageApp;
    [SerializeField] private TabletSettingsApp _settingsApp;
    [SerializeField] private TabletVisitorsApp _visitorsApp;

    [Header("Screens")]
    [SerializeField] private GameObject _home;
    [Tooltip("Indexed by the app numbers in TabletScreen.")]
    [SerializeField] private GameObject[] _apps = new GameObject[0];

    [Header("Buttons")]
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(GoHome))] private Button _homeButton;
    // The LED around the home button.
    [SerializeField] private GameObject _homeButtonLight;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(OpenVersionApp))] private Button _versionAppButton;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(OpenKawaPlayerApp))] private Button _kawaPlayerAppButton;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(OpenImageApp))] private Button _imageAppButton;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(OpenSettingsApp))] private Button _settingsAppButton;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(OpenVisitorsApp))] private Button _visitorsAppButton;

    [Header("Home")]
    [SerializeField] private Text _clockText;
    [SerializeField] private Text _dateText;
    [SerializeField] private Text _versionAppLabel;
    [SerializeField] private Text _imageAppLabel;
    [SerializeField] private Text _settingsAppLabel;
    [SerializeField] private Text _visitorsAppLabel;

    [Header("Version App")]
    [SerializeField] private Text _versionAppTitle;
    [SerializeField] private Text _updateLogText;
    [SerializeField] private TextAsset _updateLogTextAsset;
    [SerializeField] private ScrollRect _updateLogScroll;

    // Every canvas on the tablet: its own, and the player's ScreenUI in the
    // KawaPlayer app.
    [Header("Interaction")]
    [SerializeField] private Collider[] _uiColliders = new Collider[0];
    [SerializeField] private GraphicRaycaster[] _raycasters = new GraphicRaycaster[0];
    // Beyond this, the screen stops taking pointer input. Nobody presses it
    // from there, and every canvas a pointer can reach costs a raycast.
    [SerializeField] private float _interactDistance = 5f;

    // The display, turned a quarter for portrait or half when upside down,
    // and the parts that sit elsewhere in portrait: each part's anchors (min
    // x, min y, max x, max y) and position and size (x, y, width, height) in
    // either layout. KawaPlayer > Tablet Layout captures them.
    [Header("Orientation")]
    [SerializeField] private RectTransform _display;
    [SerializeField] private RectTransform[] _layoutParts = new RectTransform[0];
    [SerializeField] private Vector4[] _landscapeAnchors = new Vector4[0];
    [SerializeField] private Vector4[] _landscapeRects = new Vector4[0];
    [SerializeField] private Vector4[] _portraitAnchors = new Vector4[0];
    [SerializeField] private Vector4[] _portraitRects = new Vector4[0];
    // Layout groups that place their children in landscape only; in portrait
    // the parts above say where each child goes.
    [SerializeField] private Behaviour[] _landscapeLayouts = new Behaviour[0];
    // The KawaPlayer app stays landscape, so that its controls keep their
    // size; it turns over with the tablet, around the middle of its screen.
    [SerializeField] private Transform _kawaPlayerApp;
    [SerializeField] private Transform _kawaPlayerScreen;

    [UdonSynced] private int _appIndex = Home;
    [UdonSynced] private VRCUrl _imageUrl = VRCUrl.Empty;
    // The image app's list, newest first, and whether the app shows the
    // picture full size or the list. A URL cannot be made from text, so the
    // list lasts only as long as the instance.
    [UdonSynced] private VRCUrl[] _imageList = new VRCUrl[0];
    [UdonSynced] private bool _imageViewing;
    private int _shownApp = int.MinValue;
    private bool _interactable = true;
    // The screen's turn in degrees: 0, 90, 180 or -90.
    private float _turn;
    private bool _portrait;
    private bool _layoutApplied;
    private Vector2 _displaySize;
    private float _kawaPlayerTurn;
    private Vector3 _kawaPlayerPosition;
    private Quaternion _kawaPlayerRotation;
    private Vector3 _kawaPlayerCenter;
    private Vector3 _kawaPlayerAxis;

    private void Start()
    {
      if (Utilities.IsValid(_uiController)) _uiController.AddListener(this);
      StartOrientation();
      UpdateTranslation();
      UpdateUpdateLog();
      ShowApp();
      _OnMinute();
      _CheckDistance();
      _CheckOrientation();
    }

    public string GetTranslation(string key) => Utilities.IsValid(_uiController) ? _uiController.GetTranslation(key) : string.Empty;

    // The font of the player's language. UIController sets it on every text
    // under it, the clock included, as soon as a translation is asked for.
    public Font CurrentFont => Utilities.IsValid(_clockText) ? _clockText.font : null;

    public void GoHome() => OpenApp(Home);

    public void OpenVersionApp() => OpenApp(VersionApp);

    public void OpenKawaPlayerApp() => OpenApp(KawaPlayerApp);

    public void OpenImageApp() => OpenApp(ImageApp);

    public void OpenSettingsApp() => OpenApp(SettingsApp);

    public void OpenVisitorsApp() => OpenApp(VisitorsApp);

    public VRCUrl ImageUrl => _imageUrl;

    public VRCUrl[] ImageList => _imageList;

    public bool ImageViewing => _imageViewing;

    // Shows a picture full size: one just entered, or one picked from the
    // list.
    public void SetImageUrl(VRCUrl url)
    {
      TakeOwnership();
      _imageUrl = url;
      _imageViewing = true;
      RequestSerialization();
      if (Utilities.IsValid(_tablet)) _tablet.Touch();
      if (Utilities.IsValid(_imageApp)) _imageApp.ShowImageState();
    }

    public void ShowImageList()
    {
      TakeOwnership();
      _imageViewing = false;
      RequestSerialization();
      if (Utilities.IsValid(_tablet)) _tablet.Touch();
      if (Utilities.IsValid(_imageApp)) _imageApp.ShowImageState();
    }

    // Puts a picture first in the list, or moves it there. The image app
    // calls this once the picture has loaded for the player who entered it,
    // so a URL that does not load never takes a place. Past the last place,
    // the oldest picture leaves.
    //
    // Only the owner may: a player who has lost the screen to someone else
    // since entering the picture would send back the state they had, over
    // the newer one.
    public void AddToImageList(VRCUrl url)
    {
      if (!Utilities.IsValid(url) || string.IsNullOrEmpty(url.Get())) return;
      if (!IsObjectOwner) return;
      string key = url.Get();
      VRCUrl[] list = new VRCUrl[ImageListSize];
      list[0] = url;
      int count = 1;
      foreach (VRCUrl item in _imageList)
      {
        if (count >= ImageListSize) break;
        if (!Utilities.IsValid(item) || item.Get() == key) continue;
        list[count++] = item;
      }
      _imageList = new VRCUrl[count];
      for (int i = 0; i < count; i++) _imageList[i] = list[i];
      RequestSerialization();
      if (Utilities.IsValid(_imageApp)) _imageApp.ShowImageState();
    }

    private void OpenApp(int app)
    {
      TakeOwnership();
      _appIndex = app;
      RequestSerialization();
      ShowApp();
    }

    public override void OnDeserialization()
    {
      ShowApp();
      if (Utilities.IsValid(_imageApp)) _imageApp.ShowImageState();
    }

    private void ShowApp()
    {
      if (Utilities.IsValid(_tablet)) _tablet.Touch();

      // An app this tablet does not have shows the home screen, so a number
      // from a newer version never leaves the screen blank.
      int app = _appIndex >= 0 && _appIndex < _apps.Length && Utilities.IsValid(_apps[_appIndex]) ? _appIndex : Home;
      if (app == _shownApp) return;
      _shownApp = app;

      if (Utilities.IsValid(_home)) _home.SetActive(app == Home);
      for (int i = 0; i < _apps.Length; i++)
      {
        if (Utilities.IsValid(_apps[i])) _apps[i].SetActive(i == app);
      }
      // The home button's LED is lit while there is an app to leave (issue
      // #180). On the home screen it is off and the button takes no press:
      // one would only take the screen over and sync it for nothing.
      if (Utilities.IsValid(_homeButtonLight)) _homeButtonLight.SetActive(app != Home);
      if (Utilities.IsValid(_homeButton)) _homeButton.interactable = app != Home;
      if (app == VersionApp && Utilities.IsValid(_updateLogScroll)) _updateLogScroll.verticalNormalizedPosition = 1f;
    }

    public void AfterLanguageChanged()
    {
      UpdateTranslation();
      UpdateClockView();
      if (Utilities.IsValid(_imageApp)) _imageApp.UpdateTranslation();
      if (Utilities.IsValid(_settingsApp)) _settingsApp.UpdateTranslation();
      if (Utilities.IsValid(_visitorsApp)) _visitorsApp.UpdateTranslation();
    }

    private void UpdateTranslation()
    {
      SetTranslatedText(_versionAppLabel, "tablet.app.version");
      SetTranslatedText(_imageAppLabel, "tablet.app.image");
      SetTranslatedText(_settingsAppLabel, "menu.settings");
      SetTranslatedText(_visitorsAppLabel, "tablet.app.visitors");
      SetTranslatedText(_versionAppTitle, "tablet.app.version");
    }

    // A missing key leaves the text the prefab was saved with. The apps
    // use it too.
    public void SetTranslatedText(Text text, string key)
    {
      if (!Utilities.IsValid(text)) return;
      string value = GetTranslation(key);
      if (!string.IsNullOrEmpty(value)) text.text = value;
    }

    // UI Text wraps only at spaces, so a Japanese line with an early space
    // breaks there and leaves a stub of a line. Spaces that do not break
    // let every line run to the edge.
    private void UpdateUpdateLog()
    {
      if (!Utilities.IsValid(_updateLogText) || !Utilities.IsValid(_updateLogTextAsset)) return;
      _updateLogText.text = _updateLogTextAsset.text.Replace(" ", " ");
    }

    // Runs once a minute, just after the minute turns, instead of every
    // frame.
    public void _OnMinute()
    {
      UpdateClockView();
      DateTime now = DateTime.Now;
      SendCustomEventDelayedSeconds(nameof(_OnMinute), 60f - now.Second - now.Millisecond / 1000f + 0.05f);
    }

    private void UpdateClockView()
    {
      DateTime now = DateTime.Now;
      if (Utilities.IsValid(_clockText)) _clockText.text = string.Format("{0:00}:{1:00}", now.Hour, now.Minute);
      if (Utilities.IsValid(_dateText)) _dateText.text = FormatDate(now);
    }

    // Every language orders the date its own way, so the translation holds
    // the pattern: {0} month, {1} day, {2} weekday name, {3} month name. The
    // visitors app's log uses it too.
    public string FormatDate(DateTime date)
    {
      string format = GetTranslation("tablet.dateFormat");
      if (string.IsNullOrEmpty(format)) return date.ToString("yyyy-MM-dd");
      string[] weekdays = GetTranslation("tablet.weekdays").Split(',');
      string[] months = GetTranslation("tablet.months").Split(',');
      int weekday = (int)date.DayOfWeek;
      int month = date.Month - 1;
      return string.Format(format, date.Month, date.Day,
        weekday < weekdays.Length ? weekdays[weekday] : string.Empty,
        month < months.Length ? months[month] : string.Empty);
    }

    #region Orientation

    private void StartOrientation()
    {
      if (Utilities.IsValid(_display)) _displaySize = _display.sizeDelta;
      if (Utilities.IsValid(_kawaPlayerApp))
      {
        _kawaPlayerPosition = _kawaPlayerApp.localPosition;
        _kawaPlayerRotation = _kawaPlayerApp.localRotation;
        Transform parent = _kawaPlayerApp.parent;
        if (Utilities.IsValid(parent))
        {
          _kawaPlayerCenter = parent.InverseTransformPoint(Utilities.IsValid(_kawaPlayerScreen) ? _kawaPlayerScreen.position : _kawaPlayerApp.position);
          _kawaPlayerAxis = parent.InverseTransformDirection(transform.forward);
        }
      }
      _turn = FindTurn(true);
      ApplyOrientation();
    }

    public void _CheckOrientation()
    {
      SendCustomEventDelayedSeconds(nameof(_CheckOrientation), OrientationCheckInterval);
      float turn = FindTurn(false);
      if (turn == _turn) return;
      _turn = turn;
      ApplyOrientation();
    }

    // The quarter turn that keeps the screen upright: on the screen, from
    // the tablet's up to the world's. Lying flat, it stays as it was.
    private float FindTurn(bool immediate)
    {
      Vector3 normal = transform.forward;
      float facing = Vector3.Dot(normal, Vector3.up);
      if (Mathf.Abs(facing) > FlatLimit) return _turn;
      Vector3 up = Vector3.up - normal * facing;
      float angle = Vector3.SignedAngle(transform.up, up, normal);
      if (!immediate && Mathf.Abs(Mathf.DeltaAngle(_turn, angle)) < TurnThreshold) return _turn;
      float turn = Mathf.Round(angle / 90f) * 90f;
      return turn <= -180f ? 180f : turn;
    }

    private void ApplyOrientation()
    {
      bool portrait = Mathf.Abs(Mathf.Abs(_turn) - 90f) < 1f;
      if (Utilities.IsValid(_display))
      {
        _display.sizeDelta = portrait ? new Vector2(_displaySize.y, _displaySize.x) : _displaySize;
        _display.localEulerAngles = new Vector3(0f, 0f, _turn);
      }
      if (portrait != _portrait || !_layoutApplied)
      {
        _portrait = portrait;
        _layoutApplied = true;
        ApplyLayout(portrait);
      }
      // Held upright, the KawaPlayer app keeps the way it last faced.
      if (!portrait) _kawaPlayerTurn = _turn;
      TurnKawaPlayer(_kawaPlayerTurn);
      if (Utilities.IsValid(_tablet)) _tablet.Touch();
    }

    // In portrait, the layout groups stop first, so they do not move what the
    // parts place; in landscape they start last, and place their children.
    private void ApplyLayout(bool portrait)
    {
      if (portrait) SetLayoutsEnabled(false);
      Vector4[] anchors = portrait ? _portraitAnchors : _landscapeAnchors;
      Vector4[] rects = portrait ? _portraitRects : _landscapeRects;
      int count = Mathf.Min(_layoutParts.Length, Mathf.Min(anchors.Length, rects.Length));
      for (int i = 0; i < count; i++)
      {
        RectTransform part = _layoutParts[i];
        if (!Utilities.IsValid(part)) continue;
        Vector4 a = anchors[i];
        Vector4 r = rects[i];
        part.anchorMin = new Vector2(a.x, a.y);
        part.anchorMax = new Vector2(a.z, a.w);
        part.anchoredPosition = new Vector2(r.x, r.y);
        part.sizeDelta = new Vector2(r.z, r.w);
      }
      if (!portrait) SetLayoutsEnabled(true);

      if (Utilities.IsValid(_imageApp)) _imageApp.OnLayoutChanged();
      if (Utilities.IsValid(_visitorsApp)) _visitorsApp.SetPortrait(portrait);
    }

    private void SetLayoutsEnabled(bool enabled)
    {
      foreach (Behaviour layout in _landscapeLayouts)
      {
        if (Utilities.IsValid(layout)) layout.enabled = enabled;
      }
    }

    private void TurnKawaPlayer(float turn)
    {
      if (!Utilities.IsValid(_kawaPlayerApp)) return;
      Quaternion rotation = Quaternion.AngleAxis(turn, _kawaPlayerAxis);
      _kawaPlayerApp.localRotation = rotation * _kawaPlayerRotation;
      _kawaPlayerApp.localPosition = _kawaPlayerCenter + rotation * (_kawaPlayerPosition - _kawaPlayerCenter);
    }

    #endregion

    public void _CheckDistance()
    {
      SendCustomEventDelayedSeconds(nameof(_CheckDistance), DistanceCheckInterval);
      if (!IsLocalPlayerValid) return;
      Vector3 head = LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
      SetInteractable((head - transform.position).sqrMagnitude <= _interactDistance * _interactDistance);
    }

    private void SetInteractable(bool value)
    {
      if (value == _interactable) return;
      _interactable = value;
      foreach (var collider in _uiColliders)
      {
        if (Utilities.IsValid(collider)) collider.enabled = value;
      }
      foreach (var raycaster in _raycasters)
      {
        if (Utilities.IsValid(raycaster)) raycaster.enabled = value;
      }
    }
  }
}
