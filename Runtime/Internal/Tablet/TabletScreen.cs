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
  // joiner included (issue #108, D6). Whoever presses a button takes the
  // screen over; what happens inside an app stays with each player.
  [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
  public class TabletScreen : YamaPlayerBehaviour
  {
    // The synced app numbers. The home screen is not an app.
    private const int Home = -1;
    private const int VersionApp = 0;

    private const float DistanceCheckInterval = 0.5f;

    [SerializeField] private UIController _uiController;
    [SerializeField] private TabletPickup _tablet;

    [Header("Screens")]
    [SerializeField] private GameObject _home;
    [Tooltip("Indexed by the app numbers in TabletScreen.")]
    [SerializeField] private GameObject[] _apps = new GameObject[0];

    [Header("Buttons")]
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(GoHome))] private Button _homeButton;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(OpenVersionApp))] private Button _versionAppButton;

    [Header("Home")]
    [SerializeField] private Text _clockText;
    [SerializeField] private Text _dateText;
    [SerializeField] private Text _versionAppLabel;

    [Header("Version App")]
    [SerializeField] private Text _versionAppTitle;
    [SerializeField] private Text _updateLogText;
    [SerializeField] private TextAsset _updateLogTextAsset;
    [SerializeField] private ScrollRect _updateLogScroll;

    [Header("Interaction")]
    [SerializeField] private Collider _uiCollider;
    [SerializeField] private GraphicRaycaster _raycaster;
    // Beyond this, the screen stops taking pointer input. Nobody presses it
    // from there, and every canvas a pointer can reach costs a raycast.
    [SerializeField] private float _interactDistance = 5f;

    [UdonSynced] private int _appIndex = Home;
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

    private void OpenApp(int app)
    {
      TakeOwnership();
      _appIndex = app;
      RequestSerialization();
      ShowApp();
    }

    public override void OnDeserialization() => ShowApp();

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
      if (app == VersionApp && Utilities.IsValid(_updateLogScroll)) _updateLogScroll.verticalNormalizedPosition = 1f;
    }

    public void AfterLanguageChanged()
    {
      UpdateTranslation();
      UpdateClockView();
    }

    private void UpdateTranslation()
    {
      SetTranslatedText(_versionAppLabel, "tablet.app.version");
      SetTranslatedText(_versionAppTitle, "tablet.app.version");
    }

    // A missing key leaves the text the prefab was saved with.
    private void SetTranslatedText(Text text, string key)
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
    // the pattern: {0} month, {1} day, {2} weekday name, {3} month name.
    private string FormatDate(DateTime date)
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
      if (Utilities.IsValid(_uiCollider)) _uiCollider.enabled = value;
      if (Utilities.IsValid(_raycaster)) _raycaster.enabled = value;
    }
  }
}
