using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDKBase;

namespace Yamadev.YamaStream.UI
{
  // The search (issue #145): a row under the list's playlists opens a page
  // over the whole screen, as the settings are, with a box to type into and
  // the results as rows. A row pressed opens the playlist it stands for: its
  // tracks, with a button that adds it and one that goes back to the results.
  // A row pointed at shows two buttons of its own, icons as on the list's
  // rows: one that opens it as a press on the row does, and one that adds the
  // playlist without opening it. A result that is in the list already shows
  // a check, with no word to translate, and has no add button.
  //
  // This is only the view. What is searched, and what a result is, belong to
  // the module that answers (_handler, wired when the world is built, as the
  // refresh handler is): it is handed what was entered and hands back rows --
  // the texts to show, and for each a key and a number that go back to it,
  // unread here, when the row is pressed; then the playlist's tracks, and
  // whether it is added already. A UI the build gives no handler keeps the
  // row that opens the search hidden, and this does nothing.
  //
  // The page shares its place with the settings (Pages): the same animator
  // layer shows and hides both, the same back button closes both, and
  // whichever was asked for is the one switched on. On an object of its own
  // that stays on: the page is off until opened, and an Udon behaviour on an
  // object that is off takes no event.
  [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
  public class SearchPage : YamaPlayerBehaviour
  {
    [Header("Opening")]
    // The row under the list's playlists. Off in the list's prefab; the build
    // switches it on where it wires a handler.
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(Open))] private Button _entryButton;
    [SerializeField] private Text _entryLabel;
    // The controls' animator: its Page layer shows and hides the pages.
    [SerializeField] private Animator _animator;
    [SerializeField] private GameObject _page;
    // The settings, which the page stands in place of. Their button switches
    // them on itself (its own call in the prefab); this takes the search away.
    [SerializeField] private GameObject _settingsPage;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(ShowSettings))] private Button _settingsButton;
    // The pages' back button, which hides whichever page shows.
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(Closed))] private Button _returnButton;

    [Header("Search")]
    // What is typed into. It holds the handler's URL, which the entry is
    // typed after, since a world cannot make a URL from text; its own text
    // is not drawn, and _boxText stands for it: a hint, or what was searched.
    [SerializeField, RegisterEvent(nameof(VRCUrlInputField.onEndEdit), nameof(Submit))] private VRCUrlInputField _inputField;
    [SerializeField] private Text _boxText;
    // Under the box: how to search, then how the search went.
    [SerializeField] private Text _statusText;
    [SerializeField] private LoopScroll _resultsScroll;

    [Header("While waiting")]
    // In the middle of the page, in place of the rows, while the handler
    // fetches: waves that run (their own animator, which runs only while
    // this shows) and a word on what is fetched.
    [SerializeField] private GameObject _loading;
    [SerializeField] private Text _loadingText;

    [Header("A playlist opened from a result")]
    // In place of the results: the playlist's name, what its row said under
    // it and its track count, the two buttons, and its tracks.
    [SerializeField] private GameObject _playlist;
    [SerializeField] private Text _playlistTitle;
    [SerializeField] private Text _playlistDetail;
    [SerializeField] private Text _playlistCount;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(BackToResults))] private Button _backButton;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(AddPressed))] private Button _addButton;
    // The add button's icons: a plus, and a check once the playlist is added.
    [SerializeField] private GameObject _addIcon;
    [SerializeField] private GameObject _addedIcon;
    [SerializeField] private LoopScroll _tracksScroll;

    [SerializeField, HideInInspector] private UdonSharpBehaviour _handler;

    private UIController _uiController;
    private string _query = string.Empty;
    private string[] _titles = new string[0];
    private string[] _details = new string[0];
    private string[] _counts = new string[0];
    private string[] _keys = new string[0];
    private int[] _numbers = new int[0];
    private int _resultCount;
    private bool _handlerSpoke;
    // What shows under the box and the status line: the results, the ring,
    // or a playlist.
    private const int ShowsResults = 0;
    private const int ShowsLoading = 1;
    private const int ShowsPlaylist = 2;
    private int _shows = ShowsResults;
    // What the handler last said of the search, put back when a playlist is
    // left for the results.
    private string _searchStatus = string.Empty;
    // The playlist that is open: its key, which goes back to the handler
    // with the add button, its tracks, and whether it is added already.
    private string _playlistKey = string.Empty;
    private string _pickedDetail = string.Empty;
    private string[] _trackTitles = new string[0];
    private int _trackCount;
    private bool _added;
    private Text[] _trackLineNumbers;
    private Text[] _trackLineTitles;
    // How many frames the field has been waited for since the page opened,
    // and when to give up (the page never showed).
    private int _fieldWaits;
    private const int FieldWaitFrames = 600;
    // The parts of the result lines, found once (as the UIController finds
    // its lists' lines' parts, #326).
    private Text[] _lineTitles;
    private Text[] _lineDetails;
    private Text[] _lineCounts;
    private IndexTrigger[] _lineTriggers;
    // The two buttons a line shows while pointed at; the add button, and the
    // mark that stands for it on a result that is in the list already.
    private IndexTrigger[] _lineDetailsTriggers;
    private IndexTrigger[] _lineAddTriggers;
    private GameObject[] _lineAddButtons;
    private GameObject[] _lineAddedMarks;
    // Which results are in the list, as the handler last said.
    private bool[] _resultAdded = new bool[0];

    // Set by a result's row or one of its two buttons (their IndexTriggers)
    // before PickResult or AddResult.
    [HideInInspector] public int pickedIndex = -1;

    public UIController Ui => _uiController;
    // The results' keys, for the handler to say which are in the list.
    public string[] ResultKeys => _keys;
    public int ResultCount => _resultCount;
    // Whether the open playlist shows as added.
    public bool Added => _added;

    private void Start()
    {
      _uiController = GetComponentInParent<UIController>();
      if (!Utilities.IsValid(_uiController)) return;
      _uiController.AddListener(this);
      // The controls stay up while the box is typed into, as for the UI's
      // own URL fields.
      if (Utilities.IsValid(_handler)) _uiController.SetListEntryField(_inputField);
      UpdateTranslation();
    }

    public void AfterLanguageChanged() => UpdateTranslation();

    private void UpdateTranslation()
    {
      if (!Utilities.IsValid(_uiController)) return;
      if (Utilities.IsValid(_entryLabel)) _entryLabel.text = _uiController.GetTranslation("label.searchPlaylists");
      UpdateBoxText();
      UpdateHelp();
    }

    private void UpdateBoxText()
    {
      if (!Utilities.IsValid(_boxText) || !Utilities.IsValid(_uiController)) return;
      _boxText.text = string.IsNullOrEmpty(_query) ? _uiController.GetTranslation("label.searchHint") : _query;
    }

    // Until the handler has had something to say, the line under the box
    // says how to search: VRChat's input window shows the field's URL, and
    // what is typed has to come after it.
    private void UpdateHelp()
    {
      if (_handlerSpoke || !Utilities.IsValid(_statusText) || !Utilities.IsValid(_uiController)) return;
      _statusText.text = _uiController.GetTranslation("label.searchHelp");
    }

    private bool IsOpen => Utilities.IsValid(_page) && _page.activeSelf;

    // The row under the playlists was pressed: the page comes up the way the
    // settings do, with the list, the card and the bar out of its way.
    public void Open()
    {
      if (!Utilities.IsValid(_handler) || !Utilities.IsValid(_page) || !Utilities.IsValid(_animator)) return;
      if (Utilities.IsValid(_settingsPage)) _settingsPage.SetActive(false);
      _page.SetActive(true);
      _animator.SetTrigger("HidePlaylist");
      _animator.SetTrigger("HideFrontControl");
      _animator.SetTrigger("ShowPage");
      _animator.SetTrigger("HideVolume");
      _fieldWaits = 0;
      SendCustomEventDelayedFrames(nameof(_SetFieldWhenShown), 1);
      UpdateBoxText();
      UpdateHelp();
      // Opened on the results, whatever it was closed on: a fetch that was
      // under way has nothing to come back to, and a playlist left open
      // would show as added or not by what was true then.
      BackToResults();
      DrawResults();
    }

    // One of the three shows, the others are off.
    private void Show(int what)
    {
      _shows = what;
      if (Utilities.IsValid(_resultsScroll)) _resultsScroll.gameObject.SetActive(what == ShowsResults);
      if (Utilities.IsValid(_loading)) _loading.SetActive(what == ShowsLoading);
      if (Utilities.IsValid(_playlist)) _playlist.SetActive(what == ShowsPlaylist);
    }

    // The field gets its URL once it is really on. The pages' animator
    // switches their parent on some frames after the trigger, and a URL
    // given to a field that is off, or in the frame it comes on, is gone
    // when it shows: in a VRChat client the first entry then had no URL in
    // front of it. So this waits for the field, a frame at a time, and gives
    // the URL then and once more two frames on.
    public void _SetFieldWhenShown()
    {
      if (!IsOpen || !Utilities.IsValid(_inputField)) return;
      if (_inputField.gameObject.activeInHierarchy)
      {
        ResetField();
        SendCustomEventDelayedFrames(nameof(ResetField), 2);
        return;
      }
      _fieldWaits++;
      if (_fieldWaits < FieldWaitFrames) SendCustomEventDelayedFrames(nameof(_SetFieldWhenShown), 1);
    }

    // The settings' button was pressed: the search is not what shows.
    public void ShowSettings()
    {
      if (Utilities.IsValid(_page)) _page.SetActive(false);
    }

    // The pages' back button, which hides the page itself. From the search,
    // the list it was opened from comes back.
    public void Closed()
    {
      if (!IsOpen || !Utilities.IsValid(_animator)) return;
      _animator.ResetTrigger("HidePlaylist");
      _animator.SetTrigger("TogglePlaylist");
    }

    // The field is given its URL once the page has opened and after every
    // entry: a value set while the field is off, or as it comes on, is gone
    // when it shows, and what was typed stays in it otherwise.
    public void ResetField()
    {
      if (!Utilities.IsValid(_inputField) || !Utilities.IsValid(_handler)) return;
      var url = (VRCUrl)_handler.GetProgramVariable("searchFieldUrl");
      _inputField.SetUrl(Utilities.IsValid(url) ? url : VRCUrl.Empty);
    }

    // The field's entry ended: entered, or the keyboard closed.
    public void Submit()
    {
      if (Utilities.IsValid(_uiController)) _uiController.ListEntryEnded();
      if (!Utilities.IsValid(_inputField) || !Utilities.IsValid(_handler)) return;
      bool cancelled = _inputField.wasCanceled;
      var url = _inputField.GetUrl();
      ResetField();
      if (cancelled || !IsOpen) return;

      _handler.SetProgramVariable("searchSource", this);
      _handler.SetProgramVariable("searchInput", url);
      _handler.SendCustomEvent("OnSearchSubmitted");
    }

    // From the handler: what the box shows as searched. Empty brings the
    // hint back.
    public void ShowQuery(string query)
    {
      _query = query == null ? string.Empty : query;
      UpdateBoxText();
    }

    // From the handler: the line under the box on how the search went
    // (how many, what went wrong). Translated by the handler, whose words
    // they are. Kept, to be put back when a playlist is left.
    public void ShowStatus(string status)
    {
      _searchStatus = status == null ? string.Empty : status;
      ShowNote(_searchStatus);
    }

    // From the handler: the same line, for a word that is not the search's
    // result (about the playlist that is open, or one that would not open).
    public void ShowNote(string note)
    {
      _handlerSpoke = true;
      if (Utilities.IsValid(_statusText)) _statusText.text = note == null ? string.Empty : note;
    }

    // From the handler: it is fetching. The ring shows in place of the rows
    // with a word on what is fetched, and the line under the box is empty.
    public void ShowLoading(string message)
    {
      _handlerSpoke = true;
      if (Utilities.IsValid(_loadingText)) _loadingText.text = message == null ? string.Empty : message;
      if (Utilities.IsValid(_statusText)) _statusText.text = string.Empty;
      Show(ShowsLoading);
    }

    // From the handler: the rows. count of each array is used; keys and
    // numbers come back with a pressed row and are not read here.
    public void ShowResults(string[] titles, string[] details, string[] counts, string[] keys, int[] numbers, int count)
    {
      _titles = titles;
      _details = details;
      _counts = counts;
      _keys = keys;
      _numbers = numbers;
      _resultCount = count;
      Show(ShowsResults);
      DrawResults();
    }

    // From the handler: the playlist a pressed row stands for. key comes
    // back with the add button; added is whether the list has it already.
    public void ShowPlaylist(string title, string count, string[] trackTitles, int trackCount, string key, bool added)
    {
      _playlistKey = key == null ? string.Empty : key;
      _trackTitles = trackTitles;
      _trackCount = trackCount;
      if (Utilities.IsValid(_playlistTitle)) _playlistTitle.text = title == null ? string.Empty : title;
      if (Utilities.IsValid(_playlistDetail)) _playlistDetail.text = _pickedDetail;
      if (Utilities.IsValid(_playlistCount)) _playlistCount.text = count == null ? string.Empty : count;
      SetAdded(added);
      Show(ShowsPlaylist);
      if (Utilities.IsValid(_tracksScroll) && _tracksScroll.gameObject.activeInHierarchy)
        _tracksScroll.SetUpAtTop(_trackCount, this, nameof(UpdateTracksContent));
    }

    // From the handler: whether the open playlist is in the list. One that
    // is cannot be added again from here (the list refreshes it).
    public void SetAdded(bool added)
    {
      _added = added;
      if (Utilities.IsValid(_addButton)) _addButton.interactable = !added;
      if (Utilities.IsValid(_addIcon)) _addIcon.SetActive(!added);
      if (Utilities.IsValid(_addedIcon)) _addedIcon.SetActive(added);
    }

    // From the handler: which of the results are in the list, in their
    // order. Those show a mark, and no add button while pointed at.
    public void SetResultsAdded(bool[] added)
    {
      _resultAdded = added;
      DrawAddedMarks();
    }

    private void DrawAddedMarks()
    {
      if (_lineAddedMarks == null || !Utilities.IsValid(_resultsScroll)) return;
      for (int i = 0; i < _resultsScroll.LineCount && i < _lineAddedMarks.Length; i++)
      {
        int index = _resultsScroll.Indexes[i];
        if (index < 0 || index >= _resultCount) continue;
        bool added = index < _resultAdded.Length && _resultAdded[index];
        if (Utilities.IsValid(_lineAddedMarks[i])) _lineAddedMarks[i].SetActive(added);
        if (Utilities.IsValid(_lineAddButtons[i])) _lineAddButtons[i].SetActive(!added);
      }
    }

    // The playlist's back button, and a fetch that brought nothing: the
    // results show again as they were left, under what the search said.
    public void BackToResults()
    {
      if (_shows == ShowsResults) return;
      Show(ShowsResults);
      if (Utilities.IsValid(_statusText)) _statusText.text = _searchStatus;
      if (Utilities.IsValid(_uiController)) _uiController.TouchList();
    }

    // The playlist's add button: its key goes back to the handler, which
    // adds what it fetched to show it.
    public void AddPressed()
    {
      if (!Utilities.IsValid(_handler) || _shows != ShowsPlaylist || string.IsNullOrEmpty(_playlistKey)) return;
      _handler.SetProgramVariable("searchSource", this);
      _handler.SetProgramVariable("searchPickKey", _playlistKey);
      _handler.SendCustomEvent("OnSearchAddPressed");
    }

    // The world's playlists changed (from the UIController): the handler
    // says again which results are among them and, with its key, whether the
    // open playlist is, as one may have been added from here after a
    // question, by someone else, or deleted.
    public void AfterPlaylistsUpdated()
    {
      if (!Utilities.IsValid(_handler)) return;
      _handler.SetProgramVariable("searchSource", this);
      _handler.SetProgramVariable("searchPickKey", _shows == ShowsPlaylist ? _playlistKey : string.Empty);
      _handler.SendCustomEvent("OnSearchPlaylistsChanged");
    }

    // Called by the tracks' LoopScroll when its lines are drawn.
    public void UpdateTracksContent()
    {
      if (Utilities.IsValid(_uiController)) _uiController.TouchList();
      if (_trackLineTitles == null || _trackLineTitles.Length != _tracksScroll.LineCount) FindTrackLineParts();
      for (int i = 0; i < _tracksScroll.LineCount; i++)
      {
        int index = _tracksScroll.Indexes[i];
        if (index == _tracksScroll.LastIndexes[i] || index == -1 || index >= _trackCount) continue;
        if (Utilities.IsValid(_trackLineNumbers[i])) _trackLineNumbers[i].text = (index + 1).ToString();
        if (Utilities.IsValid(_trackLineTitles[i])) _trackLineTitles[i].text = _trackTitles[index];
      }
    }

    private void FindTrackLineParts()
    {
      int count = _tracksScroll.LineCount;
      var content = _tracksScroll.GetComponent<ScrollRect>().content;
      _trackLineNumbers = new Text[count];
      _trackLineTitles = new Text[count];
      for (int i = 0; i < count && i < content.childCount; i++)
      {
        Transform line = content.GetChild(i);
        Transform number = line.Find("Number");
        if (Utilities.IsValid(number)) _trackLineNumbers[i] = number.GetComponent<Text>();
        Transform title = line.Find("Title");
        if (Utilities.IsValid(title)) _trackLineTitles[i] = title.GetComponent<Text>();
      }
    }

    // From the first row. Results that come while the page is closed are
    // drawn when it opens: a scroll that is off cannot lay its lines out.
    private void DrawResults()
    {
      if (!Utilities.IsValid(_resultsScroll) || !_resultsScroll.gameObject.activeInHierarchy) return;
      _resultsScroll.SetUpAtTop(_resultCount, this, nameof(UpdateResultsContent));
    }

    // Called by the results' LoopScroll when its lines are drawn.
    public void UpdateResultsContent()
    {
      if (Utilities.IsValid(_uiController)) _uiController.TouchList();
      if (_lineTitles == null || _lineTitles.Length != _resultsScroll.LineCount) FindLineParts();
      for (int i = 0; i < _resultsScroll.LineCount; i++)
      {
        int index = _resultsScroll.Indexes[i];
        if (index == _resultsScroll.LastIndexes[i] || index == -1 || index >= _resultCount) continue;
        if (Utilities.IsValid(_lineTitles[i])) _lineTitles[i].text = _titles[index];
        if (Utilities.IsValid(_lineDetails[i])) _lineDetails[i].text = _details[index];
        if (Utilities.IsValid(_lineCounts[i])) _lineCounts[i].text = _counts[index];
        if (Utilities.IsValid(_lineTriggers[i])) _lineTriggers[i].SetProgramVariable("_variableObject", index);
        if (Utilities.IsValid(_lineDetailsTriggers[i])) _lineDetailsTriggers[i].SetProgramVariable("_variableObject", index);
        if (Utilities.IsValid(_lineAddTriggers[i])) _lineAddTriggers[i].SetProgramVariable("_variableObject", index);
      }
      DrawAddedMarks();
    }

    private void FindLineParts()
    {
      int count = _resultsScroll.LineCount;
      var content = _resultsScroll.GetComponent<ScrollRect>().content;
      _lineTitles = new Text[count];
      _lineDetails = new Text[count];
      _lineCounts = new Text[count];
      _lineTriggers = new IndexTrigger[count];
      _lineDetailsTriggers = new IndexTrigger[count];
      _lineAddTriggers = new IndexTrigger[count];
      _lineAddButtons = new GameObject[count];
      _lineAddedMarks = new GameObject[count];
      for (int i = 0; i < count && i < content.childCount; i++)
      {
        Transform line = content.GetChild(i);
        Transform title = line.Find("Title");
        if (Utilities.IsValid(title)) _lineTitles[i] = title.GetComponent<Text>();
        Transform detail = line.Find("Detail");
        if (Utilities.IsValid(detail)) _lineDetails[i] = detail.GetComponent<Text>();
        Transform trackCount = line.Find("TrackCount");
        if (Utilities.IsValid(trackCount)) _lineCounts[i] = trackCount.GetComponent<Text>();
        _lineTriggers[i] = line.GetComponent<IndexTrigger>();
        Transform details = line.Find("Actions/Details");
        if (Utilities.IsValid(details)) _lineDetailsTriggers[i] = details.GetComponent<IndexTrigger>();
        Transform add = line.Find("Actions/Add");
        if (Utilities.IsValid(add))
        {
          _lineAddTriggers[i] = add.GetComponent<IndexTrigger>();
          _lineAddButtons[i] = add.gameObject;
        }
        Transform mark = line.Find("Added");
        if (Utilities.IsValid(mark)) _lineAddedMarks[i] = mark.gameObject;
      }
    }

    // A result's row was pressed: its key and number go back to the handler,
    // which fetches the playlist and shows it (ShowPlaylist). The results
    // are kept, to go back to.
    public void PickResult()
    {
      if (!Utilities.IsValid(_handler) || pickedIndex < 0 || pickedIndex >= _resultCount) return;
      _pickedDetail = _details[pickedIndex];
      _handler.SetProgramVariable("searchSource", this);
      _handler.SetProgramVariable("searchPickKey", _keys[pickedIndex]);
      _handler.SetProgramVariable("searchPickNumber", _numbers[pickedIndex]);
      _handler.SendCustomEvent("OnSearchResultPicked");
    }

    // A result's own add button: the same goes back, for the handler to add
    // the playlist without showing it.
    public void AddResult()
    {
      if (!Utilities.IsValid(_handler) || pickedIndex < 0 || pickedIndex >= _resultCount) return;
      _handler.SetProgramVariable("searchSource", this);
      _handler.SetProgramVariable("searchPickKey", _keys[pickedIndex]);
      _handler.SetProgramVariable("searchPickNumber", _numbers[pickedIndex]);
      _handler.SendCustomEvent("OnSearchResultAddPicked");
    }
  }
}
