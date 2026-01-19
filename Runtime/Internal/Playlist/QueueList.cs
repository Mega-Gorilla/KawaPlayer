using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Yamadev.YamaStream
{
  [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
  public class QueueList : YamaPlayerListener
  {
    [SerializeField] private Controller _controller;
    [UdonSynced] private VideoPlayerType[] _videoPlayerTypes = new VideoPlayerType[0];
    [UdonSynced] private string[] _titles = new string[0];
    [UdonSynced] private VRCUrl[] _urls = new VRCUrl[0];
    private object[][] _tracks = new object[0][];

    private void Start()
    {
      if (!Utilities.IsValid(_controller))
      {
        PrintError($"Controller is not assigned to QueueList: {gameObject.name}");
        return;
      }
      _controller.AddListener(this);
    }

    private void GenerateTracks()
    {
      var trackCount = _urls.Length;
      _tracks = new object[trackCount][];
      for (int i = 0; i < trackCount; i++)
      {
        _tracks[i] = TrackUtils.NewTrack(_videoPlayerTypes[i], _titles[i], _urls[i]);
      }
    }

    public object[][] Tracks => _tracks;

    public int TrackCount => _tracks.Length;

    public object[] GetTrack(int index)
    {
      if (index < 0 || index >= TrackCount) return TrackUtils.CreateEmptyTrack();
      return _tracks[index];
    }

    public void AddTrack(object[] track)
    {
      int currentLength = _tracks.Length;
      object[][] newTracks = new object[currentLength + 1][];
      for (int i = 0; i < currentLength; i++)
      {
        newTracks[i] = _tracks[i];
      }
      newTracks[currentLength] = track;
      _tracks = newTracks;

      if (Networking.IsOwner(_controller.gameObject) && !_controller.IsLocal)
      {
        RequestSerialization();
      }
      _controller.SendCustomVideoEvent(nameof(AfterQueueUpdated));
    }

    public void RemoveTrack(int index)
    {
      if (index < 0 || index >= _tracks.Length) return;

      object[][] newTracks = new object[_tracks.Length - 1][];
      for (int i = 0, j = 0; i < _tracks.Length; i++)
      {
        if (i != index)
        {
          newTracks[j] = _tracks[i];
          j++;
        }
      }
      _tracks = newTracks;

      if (Networking.IsOwner(_controller.gameObject) && !_controller.IsLocal)
      {
        RequestSerialization();
      }
      _controller.SendCustomVideoEvent(nameof(AfterQueueUpdated));
    }

    public void MoveUp(int index)
    {
      if (index <= 0 || index >= _tracks.Length) return;

      object[] track = _tracks[index];
      _tracks[index] = _tracks[index - 1];
      _tracks[index - 1] = track;

      if (Networking.IsOwner(_controller.gameObject) && !_controller.IsLocal)
      {
        RequestSerialization();
      }
      _controller.SendCustomVideoEvent(nameof(AfterQueueUpdated));
    }

    public void MoveDown(int index)
    {
      if (index < 0 || index >= _tracks.Length - 1) return;

      object[] track = _tracks[index];
      _tracks[index] = _tracks[index + 1];
      _tracks[index + 1] = track;

      if (Networking.IsOwner(_controller.gameObject) && !_controller.IsLocal)
      {
        RequestSerialization();
      }
      _controller.SendCustomVideoEvent(nameof(AfterQueueUpdated));
    }

    public override void OnPreSerialization()
    {
      int trackCount = _tracks.Length;
      _videoPlayerTypes = new VideoPlayerType[trackCount];
      _titles = new string[trackCount];
      _urls = new VRCUrl[trackCount];

      for (int i = 0; i < trackCount; i++)
      {
        object[] track = _tracks[i];
        _videoPlayerTypes[i] = TrackUtils.GetPlayerType(track);
        _titles[i] = TrackUtils.GetTitle(track);
        _urls[i] = TrackUtils.GetUrl(track);
      }
    }

    public override void OnDeserialization()
    {
      GenerateTracks();
      _controller.SendCustomVideoEvent(nameof(AfterQueueUpdated));
    }

    public override void AfterOwnerChanged()
    {
      if (Networking.IsOwner(_controller.gameObject))
      {
        TakeOwnership();
      }
    }
  }
}