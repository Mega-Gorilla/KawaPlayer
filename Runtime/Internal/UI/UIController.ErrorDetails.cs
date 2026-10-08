using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace Yamadev.YamaStream.UI
{
  // The details of the last playback errors, for a viewer to send in when a
  // video will not play (issue #168). A button under the error message opens
  // them in the dialog. The fields are left untranslated, as in the image
  // app's details: they are for the developer the screenshot goes to. What
  // asks the viewer to send it, and where to, is translated.
  public partial class UIController
  {
    private const string ErrorContactUrl = "https://discord.gg/dxYEZWH66M";
    private const string VRChatLogPath = @"%USERPROFILE%\AppData\LocalLow\VRChat\VRChat\output_log_*.txt";

    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(ShowErrorDetails))] private Button _errorDetailsButton;
    [SerializeField] private Text _errorDetailsButtonLabel;

    // The Controller takes the error down before it tells the UI, so the
    // error this button is shown for is already in its log.
    private void SetErrorDetailsButtonShown(bool shown)
    {
      if (Utilities.IsValid(_errorDetailsButton)) _errorDetailsButton.gameObject.SetActive(shown);
    }

    private void UpdateErrorDetailsTranslation()
    {
      if (Utilities.IsValid(_errorDetailsButtonLabel)) _errorDetailsButtonLabel.text = GetTranslation("errorDetails.button");
    }

    public void ShowErrorDetails()
    {
      if (_controller.ErrorLogCount == 0) return;
      ShowMessage(GetTranslation("errorDetails.title"), BuildErrorDetails());
    }

    // In the order a screenshot needs it: the dialog scrolls past its height,
    // so where to send it and the latest error come first, and the earlier
    // errors and the log's place last.
    private string BuildErrorDetails()
    {
      int max = _controller.MaxErrorRetry;
      string text = GetTranslation("errorDetails.contact") + "\n" + ErrorContactUrl;

      text += $"\n\n#{_controller.GetErrorNumber(0)} · {_controller.GetErrorTime(0)} · {_controller.GetVideoError(0)}"
        + $"\nPlayer: {PlayerLabel(_controller.GetErrorPlayer(0))}, {AttemptLabel(_controller.GetErrorAttempt(0), max)} → "
        + (_controller.GetErrorWillRetry(0) ? $"next: {PlayerLabel(_controller.GetErrorNextPlayer(0))}, retry {_controller.GetErrorAttempt(0) + 1}/{max}" : "no more retries");
      string title = _controller.GetErrorTitle(0);
      if (!string.IsNullOrEmpty(title)) text += $"\nTrack: {title}";
      if (_controller.GetErrorTrackNumber(0) > 0) text += $"\nPlaylist: {_controller.GetErrorPlaylist(0)} #{_controller.GetErrorTrackNumber(0)}";
      text += $"\nURL: {_controller.GetErrorUrl(0)}";
      string detail = _controller.GetErrorDetail(0);
      if (!string.IsNullOrEmpty(detail)) text += $"\nDetail: {detail}";
      text += "\n" + BuildErrorReportLine(max);

      for (int i = 1; i < _controller.ErrorLogCount; i++)
      {
        text += (i == 1 ? "\n\n" : "\n") + $"#{_controller.GetErrorNumber(i)} · {_controller.GetErrorTime(i)} · {_controller.GetVideoError(i)} · {PlayerLabel(_controller.GetErrorPlayer(i))}, {AttemptLabel(_controller.GetErrorAttempt(i), max)}";
      }
#if !UNITY_ANDROID && !UNITY_IOS
      text += "\n\n" + GetTranslation("errorDetails.log").Replace("{0}", VRChatLogPath);
#endif
      return text;
    }

    // One line to quote when writing in, and to find the error in the log by
    // its number.
    private string BuildErrorReportLine(int max)
    {
      string players = PlayerLabel(_controller.GetErrorPlayer(0));
      if (_controller.GetErrorWillRetry(0) && _controller.GetErrorNextPlayer(0) != _controller.GetErrorPlayer(0)) players += "→" + PlayerLabel(_controller.GetErrorNextPlayer(0));
      string host = UrlUtils.GetHostFromUrl(_controller.GetErrorUrl(0));
      return $"KawaPlayer {_controller.Version} | #{_controller.GetErrorNumber(0)} {_controller.GetVideoError(0)} | {players} | retry {_controller.GetErrorAttempt(0)}/{max} | {(string.IsNullOrEmpty(host) ? "-" : host)} | {PlatformLabel()} | {_controller.MaxResolution}p";
    }

    private string AttemptLabel(int attempt, int max) => attempt == 0 ? "first try" : $"retry {attempt}/{max}";

    private string PlayerLabel(VideoPlayerType type)
    {
      switch (type)
      {
        case VideoPlayerType.UnityVideoPlayer: return "Unity";
        case VideoPlayerType.AVProVideoPlayer: return "AVPro";
        case VideoPlayerType.ImageViewer: return "Image";
        default: return type.GetString();
      }
    }

    private string PlatformLabel()
    {
      string platform;
#if UNITY_ANDROID
      platform = "Android";
#elif UNITY_IOS
      platform = "iOS";
#else
      platform = "PC";
#endif
      var player = Networking.LocalPlayer;
      return platform + (Utilities.IsValid(player) && player.IsUserInVR() ? " VR" : " Desktop");
    }
  }
}
