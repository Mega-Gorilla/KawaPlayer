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
    // What is being asked about, held from the question to the answer.
    //
    // One question at a time. Every panel with a URL field is wired to this
    // same behaviour (PlaylistLoaderBuildProcess hands them all the same
    // one), so a second question would overwrite the first and let one
    // panel's confirm load the other panel's URL.
    //
    // The slot and the URL that identified it travel with the request, so
    // the load can refuse to overwrite anything else -- see
    // PlaylistLoader.LoadPlaylistFromUrlWithFeedback.
    private VRCUrl _pendingLoadUrl;
    private DynamicPlaylist _pendingReplaced;
    private string _pendingReplacedSourceUrl = string.Empty;
    private bool _awaitingConfirm;
    // Which panel is being asked. _resultUi cannot stand in for it: a
    // refresh from another panel moves that while the dialog is up, and the
    // answer would then be reported somewhere the player is not looking.
    private UIController _pendingUi;
    // Told about a load that could not start, once the dialog it would have
    // been hidden behind is gone.
    private UIController _busyReportUi;

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

      // Someone is already being asked something. Answering that question
      // has to come first, or their answer would apply to this URL instead.
      if (_awaitingConfirm)
      {
        // Not on the panel holding the dialog. The modal is not full
        // screen, so its own URL field is still reachable, and answering
        // with a message there would replace the question -- taking its
        // buttons with it and leaving the wait with no way to end.
        if (ui != _pendingUi) ShowError(ui, "errorBusy");
        return;
      }

      _resultUi = ui;

      // Every slot full means this load costs the player a playlist they
      // already had. Show which one before it goes (issue #125) rather than
      // reporting an addition and letting them find out later.
      var replaced = _loader.GetSlotToBeReplaced(interceptUrl);
      if (Utilities.IsValid(replaced) && AskBeforeReplacing(ui, interceptUrl, replaced)) return;

      // Either nothing is lost, or there is no modal to ask with. Loading
      // anyway is what happens today, and refusing instead would strand a
      // player whose panel has no way to delete anything either.
      _loader.LoadPlaylistFromUrlWithFeedback(interceptUrl, this, null, string.Empty);
    }

    private bool AskBeforeReplacing(UIController ui, VRCUrl url, DynamicPlaylist replaced)
    {
      if (!Utilities.IsValid(ui)) return false;

      _pendingLoadUrl = url;
      _pendingUi = ui;
      _pendingReplaced = replaced;
      // Reading SourceUrl is only safe on a slot that holds something.
      _pendingReplacedSourceUrl = replaced.CanRefresh ? replaced.SourceUrl.Get() : string.Empty;
      _awaitingConfirm = true;

      // A playlist the server gave no name for still has to be named in the
      // question. The same word stands in for it as in OnLoadResult.
      string name = string.IsNullOrEmpty(replaced.PlaylistName) ? "Playlist" : replaced.PlaylistName;
      if (ui.ShowConfirm(
              ui.GetTranslation("module.playlistLoader.confirmReplaceTitle"),
              ui.GetTranslation("module.playlistLoader.confirmReplaceMessage")
                  .Replace("{0}", _loader.UsableSlotCount.ToString()).Replace("{1}", name),
              ui.GetTranslation("button.continue"),
              this,
              nameof(ConfirmReplaceOldest),
              nameof(CancelReplaceOldest)))
        return true;

      ClearPendingConfirm();
      return false;
    }

    // Answered yes. The dialog was up for as long as the player took to read
    // it, so nothing that produced the question can be assumed to still
    // hold. What they agreed to lose travels with the load, which refuses to
    // overwrite anything else.
    public void ConfirmReplaceOldest()
    {
      var url = _pendingLoadUrl;
      var ui = _pendingUi;
      var replaced = _pendingReplaced;
      string replacedSourceUrl = _pendingReplacedSourceUrl;
      ClearPendingConfirm();
      if (!Utilities.IsValid(_loader) || !Utilities.IsValid(url)) return;

      // Report to whoever was asked, not to whatever touched the loader
      // most recently while they were reading.
      if (Utilities.IsValid(ui)) _resultUi = ui;

      if (_loader.IsLoading)
      {
        // Modal.ExecuteAndClose hides the dialog after this returns, taking
        // anything shown from here with it. Say it once the dialog is gone.
        _busyReportUi = ui;
        SendCustomEventDelayedFrames(nameof(ReportBusyAfterConfirm), 1);
        return;
      }

      _loader.LoadPlaylistFromUrlWithFeedback(url, this, replaced, replacedSourceUrl);
    }

    // Answered no. Nothing happens, but the question has to be let go of or
    // no one could ask another.
    public void CancelReplaceOldest() => ClearPendingConfirm();

    public void ReportBusyAfterConfirm()
    {
      var ui = Utilities.IsValid(_busyReportUi) ? _busyReportUi : _uiController;
      _busyReportUi = null;
      ShowError(ui, "errorBusy");
    }

    private void ClearPendingConfirm()
    {
      _pendingLoadUrl = null;
      _pendingUi = null;
      _pendingReplaced = null;
      _pendingReplacedSourceUrl = string.Empty;
      _awaitingConfirm = false;
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
      // A refresh takes back the slot it already owns, so it never costs
      // anyone a playlist and makes no promise to check.
      _loader.LoadPlaylistFromUrlWithFeedback(refreshUrl, this, null, string.Empty);
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
      if (resultCode == PlaylistLoader.LoadResultReplacementChanged)
      {
        ShowError(ui, "errorReplacementChanged");
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
