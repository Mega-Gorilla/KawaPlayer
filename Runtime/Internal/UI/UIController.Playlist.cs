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
    [SerializeField] private LoopScroll _playlistsListScroll;
    [SerializeField] private LoopScroll _playlistTracksScroll;
    // The two halves of the panel where one replaces the other. Not the
    // scrolls themselves: they are the same object as the page on the main
    // screen but sit inside it on the playlist panel, where hiding the
    // scroll would leave the header behind.
    //
    // Left unwired where both halves are on screen at once -- the playlist
    // panel puts the list and the tracks side by side, so there is nowhere
    // to go back to and nothing to switch.
    [SerializeField] private GameObject _playlistListPage;
    [SerializeField] private GameObject _playlistTracksPage;
    [SerializeField, RegisterEvent(nameof(Toggle.onValueChanged), nameof(GenerateQueueView))] private Toggle _queueTabToggle;
    [SerializeField, RegisterEvent(nameof(Toggle.onValueChanged), nameof(GenerateHistoryView))] private Toggle _historyTabToggle;
    [SerializeField] private Toggle _playlistsTabToggle;

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
    [SerializeField] private Text _playlistRefreshButtonLabel;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(DeletePlaylist))] private Button _playlistDeleteButton;
    [SerializeField] private Text _playlistDeleteButtonLabel;

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

    private int ToRealPlaylistIndex(int visibleIndex)
    {
      if (visibleIndex < 0) return -1;
      var playlists = _controller.Playlists;
      int seen = 0;
      for (int i = 0; i < playlists.Length; i++)
      {
        if (!Utilities.IsValid(playlists[i]) || playlists[i].TrackCount == 0) continue;
        if (seen == visibleIndex) return i;
        seen++;
      }
      return -1;
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
      // Takes the title bar -- name, delete, refresh -- away with it.
      if (Utilities.IsValid(_userUIAnimator)) _userUIAnimator.SetTrigger("HidePlaylistTitle");
    }

    public void DeletePlaylist()
    {
      if (!InvokeBeforeEvent("BeforeUserDeletePlaylist")) return;

      var slot = FindDynamicPlaylist(_playlistIndex);
      if (!Utilities.IsValid(slot) || slot.IsEmpty) return;

      // No silent fallback: without a dialog there is no way to confirm, and
      // the button is hidden in that case anyway.
      if (!Utilities.IsValid(_modalDialog)) return;

      _pendingDeletePlaylistIndex = _playlistIndex;
      _pendingDeleteSlot = slot;
      _pendingDeleteSourceUrl = slot.CanRefresh ? slot.SourceUrl.Get() : string.Empty;

      _modalDialog.Show(
        GetTranslation("msg.confirmDeletePlaylist"),
        GetTranslation("msg.confirmDeletePlaylistDetail"),
        GetTranslation("button.cancel"),
        GetTranslation("button.remove"),
        this,
        null,
        nameof(DeletePlaylistInternal));
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
      for (int i = 0; i < _playlistsListScroll.LineCount; i++)
      {
        if (_playlistsListScroll.Indexes[i] == _playlistsListScroll.LastIndexes[i] || _playlistsListScroll.Indexes[i] == -1) continue;
        int realIndex = ToRealPlaylistIndex(_playlistsListScroll.Indexes[i]);
        if (realIndex < 0) continue;
        var cell = _playlistsListScroll.GetComponent<ScrollRect>().content.GetChild(i);
        var playlist = _controller.Playlists[realIndex];

        var n = cell.Find("Text");
        if (Utilities.IsValid(n))
        {
          var name = n.GetComponent<Text>();
          if (Utilities.IsValid(name))
            name.text = playlist.PlaylistName;
        }

        var tr = cell.Find("TrackCount");
        if (Utilities.IsValid(tr))
        {
          var trackCount = tr.GetComponent<Text>();
          if (Utilities.IsValid(trackCount))
            trackCount.text = playlist.TrackCount > 0 ? $"{GetTranslation("label.total")} {playlist.TrackCount} {GetTranslation("label.tracks")}" : string.Empty;
        }

        var trigger = cell.GetComponent<IndexTrigger>();
        if (Utilities.IsValid(trigger)) trigger.SetProgramVariable("_variableObject", realIndex);
      }
    }

    public void GenerateQueueView()
    {
      UpdatePlaylistActionButtons();
      if (!IsQueuePage || !Utilities.IsValid(_playlistTracksScroll)) return;
      _playlistTracksScroll.SetUp(_controller.Queue.TrackCount, this, nameof(UpdatePlaylistTracksContent));
    }

    public void GenerateHistoryView()
    {
      UpdatePlaylistActionButtons();
      if (!IsHistoryPage || !Utilities.IsValid(_playlistTracksScroll)) return;
      _playlistTracksScroll.SetUp(_controller.History.TrackCount, this, nameof(UpdatePlaylistTracksContent));
    }

    public void GeneratePlaylistsView()
    {
      if (!Utilities.IsValid(_playlistsTabToggle) || !_playlistsTabToggle.isOn || !Utilities.IsValid(_playlistsListScroll) || _playlistIndex < 0) return;
      _playlistsListScroll.SetUp(VisiblePlaylistCount(), this, nameof(UpdatePlaylistsContent));
    }

    public void GeneratePlaylistTracks()
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
      }
      else return;

      _playlistTracksScroll.SetUp(trackCount, this, nameof(UpdatePlaylistTracksContent));
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

      for (int i = 0; i < _playlistTracksScroll.LineCount; i++)
      {
        if (_playlistTracksScroll.Indexes[i] == _playlistTracksScroll.LastIndexes[i] || _playlistTracksScroll.Indexes[i] == -1) continue;
        if (_playlistTracksScroll.Indexes[i] >= tracks.Length) continue;

        var track = tracks[_playlistTracksScroll.Indexes[i]];
        var trackTitle = TrackUtils.GetTitle(track);
        var trackUrl = TrackUtils.GetUrl(track);
        bool isPlaying = !IsQueuePage && !IsHistoryPage && _playlistIndex == _controller.ActivePlaylistIndex && _playlistTracksScroll.Indexes[i] == _controller.PlayingTrackIndex;

        var cell = _playlistTracksScroll.GetComponent<ScrollRect>().content.GetChild(i);
        var info = cell.Find("Info");
        if (Utilities.IsValid(info))
        {
          var ti = info.Find("Title");
          if (Utilities.IsValid(ti))
          {
            var title = ti.GetComponent<Text>();
            if (Utilities.IsValid(title))
            {
              title.text = string.IsNullOrEmpty(trackTitle) ? trackUrl.Get() : trackTitle;
              title.color = isPlaying ? _primaryColor : Color.white;
            }
          }
          var u = info.Find("Url");
          if (Utilities.IsValid(u))
          {
            var urlText = u.GetComponent<Text>();
            if (Utilities.IsValid(urlText))
              urlText.text = string.IsNullOrEmpty(trackTitle) ? string.Empty : trackUrl.Get();
          }
          var no = info.Find("No");
          if (Utilities.IsValid(no))
          {
            var numberText = no.GetComponent<Text>();
            if (Utilities.IsValid(numberText))
            {
              numberText.text = $"{_playlistTracksScroll.Indexes[i] + 1}";
              numberText.gameObject.SetActive(!isPlaying);
            }
          }
          var playingMark = info.Find("PlayingMark");
          if (Utilities.IsValid(playingMark)) playingMark.gameObject.SetActive(isPlaying);
        }
        var actions = cell.Find("Actions");
        if (Utilities.IsValid(actions))
        {
          var upMark = actions.Find("Up");
          if (Utilities.IsValid(upMark)) upMark.gameObject.SetActive(IsQueuePage);
          var downMark = actions.Find("Down");
          if (Utilities.IsValid(downMark)) downMark.gameObject.SetActive(IsQueuePage);
          var removeMark = actions.Find("Remove");
          if (Utilities.IsValid(removeMark)) removeMark.gameObject.SetActive(IsQueuePage);
          var copyUrl = actions.Find("Copy");
          if (Utilities.IsValid(copyUrl))
          {
            var urlTransform = copyUrl.Find("URL");
            if (Utilities.IsValid(urlTransform))
            {
              var trackUrlText = urlTransform.GetComponent<InputField>();
              if (Utilities.IsValid(trackUrlText))
              {
                copyUrl.gameObject.SetActive(!IsQueuePage);
                trackUrlText.text = trackUrl.Get();
              }
            }
          }
          var addMark = actions.Find("Add");
          if (Utilities.IsValid(addMark)) addMark.gameObject.SetActive(!IsQueuePage);
          var PlayMark = actions.Find("Play");
          if (Utilities.IsValid(PlayMark)) PlayMark.gameObject.SetActive(!IsQueuePage);
        }
        var ani = cell.GetComponent<Animator>();
        if (Utilities.IsValid(ani)) ani.SetTrigger("Reset");
        var trigger = cell.GetComponent<IndexTrigger>();
        if (Utilities.IsValid(trigger)) trigger.SetProgramVariable("_variableObject", _playlistTracksScroll.Indexes[i]);
      }
    }

    public void RemoveFromQueue()
    {
      if (!InvokeBeforeEvent("BeforeUserRemoveTrackFromQueue")) return;
      if (!_playlistTracksScroll || _playlistTrackIndex < 0) return;

      _controller.Queue.TakeOwnership();
      if (_playlistTrackIndex < _controller.Queue.TrackCount) _controller.Queue.RemoveTrack(_playlistTrackIndex);
    }

    public void AddPlaylistTrackToQueue()
    {
      if (!InvokeBeforeEvent("BeforeUserAddTrackToQueue")) return;
      if (!_playlistTracksScroll || _playlistTrackIndex < 0) return;

      object[] track;
      if (IsHistoryPage)
      {
        track = _controller.History.GetTrack(_playlistTrackIndex);
      }
      else if (_playlistIndex >= 0 && _playlistIndex < _controller.Playlists.Length)
      {
        track = _controller.Playlists[_playlistIndex].GetTrack(_playlistTrackIndex);
      }
      else return;

      _controller.TakeOwnership();
      _controller.Queue.AddTrack(track);
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