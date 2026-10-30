using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace Yamadev.YamaStream.UI
{
  public partial class UIController
  {
    [Header("Playlist - Settings")]
    [SerializeField] private bool _defaultPlaylistOpen;

    [Header("Playlist - UI Components")]
    [SerializeField] private Text _currentPlaylistNameText;
    // Beside the name: how many tracks the open playlist has (issue #258).
    [SerializeField] private Text _currentPlaylistTrackCountText;
    [SerializeField] private LoopScroll _playlistsListScroll;
    [SerializeField] private LoopScroll _playlistTracksScroll;
    // The two halves of the list, where one replaces the other: the
    // playlists, and a playlist's tracks. A UI without the list (the
    // Controller Bar, the Global Reload Button) leaves them unwired.
    [SerializeField] private GameObject _playlistListPage;
    [SerializeField] private GameObject _playlistTracksPage;
    [SerializeField, RegisterEvent(nameof(Toggle.onValueChanged), nameof(GenerateQueueView))] private Toggle _queueTabToggle;
    [SerializeField, RegisterEvent(nameof(Toggle.onValueChanged), nameof(GenerateHistoryView))] private Toggle _historyTabToggle;
    [SerializeField, RegisterEvent(nameof(Toggle.onValueChanged), nameof(OpenPlaylistsTab))] private Toggle _playlistsTabToggle;

    [Header("Playlist - Actions")]
    // Back to the playlist list. Wired the way the delete and refresh
    // buttons beside it are, so the transition has one implementation
    // instead of one here and one in the prefab's UnityEvent.
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(ReturnToPlaylistList))] private Button _playlistReturnButton;
    // Dynamic playlist slots under this Controller (issue #92), wired at
    // build time by DynamicPlaylistBuildProcess. Lets the header tell a
    // runtime-filled playlist apart from one baked into the world.
    [SerializeField, HideInInspector] private DynamicPlaylist[] _dynamicPlaylists = new DynamicPlaylist[0];
    // Refetches the open playlist from wherever it came from (issue #91).
    // Wired at build time by the module that fills slots, the same way
    // _urlInterceptor is; core never learns what a VHub playlist is.
    [SerializeField, HideInInspector] private UdonSharpBehaviour _playlistRefreshHandler;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(RefreshPlaylist))] private Button _playlistRefreshButton;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(DeletePlaylist))] private Button _playlistDeleteButton;
    // The header over a playlist's tracks (back, name, count, "…") and the
    // strip of its actions under it (issue #258). Shown and hidden here
    // rather than by an animator, so that the list works the same wherever
    // it is placed: the screen and the Playlist Panel share it.
    [SerializeField] private GameObject _playlistHeader;
    [SerializeField] private GameObject _playlistActionStrip;
    // The "…" that opens that strip, shown only when one of its actions can
    // be used.
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(TogglePlaylistActions))] private Button _playlistActionsButton;
    // Shows the URL a playlist loaded in the world came from, to copy.
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(ShowPlaylistUrl))] private Button _playlistUrlButton;

    private int _playlistIndex = -1;
    private int _playlistTrackIndex = -1;
    // What the open confirmation dialog is about. An index alone is not an
    // identity: anyone can overwrite that slot while the dialog is up (LRU
    // replacement, or a reload into a slot freed a moment ago), and the
    // confirm would then delete a playlist the player never saw. Hold the
    // slot itself and the source it was showing, and re-check both.
    private int _pendingDeletePlaylistIndex = -1;
    private DynamicPlaylist _pendingDeleteSlot;
    private string _pendingDeleteSourceUrl = string.Empty;
    // The colour a track's title has while it is not playing: the one the
    // build gave the row (its role's colour, with the prefab's alpha), read
    // off the first title before any of them is recoloured (issue #163).
    private Color _trackTitleColor;
    private bool _trackTitleColorRead;
    // The parts of the lines, found once when the lines are first drawn
    // rather than again for every line drawn, which took about a tenth of
    // opening a playlist and a third of scrolling by a line (#326). A part
    // a line lacks stays null. A track's actions are TrackActionCount to a
    // line, in the order of FindTrackLineParts.
    private Text[] _trackLineTitles;
    private Text[] _trackLineUrls;
    private Text[] _trackLineNumbers;
    private GameObject[] _trackLineMarks;
    private GameObject[] _trackLineActions;
    private Animator[] _trackLineAnimators;
    private IndexTrigger[] _trackLineTriggers;
    private Text[] _playlistLineNames;
    private Text[] _playlistLineCounts;
    private IndexTrigger[] _playlistLineTriggers;
    private const int TrackActionCount = 6;

    private bool IsQueuePage => Utilities.IsValid(_queueTabToggle) && _queueTabToggle.isOn;
    private bool IsHistoryPage => Utilities.IsValid(_historyTabToggle) && _historyTabToggle.isOn;

    // Empty playlists are dynamic slots waiting to be filled (issue #88);
    // static ones are dropped at build time when they have no tracks
    // (PlaylistBuildProcess), so an empty entry is never something the
    // player should see. The list is rendered over visible playlists only,
    // while _playlistIndex stays a real index into Controller.Playlists so
    // every consumer below is unaffected.
    private int VisiblePlaylistCount()
    {
      var playlists = _controller.Playlists;
      int count = 0;
      for (int i = 0; i < playlists.Length; i++)
      {
        if (Utilities.IsValid(playlists[i]) && playlists[i].TrackCount > 0) count++;
      }
      return count;
    }

    // Playlists baked into the world come first, in hierarchy order, then
    // the dynamic ones in the order they were added (issue #128). Hierarchy
    // order alone put a new playlist wherever a slot happened to be free --
    // into the gap a deleted one left -- which read as the deleted playlist
    // turning into the new one. AddedOrder rather than Sequence: Sequence
    // moves on every refetch, and a refreshed row would jump to the bottom
    // under the player's finger.
    private int ToRealPlaylistIndex(int visibleIndex)
    {
      if (visibleIndex < 0) return -1;
      var playlists = _controller.Playlists;
      int seen = 0;
      for (int i = 0; i < playlists.Length; i++)
      {
        if (!Utilities.IsValid(playlists[i]) || playlists[i].TrackCount == 0) continue;
        if (Utilities.IsValid(FindDynamicPlaylist(i))) continue;
        if (seen == visibleIndex) return i;
        seen++;
      }

      // The slot whose rank in added order matches. Five slots in the stock
      // prefab, so counting the ones ahead of each beats sorting.
      int rank = visibleIndex - seen;
      for (int i = 0; i < _dynamicPlaylists.Length; i++)
      {
        int realIndex = ShownSlotIndex(_dynamicPlaylists[i]);
        if (realIndex < 0) continue;

        int ahead = 0;
        int order = _dynamicPlaylists[i].AddedOrder;
        for (int j = 0; j < _dynamicPlaylists.Length; j++)
        {
          if (j == i || ShownSlotIndex(_dynamicPlaylists[j]) < 0) continue;
          int other = _dynamicPlaylists[j].AddedOrder;
          if (other < order || (other == order && j < i)) ahead++;
        }
        if (ahead == rank) return realIndex;
      }
      return -1;
    }

    // Index in Controller.Playlists of a dynamic slot the list shows, or -1
    // when it is empty (or not one of the controller's playlists).
    private int ShownSlotIndex(DynamicPlaylist slot)
    {
      if (!Utilities.IsValid(slot)) return -1;
      var playlist = slot.Playlist;
      if (!Utilities.IsValid(playlist) || playlist.TrackCount == 0) return -1;
      return Array.IndexOf(_controller.Playlists, playlist);
    }

    // The dynamic slot backing a playlist, or null when the playlist is a
    // static one baked into the world. The slot count is small (five in the
    // stock prefab), so scanning beats keeping a parallel map in sync.
    private DynamicPlaylist FindDynamicPlaylist(int realIndex)
    {
      var playlists = _controller.Playlists;
      if (realIndex < 0 || realIndex >= playlists.Length) return null;

      var playlist = playlists[realIndex];
      for (int i = 0; i < _dynamicPlaylists.Length; i++)
      {
        var slot = _dynamicPlaylists[i];
        if (Utilities.IsValid(slot) && slot.Playlist == playlist) return slot;
      }
      return null;
    }

    // Per-playlist actions only apply to a filled dynamic slot the player is
    // actually looking at. Call this BEFORE the early returns in the three
    // view generators: leaving a tab fires that tab's toggle too, and those
    // generators bail out as soon as their page stops being the active one.
    //
    // A dialog surface is part of the requirement, not a nicety: deleting is
    // destructive and shared, so it has to be confirmed. PlaylistPanel ships
    // without a Modal of its own, so its buttons stay hidden until a world
    // creator points its _modalDialog at one.
    private void UpdatePlaylistActionButtons()
    {
      var slot = IsQueuePage || IsHistoryPage ? null : FindDynamicPlaylist(_playlistIndex);
      bool isDynamic = Utilities.IsValid(slot) && !slot.IsEmpty && Utilities.IsValid(_modalDialog);

      if (Utilities.IsValid(_playlistDeleteButton)) _playlistDeleteButton.gameObject.SetActive(isDynamic);
      if (Utilities.IsValid(_playlistRefreshButton))
        _playlistRefreshButton.gameObject.SetActive(isDynamic && Utilities.IsValid(_playlistRefreshHandler) && slot.CanRefresh);
      if (Utilities.IsValid(_playlistUrlButton)) _playlistUrlButton.gameObject.SetActive(isDynamic && slot.CanRefresh);
      // Delete shows with any of them, so the strip is never empty.
      if (Utilities.IsValid(_playlistActionsButton)) _playlistActionsButton.gameObject.SetActive(isDynamic);
    }

    // A playlist baked into the world keeps no URL at run time, so the
    // button shows only for one loaded in the world, whose source is the
    // URL it was loaded from (the VRChat URL VHub gives it).
    public void ShowPlaylistUrl()
    {
      var slot = FindDynamicPlaylist(_playlistIndex);
      if (!Utilities.IsValid(slot) || slot.IsEmpty || !slot.CanRefresh || !Utilities.IsValid(_modalDialog)) return;
      string title = Utilities.IsValid(_currentPlaylistNameText) ? _currentPlaylistNameText.text : string.Empty;
      if (!_modalDialog.TryShow(title, string.Empty, GetTranslation("button.close"), "", this, null, null)) return;
      _modalDialog.ShowCopyField(slot.SourceUrl.Get(), GetTranslation("button.copyUrl"));
    }

    public void RefreshPlaylist()
    {
      if (!Utilities.IsValid(_playlistRefreshHandler)) return;

      var slot = FindDynamicPlaylist(_playlistIndex);
      if (!Utilities.IsValid(slot) || !slot.CanRefresh) return;

      // Refetching is a load, so it answers to the same permission as one.
      if (!InvokeBeforeEvent("BeforeUserLoadPlaylist")) return;

      // Hand over the panel that asked, so the result modal opens here
      // rather than wherever a URL was last typed.
      _playlistRefreshHandler.SetProgramVariable("refreshUrl", slot.SourceUrl);
      _playlistRefreshHandler.SetProgramVariable("refreshSource", this);
      _playlistRefreshHandler.SendCustomEvent("OnPlaylistRefreshRequested");
    }

    // A slot the player is looking at can be emptied by anyone, on any
    // client, so this has to run wherever the playlist set changes rather
    // than only where the delete was pressed. Leaving the selection pointed
    // at the index would hand the detail view and the action buttons to
    // whatever refills the slot, without the player ever choosing it.
    //
    // Static playlists never reach zero tracks (PlaylistBuildProcess drops
    // empty ones at build time), so this only ever fires on a freed slot.
    private void ClearSelectionIfEmptied()
    {
      if (_playlistIndex < 0) return;

      var playlists = _controller.Playlists;
      if (_playlistIndex < playlists.Length)
      {
        var playlist = playlists[_playlistIndex];
        if (Utilities.IsValid(playlist) && playlist.TrackCount > 0) return;
      }

      _playlistIndex = -1;
      _playlistTrackIndex = -1;
      if (Utilities.IsValid(_currentPlaylistNameText)) _currentPlaylistNameText.text = string.Empty;
      if (Utilities.IsValid(_currentPlaylistTrackCountText)) _currentPlaylistTrackCountText.text = string.Empty;
      // GeneratePlaylistTracks returns early with no selection, so the rows
      // have to be dropped here. The queue and history pages feed the same
      // scroll and are redrawn right after, so leave them alone.
      if (!IsQueuePage && !IsHistoryPage && Utilities.IsValid(_playlistTracksScroll))
        _playlistTracksScroll.SetUp(0, this, nameof(UpdatePlaylistTracksContent));

      // What was being looked at is gone, so leaving the player on a detail
      // view with no name and no rows shows a playlist that no longer
      // exists (issue #113). Only when that view is what is on screen: the
      // queue and history pages borrow the same object, and the list page is
      // already where this would go.
      if (!IsQueuePage && !IsHistoryPage &&
          Utilities.IsValid(_playlistTracksPage) && _playlistTracksPage.activeSelf)
        ReturnToPlaylistList();
    }

    // The only way the panel goes from a track list back to the playlist
    // list. The return button is wired to it and the delete path calls it,
    // so pressing back and having the playlist taken away cannot end up
    // doing different things.
    //
    // What the button used to do from the prefab, minus a call whose target
    // was missing and whose method exists in no script.
    //
    // Deliberately about the view only. ClearSelectionIfEmptied has already
    // dropped the selection by the time it calls this, and leaving the index
    // alone is also what the button did before -- pressing back has never
    // cleared the selection.
    public void ReturnToPlaylistList()
    {
      if (!Utilities.IsValid(_playlistListPage)) return;
      if (!Utilities.IsValid(_playlistTracksPage)) return;

      _playlistTracksPage.SetActive(false);
      _playlistListPage.SetActive(true);
      HidePlaylistHeader();
    }

    // The playlist tab goes back to the playlist list the way the back button
    // does, so its page and the header never part. Pressed again while a
    // playlist's tracks show it reports on again (a toggle on in its group
    // still calls back); leaving it for the queue or history reports off,
    // which must leave their page alone.
    public void OpenPlaylistsTab()
    {
      if (!Utilities.IsValid(_playlistsTabToggle) || !_playlistsTabToggle.isOn) return;
      ReturnToPlaylistList();
    }

    // A playlist pressed in the list: its tracks, under its header with the
    // strip of its actions closed. The list's row calls this through its
    // IndexTrigger, after setting _playlistIndex. The tracks' page shows
    // before its rows are filled, as a row's animator cannot be told
    // anything while it is inactive.
    public void OpenPlaylist()
    {
      if (Utilities.IsValid(_playlistListPage)) _playlistListPage.SetActive(false);
      if (Utilities.IsValid(_playlistTracksPage)) _playlistTracksPage.SetActive(true);
      ShowPlaylistTracks(true);
      if (Utilities.IsValid(_playlistHeader)) _playlistHeader.SetActive(true);
      if (Utilities.IsValid(_playlistActionStrip)) _playlistActionStrip.SetActive(false);
    }

    public void TogglePlaylistActions()
    {
      if (!Utilities.IsValid(_playlistActionStrip)) return;
      _playlistActionStrip.SetActive(!_playlistActionStrip.activeSelf);
    }

    // Leaving a playlist's tracks -- back, the queue or history tab, or the
    // playlist taken away -- closes its header (name, count, "…") and the
    // strip of its actions. The tabs stay in view over the header (issue
    // #258), so a tab can be pressed while they show.
    private void HidePlaylistHeader()
    {
      if (Utilities.IsValid(_playlistHeader)) _playlistHeader.SetActive(false);
      if (Utilities.IsValid(_playlistActionStrip)) _playlistActionStrip.SetActive(false);
    }

    public void DeletePlaylist()
    {
      if (!InvokeBeforeEvent("BeforeUserDeletePlaylist")) return;

      var slot = FindDynamicPlaylist(_playlistIndex);
      if (!Utilities.IsValid(slot) || slot.IsEmpty) return;

      // No silent fallback: without a dialog there is no way to confirm, and
      // the button is hidden in that case anyway.
      if (!Utilities.IsValid(_modalDialog)) return;

      // Recorded only once the question is actually on screen. Refused --
      // another question is already up -- this would otherwise repoint the
      // delete at a playlist nobody was asked about.
      if (!_modalDialog.TryShow(
        GetTranslation("msg.confirmDeletePlaylist"),
        GetTranslation("msg.confirmDeletePlaylistDetail"),
        GetTranslation("button.cancel"),
        GetTranslation("button.remove"),
        this,
        null,
        nameof(DeletePlaylistInternal))) return;

      _pendingDeletePlaylistIndex = _playlistIndex;
      _pendingDeleteSlot = slot;
      _pendingDeleteSourceUrl = slot.CanRefresh ? slot.SourceUrl.Get() : string.Empty;
    }

    public void DeletePlaylistInternal()
    {
      int index = _pendingDeletePlaylistIndex;
      var expectedSlot = _pendingDeleteSlot;
      string expectedSourceUrl = _pendingDeleteSourceUrl;
      _pendingDeletePlaylistIndex = -1;
      _pendingDeleteSlot = null;
      _pendingDeleteSourceUrl = string.Empty;

      var slot = FindDynamicPlaylist(index);
      string currentSourceUrl = Utilities.IsValid(slot) && slot.CanRefresh ? slot.SourceUrl.Get() : string.Empty;

      // Anything other than the exact playlist the dialog was opened on is
      // left alone: the slot may have been refilled by someone else while
      // the dialog was up. Sequence is no help as an identity, because
      // deleting resets it to 0 and the same value comes back around.
      if (!Utilities.IsValid(slot) || slot != expectedSlot || slot.IsEmpty || currentSourceUrl != expectedSourceUrl)
      {
        GeneratePlaylistView();
        GeneratePlaylistTracks();
        return;
      }

      _controller.TakeOwnership();
      // Deleting the playlist that is playing leaves the current video
      // alone: stopping someone else playback as a side effect of a list
      // edit would be worse than the list going away. Dropping the indexes
      // makes Forward() return early, so playback simply does not advance
      // when the video ends.
      //
      // ClearPlaylistIndexes only writes the synced fields. Without pushing
      // them, everyone else keeps the old indexes, and once this slot is
      // refilled they would advance into a playlist nobody queued.
      if (_controller.ActivePlaylistIndex == index)
      {
        _controller.ClearPlaylistIndexes();
        if (!_controller.IsLocal) _controller.SyncVariables();
      }

      // Clear() raises AfterPlaylistsUpdated on every listener, on every
      // client, and that is what drops the selection here and elsewhere.
      slot.Clear();
    }

    public void GeneratePlaylistView()
    {
      if (!Utilities.IsValid(_playlistsListScroll)) return;
      _playlistsListScroll.SetUp(VisiblePlaylistCount(), this, nameof(UpdatePlaylistsContent));
    }

    public void UpdatePlaylistsContent()
    {
      if (_playlistLineNames == null || _playlistLineNames.Length != _playlistsListScroll.LineCount) FindPlaylistLineParts();
      for (int i = 0; i < _playlistsListScroll.LineCount; i++)
      {
        if (_playlistsListScroll.Indexes[i] == _playlistsListScroll.LastIndexes[i] || _playlistsListScroll.Indexes[i] == -1) continue;
        int realIndex = ToRealPlaylistIndex(_playlistsListScroll.Indexes[i]);
        if (realIndex < 0) continue;
        var playlist = _controller.Playlists[realIndex];

        var name = _playlistLineNames[i];
        if (Utilities.IsValid(name)) name.text = playlist.PlaylistName;
        var trackCount = _playlistLineCounts[i];
        if (Utilities.IsValid(trackCount))
          trackCount.text = playlist.TrackCount > 0 ? $"{GetTranslation("label.total")} {playlist.TrackCount} {GetTranslation("label.tracks")}" : string.Empty;
        var trigger = _playlistLineTriggers[i];
        if (Utilities.IsValid(trigger)) trigger.SetProgramVariable("_variableObject", realIndex);
      }
    }

    private void FindPlaylistLineParts()
    {
      int count = _playlistsListScroll.LineCount;
      var content = _playlistsListScroll.GetComponent<ScrollRect>().content;
      _playlistLineNames = new Text[count];
      _playlistLineCounts = new Text[count];
      _playlistLineTriggers = new IndexTrigger[count];
      for (int i = 0; i < count; i++)
      {
        var line = content.GetChild(i);
        _playlistLineNames[i] = TextAt(line, "Text");
        _playlistLineCounts[i] = TextAt(line, "TrackCount");
        _playlistLineTriggers[i] = line.GetComponent<IndexTrigger>();
      }
    }

    private void FindTrackLineParts()
    {
      int count = _playlistTracksScroll.LineCount;
      var content = _playlistTracksScroll.GetComponent<ScrollRect>().content;
      _trackLineTitles = new Text[count];
      _trackLineUrls = new Text[count];
      _trackLineNumbers = new Text[count];
      _trackLineMarks = new GameObject[count];
      _trackLineActions = new GameObject[count * TrackActionCount];
      _trackLineAnimators = new Animator[count];
      _trackLineTriggers = new IndexTrigger[count];
      for (int i = 0; i < count; i++)
      {
        var line = content.GetChild(i);
        var info = line.Find("Info");
        if (Utilities.IsValid(info))
        {
          _trackLineTitles[i] = TextAt(info, "Title");
          _trackLineUrls[i] = TextAt(info, "Url");
          _trackLineNumbers[i] = TextAt(info, "No");
          var mark = info.Find("PlayingMark");
          if (Utilities.IsValid(mark)) _trackLineMarks[i] = mark.gameObject;
        }
        var actions = line.Find("Actions");
        if (Utilities.IsValid(actions))
        {
          int at = i * TrackActionCount;
          _trackLineActions[at] = ObjectAt(actions, "Up");
          _trackLineActions[at + 1] = ObjectAt(actions, "Down");
          _trackLineActions[at + 2] = ObjectAt(actions, "Remove");
          _trackLineActions[at + 3] = ObjectAt(actions, "Copy");
          _trackLineActions[at + 4] = ObjectAt(actions, "Add");
          _trackLineActions[at + 5] = ObjectAt(actions, "Play");
        }
        _trackLineAnimators[i] = line.GetComponent<Animator>();
        _trackLineTriggers[i] = line.GetComponent<IndexTrigger>();
      }
    }

    private Text TextAt(Transform parent, string name)
    {
      var child = parent.Find(name);
      return Utilities.IsValid(child) ? child.GetComponent<Text>() : null;
    }

    private GameObject ObjectAt(Transform parent, string name)
    {
      var child = parent.Find(name);
      return Utilities.IsValid(child) ? child.gameObject : null;
    }

    // The queue and history tabs show their list from the top, drawn once.
    public void GenerateQueueView()
    {
      UpdatePlaylistActionButtons();
      if (!IsQueuePage || !Utilities.IsValid(_playlistTracksScroll)) return;
      HidePlaylistHeader();
      _playlistTracksScroll.SetUpAtTop(_controller.Queue.TrackCount, this, nameof(UpdatePlaylistTracksContent));
    }

    public void GenerateHistoryView()
    {
      UpdatePlaylistActionButtons();
      if (!IsHistoryPage || !Utilities.IsValid(_playlistTracksScroll)) return;
      HidePlaylistHeader();
      _playlistTracksScroll.SetUpAtTop(_controller.History.TrackCount, this, nameof(UpdatePlaylistTracksContent));
    }

    public void GeneratePlaylistsView()
    {
      if (!Utilities.IsValid(_playlistsTabToggle) || !_playlistsTabToggle.isOn || !Utilities.IsValid(_playlistsListScroll) || _playlistIndex < 0) return;
      _playlistsListScroll.SetUp(VisiblePlaylistCount(), this, nameof(UpdatePlaylistsContent));
    }

    // Redraws the tracks where they are scrolled to, as a change to their
    // list (the queue, the history or the playlist) needs.
    public void GeneratePlaylistTracks() => ShowPlaylistTracks(false);

    // fromTop: a playlist just opened, shown from its first track.
    private void ShowPlaylistTracks(bool fromTop)
    {
      UpdatePlaylistActionButtons();
      if (!Utilities.IsValid(_playlistsListScroll) || !Utilities.IsValid(_playlistTracksScroll)) return;
      if (!IsQueuePage && !IsHistoryPage && _playlistIndex < 0) return;

      int trackCount;
      if (IsQueuePage)
      {
        trackCount = _controller.Queue.TrackCount;
      }
      else if (IsHistoryPage)
      {
        trackCount = _controller.History.TrackCount;
      }
      else if (_playlistIndex >= 0 && _playlistIndex < _controller.Playlists.Length)
      {
        var playlist = _controller.Playlists[_playlistIndex];
        trackCount = playlist.TrackCount;

        if (Utilities.IsValid(_currentPlaylistNameText))
        {
          _currentPlaylistNameText.text = playlist.PlaylistName;
        }
        if (Utilities.IsValid(_currentPlaylistTrackCountText))
          _currentPlaylistTrackCountText.text = $"{playlist.TrackCount} {GetTranslation("label.tracks")}";
      }
      else return;

      if (fromTop) _playlistTracksScroll.SetUpAtTop(trackCount, this, nameof(UpdatePlaylistTracksContent));
      else _playlistTracksScroll.SetUp(trackCount, this, nameof(UpdatePlaylistTracksContent));
    }

    public void UpdatePlaylistTracksContent()
    {
      object[][] tracks;
      if (IsQueuePage)
      {
        tracks = _controller.Queue.Tracks;
      }
      else if (IsHistoryPage)
      {
        tracks = _controller.History.Tracks;
      }
      else if (_playlistIndex >= 0 && _playlistIndex < _controller.Playlists.Length)
      {
        tracks = _controller.Playlists[_playlistIndex].Tracks;
      }
      else return;

      if (_trackLineTitles == null || _trackLineTitles.Length != _playlistTracksScroll.LineCount) FindTrackLineParts();
      bool queuePage = IsQueuePage;
      bool historyPage = IsHistoryPage;
      // The URL action shows the track's URL in the dialog's copy field,
      // so a UI without a dialog (the Playlist Panel) does not offer it.
      bool urlAction = !queuePage && Utilities.IsValid(_modalDialog);
      for (int i = 0; i < _playlistTracksScroll.LineCount; i++)
      {
        int index = _playlistTracksScroll.Indexes[i];
        if (index == _playlistTracksScroll.LastIndexes[i] || index == -1) continue;
        if (index >= tracks.Length) continue;

        var track = tracks[index];
        var trackTitle = TrackUtils.GetTitle(track);
        var trackUrl = TrackUtils.GetUrl(track);
        bool isPlaying = !queuePage && !historyPage && _playlistIndex == _controller.ActivePlaylistIndex && index == _controller.PlayingTrackIndex;

        var title = _trackLineTitles[i];
        if (Utilities.IsValid(title))
        {
          if (!_trackTitleColorRead)
          {
            _trackTitleColor = title.color;
            _trackTitleColorRead = true;
          }
          title.text = string.IsNullOrEmpty(trackTitle) ? trackUrl.Get() : trackTitle;
          title.color = isPlaying ? _primaryColor : _trackTitleColor;
        }
        var urlText = _trackLineUrls[i];
        if (Utilities.IsValid(urlText)) urlText.text = string.IsNullOrEmpty(trackTitle) ? string.Empty : trackUrl.Get();
        var numberText = _trackLineNumbers[i];
        if (Utilities.IsValid(numberText))
        {
          numberText.text = $"{index + 1}";
          numberText.gameObject.SetActive(!isPlaying);
        }
        var playingMark = _trackLineMarks[i];
        if (Utilities.IsValid(playingMark)) playingMark.SetActive(isPlaying);

        int at = i * TrackActionCount;
        SetLineAction(at, queuePage);
        SetLineAction(at + 1, queuePage);
        SetLineAction(at + 2, queuePage);
        SetLineAction(at + 3, urlAction);
        SetLineAction(at + 4, !queuePage);
        SetLineAction(at + 5, !queuePage);

        var ani = _trackLineAnimators[i];
        if (Utilities.IsValid(ani)) ani.SetTrigger("Reset");
        var trigger = _trackLineTriggers[i];
        if (Utilities.IsValid(trigger)) trigger.SetProgramVariable("_variableObject", index);
      }
    }

    private void SetLineAction(int at, bool shown)
    {
      var action = _trackLineActions[at];
      if (Utilities.IsValid(action)) action.SetActive(shown);
    }

    public void RemoveFromQueue()
    {
      if (!InvokeBeforeEvent("BeforeUserRemoveTrackFromQueue")) return;
      if (!_playlistTracksScroll || _playlistTrackIndex < 0) return;

      _controller.Queue.TakeOwnership();
      if (_playlistTrackIndex < _controller.Queue.TrackCount) _controller.Queue.RemoveTrack(_playlistTrackIndex);
    }

    // The track whose actions were pressed, on the history or the open
    // playlist (the queue's actions name their index instead). Null when
    // there is none.
    private object[] SelectedTrack()
    {
      if (!_playlistTracksScroll || _playlistTrackIndex < 0) return null;
      if (IsHistoryPage) return _controller.History.GetTrack(_playlistTrackIndex);
      if (_playlistIndex >= 0 && _playlistIndex < _controller.Playlists.Length)
        return _controller.Playlists[_playlistIndex].GetTrack(_playlistTrackIndex);
      return null;
    }

    public void AddPlaylistTrackToQueue()
    {
      if (!InvokeBeforeEvent("BeforeUserAddTrackToQueue")) return;
      var track = SelectedTrack();
      if (track == null) return;

      _controller.TakeOwnership();
      _controller.Queue.AddTrack(track);
    }

    // Shows a track's URL in the dialog's copy field, as the strip under "…"
    // shows a playlist's: a world cannot open a link, but the viewer can copy
    // what the field holds.
    public void ShowPlaylistTrackUrl()
    {
      var track = SelectedTrack();
      if (track == null || !Utilities.IsValid(_modalDialog)) return;
      var url = TrackUtils.GetUrl(track);
      var title = TrackUtils.GetTitle(track);
      if (!_modalDialog.TryShow(string.IsNullOrEmpty(title) ? url.Get() : title, string.Empty, GetTranslation("button.close"), "", this, null, null)) return;
      _modalDialog.ShowCopyField(url.Get(), GetTranslation("button.copyUrl"));
    }

    public void MoveUp()
    {
      if (!InvokeBeforeEvent("BeforeUserMoveTrackUp")) return;
      _controller.TakeOwnership();
      _controller.Queue.MoveUp(_playlistTrackIndex);
    }

    public void MoveDown()
    {
      if (!InvokeBeforeEvent("BeforeUserMoveTrackDown")) return;
      _controller.TakeOwnership();
      _controller.Queue.MoveDown(_playlistTrackIndex);
    }

    public void PlayPlaylistTrack()
    {
      if (!InvokeBeforeEvent("BeforeUserPlayTrack")) return;
      if (!_playlistTracksScroll || _playlistTrackIndex < 0) return;

      if (IsHistoryPage)
      {
        _controller.TakeOwnership();
        _controller.PlayTrackFromHistory(_playlistTrackIndex);
        return;
      }

      if (_playlistIndex >= 0 && _playlistIndex < _controller.Playlists.Length)
      {
        _controller.TakeOwnership();
        _controller.PlayTrack(_controller.Playlists[_playlistIndex], _playlistTrackIndex);
        GeneratePlaylistTracks();
      }
    }
  }
}