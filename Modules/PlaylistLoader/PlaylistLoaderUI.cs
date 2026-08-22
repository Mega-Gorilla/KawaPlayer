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

    // The UI that initiated the load currently in flight; OnLoadResult
    // reports back to it.
    private UIController _resultUi;

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
      _loader.LoadPlaylistFromUrlWithFeedback(interceptUrl, this);
    }

    // Called by PlaylistLoader exactly once per feedback load, on every
    // terminal path. Translations are resolved at show time, so no language
    // change listener is needed.
    public void OnLoadResult(int resultCode, string playlistName, int added, int skipped, int httpErrorCode)
    {
      var ui = Utilities.IsValid(_resultUi) ? _resultUi : _uiController;
      if (!Utilities.IsValid(ui)) return;

      if (resultCode == PlaylistLoader.LoadResultSuccess)
      {
        string name = string.IsNullOrEmpty(playlistName) ? "Playlist" : playlistName;
        ui.ShowMessage(
            ui.GetTranslation("module.playlistLoader.loadedTitle"),
            ui.GetTranslation("module.playlistLoader.loadedMessage")
                .Replace("{0}", name).Replace("{1}", added.ToString()));
        return;
      }
      if (resultCode == PlaylistLoader.LoadResultPartial)
      {
        ui.ShowMessage(
            ui.GetTranslation("module.playlistLoader.loadedTitle"),
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
