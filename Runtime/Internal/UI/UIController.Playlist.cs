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
    [SerializeField, RegisterEvent(nameof(Toggle.onValueChanged), nameof(GenerateQueueView))] private Toggle _queueTabToggle;
    [SerializeField, RegisterEvent(nameof(Toggle.onValueChanged), nameof(GenerateHistoryView))] private Toggle _historyTabToggle;
    [SerializeField] private Toggle _playlistsTabToggle;

    private int _playlistIndex = -1;
    private int _playlistTrackIndex = -1;

    public void GeneratePlaylistView()
    {
      if (!Utilities.IsValid(_playlistsListScroll)) return;
      _playlistsListScroll.SetUp(_controller.Playlists.Length, this, nameof(UpdatePlaylistsContent));
    }

    public void UpdatePlaylistsContent()
    {
      for (int i = 0; i < _playlistsListScroll.LineCount; i++)
      {
        if (_playlistsListScroll.Indexes[i] == _playlistsListScroll.LastIndexes[i] || _playlistsListScroll.Indexes[i] == -1) continue;
        var cell = _playlistsListScroll.GetComponent<ScrollRect>().content.GetChild(i);
        var playlist = _controller.Playlists[_playlistsListScroll.Indexes[i]];
        if (cell.TryFind("Text", out var n) && n.TryGetComponentLocal(out Text name))
          name.text = _controller.Playlists[_playlistsListScroll.Indexes[i]].PlaylistName;
        if (cell.TryFind("TrackCount", out var tr) && tr.TryGetComponentLocal(out Text trackCount))
          trackCount.text = playlist.TrackCount > 0 ? $"{GetTranslation("label.total")} {playlist.TrackCount} {GetTranslation("label.tracks")}" : string.Empty;
        if (cell.TryGetComponentLocal<IndexTrigger>(out var trigger)) trigger.SetProgramVariable("_variableObject", _playlistsListScroll.Indexes[i]);
      }
    }

    public void GenerateQueueView()
    {
      if (!Utilities.IsValid(_queueTabToggle) || !_queueTabToggle.isOn || !Utilities.IsValid(_playlistTracksScroll)) return;
      _playlistTracksScroll.SetUp(_controller.Queue.TrackCount, this, nameof(UpdatePlaylistTracksContent));
    }

    public void GenerateHistoryView()
    {
      if (!Utilities.IsValid(_historyTabToggle) || !_historyTabToggle.isOn || !Utilities.IsValid(_playlistTracksScroll)) return;
      _playlistTracksScroll.SetUp(_controller.History.TrackCount, this, nameof(UpdatePlaylistTracksContent));
    }

    public void GeneratePlaylistsView()
    {
      if (!Utilities.IsValid(_playlistsTabToggle) || !_playlistsTabToggle.isOn || !Utilities.IsValid(_playlistsListScroll) || _playlistIndex < 0) return;
      _playlistsListScroll.SetUp(_controller.Playlists.Length, this, nameof(UpdatePlaylistsContent));
    }

    public void GeneratePlaylistTracks()
    {
      if (!Utilities.IsValid(_playlistsListScroll) || !Utilities.IsValid(_playlistTracksScroll)) return;
      if (!_queueTabToggle.isOn && !_historyTabToggle.isOn && _playlistIndex < 0) return;

      int trackCount;
      if (_queueTabToggle.isOn)
      {
        trackCount = _controller.Queue.TrackCount;
      }
      else if (_historyTabToggle.isOn)
      {
        trackCount = _controller.History.TrackCount;
      }
      else if (_playlistIndex >= 0 && _playlistIndex < _controller.Playlists.Length)
      {
        trackCount = _controller.Playlists[_playlistIndex].TrackCount;
      }
      else return;

      if (Utilities.IsValid(_currentPlaylistNameText) && _playlistsTabToggle.isOn)
      {
        _currentPlaylistNameText.text = _controller.Playlists[_playlistIndex].PlaylistName;
      }

      _playlistTracksScroll.SetUp(trackCount, this, nameof(UpdatePlaylistTracksContent));
    }

    public void UpdatePlaylistTracksContent()
    {
      object[][] tracks;
      if (_queueTabToggle.isOn)
      {
        tracks = _controller.Queue.Tracks;
      }
      else if (_historyTabToggle.isOn)
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
        bool isPlaying = _playlistIndex == _controller.ActivePlaylistIndex && _playlistTracksScroll.Indexes[i] == _controller.PlayingTrackIndex;

        var cell = _playlistTracksScroll.GetComponent<ScrollRect>().content.GetChild(i);
        if (cell.TryFind("Info", out var info))
        {
          if (info.TryFind("Title", out var ti) && ti.TryGetComponentLocal(out Text title))
          {
            title.text = string.IsNullOrEmpty(trackTitle) ? trackUrl.Get() : trackTitle;
            title.color = isPlaying ? _primaryColor : Color.white;
          }
          if (info.TryFind("Url", out var u) && u.TryGetComponentLocal(out Text url))
            url.text = string.IsNullOrEmpty(trackTitle) ? string.Empty : trackUrl.Get();
          if (info.TryFind("No", out var no) && no.TryGetComponentLocal(out Text numberText))
          {
            numberText.text = $"{_playlistTracksScroll.Indexes[i] + 1}";
            numberText.gameObject.SetActive(!isPlaying);
          }
          if (info.TryFind("PlayingMark", out var playingMark)) playingMark.gameObject.SetActive(isPlaying);
        }
        if (cell.TryFind("Actions", out var actions))
        {
          if (actions.TryFind("Up", out var upMark)) upMark.gameObject.SetActive(_queueTabToggle.isOn);
          if (actions.TryFind("Down", out var downMark)) downMark.gameObject.SetActive(_queueTabToggle.isOn);
          if (actions.TryFind("Remove", out var removeMark)) removeMark.gameObject.SetActive(_queueTabToggle.isOn);
          if (actions.TryFind("Copy", out var copyUrl) && copyUrl.TryFind("URL", out var url) && url.TryGetComponentLocal<InputField>(out var trackUrlText))
          {
            copyUrl.gameObject.SetActive(!_queueTabToggle.isOn);
            trackUrlText.text = trackUrl.Get();
          }
          if (actions.TryFind("Add", out var addMark)) addMark.gameObject.SetActive(!_queueTabToggle.isOn);
          if (actions.TryFind("Play", out var PlayMark)) PlayMark.gameObject.SetActive(!_queueTabToggle.isOn);
        }
        if (cell.TryGetComponentLocal<Animator>(out var ani)) ani.SetTrigger("Reset");
        if (cell.TryGetComponentLocal<IndexTrigger>(out var trigger)) trigger.SetProgramVariable("_variableObject", _playlistTracksScroll.Indexes[i]);
      }
    }

    public void RemoveFromQueue()
    {
      if (!InvokeBeforeEvent(nameof(BeforeUserRemoveTrackFromQueue))) return;
      if (!_playlistTracksScroll || _playlistTrackIndex < 0) return;

      _controller.Queue.TakeOwnership();
      if (_playlistTrackIndex < _controller.Queue.TrackCount) _controller.Queue.RemoveTrack(_playlistTrackIndex);
    }

    public void AddPlaylistTrackToQueue()
    {
      if (!InvokeBeforeEvent(nameof(BeforeUserAddTrackToQueue))) return;
      if (!_playlistTracksScroll || _playlistTrackIndex < 0) return;

      object[] track;
      if (_historyTabToggle.isOn)
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
      if (!InvokeBeforeEvent(nameof(BeforeUserMoveTrackUp))) return;
      _controller.TakeOwnership();
      _controller.Queue.MoveUp(_playlistTrackIndex);
    }

    public void MoveDown()
    {
      if (!InvokeBeforeEvent(nameof(BeforeUserMoveTrackDown))) return;
      _controller.TakeOwnership();
      _controller.Queue.MoveDown(_playlistTrackIndex);
    }

    public void PlayPlaylistTrack()
    {
      if (!InvokeBeforeEvent(nameof(BeforeUserPlayTrack))) return;
      if (!_playlistTracksScroll || _playlistTrackIndex < 0) return;

      if (_historyTabToggle.isOn)
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