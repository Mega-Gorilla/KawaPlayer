using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace Yamadev.YamaStream.Tablet
{
  // The visitors app (issue #153): who is here and who has been, and the log
  // of arrivals and departures, read from the world's TabletVisitorRecorder.
  // Which of the two views is open stays with each player, as the settings
  // app's categories do.
  //
  // The log alone can hold 1,000 entries, so each view has only enough row
  // objects to fill the screen and moves them as it scrolls. A row is either
  // a heading (a section, a date) or an entry; the two differ in height.
  [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
  public class TabletVisitorsApp : YamaPlayerBehaviour
  {
    private const int KindPresentHeading = 0;
    private const int KindDepartedHeading = 1;
    private const int KindDateHeading = 2;
    private const int KindVisitor = 3;
    private const int KindLogEntry = 4;

    private const long TicksPerMinute = 600000000L;
    private const long TicksPerDay = 864000000000L;

    [SerializeField] private TabletScreen _screen;
    [Tooltip("Set by the build: the world's one recorder.")]
    [SerializeField] private TabletVisitorRecorder _recorder;

    [Header("Header")]
    [SerializeField] private Text _titleText;
    [SerializeField] private Text _sinceText;
    [SerializeField] private Text _listTabLabel;
    [SerializeField] private Text _logTabLabel;

    [Header("Views")]
    [SerializeField] private ScrollRect _listScroll;
    [SerializeField] private ScrollRect _logScroll;
    // A row: a Heading and an Entry, one of them shown. The template stays
    // hidden; the rows are made from it.
    [SerializeField] private RectTransform _listRowTemplate;
    [SerializeField] private RectTransform _logRowTemplate;
    [SerializeField] private float _listHeadingHeight = 64f;
    [SerializeField] private float _listEntryHeight = 136f;
    [SerializeField] private float _logHeadingHeight = 64f;
    [SerializeField] private float _logEntryHeight = 88f;
    // The rows for a tablet held upright (issue #183): narrower, so their
    // parts sit differently, and an entry takes more lines.
    [SerializeField] private RectTransform _listRowTemplatePortrait;
    [SerializeField] private RectTransform _logRowTemplatePortrait;
    [SerializeField] private float _listEntryHeightPortrait = 180f;
    [SerializeField] private float _logEntryHeightPortrait = 136f;

    [Header("Icons")]
    [SerializeField] private Sprite _vrIcon;
    [SerializeField] private Sprite _desktopIcon;
    [SerializeField] private Sprite _mobileIcon;

    private bool _dirty = true;
    private bool _portrait;
    private long _utcOffsetTicks;

    // What each view shows, top to bottom: the kind of row, what it is about
    // (a visitor, a log entry, a day), and where its top is.
    private int[] _listKinds = new int[0];
    private int[] _listValues = new int[0];
    private float[] _listTops = new float[0];
    private int[] _logKinds = new int[0];
    private int[] _logValues = new int[0];
    private float[] _logTops = new float[0];
    private int _presentCount;
    private int _departedCount;
    // The view being built.
    private int[] _buildKinds;
    private int[] _buildValues;
    private float[] _buildTops;
    private int _buildCount;
    private float _buildTop;

    // The row objects of each view and the item each now shows (-1: none).
    private RectTransform[] _listRows = new RectTransform[0];
    private int[] _listRowItems = new int[0];
    private RectTransform[] _logRows = new RectTransform[0];
    private int[] _logRowItems = new int[0];

    // The language is set before the app first opens, so it takes its texts
    // here; AfterLanguageChanged reaches it after that.
    private void Start()
    {
      UpdateTranslation();
    }

    private void OnEnable()
    {
      if (_dirty) Refresh();
    }

    // Called by the recorder. A closed app catches up when it opens.
    public void OnVisitorsChanged()
    {
      _dirty = true;
      if (gameObject.activeInHierarchy) Refresh();
    }

    public void OnPhotosChanged()
    {
      if (!gameObject.activeInHierarchy) return;
      for (int i = 0; i < _listRows.Length; i++)
      {
        int item = _listRowItems[i];
        if (item >= 0 && _listKinds[item] == KindVisitor) ShowPhoto(_listRows[i], _listValues[item]);
      }
    }

    public void UpdateTranslation()
    {
      if (!Utilities.IsValid(_screen)) return;
      _screen.SetTranslatedText(_titleText, "tablet.app.visitors");
      _screen.SetTranslatedText(_listTabLabel, "tablet.visitors.list");
      _screen.SetTranslatedText(_logTabLabel, "tablet.visitors.log");
      _dirty = true;
      if (gameObject.activeInHierarchy) Refresh();
    }

    // TabletScreen calls this when the tablet turns between landscape and
    // portrait: the rows are made again from the other templates, as many as
    // the view's new height needs.
    public void SetPortrait(bool portrait)
    {
      if (portrait == _portrait) return;
      _portrait = portrait;
      DestroyRows(_listRows);
      DestroyRows(_logRows);
      _listRows = new RectTransform[0];
      _listRowItems = new int[0];
      _logRows = new RectTransform[0];
      _logRowItems = new int[0];
      _dirty = true;
      if (gameObject.activeInHierarchy) Refresh();
    }

    private void DestroyRows(RectTransform[] rows)
    {
      foreach (RectTransform row in rows)
      {
        if (!Utilities.IsValid(row)) continue;
        row.gameObject.SetActive(false);
        Destroy(row.gameObject);
      }
    }

    private float ListEntryHeight => _portrait ? _listEntryHeightPortrait : _listEntryHeight;

    private float LogEntryHeight => _portrait ? _logEntryHeightPortrait : _logEntryHeight;

    // Each view's ScrollRect calls its own as it moves.
    public void OnListScroll() => ShowRows(true, _listScroll, _listRows, _listRowItems, _listTops);

    public void OnLogScroll() => ShowRows(false, _logScroll, _logRows, _logRowItems, _logTops);

    private void Refresh()
    {
      _dirty = false;
      if (!Utilities.IsValid(_recorder)) return;
      if (_listRows.Length == 0) MakeRows();
      // From one reading of the clock: two readings can fall on either side
      // of a tick and come out a minute short.
      DateTime utcNow = DateTime.UtcNow;
      _utcOffsetTicks = (utcNow.ToLocalTime() - utcNow).Ticks;

      if (Utilities.IsValid(_sinceText))
      {
        _sinceText.text = _recorder.StartTicks == 0 ? string.Empty : string.Format(Translate("tablet.visitors.since"), FormatTime(0));
      }
      BuildList();
      BuildLog();
      ResetRows(_listRowItems);
      ResetRows(_logRowItems);
      OnListScroll();
      OnLogScroll();
    }

    #region Building the views

    // Those here, by arrival, so the rows above stay put as people come;
    // then those who have left, the latest first.
    private void BuildList()
    {
      int count = _recorder.VisitorCount;
      int[] present = new int[count];
      int[] presentKeys = new int[count];
      int[] departed = new int[count];
      int[] departedKeys = new int[count];
      _presentCount = 0;
      _departedCount = 0;
      for (int i = 0; i < count; i++)
      {
        if (_recorder.IsPresent(i))
        {
          present[_presentCount] = i;
          presentKeys[_presentCount] = _recorder.GetArrival(i);
          _presentCount++;
        }
        else
        {
          departed[_departedCount] = i;
          departedKeys[_departedCount] = -_recorder.GetDeparture(i);
          _departedCount++;
        }
      }
      SortByKey(present, presentKeys, _presentCount);
      SortByKey(departed, departedKeys, _departedCount);

      StartBuilding(2 + count);
      AddItem(KindPresentHeading, 0, _listHeadingHeight);
      for (int i = 0; i < _presentCount; i++) AddItem(KindVisitor, present[i], ListEntryHeight);
      if (_departedCount > 0)
      {
        AddItem(KindDepartedHeading, 0, _listHeadingHeight);
        for (int i = 0; i < _departedCount; i++) AddItem(KindVisitor, departed[i], ListEntryHeight);
      }
      _listKinds = TakeKinds();
      _listValues = TakeValues();
      _listTops = TakeTops();
      SetContentHeight(_listScroll, _buildTop);
    }

    // The latest first, under a heading for each day.
    private void BuildLog()
    {
      int count = _recorder.LogCount;
      StartBuilding(count * 2);
      long lastDay = long.MinValue;
      for (int i = count - 1; i >= 0; i--)
      {
        long day = LocalTicks(_recorder.GetLogTime(i)) / TicksPerDay;
        if (day != lastDay)
        {
          lastDay = day;
          AddItem(KindDateHeading, i, _logHeadingHeight);
        }
        AddItem(KindLogEntry, i, LogEntryHeight);
      }
      _logKinds = TakeKinds();
      _logValues = TakeValues();
      _logTops = TakeTops();
      SetContentHeight(_logScroll, _buildTop);
    }

    private void StartBuilding(int capacity)
    {
      _buildKinds = new int[capacity];
      _buildValues = new int[capacity];
      _buildTops = new float[capacity];
      _buildCount = 0;
      _buildTop = 0f;
    }

    private void AddItem(int kind, int value, float height)
    {
      _buildKinds[_buildCount] = kind;
      _buildValues[_buildCount] = value;
      _buildTops[_buildCount] = _buildTop;
      _buildCount++;
      _buildTop += height;
    }

    private int[] TakeKinds()
    {
      int[] kinds = new int[_buildCount];
      Array.Copy(_buildKinds, kinds, _buildCount);
      return kinds;
    }

    private int[] TakeValues()
    {
      int[] values = new int[_buildCount];
      Array.Copy(_buildValues, values, _buildCount);
      return values;
    }

    private float[] TakeTops()
    {
      float[] tops = new float[_buildCount];
      Array.Copy(_buildTops, tops, _buildCount);
      return tops;
    }

    private void SetContentHeight(ScrollRect scroll, float height)
    {
      if (!Utilities.IsValid(scroll)) return;
      RectTransform content = scroll.content;
      content.sizeDelta = new Vector2(content.sizeDelta.x, height);
    }

    // Stable, ascending: a merge sort, so a long list costs n log n.
    private void SortByKey(int[] items, int[] keys, int count)
    {
      int[] items2 = new int[count];
      int[] keys2 = new int[count];
      for (int width = 1; width < count; width *= 2)
      {
        for (int start = 0; start < count; start += width * 2)
        {
          int middle = Mathf.Min(start + width, count);
          int end = Mathf.Min(start + width * 2, count);
          int a = start, b = middle, k = start;
          while (a < middle && b < end)
          {
            if (keys[b] < keys[a]) { items2[k] = items[b]; keys2[k] = keys[b]; b++; }
            else { items2[k] = items[a]; keys2[k] = keys[a]; a++; }
            k++;
          }
          while (a < middle) { items2[k] = items[a]; keys2[k] = keys[a]; a++; k++; }
          while (b < end) { items2[k] = items[b]; keys2[k] = keys[b]; b++; k++; }
        }
        Array.Copy(items2, items, count);
        Array.Copy(keys2, keys, count);
      }
    }

    #endregion

    #region Rows

    private void MakeRows()
    {
      RectTransform listTemplate = _portrait && Utilities.IsValid(_listRowTemplatePortrait) ? _listRowTemplatePortrait : _listRowTemplate;
      RectTransform logTemplate = _portrait && Utilities.IsValid(_logRowTemplatePortrait) ? _logRowTemplatePortrait : _logRowTemplate;
      _listRows = MakeRows(_listScroll, listTemplate, Mathf.Min(_listHeadingHeight, ListEntryHeight));
      _listRowItems = new int[_listRows.Length];
      _logRows = MakeRows(_logScroll, logTemplate, Mathf.Min(_logHeadingHeight, LogEntryHeight));
      _logRowItems = new int[_logRows.Length];
    }

    // Enough to fill the view with its shortest rows, and one more for the
    // row half out at each edge.
    private RectTransform[] MakeRows(ScrollRect scroll, RectTransform template, float shortest)
    {
      if (!Utilities.IsValid(scroll) || !Utilities.IsValid(template) || shortest <= 0f) return new RectTransform[0];
      template.gameObject.SetActive(false);
      int count = Mathf.CeilToInt(scroll.viewport.rect.height / shortest) + 2;
      RectTransform[] rows = new RectTransform[count];
      for (int i = 0; i < count; i++)
      {
        GameObject row = Instantiate(template.gameObject);
        row.transform.SetParent(scroll.content, false);
        rows[i] = row.GetComponent<RectTransform>();
      }
      return rows;
    }

    private void ResetRows(int[] rowItems)
    {
      for (int i = 0; i < rowItems.Length; i++) rowItems[i] = -1;
    }

    // Item i always goes in row i % rows: the items in view are consecutive
    // and never more than the rows, so a row keeps its item while it stays
    // in view and is filled again only when it comes back in at the other
    // edge.
    private void ShowRows(bool list, ScrollRect scroll, RectTransform[] rows, int[] rowItems, float[] tops)
    {
      if (rows.Length == 0 || !Utilities.IsValid(scroll)) return;
      float viewTop = scroll.content.anchoredPosition.y;
      float viewBottom = viewTop + scroll.viewport.rect.height;
      int first = FindFirstInView(tops, viewTop);
      int last = first;
      while (last + 1 < tops.Length && tops[last + 1] < viewBottom && last + 1 - first < rows.Length) last++;

      for (int i = 0; i < rows.Length; i++)
      {
        int item = rowItems[i];
        if (item >= first && item <= last && item % rows.Length == i) continue;
        rowItems[i] = -1;
        rows[i].gameObject.SetActive(false);
      }
      for (int item = first; item <= last && item < tops.Length; item++)
      {
        int i = item % rows.Length;
        if (rowItems[i] == item) continue;
        rowItems[i] = item;
        RectTransform row = rows[i];
        row.anchoredPosition = new Vector2(row.anchoredPosition.x, -tops[item]);
        if (list) FillListRow(row, item);
        else FillLogRow(row, item);
        row.gameObject.SetActive(true);
      }
    }

    // The last item whose top is at or above the top of the view.
    private int FindFirstInView(float[] tops, float viewTop)
    {
      int low = 0, high = tops.Length - 1, found = 0;
      while (low <= high)
      {
        int middle = (low + high) / 2;
        if (tops[middle] <= viewTop) { found = middle; low = middle + 1; }
        else high = middle - 1;
      }
      return found;
    }

    private void FillListRow(RectTransform row, int item)
    {
      int kind = _listKinds[item];
      Transform heading = row.Find("Heading");
      Transform entry = row.Find("Entry");
      heading.gameObject.SetActive(kind != KindVisitor);
      entry.gameObject.SetActive(kind == KindVisitor);
      if (kind != KindVisitor)
      {
        bool present = kind == KindPresentHeading;
        heading.GetComponent<Text>().text = string.Format("{0}  <size=30>{1}</size>",
          Translate(present ? "tablet.visitors.present" : "tablet.visitors.departed"),
          string.Format(Translate("tablet.visitors.people"), present ? _presentCount : _departedCount));
        return;
      }

      int visitor = _listValues[item];
      entry.Find("Name/Text").GetComponent<Text>().text = _recorder.GetName(visitor);
      Transform owner = entry.Find("Name/Owner");
      owner.gameObject.SetActive(_recorder.WasInstanceOwner(visitor));
      owner.Find("Label").GetComponent<Text>().text = Translate("tablet.visitors.owner");
      int visits = _recorder.GetVisits(visitor);
      Transform visitsChip = entry.Find("Name/Visits");
      visitsChip.gameObject.SetActive(visits >= 2);
      visitsChip.Find("Label").GetComponent<Text>().text = string.Format(Translate("tablet.visitors.visits"), visits);
      int environment = _recorder.GetEnvironment(visitor);
      entry.Find("Mode").GetComponent<Image>().sprite = ModeIcon(environment);
      entry.Find("Environment").GetComponent<Text>().text = EnvironmentText(environment);
      entry.Find("ArrivalLabel").GetComponent<Text>().text = Translate("tablet.visitors.arrival");
      entry.Find("DepartureLabel").GetComponent<Text>().text = Translate("tablet.visitors.departure");
      entry.Find("Arrival").GetComponent<Text>().text = FormatTime(_recorder.GetArrival(visitor));
      entry.Find("Departure").GetComponent<Text>().text = _recorder.IsPresent(visitor) ? "—" : FormatTime(_recorder.GetDeparture(visitor));
      ShowPhoto(row, visitor);
    }

    private void ShowPhoto(RectTransform row, int visitor)
    {
      int photo = _recorder.FindPhoto(_recorder.GetName(visitor));
      Transform entry = row.Find("Entry");
      RawImage image = entry.Find("Photo").GetComponent<RawImage>();
      image.gameObject.SetActive(photo >= 0);
      entry.Find("NoPhoto").gameObject.SetActive(photo < 0);
      if (photo < 0) return;
      image.texture = _recorder.PhotoTexture;
      image.uvRect = _recorder.GetPhotoRect(photo);
    }

    private void FillLogRow(RectTransform row, int item)
    {
      int kind = _logKinds[item];
      int entryIndex = _logValues[item];
      Transform heading = row.Find("Heading");
      Transform entry = row.Find("Entry");
      heading.gameObject.SetActive(kind == KindDateHeading);
      entry.gameObject.SetActive(kind == KindLogEntry);
      if (kind == KindDateHeading)
      {
        heading.GetComponent<Text>().text = Utilities.IsValid(_screen)
          ? _screen.FormatDate(new DateTime(LocalTicks(_recorder.GetLogTime(entryIndex))))
          : string.Empty;
        return;
      }

      bool arrival = _recorder.IsLogArrival(entryIndex);
      entry.Find("Time").GetComponent<Text>().text = FormatClock(LocalTicks(_recorder.GetLogTime(entryIndex)));
      Transform arrivalMark = entry.Find("Arrival");
      Transform departureMark = entry.Find("Departure");
      arrivalMark.gameObject.SetActive(arrival);
      departureMark.gameObject.SetActive(!arrival);
      (arrival ? arrivalMark : departureMark).Find("Label").GetComponent<Text>().text =
        Translate(arrival ? "tablet.visitors.arrival" : "tablet.visitors.departure");
      entry.Find("Name").GetComponent<Text>().text = _recorder.GetName(_recorder.GetLogVisitor(entryIndex));
      int environment = _recorder.GetLogEnvironment(entryIndex);
      entry.Find("Mode").GetComponent<Image>().sprite = ModeIcon(environment);
      entry.Find("Environment").GetComponent<Text>().text = EnvironmentText(environment);
    }

    #endregion

    #region Text

    private string Translate(string key) => Utilities.IsValid(_screen) ? _screen.GetTranslation(key) : string.Empty;

    // Seconds into the record, as the viewer's local ticks.
    private long LocalTicks(int seconds) => _recorder.StartTicks + seconds * 10000000L + _utcOffsetTicks;

    private string FormatClock(long localTicks)
    {
      // Udon has no remainder for long.
      int minutes = (int)((localTicks - localTicks / TicksPerDay * TicksPerDay) / TicksPerMinute);
      return string.Format("{0:00}:{1:00}", minutes / 60, minutes % 60);
    }

    // The time, with the date in front when it is not today.
    private string FormatTime(int seconds)
    {
      long ticks = LocalTicks(seconds);
      string clock = FormatClock(ticks);
      if (ticks / TicksPerDay == DateTime.Now.Ticks / TicksPerDay) return clock;
      DateTime date = new DateTime(ticks);
      return string.Format(Translate("tablet.visitors.dateTime"), date.Month, date.Day, clock);
    }

    private Sprite ModeIcon(int environment)
    {
      if ((environment & TabletVisitorRecorder.EnvironmentVR) != 0) return _vrIcon;
      return Platform(environment) == TabletVisitorRecorder.PlatformPC ? _desktopIcon : _mobileIcon;
    }

    private int Platform(int environment) => (environment >> TabletVisitorRecorder.EnvironmentPlatformShift) & 0xFF;

    // "PC · Index": the platform, then the input. Product names stay as they
    // are; the rest is translated.
    private string EnvironmentText(int environment)
    {
      int platform = Platform(environment);
      string platformName = platform == TabletVisitorRecorder.PlatformAndroid ? "Android"
        : platform == TabletVisitorRecorder.PlatformIOS ? "iOS" : "PC";
      int input = (environment >> TabletVisitorRecorder.EnvironmentInputShift) & TabletVisitorRecorder.EnvironmentInputMask;
      return platformName + " · " + InputName(input, (environment & TabletVisitorRecorder.EnvironmentVR) != 0, platform);
    }

    // By VRCInputMethod.
    private string InputName(int input, bool vr, int platform)
    {
      switch (input)
      {
        case 0:
        case 1: return Translate("tablet.visitors.input.keyboard");
        case 2: return Translate("tablet.visitors.input.gamepad");
        case 5: return "Vive";
        // Meta's controllers: on Android a Quest; on PC a Rift, or a Quest
        // through Link, which the input does not tell apart.
        case 6: return platform == TabletVisitorRecorder.PlatformAndroid ? "Quest" : "Oculus";
        case 7: return "Vive XR";
        case 10: return "Index";
        case 11: return "WMR";
        case 12: return "OSC";
        case 13: return Translate("tablet.visitors.input.questHands");
        case 15: return Translate("tablet.visitors.input.touch");
        case 16: return "OpenXR";
        case 17: return "Pico";
        case 18: return "SteamVR";
        default: return vr ? "VR" : Translate("tablet.visitors.input.keyboard");
      }
    }

    #endregion
  }
}
