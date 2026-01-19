using UdonSharp;
using UnityEngine;
using UnityEngine.Playables;
using VRC.SDKBase;

namespace Yamadev.YamaStream.Modules.TimelineSync
{
  [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
  public class TimelineSync : YamaPlayerModule
  {
    [SerializeField] private string[] _urls;
    [SerializeField] private PlayableDirector[] _timelines;

    private PlayableDirector _currentTimeline;

    public string[] Urls => _urls;
    public PlayableDirector[] Timelines => _timelines;

    private PlayableDirector GetTimelineForUrl(string url)
    {
      if (!Utilities.IsValid(_urls) || !Utilities.IsValid(_timelines)) return null;
      if (string.IsNullOrEmpty(url)) return null;

      for (int i = 0; i < _urls.Length; i++)
      {
        if (i >= _timelines.Length) break;
        if (_urls[i] == url) return _timelines[i];
      }
      return null;
    }

    private void PlayTimeline(float time)
    {
      if (!Utilities.IsValid(_currentTimeline)) return;

      if (!_currentTimeline.gameObject.activeSelf || !_currentTimeline.enabled)
      {
        _currentTimeline.gameObject.SetActive(true);
        _currentTimeline.enabled = true;
      }

      _currentTimeline.time = time;
      _currentTimeline.Play();
    }

    private void PauseTimeline(float time)
    {
      if (!Utilities.IsValid(_currentTimeline)) return;

      _currentTimeline.time = time;
      _currentTimeline.Pause();
    }

    private void SetTimelineTime(float time)
    {
      if (!Utilities.IsValid(_currentTimeline)) return;

      _currentTimeline.time = time;
    }

    private void StopTimeline()
    {
      if (!Utilities.IsValid(_currentTimeline)) return;

      _currentTimeline.time = 0f;
      _currentTimeline.Stop();
      _currentTimeline.gameObject.SetActive(false);
      _currentTimeline = null;
    }

    private void UpdateCurrentTimeline()
    {
      if (!Utilities.IsValid(_controller)) return;

      string url = TrackUtils.GetUrl(_controller.Track).Get();
      var timeline = GetTimelineForUrl(url);

      if (_currentTimeline != timeline)
      {
        StopTimeline();
        _currentTimeline = timeline;
      }
    }

    public override void AfterVideoReady()
    {
      UpdateCurrentTimeline();
      PlayTimeline(0f);
    }

    public override void AfterVideoLooped()
    {
      PlayTimeline(0f);
    }

    public override void AfterVideoPlayed()
    {
      if (!Utilities.IsValid(_controller)) return;
      PlayTimeline(_controller.VideoTime);
    }

    public override void AfterVideoPaused()
    {
      if (!Utilities.IsValid(_controller)) return;
      PauseTimeline(_controller.VideoTime);
    }

    public override void AfterVideoStopped()
    {
      StopTimeline();
    }

    public override void AfterTimeChanged(float time)
    {
      SetTimelineTime(time);
    }

    public override void AfterTrackLoaded()
    {
      if (!Utilities.IsValid(_controller)) return;

      string url = TrackUtils.GetUrl(_controller.Track).Get();
      var timeline = GetTimelineForUrl(url);

      if (_currentTimeline != timeline)
      {
        StopTimeline();
      }
    }
  }
}
