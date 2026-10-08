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
    // Dressed as a button -- icon, label, no visible box -- and clicked like
    // one. Clicking a VRCUrlInputField is what opens VRChat's text entry, so
    // making the field itself the button gets there in one press without
    // asking Udon to focus anything. ActivateInputField() was tried and did
    // not open the entry screen in world.
    //
    // It draws no text of its own: the label has to read as a button, and a
    // URL appearing in its place turns the button back into a box that only
    // looks pressable while it happens to be empty. The saved URL is read
    // from the line above instead. This is how the other two URL fields in
    // this UI are set up as well. The field is still kept holding the saved
    // URL so VRChat's entry screen opens on it rather than on nothing.
    //
    // Colour transition is off, because ColorTint writes normalColor over the
    // transparent background every time the field is enabled, and any Outline
    // has to go too: useGraphicAlpha ignores how transparent the graphic
    // under it is.
    //
    // Submitting is saving. A separate save button would look like a chance
    // to check the URL before committing it, and there is none: text entry
    // closes onto the saved value either way. The one exception is a save
    // that would push a playlist out of the playlist list, which asks first
    // (issue #129; see OnUrlSubmitted).
    [SerializeField, RegisterEvent(nameof(VRCUrlInputField.onEndEdit), nameof(OnUrlSubmitted))]
    private VRCUrlInputField _urlInput;
    // The only place the saved URL can be read. The field it was typed into
    // does not show it back; see _urlInput.
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

    [SerializeField] private Text _titleText;
    [SerializeField] private Text _descriptionText;
    [SerializeField] private Text _enterUrlButtonLabel;
    // VRCUrlInputField reports the width of the text it holds as a layout
    // size, the way InputField does, and at the same priority a LayoutElement
    // uses -- so the wider of the two wins. A saved URL is several times the
    // label, which is how the button came to be sized for a string it never
    // draws. The prefab gives the LayoutElement the higher priority; the
    // width it carries is set from the label below, so the button fits
    // whichever translation is on screen instead of the longest of the nine.
    [SerializeField] private LayoutElement _enterUrlButtonLayout;
    // Everything the button is wide apart from the label: padding either
    // side, the icon, and the gap after it.
    [SerializeField] private float _enterUrlButtonChrome = 66f;
    [SerializeField] private Text _clearButtonLabel;
    // The sizes the title's "(Global)" and the headline that says the
    // feature is unavailable are drawn at. They are in the text's own units,
    // so a panel whose text is set larger or smaller sets them to match (the
    // tablet's settings app, issue #159).
    [SerializeField] private int _globalSuffixSize = 44;
    [SerializeField] private int _headlineSize = 48;

    private UIController _uiController;
    private string _lastSyncedUrl = null;

    // The save waiting on an answer to "the playlist list is full, replace
    // the oldest?" (issue #129), and what the question named.
    private VRCUrl _pendingSaveUrl;
    private DynamicPlaylist _pendingReplaced;
    private string _pendingReplacedSourceUrl = string.Empty;

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
          _titleText.text = $"{t}<size={_globalSuffixSize}>(Global)</size>";
      }
      if (_enterUrlButtonLabel != null)
      {
        // Reuses the core label rather than adding a ninth translation of the
        // same two words.
        string t = _uiController.GetTranslation("label.inputUrl");
        if (!string.IsNullOrEmpty(t))
          _enterUrlButtonLabel.text = t;
      }
      ResizeEnterUrlButton();
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
                ? $"<size={_headlineSize}><b>✕ {headline}</b></size>"
                : $"<size={_headlineSize}><b>✕ {headline}</b></size>\n{reason}";
        }
      }
    }

    // The clear button needs no equivalent: nothing on it claims a layout
    // size, so its own layout group already sizes it from its contents.
    private void ResizeEnterUrlButton()
    {
      if (_enterUrlButtonLayout == null) return;
      if (_enterUrlButtonLabel == null) return;

      float width = _enterUrlButtonChrome + _enterUrlButtonLabel.preferredWidth;
      _enterUrlButtonLayout.minWidth = width;
      _enterUrlButtonLayout.preferredWidth = width;
    }

    private void UpdateDisplay()
    {
      if (_controller == null) return;
      if (_currentUrlDisplay == null) return;
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

    // Fired when VRChat's text entry closes, however it closed: confirmed,
    // cancelled, or confirmed empty.
    public void OnUrlSubmitted()
    {
      if (!CanEdit()) return;
      if (_urlInput == null) return;

      // The question about an earlier save is still up. Saving now would
      // either be turned away by the dialog or go through and then be
      // overwritten when that question is answered, so the answer comes
      // first, as on the URL field (PlaylistLoaderUI). The entry is dropped
      // and the field goes back to what is saved.
      if (IsAwaitingSaveAnswer())
      {
        ResyncInputField();
        return;
      }

      // A cancelled entry raises this too, with the old text already put
      // back. Saving here would rewrite and re-sync a value nobody changed.
      if (_urlInput.wasCanceled)
      {
        ResyncInputField();
        return;
      }

      var url = _urlInput.GetUrl();
      if (!Utilities.IsValid(url) || string.IsNullOrEmpty(url.Get()))
      {
        // Emptying the box is not how the setting is cleared -- the clear
        // button is -- so keep what is saved rather than reading the empty
        // box as an instruction.
        ResyncInputField();
        return;
      }

      // Saving a VHub playlist while the player is stopped loads it at once,
      // and with the playlist list full that pushes the oldest one out. The
      // URL field asks before doing that (issue #125); saving it as the
      // default URL is the same load and asks the same question (issue #129).
      var replaced = _controller != null ? _controller.GetSlotReplacedBySaving(url) : null;
      if (Utilities.IsValid(replaced))
      {
        if (AskBeforeReplacing(url, replaced)) return;
        // Asking failed. A panel with a dialog could have asked and did not
        // only because another question is up, and saving anyway would take
        // a playlist nobody agreed to lose -- so nothing is saved, as with
        // the URL field. With no dialog at all there is no one to ask, and
        // the save goes ahead as it always has.
        if (_uiController != null && _uiController.HasModalDialog)
        {
          ResyncInputField();
          return;
        }
      }

      Save(url);
    }

    private void Save(VRCUrl url)
    {
      if (_controller != null)
        _controller.SetDefaultUrl(url);

      var storage = FindOwnStorage();
      if (storage != null) storage.SaveDefaultUrl(url);

      UpdateDisplay();
      RefreshInputField();
    }

    // This player's own copy of the storage, spawned from the template into
    // their player objects. Null without a template, or before the copy is
    // spawned.
    private OwnerDefaultUrlStorage FindOwnStorage()
    {
      if (_storageTemplate == null) return null;
      return (OwnerDefaultUrlStorage)Networking.FindComponentInPlayerObjects(Networking.LocalPlayer, _storageTemplate);
    }

    // The same question, in the same words, as the URL field asks
    // (PlaylistLoaderUI.AskBeforeReplacing). Its translations are there
    // whenever this can be asked: a playlist only gets replaced when the
    // PlaylistLoader module is present.
    private bool AskBeforeReplacing(VRCUrl url, DynamicPlaylist replaced)
    {
      if (_uiController == null) return false;

      // Reading SourceUrl is only safe on a slot that holds something.
      string replacedSourceUrl = replaced.CanRefresh ? replaced.SourceUrl.Get() : string.Empty;
      // PlaylistLoader alone names the module's namespace here, not its class.
      string name = string.IsNullOrEmpty(replaced.PlaylistName) ? PlaylistLoader.PlaylistLoader.UnnamedPlaylistName : replaced.PlaylistName;
      if (!_uiController.ShowConfirm(
              _uiController.GetTranslation("module.playlistLoader.confirmReplaceTitle"),
              _uiController.GetTranslation("module.playlistLoader.confirmReplaceMessage")
                  .Replace("{0}", _controller.PlaylistSlotCount.ToString()).Replace("{1}", name),
              _uiController.GetTranslation("button.continue"),
              this,
              nameof(ConfirmSaveReplacing),
              nameof(CancelSaveReplacing)))
        return false;

      // Recorded only once the question is up, so one that was turned away
      // cannot overwrite or let go of a save still waiting on its answer.
      _pendingSaveUrl = url;
      _pendingReplaced = replaced;
      _pendingReplacedSourceUrl = replacedSourceUrl;
      return true;
    }

    // Whether a save is waiting on the question about replacing a playlist.
    // One whose dialog is no longer waiting on anyone is let go of rather
    // than kept: nothing would ever answer it, and it would turn every later
    // save and clear away.
    private bool IsAwaitingSaveAnswer()
    {
      if (!Utilities.IsValid(_pendingSaveUrl)) return false;
      if (_uiController != null && _uiController.IsModalBusy) return true;
      ClearPendingSave();
      return false;
    }

    // Answered yes. The dialog was up for as long as it took to read, so the
    // playlist that saving pushes out may no longer be the one named. Rather
    // than take a different one, ask again about that one.
    public void ConfirmSaveReplacing()
    {
      var url = _pendingSaveUrl;
      var replaced = _pendingReplaced;
      string replacedSourceUrl = _pendingReplacedSourceUrl;
      ClearPendingSave();
      if (!CanEdit() || !Utilities.IsValid(url))
      {
        ResyncInputField();
        return;
      }

      var now = _controller.GetSlotReplacedBySaving(url);
      if (Utilities.IsValid(now))
      {
        string nowSourceUrl = now.CanRefresh ? now.SourceUrl.Get() : string.Empty;
        if (now != replaced || nowSourceUrl != replacedSourceUrl)
        {
          if (!AskBeforeReplacing(url, now)) ResyncInputField();
          return;
        }
      }

      Save(url);
    }

    // Answered no. Nothing is saved, and the field goes back to what is.
    public void CancelSaveReplacing()
    {
      ClearPendingSave();
      ResyncInputField();
    }

    private void ClearPendingSave()
    {
      _pendingSaveUrl = null;
      _pendingReplaced = null;
      _pendingReplacedSourceUrl = string.Empty;
    }

    // Puts the saved URL back into the field after an entry that did not save.
    // RefreshInputField only writes when the saved value differs from what it
    // last wrote, so the memo has to be cleared or a field left holding
    // something else would keep it until the saved value happened to change.
    private void ResyncInputField()
    {
      _lastSyncedUrl = null;
      RefreshInputField();
    }

    public void OnClearPressed()
    {
      if (!CanEdit()) return;
      // Answered later, the question would save its URL again over the
      // clear. It is answered first.
      if (IsAwaitingSaveAnswer()) return;

      if (_controller != null)
        _controller.SetDefaultUrl(VRCUrl.Empty);

      var storage = FindOwnStorage();
      if (storage != null) storage.ClearSavedUrl();

      UpdateDisplay();
      RefreshInputField();
    }

    // No OnValidate warning about a missing _controller or _storageTemplate (#59): they are
    // empty in ScreenUI.prefab itself and wired by KawaPlayer.prefab's override.
  }
}
