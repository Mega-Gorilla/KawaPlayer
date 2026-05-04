using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDKBase;
using Yamadev.YamaStream.UI;

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

    [SerializeField] private Text _titleText;
    [SerializeField] private Text _descriptionText;
    [SerializeField] private Text _saveButtonLabel;
    [SerializeField] private Text _clearButtonLabel;

    private UIController _uiController;
    private string _lastSyncedUrl = null;

    void Start()
    {
      _uiController = GetComponentInParent<UIController>();
      if (_uiController != null) _uiController.AddListener(this);
      UpdateTranslation();
      UpdateOwnerVisibility();
      UpdateDisplay();
      RefreshInputField();
      SchedulePoll();
    }

    public void AfterLanguageChanged() => UpdateTranslation();

    private void UpdateTranslation()
    {
      if (_uiController == null) return;
      // Skip writes when GetTranslation returns "" so a missing key (e.g. before
      // LocalizationBuildProcess has merged module translations) does not wipe
      // out the prefab-baked Japanese fallback text.
      if (_titleText != null)
      {
        string t = _uiController.GetTranslation("module.defaultUrl.title");
        if (!string.IsNullOrEmpty(t))
          _titleText.text = $"{t}<size=44>(Global)</size>";
      }
      if (_descriptionText != null)
      {
        string t = _uiController.GetTranslation("module.defaultUrl.description");
        if (!string.IsNullOrEmpty(t))
          _descriptionText.text = t;
      }
      if (_saveButtonLabel != null)
      {
        string t = _uiController.GetTranslation("module.defaultUrl.save");
        if (!string.IsNullOrEmpty(t))
          _saveButtonLabel.text = t;
      }
      if (_clearButtonLabel != null)
      {
        string t = _uiController.GetTranslation("module.defaultUrl.clear");
        if (!string.IsNullOrEmpty(t))
          _clearButtonLabel.text = t;
      }
      UpdateDisplay();
    }

    public void SchedulePoll()
    {
      UpdateOwnerVisibility();
      UpdateDisplay();
      RefreshInputField();
      SendCustomEventDelayedSeconds(nameof(SchedulePoll), 1.0f);
    }

    public override void OnPlayerJoined(VRCPlayerApi player)
    {
      if (player == Networking.LocalPlayer)
      {
        UpdateOwnerVisibility();
        UpdateDisplay();
        RefreshInputField();
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
      bool hasUrl = Utilities.IsValid(url) && !string.IsNullOrEmpty(url.Get());
      string prefix = "現在: ";
      string notSet = "(デフォルト URL は未設定です)";
      if (_uiController != null)
      {
        string p = _uiController.GetTranslation("module.defaultUrl.currentPrefix");
        if (!string.IsNullOrEmpty(p)) prefix = p;
        string n = _uiController.GetTranslation("module.defaultUrl.notSet");
        if (!string.IsNullOrEmpty(n)) notSet = n;
      }
      _currentUrlDisplay.text = hasUrl ? prefix + url.Get() : notSet;
    }

    private void RefreshInputField()
    {
      if (_urlInput == null) return;
      if (_controller == null) return;
      if (!Utilities.IsValid(Networking.LocalPlayer)) return;
      if (!Networking.LocalPlayer.isInstanceOwner) return;

      var url = _controller.DefaultUrl;
      bool hasUrl = Utilities.IsValid(url) && !string.IsNullOrEmpty(url.Get());
      string urlStr = hasUrl ? url.Get() : "";

      if (_lastSyncedUrl != urlStr)
      {
        _lastSyncedUrl = urlStr;
        _urlInput.SetUrl(hasUrl ? url : VRCUrl.Empty);
      }
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
      RefreshInputField();
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
      RefreshInputField();
    }

    // OnValidate warning was removed (#59): _controller / _storageTemplate are intentionally null
    // at ScreenUI.prefab asset level (this script lives in ScreenUI.prefab/.../DefaultUrlSetting/).
    // Cross-prefab override in KawaPlayer.prefab wires them at runtime instance level.
    // Same approach as DefaultUrlController.OnValidate removal in PR #58 (UdonSharp does not expose
    // UnityEditor.PrefabUtility/Scene.IsValid() for in-Udon detection of prefab-asset context, so
    // we cannot conditionally suppress; removing OnValidate is cleaner and matches existing modules).
  }
}
