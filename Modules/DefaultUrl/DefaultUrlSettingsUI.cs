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
    // The saved URL is shown here rather than in the input field. A
    // VRCUrlInputField in this UI is a place to type, not a place to read:
    // the two older ones on the main screen have their text alpha at zero and
    // are cleared after every submit, and this one draws nothing either.
    [SerializeField] private Text _currentUrlDisplay;
    // Everything the owner acts on lives here and is hidden from anyone who
    // cannot edit (issue #115). What made the old behaviour read as a bug was
    // that the controls vanished with nothing saying why, not that they
    // vanished, so the description is swapped for the reason rather than a
    // line being added. Leaving them visible but disabled was tried and
    // rejected: at VR viewing distance a dimmed field still invites a click,
    // and a click that does nothing reads as broken just as the empty space
    // did.
    [SerializeField] private GameObject _ownerOnlySection;
    // The input row, folded away until "enter URL" is pressed. An input field
    // that is always on screen and always looks empty invites the reading
    // that it is broken.
    [SerializeField] private GameObject _urlEntrySection;

    [SerializeField] private Text _titleText;
    [SerializeField] private Text _descriptionText;
    [SerializeField] private Text _enterUrlButtonLabel;
    [SerializeField] private Text _saveButtonLabel;
    [SerializeField] private Text _clearButtonLabel;

    private UIController _uiController;
    private string _lastSyncedUrl = null;

    void Start()
    {
      _uiController = GetComponentInParent<UIController>();
      if (_uiController != null) _uiController.AddListener(this);
      UpdateTranslation();
      UpdateEditability();
      UpdateDisplay();
      RefreshInputField();
      SchedulePoll();
    }

    // Everything that gates on permission asks the controller, so the button
    // state and the write guards cannot disagree. A missing controller means
    // no editing rather than an unguarded write.
    private bool CanEdit()
    {
      if (_controller == null) return false;
      return _controller.CanEditDefaultUrl();
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
      if (_enterUrlButtonLabel != null)
      {
        // Reuses the core label rather than adding a ninth translation of the
        // same two words.
        string t = _uiController.GetTranslation("label.inputUrl");
        if (!string.IsNullOrEmpty(t))
          _enterUrlButtonLabel.text = t;
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
      UpdateEditability();
      UpdateDisplay();
    }

    public void SchedulePoll()
    {
      UpdateEditability();
      UpdateDisplay();
      RefreshInputField();
      SendCustomEventDelayedSeconds(nameof(SchedulePoll), 1.0f);
    }

    public override void OnPlayerJoined(VRCPlayerApi player)
    {
      if (player == Networking.LocalPlayer)
      {
        UpdateEditability();
        UpdateDisplay();
        RefreshInputField();
      }
    }

    private void UpdateEditability()
    {
      bool canEdit = CanEdit();

      if (_ownerOnlySection != null)
        _ownerOnlySection.SetActive(canEdit);

      // Only ever folds it away. This runs once a second, so forcing it open
      // would fight whoever is typing.
      if (!canEdit && _urlEntrySection != null)
        _urlEntrySection.SetActive(false);

      // One block, not three. Someone who cannot use the feature has no use
      // for its description or its current value, and repeating the rule in
      // both the description and a separate notice states it twice.
      //
      // The headline is composed here rather than baked into the translation
      // so the markup stays out of the language files, matching how the title
      // appends its "(Global)" suffix above.
      if (_descriptionText != null && _uiController != null)
      {
        if (canEdit)
        {
          string t = _uiController.GetTranslation("module.defaultUrl.description");
          if (!string.IsNullOrEmpty(t))
            _descriptionText.text = t;
        }
        else
        {
          string headline = _uiController.GetTranslation("module.defaultUrl.unavailable");
          string reason = _uiController.GetTranslation("module.defaultUrl.noPermission");
          if (!string.IsNullOrEmpty(headline))
            _descriptionText.text = string.IsNullOrEmpty(reason)
                ? $"<size=48><b>✕ {headline}</b></size>"
                : $"<size=48><b>✕ {headline}</b></size>\n{reason}";
        }
      }
    }

    private void UpdateDisplay()
    {
      if (_currentUrlDisplay == null) return;
      if (_controller == null) return;
      var url = _controller.DefaultUrl;
      bool hasUrl = Utilities.IsValid(url) && !string.IsNullOrEmpty(url.Get());
      string prefix = "設定値: ";
      string notSet = "(未設定)";
      if (_uiController != null)
      {
        string p = _uiController.GetTranslation("module.defaultUrl.currentPrefix");
        if (!string.IsNullOrEmpty(p)) prefix = p;
        string n = _uiController.GetTranslation("module.defaultUrl.notSet");
        if (!string.IsNullOrEmpty(n)) notSet = n;
      }
      // Prefixed either way so the line reads the same whether or not a URL
      // is set, instead of switching between a value and a sentence.
      _currentUrlDisplay.text = prefix + (hasUrl ? url.Get() : notSet);
    }

    // Folds the input row in and out. The row starts folded, so the panel
    // shows what is set and how to change it, not an empty box.
    public void OnEnterUrlPressed()
    {
      if (!CanEdit()) return;
      if (_urlEntrySection == null) return;
      _urlEntrySection.SetActive(!_urlEntrySection.activeSelf);
    }

    private void RefreshInputField()
    {
      if (_urlInput == null) return;
      if (_controller == null) return;
      if (!CanEdit()) return;

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
      if (!CanEdit()) return;
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

      if (_urlEntrySection != null) _urlEntrySection.SetActive(false);
      UpdateDisplay();
      RefreshInputField();
    }

    public void OnClearPressed()
    {
      if (!CanEdit()) return;

      if (_controller != null)
        _controller.SetDefaultUrl(VRCUrl.Empty);

      if (_storageTemplate != null)
      {
        var spawned = (OwnerDefaultUrlStorage)Networking.FindComponentInPlayerObjects(
          Networking.LocalPlayer, _storageTemplate);
        if (spawned != null) spawned.ClearSavedUrl();
      }

      if (_urlEntrySection != null) _urlEntrySection.SetActive(false);
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
