using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDKBase;

namespace Yamadev.YamaStream.UI
{
  // The search (issue #145): a row under the list's playlists opens a page
  // over the whole screen, as the settings are, with a box to type into and
  // the results as rows, which load the playlist they stand for when pressed.
  //
  // This is only the view. What is searched, and what a result is, belong to
  // the module that answers (_handler, wired when the world is built, as the
  // refresh handler is): it is handed what was entered and hands back rows --
  // the texts to show, and for each a key and a number that go back to it,
  // unread here, when the row is pressed. A UI the build gives no handler
  // keeps the row that opens the search hidden, and this does nothing.
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

    // Set by a result's row (its IndexTrigger) before it sends PickResult.
    [HideInInspector] public int pickedIndex = -1;

    public UIController Ui => _uiController;

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
      DrawResults();
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

    // From the handler: the line under the box (searching, how many, what
    // went wrong). Translated by the handler, whose words they are.
    public void ShowStatus(string status)
    {
      _handlerSpoke = true;
      if (Utilities.IsValid(_statusText)) _statusText.text = status == null ? string.Empty : status;
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
      DrawResults();
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
      }
    }

    private void FindLineParts()
    {
      int count = _resultsScroll.LineCount;
      var content = _resultsScroll.GetComponent<ScrollRect>().content;
      _lineTitles = new Text[count];
      _lineDetails = new Text[count];
      _lineCounts = new Text[count];
      _lineTriggers = new IndexTrigger[count];
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
      }
    }

    // A result's row was pressed: its key and number go back to the handler.
    // The results stay, so that another can be picked after it.
    public void PickResult()
    {
      if (!Utilities.IsValid(_handler) || pickedIndex < 0 || pickedIndex >= _resultCount) return;
      _handler.SetProgramVariable("searchSource", this);
      _handler.SetProgramVariable("searchPickKey", _keys[pickedIndex]);
      _handler.SetProgramVariable("searchPickNumber", _numbers[pickedIndex]);
      _handler.SendCustomEvent("OnSearchResultPicked");
    }
  }
}
