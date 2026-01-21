using UnityEngine;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components.Base;
using VRC.SDKBase;
using VRC.Udon.Common.Enums;

namespace Yamadev.YamaStream
{
  [RequireComponent(typeof(Renderer))]
  [RequireComponent(typeof(BaseVRCVideoPlayer))]
  [RequireComponent(typeof(Animator))]
  public sealed class BaseVideoPlayerHandler : PlayerHandler
  {
    [SerializeField] private string _textureName = "_MainTex";
    [SerializeField] private bool _useMaterial;
    [SerializeField] private Material _blitMaterial;
    private BaseVRCVideoPlayer _baseVideoPlayer;
    private Animator _animator;
    private Renderer _renderer;
    private MaterialPropertyBlock _properties;
    private Texture _videoTexture;
    private RenderTexture _blitTexture;
    private bool _stopped = true;

    private void Start()
    {
      _animator = GetComponent<Animator>();
      _animator.Rebind();
      _baseVideoPlayer = GetComponent<BaseVRCVideoPlayer>();
      _renderer = GetComponent<Renderer>();
      _properties = new MaterialPropertyBlock();
    }

    private void Update()
    {
      if (!Utilities.IsValid(_renderer) || _stopped || _loading) return;
      if (_useMaterial) _videoTexture = _renderer.sharedMaterial.GetTexture(_textureName);
      else
      {
        _renderer.GetPropertyBlock(_properties);
        _videoTexture = _properties.GetTexture(_textureName);
      }

      if (!Utilities.IsValid(_videoTexture))
      {
        if (Utilities.IsValid(_listener)) _listener.AfterTextureUpdated(null);
        return;
      }

#if UNITY_STANDALONE_WIN
      if (_type == VideoPlayerType.AVProVideoPlayer)
      {
        SendCustomEventDelayedFrames(nameof(BlitLastUpdate), 0, EventTiming.LateUpdate);
        return;
      }
#endif

      if (Utilities.IsValid(_listener)) _listener.AfterTextureUpdated(_videoTexture);
    }

    public override bool IsLoading
    {
      get
      {
        if (UseFallbackHandler) return _fallbackHandler.IsLoading;
        return _loading;
      }
    }

    public override bool IsPlaying
    {
      get
      {
        if (UseFallbackHandler) return _fallbackHandler.IsPlaying;

        if (!_baseVideoPlayer) return false;
        return _baseVideoPlayer.IsPlaying;
      }
    }

    public override bool IsPaused
    {
      get
      {
        if (UseFallbackHandler) return _fallbackHandler.IsPaused;

        if (_stopped || _loading) return false;
        return !IsPlaying;
      }
    }

    public override bool IsStopped
    {
      get
      {
        if (UseFallbackHandler) return _fallbackHandler.IsStopped;
        return _stopped;
      }
    }

    public override bool Loop
    {
      get
      {
        if (UseFallbackHandler) return _fallbackHandler.Loop;
        if (!_baseVideoPlayer) return false;
        return _baseVideoPlayer.Loop;
      }
      set
      {
        if (UseFallbackHandler)
        {
          _fallbackHandler.Loop = value;
          return;
        }
        if (!_baseVideoPlayer) return;
        _baseVideoPlayer.Loop = value;
      }
    }

    public override float Speed
    {
      get
      {
        if (UseFallbackHandler) return _fallbackHandler.Speed;
        return _speed;
      }
      set
      {
        if (UseFallbackHandler)
        {
          _fallbackHandler.Speed = value;
          return;
        }
        if (!Utilities.IsValid(_animator)) return;
        _speed = value;
        _animator.SetFloat("Speed", _speed);
        _animator.Update(0f);
      }
    }

    public override bool IsReady
    {
      get
      {
        if (UseFallbackHandler) return _fallbackHandler.IsReady;
        if (!Utilities.IsValid(_baseVideoPlayer)) return false;
        return _baseVideoPlayer.IsReady;
      }
    }

    public override int VideoWidth
    {
      get
      {
        if (UseFallbackHandler) return _fallbackHandler.VideoWidth;
        if (!Utilities.IsValid(_baseVideoPlayer)) return 0;
        return _baseVideoPlayer.VideoWidth;
      }
    }

    public override int VideoHeight
    {
      get
      {
        if (UseFallbackHandler) return _fallbackHandler.VideoHeight;
        if (!Utilities.IsValid(_baseVideoPlayer)) return 0;
        return _baseVideoPlayer.VideoHeight;
      }
    }

    public override int MaxResolution
    {
      set
      {
        if (UseFallbackHandler)
        {
          _fallbackHandler.MaxResolution = value;
          return;
        }
        if (!Utilities.IsValid(_animator)) return;
        _animator.SetFloat("Resolution", value / 4320f);
        _animator.Update(0f);
      }
    }

