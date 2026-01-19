using System;
using UnityEngine;
using UdonSharp;
using VRC.SDKBase;

namespace Yamadev.YamaStream
{
  public partial class Controller
  {
    [SerializeField] private bool _mute;
    [SerializeField, Range(0f, 1f)] private float _volume;
    [SerializeField] private AudioSource[] _audioSources = new AudioSource[0];

    private void InitializeAudio()
    {
      UpdateAudioVolume();
      UpdateAudioPitch();
    }

    public AudioSource[] AudioSources
    {
      get => _audioSources;
      set => _audioSources = value;
    }

    public float Volume
    {
      get => _volume;
      set
      {
        _volume = Mathf.Clamp01(value);
        UpdateAudioVolume();
        PrintLog($"Volume changed to {_volume * 100}%.");
        foreach (YamaPlayerListener listener in EventListeners) listener.AfterVolumeChanged(_volume);
      }
    }

    public bool Mute
    {
      get => _mute;
      set
      {
        _mute = value;
        UpdateAudioVolume();
        PrintLog($"Mute changed to {_mute}.");
        foreach (YamaPlayerListener listener in EventListeners) listener.AfterMuteChanged(_mute);
      }
    }

    public void AddAudioSource(AudioSource audioSource)
    {
      if (Array.IndexOf(AudioSources, audioSource) >= 0) return;

      AudioSources = AudioSources.Add(audioSource);
      UpdateAudioVolume();
      UpdateAudioPitch();
    }

    private void UpdateAudioVolume()
    {
      foreach (AudioSource audioSource in AudioSources)
      {
        if (!Utilities.IsValid(audioSource)) continue;

        audioSource.volume = _volume;
        audioSource.mute = _mute;
      }
    }

    private void UpdateAudioPitch()
    {
      foreach (AudioSource audioSource in AudioSources)
      {
        if (!Utilities.IsValid(audioSource)) continue;
        audioSource.pitch = IsLive ? 1 : _speed;
      }
    }
  }
}