using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Yamadev.YamaStream.Modules.SlideShower
{
  [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
  public class SlideShower : YamaPlayerModule
  {
    [SerializeField, TranslationKey("module.slideshower.slideMode"), UdonSynced, FieldChangeCallback(nameof(SlideMode))] private bool _slideMode;
    [SerializeField, TranslationKey("module.slideshower.slideSeconds"), UdonSynced, FieldChangeCallback(nameof(SlideSeconds))] private int _slideSeconds = 1;
    private YamaPlayerListener[] _listeners = new YamaPlayerListener[0];
    private int _lastSetPageFrame = 0;

    public void AddListener(YamaPlayerListener listener)
    {
      if (!Utilities.IsValid(listener) || Array.IndexOf(_listeners, listener) >= 0) return;
      _listeners = _listeners.Add(listener);
    }

    public int SlidePage
    {
      get
      {
        if (!_slideMode || _controller.Stopped || _controller.IsLoading) return -1;
        return Mathf.FloorToInt(_controller.VideoTime) / _slideSeconds + 1;
      }
    }
    public int SlidePageCount
    {
      get
      {
        if (!_slideMode || _controller.Stopped || _controller.IsLoading) return -1;
        return Mathf.FloorToInt(_controller.Duration) / _slideSeconds;
      }
    }

    public bool SlideMode
    {
      get => _slideMode;
      set
      {
        if (_slideMode == value) return;

        _slideMode = value;
        if (_slideMode) _controller.Pause();
        if (Networking.IsOwner(_controller.gameObject) && !_controller.IsLocal) RequestSerialization();

        foreach (var listener in _listeners)
        {
          if (Utilities.IsValid(listener)) listener.SendCustomEvent("AfterSlideModeChanged");
        }
        PrintLog($"Slide mode changed to {_slideMode}.");
      }
    }

    public int SlideSeconds
    {
      get => _slideSeconds;
      set
      {
        if (_slideSeconds == value || value < 1) return;

        _slideSeconds = value;
        if (Networking.IsOwner(_controller.gameObject) && !_controller.IsLocal) RequestSerialization();

        foreach (var listener in _listeners)
        {
          if (Utilities.IsValid(listener)) listener.SendCustomEvent("AfterSlideSecondsChanged");
        }
        PrintLog($"Slide seconds changed to {_slideSeconds}.");
      }
    }

    public void SetSlidePage(int page)
    {
      if (!_slideMode || page < 1 || page > SlidePageCount || Time.frameCount == _lastSetPageFrame) return;

      _controller.SetTime(page * _slideSeconds - 0.5f);
      _lastSetPageFrame = Time.frameCount;
      PrintLog($"Set slide page to {page}.");
    }

    public void NextSlide()
    {
      if (!_slideMode) return;
      int nextPage = SlidePage + 1;
      if (nextPage <= SlidePageCount) SetSlidePage(nextPage);
    }

    public void PreviousSlide()
    {
      if (!_slideMode) return;
      int prevPage = SlidePage - 1;
      if (prevPage >= 1) SetSlidePage(prevPage);
    }

    public void FirstSlide()
    {
      if (!_slideMode) return;
      SetSlidePage(1);
    }

    public void LastSlide()
    {
      if (!_slideMode) return;
      SetSlidePage(SlidePageCount);
    }

    public void ToggleSlideMode()
    {
      SlideMode = !_slideMode;
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

    #region Video Events
    public override void AfterVideoReady()
    {
      foreach (var listener in _listeners)
      {
        if (Utilities.IsValid(listener)) listener.AfterVideoReady();
      }
    }

    public override void AfterVideoStarted()
    {
      if (_slideMode) _controller.Pause();
      foreach (var listener in _listeners)
      {
        if (Utilities.IsValid(listener)) listener.AfterVideoStarted();
      }
    }

    public override void AfterVideoEnded()
    {
      foreach (var listener in _listeners)
      {
        if (Utilities.IsValid(listener)) listener.AfterVideoEnded();
      }
    }

    public override void AfterVideoPlayed()
    {
      foreach (var listener in _listeners)
      {
        if (Utilities.IsValid(listener)) listener.AfterVideoPlayed();
      }
    }

    public override void AfterVideoPaused()
    {
      foreach (var listener in _listeners)
      {
        if (Utilities.IsValid(listener)) listener.AfterVideoPaused();
      }
    }

    public override void AfterVideoStopped()
    {
      foreach (var listener in _listeners)
      {
        if (Utilities.IsValid(listener)) listener.AfterVideoStopped();
      }
    }

    public override void AfterTrackLoaded()
    {
      foreach (var listener in _listeners)
      {
        if (Utilities.IsValid(listener)) listener.AfterTrackLoaded();
      }
    }

    public override void AfterTimeChanged(float time)
    {
      foreach (var listener in _listeners)
      {
        if (Utilities.IsValid(listener)) listener.AfterTimeChanged(time);
      }
    }

    public override void BeforeUserSetTime()
    {
      foreach (var listener in _listeners)
      {
        if (Utilities.IsValid(listener)) listener.BeforeUserSetTime();
      }
    }

    public override void BeforeUserBackward()
    {
      foreach (var listener in _listeners)
      {
        if (Utilities.IsValid(listener)) listener.BeforeUserBackward();
      }
    }

    public override void BeforeUserForward()
    {
      foreach (var listener in _listeners)
      {
        if (Utilities.IsValid(listener)) listener.BeforeUserForward();
      }
    }

    public void AfterLanguageChanged()
    {
      foreach (var listener in _listeners)
      {
        if (Utilities.IsValid(listener)) listener.SendCustomEvent("AfterLanguageChanged");
      }
    }
    #endregion
  }
}