    public override float Time
    {
      get
      {
        if (UseFallbackHandler) return _fallbackHandler.Time;
        if (!Utilities.IsValid(_baseVideoPlayer)) return 0;
        return _baseVideoPlayer.GetTime();
      }
      set
      {
        if (UseFallbackHandler)
        {
          _fallbackHandler.Time = value;
          return;
        }
        if (!Utilities.IsValid(_baseVideoPlayer)) return;
        _baseVideoPlayer.SetTime(value);
      }
    }

    public override float Duration
    {
      get
      {
        if (UseFallbackHandler) return _fallbackHandler.Duration;
        if (!Utilities.IsValid(_baseVideoPlayer)) return 0;
        return _baseVideoPlayer.GetDuration();
      }
    }

    public override bool IsLive
    {
      get
      {
        if (UseFallbackHandler) return _fallbackHandler.IsLive;
        return float.IsInfinity(Duration);
      }
    }

    public override void PlayUrl(VRCUrl url)
    {
      if (UseFallbackHandler)
      {
        _fallbackHandler.PlayUrl(url);
        return;
      }
      if (!Utilities.IsValid(_baseVideoPlayer)) return;

      _baseVideoPlayer.PlayURL(url);
      _loadedUrl = url;
      _stopped = false;
      _loading = true;
    }

    public override void LoadUrl(VRCUrl url)
    {
      if (UseFallbackHandler)
      {
        _fallbackHandler.LoadUrl(url);
        return;
      }
      if (!Utilities.IsValid(_baseVideoPlayer)) return;

      _baseVideoPlayer.LoadURL(url);
      _loadedUrl = url;
      _stopped = false;
      _loading = true;
    }

    public override void Play()
    {
      if (UseFallbackHandler)
      {
        _fallbackHandler.Play();
        return;
      }
      if (_stopped || IsPlaying || !Utilities.IsValid(_baseVideoPlayer)) return;
      _baseVideoPlayer.Play();
      if (Utilities.IsValid(_listener)) _listener.AfterVideoPlayed();
    }

    public override void Pause()
    {
      if (UseFallbackHandler)
      {
        _fallbackHandler.Pause();
        return;
      }
      if (_stopped || !IsPlaying || !Utilities.IsValid(_baseVideoPlayer)) return;
      _baseVideoPlayer.Pause();
      if (Utilities.IsValid(_listener)) _listener.AfterVideoPaused();
    }

    public override void Stop()
    {
      if (UseFallbackHandler)
      {
        _fallbackHandler.Stop();
        return;
      }
      if (!Utilities.IsValid(_baseVideoPlayer)) return;

      _baseVideoPlayer.Stop();
      _loadedUrl = VRCUrl.Empty;
      _loading = false;
      _stopped = true;

      if (!Utilities.IsValid(_videoTexture) && Utilities.IsValid(_blitTexture))
      {
        _blitTexture.Release();
        _blitTexture = null;
      }

      if (Utilities.IsValid(_listener)) _listener.AfterTextureUpdated(null);
      if (Utilities.IsValid(_listener)) _listener.AfterVideoStopped();
    }

    public override Texture Texture
    {
      get
      {
        if (UseFallbackHandler) return _fallbackHandler.Texture;
        return _blitTexture ?? _videoTexture;
      }
    }

    public void BlitLastUpdate()
    {
      if (!Utilities.IsValid(_videoTexture))
      {
        if (Utilities.IsValid(_listener)) _listener.AfterTextureUpdated(null);
        return;
      }

      var width = _videoTexture.width;
      var height = _videoTexture.height;
      if (!Utilities.IsValid(_blitTexture) || _blitTexture.width != width || _blitTexture.height != height)
      {
        _blitTexture = VRCRenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB64, RenderTextureReadWrite.sRGB, 1);
        _blitTexture.filterMode = FilterMode.Bilinear;
        _blitTexture.wrapMode = TextureWrapMode.Clamp;
      }

      VRCGraphics.Blit(_videoTexture, _blitTexture, _blitMaterial);
      if (Utilities.IsValid(_listener)) _listener.AfterTextureUpdated(_blitTexture);
    }

    #region VRChat Video Events
    public override void OnVideoReady()
    {
      _loading = false;
      if (_stopped)
      {
        _baseVideoPlayer.Stop();
        return;
      }

      if (Utilities.IsValid(_listener)) _listener.AfterVideoReady();
    }

    public override void OnVideoStart()
    {
      if (Utilities.IsValid(_listener)) _listener.AfterVideoStarted();
    }

    public override void OnVideoEnd()
    {
      if (IsLive || Duration == 0) return;
      if (Utilities.IsValid(_listener)) _listener.AfterVideoEnded();
    }

    public override void OnVideoError(VideoError videoError)
    {
      _loading = false;
      _stopped = true;
      if (Utilities.IsValid(_listener)) _listener.AfterVideoErrorOccurred(videoError);
    }

    public override void OnVideoLoop()
    {
      if (Utilities.IsValid(_listener)) _listener.AfterVideoLooped();
    }
    #endregion
  }
}