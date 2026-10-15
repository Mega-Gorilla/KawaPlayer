using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using VRC.SDKBase;
using Object = UnityEngine.Object;

namespace Yamadev.YamaStream
{
  public enum ScreenType
  {
    Renderer,
    RawImage,
    Material,
    Unknown
  }

  public partial class Controller
  {
    [SerializeField] private int _maxResolution;
    [SerializeField, FormerlySerializedAs("_mirrorInverse")] private bool _mirrorFlip = true;
    [SerializeField, FormerlySerializedAs("_emission"), Range(0f, 1f)] private float _brightness = 1f;
    [SerializeField, HideInInspector] private ScreenType[] _screenTypes;
    [SerializeField, HideInInspector] private Object[] _screens;
    [SerializeField, HideInInspector] private string[] _textureProperties;
    private MaterialPropertyBlock _propertyBlock;
    // Given to a renderer screen while there is no video texture, to take off
    // what _propertyBlock put there. It is never filled, so one serves every
    // frame; a new one each frame while a video started was garbage (issue #195).
    private MaterialPropertyBlock _emptyPropertyBlock;

    private void InitializeScreen()
    {
      if (_screenTypes.Length != ScreenCount || _textureProperties.Length != ScreenCount)
      {
        PrintError($"Screen types or texture properties count does not match the screen count in {name}");
        return;
      }

      UpdateScreenMaterial();
      UpdateHandlerMaxResolution();
    }

    private void UpdateHandlerMaxResolution()
    {
      int len = _videoPlayerHandlers.Length;
      for (int i = 0; i < len; i++)
      {
        var handler = _videoPlayerHandlers[i];
        if (Utilities.IsValid(handler)) handler.MaxResolution = _maxResolution;
      }
    }

    private void InitializePropertyBlock()
    {
      if (!Utilities.IsValid(_propertyBlock))
      {
        _propertyBlock = new MaterialPropertyBlock();
      }
    }

    private void UpdateScreenMaterial()
    {
      InitializePropertyBlock();

      _propertyBlock.SetInt("_MirrorFlip", _mirrorFlip ? 1 : 0);
      _propertyBlock.SetFloat("_Brightness", _brightness);
    }

    public Texture Texture
    {
      get
      {
        if (!Utilities.IsValid(Handler)) return null;
        return ActiveHandler.Texture;
      }
    }

    public Object[] Screens => _screens;

    public int ScreenCount => _screens.Length;

    public int MaxResolution
    {
      get => _maxResolution;
      set
      {
        if (value == _maxResolution) return;
        _maxResolution = value;
        UpdateHandlerMaxResolution();
        if (!Stopped) SendCustomEventDelayedFrames(nameof(ReloadForSettings), 0);
        int len = _listeners.Length;
        for (int i = 0; i < len; i++)
        {
          var listener = _listeners[i];
          if (Utilities.IsValid(listener)) listener.AfterMaxResolutionChanged(value);
        }
      }
    }

    public bool MirrorFlip
    {
      get => _mirrorFlip;
      set
      {
        _mirrorFlip = value;
        UpdateScreenMaterial();
        int len = _listeners.Length;
        for (int i = 0; i < len; i++)
        {
          var listener = _listeners[i];
          if (Utilities.IsValid(listener)) listener.AfterMirrorFlipChanged(value);
        }
      }
    }

    public float Brightness
    {
      get => _brightness;
      set
      {
        _brightness = value;
        UpdateScreenMaterial();
        int len = _listeners.Length;
        for (int i = 0; i < len; i++)
        {
          var listener = _listeners[i];
          if (Utilities.IsValid(listener)) listener.AfterBrightnessChanged(value);
        }
      }
    }

    public override void AfterTextureUpdated(Texture texture)
    {
      int screenCount = _screens.Length;
      for (int i = 0; i < screenCount; i++)
      {
        if (!Utilities.IsValid(_screens[i])) continue;

        string textureProperty = _textureProperties[i];
        switch (_screenTypes[i])
        {
          case ScreenType.Renderer:
            if (!Utilities.IsValid(texture))
            {
              if (!Utilities.IsValid(_emptyPropertyBlock)) _emptyPropertyBlock = new MaterialPropertyBlock();
              ((Renderer)_screens[i]).SetPropertyBlock(_emptyPropertyBlock, 0);
            }
            else
            {
              _propertyBlock.SetTexture(textureProperty, texture);
              ((Renderer)_screens[i]).SetPropertyBlock(_propertyBlock, 0);
            }
            break;
          case ScreenType.RawImage:
            ((RawImage)_screens[i]).texture = texture;
            break;
          case ScreenType.Material:
            ((Material)_screens[i]).SetTexture(textureProperty, texture);
            break;
        }
      }
      int listenerCount = _listeners.Length;
      for (int i = 0; i < listenerCount; i++)
      {
        var listener = _listeners[i];
        if (Utilities.IsValid(listener)) listener.AfterTextureUpdated(texture);
      }
    }

    public void AddScreen(ScreenType screenType, Object screen, string textureProperty = "_MainTex")
    {
      if (!Utilities.IsValid(screen) || string.IsNullOrEmpty(textureProperty))
      {
        PrintError($"Cannot add screen: invalid screen or texture property");
        return;
      }

      for (int i = 0; i < ScreenCount; i++)
      {
        if (_screens[i] == screen && _textureProperties[i] == textureProperty)
        {
          PrintWarning($"Screen {_screens[i].name} with texture property {_textureProperties[i]} already exists");
          return;
        }
      }

      _screenTypes = _screenTypes.Add(screenType);
      _screens = _screens.Add(screen);
      _textureProperties = _textureProperties.Add(textureProperty);
    }
  }
}