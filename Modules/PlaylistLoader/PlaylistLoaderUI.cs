using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using Yamadev.YamaStream.UI;

namespace Yamadev.YamaStream.Modules.PlaylistLoader
{
  // Intercepts URLs entered into the main URL input (issue #82): VHub
  // playlist URLs are routed to PlaylistLoader instead of the video player.
  // Wired to UIController._urlInterceptor by PlaylistLoaderBuildProcess.
  [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
  public class PlaylistLoaderUI : YamaPlayerListener
  {
    [SerializeField] private PlaylistLoader _loader;
    // Fallback UI for feedback when no caller was passed (wired by
    // PlaylistLoaderBuildProcess). Interactions prefer interceptSource so a
    // shared interceptor answers on the panel the URL was entered on.
    [SerializeField] private UIController _uiController;

    // Contract with UIController.PlayUrlField: the caller writes these
    // variables via SetProgramVariable before sending OnUrlSubmitted, then
    // reads interceptHandled back. handled=true means the URL must not
    // continue into the video path, even when the load was refused here.
    public VRCUrl interceptUrl;
    public UIController interceptSource;
    public bool interceptHandled;

    // Contract with UIController.RefreshPlaylist (issue #91): the caller
    // writes these before sending OnPlaylistRefreshRequested. refreshSource
    // is the panel the button was pressed on, so the result lands there.
    public VRCUrl refreshUrl;
    public UIController refreshSource;

    // The UI that initiated the load currently in flight; OnLoadResult
    // reports back to it.
    private UIController _resultUi;
    // Held while the player is being asked whether to replace the oldest
    // playlist. Cleared by whichever answer arrives; a second question
    // simply overwrites it, so a cancelled dialog leaves nothing behind.
    private VRCUrl _pendingLoadUrl;

    public void OnUrlSubmitted()
    {
      interceptHandled = false;
      if (!Utilities.IsValid(_loader) || !Utilities.IsValid(interceptUrl)) return;

      int kind = _loader.ClassifyUrl(interceptUrl.Get());
      if (kind == PlaylistLoader.UrlKindNotOurs) return;

      interceptHandled = true;
      var ui = Utilities.IsValid(interceptSource) ? interceptSource : _uiController;

      if (kind == PlaylistLoader.UrlKindOtherPool)
      {
        ShowError(ui, "errorPoolMismatch");
        return;
      }
      if (kind != PlaylistLoader.UrlKindOwnPlaylist)
      {
        // Web-page URL (/playlists/{id}) or malformed /r/ path: guide the
        // user to the share URL. Rewriting to /r/... is impossible at
        // runtime (string -> VRCUrl conversion is editor-only).
        ShowError(ui, "errorSharePageUrl");
        return;
      }

      // Permission first so a viewer without rights sees the permission
      // modal (shown by PermissionManagementUI) rather than a busy message.
      if (Utilities.IsValid(ui) && !ui.InvokeBeforeEvent("BeforeUserLoadPlaylist"))
        return;

      if (_loader.IsLoading)
      {
        ShowError(ui, "errorBusy");
        return;
      }

      _resultUi = ui;

      // Every slot full means this load costs the player a playlist they
      // already had. Show which one before it goes (issue #125) rather than
      // reporting an addition and letting them find out later.
      string replaced = _loader.GetPlaylistToBeReplaced(interceptUrl);
      if (!string.IsNullOrEmpty(replaced))
      {
        _pendingLoadUrl = interceptUrl;
        if (AskBeforeReplacing(ui, replaced)) return;
        // No modal on this UI, so there is no way to ask. Loading anyway is
        // what happens today, and refusing instead would strand a player
        // whose panel has no way to delete anything either.
        _pendingLoadUrl = null;
      }

      _loader.LoadPlaylistFromUrlWithFeedback(interceptUrl, this);
    }

    private bool AskBeforeReplacing(UIController ui, string replaced)
    {
      if (!Utilities.IsValid(ui)) return false;

      string name = string.IsNullOrEmpty(replaced) ? "Playlist" : replaced;
      return ui.ShowConfirm(
          ui.GetTranslation("module.playlistLoader.confirmReplaceTitle"),
          ui.GetTranslation("module.playlistLoader.confirmReplaceMessage")
              .Replace("{0}", _loader.UsableSlotCount.ToString()).Replace("{1}", name),
          ui.GetTranslation("button.continue"),
          this,
          nameof(ConfirmReplaceOldest));
    }

    // Answered yes. The dialog was up for as long as the player took to read
    // it, so nothing about the state that produced the question can be
    // assumed to still hold.
    public void ConfirmReplaceOldest()
    {
      var url = _pendingLoadUrl;
      _pendingLoadUrl = null;
      if (!Utilities.IsValid(_loader) || !Utilities.IsValid(url)) return;

      var ui = Utilities.IsValid(_resultUi) ? _resultUi : _uiController;
      if (_loader.IsLoading)
      {
        ShowError(ui, "errorBusy");
        return;
      }

      _loader.LoadPlaylistFromUrlWithFeedback(url, this);
    }

    // Refetches a slot from the URL that filled it (issue #91). Deliberately
    // mirrors OnUrlSubmitted: same permission gate (applied by the caller),
    // same busy handling, same feedback surface.
    public void OnPlaylistRefreshRequested()
    {
      if (!Utilities.IsValid(_loader) || !Utilities.IsValid(refreshUrl)) return;

      var ui = Utilities.IsValid(refreshSource) ? refreshSource : _uiController;

      if (_loader.ClassifyUrl(refreshUrl.Get()) != PlaylistLoader.UrlKindOwnPlaylist)
      {
        ShowError(ui, "errorPoolMismatch");
        return;
      }

      if (_loader.IsLoading)
      {
        ShowError(ui, "errorBusy");
        return;
      }

      // _resultUi is not cleared after a load, so leaving it alone here
      // would report this refresh on whichever panel last submitted a URL.
      _resultUi = ui;
      _loader.LoadPlaylistFromUrlWithFeedback(refreshUrl, this);
    }

    // Called by PlaylistLoader exactly once per feedback load, on every
    // terminal path. Translations are resolved at show time, so no language
    // change listener is needed.
    //
    // reusedExistingSlot says the playlist was already in the list and got
    // refreshed, rather than a new entry appearing (issue #117). It comes
    // from the loader instead of from which button was pressed, because
    // re-entering a URL that is already loaded refreshes it too and no
    // button says so.
    public void OnLoadResult(int resultCode, string playlistName, int added, int skipped,
        int httpErrorCode, bool reusedExistingSlot)
    {
      var ui = Utilities.IsValid(_resultUi) ? _resultUi : _uiController;
      if (!Utilities.IsValid(ui)) return;

      if (resultCode == PlaylistLoader.LoadResultSuccess)
      {
        string name = string.IsNullOrEmpty(playlistName) ? "Playlist" : playlistName;
        ui.ShowMessage(
            ui.GetTranslation(reusedExistingSlot
                ? "module.playlistLoader.updatedTitle"
                : "module.playlistLoader.loadedTitle"),
            ui.GetTranslation(reusedExistingSlot
                ? "module.playlistLoader.updatedMessage"
                : "module.playlistLoader.loadedMessage")
                .Replace("{0}", name).Replace("{1}", added.ToString()));
        return;
      }
      if (resultCode == PlaylistLoader.LoadResultPartial)
      {
        // One message for both, worded so it reads either way: the title
        // already says whether this was an addition or a refresh, and the
        // counts are what the body is for.
        ui.ShowMessage(
            ui.GetTranslation(reusedExistingSlot
                ? "module.playlistLoader.updatedTitle"
                : "module.playlistLoader.loadedTitle"),
            ui.GetTranslation("module.playlistLoader.loadedPartialMessage")
                .Replace("{0}", added.ToString()).Replace("{1}", skipped.ToString()));
        return;
      }

      if (resultCode == PlaylistLoader.LoadResultDownloadError)
      {
        ShowError(ui, httpErrorCode == 404 ? "errorNotFound" : "errorDownload");
        return;
      }
      if (resultCode == PlaylistLoader.LoadResultEmpty)
      {
        ShowError(ui, "errorEmpty");
        return;
      }
      if (resultCode == PlaylistLoader.LoadResultPoolMismatch)
      {
        ShowError(ui, "errorPoolMismatch");
        return;
      }
      if (resultCode == PlaylistLoader.LoadResultServerError)
      {
        // The raw server error text is already in the log.
        ShowError(ui, "errorServer");
        return;
      }
      if (resultCode == PlaylistLoader.LoadResultQueueUnavailable)
      {
        ShowError(ui, "errorInternal");
        return;
      }
      ShowError(ui, "errorInvalidResponse");
    }

    private void ShowError(UIController ui, string messageKey)
    {
      if (!Utilities.IsValid(ui)) return;
      ui.ShowMessage(
          ui.GetTranslation("module.playlistLoader.errorTitle"),
          ui.GetTranslation("module.playlistLoader." + messageKey));
    }
  }
}
