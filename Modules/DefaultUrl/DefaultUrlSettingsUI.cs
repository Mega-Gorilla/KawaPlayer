using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDKBase;

namespace Yamadev.YamaStream.Modules.DefaultUrl
{
  [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
  public class DefaultUrlSettingsUI : YamaPlayerListener
  {
    [SerializeField] private DefaultUrlController _controller;
    [SerializeField] private OwnerDefaultUrlStorage _storageTemplate;
    [SerializeField] private VRCUrlInputField _urlInput;
    [SerializeField] private Text _currentUrlDisplay;
    [SerializeField] private GameObject _ownerOnlySection;

    void Start()
    {
      UpdateOwnerVisibility();
      UpdateDisplay();
      SchedulePoll();
    }

    public void SchedulePoll()
    {
      UpdateOwnerVisibility();
      UpdateDisplay();
      SendCustomEventDelayedSeconds(nameof(SchedulePoll), 1.0f);
    }

    public override void OnPlayerJoined(VRCPlayerApi player)
    {
      if (player == Networking.LocalPlayer)
      {
        UpdateOwnerVisibility();
        UpdateDisplay();
      }
    }

    private void UpdateOwnerVisibility()
    {
      if (_ownerOnlySection == null) return;
      if (!Utilities.IsValid(Networking.LocalPlayer))
      {
        _ownerOnlySection.SetActive(false);
        return;
      }
      _ownerOnlySection.SetActive(Networking.LocalPlayer.isInstanceOwner);
    }

    private void UpdateDisplay()
    {
      if (_currentUrlDisplay == null) return;
      if (_controller == null) return;
      var url = _controller.DefaultUrl;
      if (Utilities.IsValid(url) && !string.IsNullOrEmpty(url.Get()))
        _currentUrlDisplay.text = "Current: " + url.Get();
      else
        _currentUrlDisplay.text = "(no default URL set)";
    }

    public void OnSavePressed()
    {
      if (!Utilities.IsValid(Networking.LocalPlayer)) return;
      if (!Networking.LocalPlayer.isInstanceOwner) return;
      if (_urlInput == null) return;

      var url = _urlInput.GetUrl();
      if (!Utilities.IsValid(url) || string.IsNullOrEmpty(url.Get())) return;

      if (_controller != null)
        _controller.SetDefaultUrl(url);

      if (_storageTemplate != null)
      {
        var spawned = (OwnerDefaultUrlStorage)Networking.FindComponentInPlayerObjects(
          Networking.LocalPlayer, _storageTemplate);
        if (spawned != null) spawned.SaveDefaultUrl(url);
      }

      UpdateDisplay();
    }

    public void OnClearPressed()
    {
      if (!Utilities.IsValid(Networking.LocalPlayer)) return;
      if (!Networking.LocalPlayer.isInstanceOwner) return;

      if (_controller != null)
        _controller.SetDefaultUrl(VRCUrl.Empty);

      if (_storageTemplate != null)
      {
        var spawned = (OwnerDefaultUrlStorage)Networking.FindComponentInPlayerObjects(
          Networking.LocalPlayer, _storageTemplate);
        if (spawned != null) spawned.ClearSavedUrl();
      }

      UpdateDisplay();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
      if (_controller == null)
        Debug.LogWarning("[DefaultUrlSettingsUI] " + gameObject.name + ": _controller is not set.", this);
      if (_storageTemplate == null)
        Debug.LogWarning("[DefaultUrlSettingsUI] " + gameObject.name + ": _storageTemplate is not set.", this);
      if (_urlInput == null)
        Debug.LogWarning("[DefaultUrlSettingsUI] " + gameObject.name + ": _urlInput is not set.", this);
      if (_currentUrlDisplay == null)
        Debug.LogWarning("[DefaultUrlSettingsUI] " + gameObject.name + ": _currentUrlDisplay is not set.", this);
      if (_ownerOnlySection == null)
        Debug.LogWarning("[DefaultUrlSettingsUI] " + gameObject.name + ": _ownerOnlySection is not set.", this);
    }
#endif
  }
}
