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
    // The texture last passed on. A handler tells of a texture only when it
    // changes (#356), so a screen or a listener that comes later is given
    // this one as it comes, and a change of mirror flip or brightness puts
    // it back.
    private Texture _shownTexture;

    private void InitializeScreen()
    {
      // Said, and no more: a screen without its type or its property is left
      // out (ShowTexture), the others show. Giving up here left the property
      // block unmade, and the first texture then stopped the whole Controller:
      // sound, no picture, and no control answering (issue #373).
      if (_screenTypes.Length != ScreenCount || _textureProperties.Length != ScreenCount)
        PrintError($"Screen types or texture properties count does not match the screen count in {name}");

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

    // Mirror flip and brightness reach a renderer screen with its texture, in
    // one property block.
    private void SetUpPropertyBlock()
    {
      if (!Utilities.IsValid(_propertyBlock)) _propertyBlock = new MaterialPropertyBlock();
      _propertyBlock.SetInt("_MirrorFlip", _mirrorFlip ? 1 : 0);
      _propertyBlock.SetFloat("_Brightness", _brightness);
    }

    // The texture showing is put back with mirror flip and brightness: a
    // handler tells of a texture only when it changes (#356).
    private void UpdateScreenMaterial()
    {
      SetUpPropertyBlock();
      if (!Utilities.IsValid(_shownTexture)) return;
      int screenCount = _screens.Length;
      for (int i = 0; i < screenCount; i++) ShowTexture(i, _shownTexture);
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
      _shownTexture = texture;
      int screenCount = _screens.Length;
      for (int i = 0; i < screenCount; i++) ShowTexture(i, texture);
      int listenerCount = _listeners.Length;
      for (int i = 0; i < listenerCount; i++)
      {
        var listener = _listeners[i];
        if (Utilities.IsValid(listener)) listener.AfterTextureUpdated(texture);
      }
    }

    private void ShowTexture(int index, Texture texture)
    {
      var screen = _screens[index];
      if (!Utilities.IsValid(screen)) return;
      if (index >= _screenTypes.Length || index >= _textureProperties.Length) return;

      string textureProperty = _textureProperties[index];
      switch (_screenTypes[index])
      {
        case ScreenType.Renderer:
          if (!Utilities.IsValid(texture))
          {
            if (!Utilities.IsValid(_emptyPropertyBlock)) _emptyPropertyBlock = new MaterialPropertyBlock();
            ((Renderer)screen).SetPropertyBlock(_emptyPropertyBlock, 0);
          }
          else
          {
            // Whatever order the texture and the screens' setup come in (issue #373).
            if (!Utilities.IsValid(_propertyBlock)) SetUpPropertyBlock();
            _propertyBlock.SetTexture(textureProperty, texture);
            ((Renderer)screen).SetPropertyBlock(_propertyBlock, 0);
          }
          break;
        case ScreenType.RawImage:
          ((RawImage)screen).texture = texture;
          break;
        case ScreenType.Material:
          ((Material)screen).SetTexture(textureProperty, texture);
          break;
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
      if (Utilities.IsValid(_shownTexture)) ShowTexture(ScreenCount - 1, _shownTexture);
    }
  }
}