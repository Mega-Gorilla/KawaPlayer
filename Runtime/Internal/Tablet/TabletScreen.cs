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
    // How bright the home button stays on the home screen.
    private const float HomeButtonDimAlpha = 0.35f;

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
    // The home button with its ring and icon, dimmed together.
    [SerializeField] private CanvasGroup _homeButtonGroup;
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

    [UdonSynced] private int _appIndex = Home;
    [UdonSynced] private VRCUrl _imageUrl = VRCUrl.Empty;
    private int _shownApp = int.MinValue;
    private bool _interactable = true;

    private void Start()
    {
      if (Utilities.IsValid(_uiController)) _uiController.AddListener(this);
      UpdateTranslation();
      UpdateUpdateLog();
      ShowApp();
      _OnMinute();
      _CheckDistance();
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

    public void SetImageUrl(VRCUrl url)
    {
      TakeOwnership();
      _imageUrl = url;
      RequestSerialization();
      if (Utilities.IsValid(_tablet)) _tablet.Touch();
      if (Utilities.IsValid(_imageApp)) _imageApp.ShowUrl();
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
      if (Utilities.IsValid(_imageApp)) _imageApp.ShowUrl();
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
      // The home button is lit while there is an app to leave (issue #180).
      // On the home screen it is dim and takes no press: one would only take
      // the screen over and sync it for nothing.
      if (Utilities.IsValid(_homeButtonGroup))
      {
        _homeButtonGroup.alpha = app == Home ? HomeButtonDimAlpha : 1f;
        _homeButtonGroup.interactable = app != Home;
      }
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
