using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace Yamadev.YamaStream
{
  // Instance-lifetime playlist slot (issue #88). Playlist itself is unsynced
  // and its tracks come from build-time serialized fields; this behaviour
  // carries the synced payload for a slot that is filled at runtime and
  // pushes the result into the child Playlist via SetTracks, so Playlist
  // stays untouched and every existing playlist code path (UI listing,
  // PlayTrack, Forward's wrap-around) works on a filled slot unchanged.
  //
  // Deliberately generic: it knows nothing about VHub or the redirect pool.
  // A module (PlaylistLoader) decides what goes in, the same way UIController
  // holds a generic _urlInterceptor slot that a module wires up.
  //
  // The Playlist lives on a CHILD GameObject, not this one. UdonSharpGUI
  // flags any GameObject that mixes a Manual-sync behaviour with one that is
  // not NoVariableSync as a conflicting-sync error, and Playlist is
  // BehaviourSyncMode.None, so co-locating them would show a red error in
  // every world creator's inspector even though the runtime setter skips
  // None-sync behaviours.
  [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
  public class DynamicPlaylist : YamaPlayerListener
  {
    [SerializeField] private Controller _controller;
    [SerializeField] private Playlist _playlist;
    [UdonSynced] private string _playlistName = string.Empty;
    // Where the contents came from, kept so the slot can be re-fetched and so
    // reloading the same source reuses its slot instead of consuming a fresh
    // one. Stored as the URL rather than a derived key because a string
    // cannot be turned back into a VRCUrl at runtime; whoever fills the slot
    // derives whatever comparison key it needs. Only meaningful while the
    // slot holds tracks -- see CanRefresh.
    [UdonSynced] private VRCUrl _sourceUrl = VRCUrl.Empty;
    // Fill order, assigned by the caller. 0 means never filled, which makes
    // empty slots sort first when picking the least recently filled one.
    [UdonSynced] private int _sequence = 0;
    [UdonSynced] private VideoPlayerType[] _videoPlayerTypes = new VideoPlayerType[0];
    [UdonSynced] private string[] _titles = new string[0];
    [UdonSynced] private VRCUrl[] _urls = new VRCUrl[0];
    [UdonSynced] private byte[] _extensionsBlob = new byte[0];
    [UdonSynced] private int[] _extensionOffsets = new int[0];
    private object[][] _tracks = new object[0][];

    private void Start()
    {
      if (!Utilities.IsValid(_controller))
      {
        PrintError($"Controller is not assigned to DynamicPlaylist: {gameObject.name}");
        return;
      }
      if (!Utilities.IsValid(_playlist))
      {
        PrintError($"Playlist is not assigned to DynamicPlaylist: {gameObject.name}");
        return;
      }
      _controller.AddListener(this);
    }

    public string PlaylistName => _playlistName;

    public VRCUrl SourceUrl => _sourceUrl;

    // An empty slot's _sourceUrl is meaningless, and reading it is actively
    // unsafe in ClientSim, where VRCUrl.Empty.Get() returns a stale URL from
    // elsewhere in the session. Gate every read of SourceUrl on this.
    public bool CanRefresh => _tracks.Length > 0 && Utilities.IsValid(_sourceUrl) && !string.IsNullOrEmpty(_sourceUrl.Get());

    public int Sequence => _sequence;

    public Playlist Playlist => _playlist;

    public int TrackCount => _tracks.Length;

    public bool IsEmpty => _tracks.Length == 0;

    // Replaces the slot contents. The caller owns sequencing so that a single
    // load can compare every slot before deciding which one to overwrite.
    public void Fill(VRCUrl sourceUrl, string playlistName, object[][] tracks, int sequence)
    {
      if (!Utilities.IsValid(_playlist)) return;

      TakeOwnership();

      _sourceUrl = Utilities.IsValid(sourceUrl) ? sourceUrl : VRCUrl.Empty;
      _playlistName = playlistName == null ? string.Empty : playlistName;
      _sequence = sequence;
      _tracks = tracks == null ? new object[0][] : tracks;

      if (Networking.IsOwner(_controller.gameObject) && !_controller.IsLocal)
      {
        RequestSerialization();
      }
      ApplyLocal();
    }

    // Empties the slot so it can be reused. Resetting _sequence matters as
    // much as clearing the tracks: a slot that keeps a high sequence would
    // sort last when the filler looks for somewhere to put a new playlist,
    // even though it is now the obvious place to use.
    public void Clear()
    {
      if (!Utilities.IsValid(_playlist)) return;

      TakeOwnership();

      _sourceUrl = VRCUrl.Empty;
      _playlistName = string.Empty;
      _sequence = 0;
      _tracks = new object[0][];

      if (Networking.IsOwner(_controller.gameObject) && !_controller.IsLocal)
      {
        RequestSerialization();
      }
      ApplyLocal();
    }

    // Pushes the current tracks into the child Playlist and lets the UI
    // know the playlist set changed. Called on the filling client directly
    // and on every other client from OnDeserialization.
    private void ApplyLocal()
    {
      _playlist.SetTracks(_playlistName, _tracks);
      _controller.SendCustomVideoEvent(nameof(AfterPlaylistsUpdated));
    }

    private void GenerateTracks()
    {
      var trackCount = _urls.Length;
      bool extensionsValid = _extensionsBlob != null && _extensionOffsets != null && _extensionOffsets.Length == trackCount + 1;
      _tracks = new object[trackCount][];
      for (int i = 0; i < trackCount; i++)
      {
        byte[] extension = null;
        if (extensionsValid)
        {
          int start = _extensionOffsets[i];
          int end = _extensionOffsets[i + 1];
          if (start >= 0 && end > start && end <= _extensionsBlob.Length)
          {
            extension = new byte[end - start];
            Buffer.BlockCopy(_extensionsBlob, start, extension, 0, extension.Length);
          }
        }
        _tracks[i] = extension == null
          ? TrackUtils.NewTrack(_videoPlayerTypes[i], _titles[i], _urls[i])
          : TrackUtils.NewTrackWithExtension(_videoPlayerTypes[i], _titles[i], _urls[i], extension);
      }
    }

    public override void OnPreSerialization()
    {
      int trackCount = _tracks.Length;
      _videoPlayerTypes = new VideoPlayerType[trackCount];
      _titles = new string[trackCount];
      _urls = new VRCUrl[trackCount];
      _extensionOffsets = new int[trackCount + 1];

      int totalLength = 0;
      for (int i = 0; i < trackCount; i++)
      {
        object[] track = _tracks[i];
        _videoPlayerTypes[i] = TrackUtils.GetPlayerType(track);
        _titles[i] = TrackUtils.GetTitle(track);
        _urls[i] = TrackUtils.GetUrl(track);
        totalLength += TrackUtils.GetExtension(track).Length;
      }

      _extensionsBlob = new byte[totalLength];
      int cursor = 0;
      for (int i = 0; i < trackCount; i++)
      {
        _extensionOffsets[i] = cursor;
        byte[] extension = TrackUtils.GetExtension(_tracks[i]);
        Buffer.BlockCopy(extension, 0, _extensionsBlob, cursor, extension.Length);
        cursor += extension.Length;
      }
      _extensionOffsets[trackCount] = cursor;
    }

    public override void OnDeserialization()
    {
      GenerateTracks();
      ApplyLocal();
    }

    // A playlist is by far the largest thing this player syncs, and Udon's
    // world-wide bandwidth is shared, so record what each send actually cost
    // and make a rejected payload visible instead of silently leaving remote
    // clients with the previous contents.
    public override void OnPostSerialization(SerializationResult result)
    {
      if (!result.success)
      {
        PrintError($"Failed to sync playlist '{_playlistName}' ({_tracks.Length} tracks, {result.byteCount} bytes).");
        return;
      }
      PrintLog($"Synced playlist '{_playlistName}': {_tracks.Length} tracks, {result.byteCount} bytes.");
    }

    public override void TakeOwnership()
    {
      base.TakeOwnership();
      if (Utilities.IsValid(_controller)) _controller.TakeOwnership();
    }

    public override void AfterOwnerChanged()
    {
      if (Utilities.IsValid(_controller) && Networking.IsOwner(_controller.gameObject))
      {
        TakeOwnership();
      }
    }
  }
}
