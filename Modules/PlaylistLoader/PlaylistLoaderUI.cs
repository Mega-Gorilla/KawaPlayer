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
    [SerializeField] private UIController _uiController;

    // Contract with UIController.PlayUrlField: the caller writes both
    // variables via SetProgramVariable before sending OnUrlSubmitted, then
    // reads interceptHandled back. handled=true means the URL must not
    // continue into the video path, even when the load was refused here.
    public VRCUrl interceptUrl;
    public bool interceptHandled;

    public void OnUrlSubmitted()
    {
      interceptHandled = false;
      if (!Utilities.IsValid(_loader) || !Utilities.IsValid(interceptUrl)) return;

      int kind = _loader.ClassifyUrl(interceptUrl.Get());
      if (kind == PlaylistLoader.UrlKindNotOurs) return;

      interceptHandled = true;

      if (kind == PlaylistLoader.UrlKindOtherPool)
      {
        ShowError("errorPoolMismatch");
        return;
      }
      if (kind != PlaylistLoader.UrlKindOwnPlaylist)
      {
        // Web-page URL (/playlists/{id}) or malformed /r/ path: guide the
        // user to the share URL. Rewriting to /r/... is impossible at
        // runtime (string -> VRCUrl conversion is editor-only).
        ShowError("errorSharePageUrl");
        return;
      }

      // Permission first so a viewer without rights sees the permission
      // modal (shown by PermissionManagementUI) rather than a busy message.
      if (Utilities.IsValid(_uiController)
          && !_uiController.InvokeBeforeEvent("BeforeUserLoadPlaylist"))
        return;

      if (_loader.IsLoading)
      {
        ShowError("errorBusy");
        return;
      }

      _loader.LoadPlaylistFromUrlWithFeedback(interceptUrl, this);
    }

    // Called by PlaylistLoader exactly once per feedback load, on every
    // terminal path. Translations are resolved at show time, so no language
    // change listener is needed.
    public void OnLoadResult(int resultCode, string playlistName, int added, int skipped, int httpErrorCode)
    {
      if (!Utilities.IsValid(_uiController)) return;

      if (resultCode == PlaylistLoader.LoadResultSuccess)
      {
        string name = string.IsNullOrEmpty(playlistName) ? "Playlist" : playlistName;
        _uiController.ShowMessage(
            GetTranslation("module.playlistLoader.loadedTitle"),
            GetTranslation("module.playlistLoader.loadedMessage")
                .Replace("{0}", name).Replace("{1}", added.ToString()));
        return;
      }
      if (resultCode == PlaylistLoader.LoadResultPartial)
      {
        _uiController.ShowMessage(
            GetTranslation("module.playlistLoader.loadedTitle"),
            GetTranslation("module.playlistLoader.loadedPartialMessage")
                .Replace("{0}", added.ToString()).Replace("{1}", skipped.ToString()));
        return;
      }

      if (resultCode == PlaylistLoader.LoadResultDownloadError)
      {
        ShowError(httpErrorCode == 404 ? "errorNotFound" : "errorDownload");
        return;
      }
      if (resultCode == PlaylistLoader.LoadResultEmpty)
      {
        ShowError("errorEmpty");
        return;
      }
      if (resultCode == PlaylistLoader.LoadResultPoolMismatch)
      {
        ShowError("errorPoolMismatch");
        return;
      }
      // LoadResultInvalidResponse / LoadResultServerError /
      // LoadResultQueueUnavailable: details are already in the log.
      ShowError("errorInvalidResponse");
    }

    private void ShowError(string messageKey)
    {
      if (!Utilities.IsValid(_uiController)) return;
      _uiController.ShowMessage(
          GetTranslation("module.playlistLoader.errorTitle"),
          GetTranslation("module.playlistLoader." + messageKey));
    }

    private string GetTranslation(string key) => _uiController.GetTranslation(key);
  }
}
