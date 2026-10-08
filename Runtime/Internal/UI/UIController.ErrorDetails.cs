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
    private PlaybackErrorLog _errorLog;

    private PlaybackErrorLog ErrorLog
    {
      get
      {
        if (!Utilities.IsValid(_errorLog) && Utilities.IsValid(_controller)) _errorLog = _controller.GetComponent<PlaybackErrorLog>();
        return _errorLog;
      }
    }

    // Not gated on the log having an entry: the log may hear of this error
    // after the UI does.
    private void SetErrorDetailsButtonShown(bool shown)
    {
      if (Utilities.IsValid(_errorDetailsButton)) _errorDetailsButton.gameObject.SetActive(shown && Utilities.IsValid(ErrorLog));
    }

    private void UpdateErrorDetailsTranslation()
    {
      if (Utilities.IsValid(_errorDetailsButtonLabel)) _errorDetailsButtonLabel.text = GetTranslation("errorDetails.button");
    }

    public void ShowErrorDetails()
    {
      var log = ErrorLog;
      if (!Utilities.IsValid(log) || log.EntryCount == 0) return;
      ShowMessage(GetTranslation("errorDetails.title"), BuildErrorDetails(log));
    }

    // In the order a screenshot needs it: the dialog scrolls past its height,
    // so where to send it and the latest error come first, and the earlier
    // errors and the log's place last.
    private string BuildErrorDetails(PlaybackErrorLog log)
    {
      int max = log.MaxRetry;
      string text = GetTranslation("errorDetails.contact") + "\n" + ErrorContactUrl;

      text += $"\n\n#{log.GetNumber(0)} · {log.GetTime(0)} · {log.GetError(0)}"
        + $"\nPlayer: {PlayerLabel(log.GetPlayer(0))}, {AttemptLabel(log.GetAttempt(0), max)} → "
        + (log.WillRetry(0) ? $"next: {PlayerLabel(log.GetNextPlayer(0))}, retry {log.GetAttempt(0) + 1}/{max}" : "no more retries");
      string title = log.GetTitle(0);
      if (!string.IsNullOrEmpty(title)) text += $"\nTrack: {title}";
      if (log.GetTrackNumber(0) > 0) text += $"\nPlaylist: {log.GetPlaylist(0)} #{log.GetTrackNumber(0)}";
      text += $"\nURL: {log.GetUrl(0)}";
      string detail = log.GetDetail(0);
      if (!string.IsNullOrEmpty(detail)) text += $"\nDetail: {detail}";
      text += "\n" + BuildErrorReportLine(log, max);

      for (int i = 1; i < log.EntryCount; i++)
      {
        text += (i == 1 ? "\n\n" : "\n") + $"#{log.GetNumber(i)} · {log.GetTime(i)} · {log.GetError(i)} · {PlayerLabel(log.GetPlayer(i))}, {AttemptLabel(log.GetAttempt(i), max)}";
      }
#if !UNITY_ANDROID && !UNITY_IOS
      text += "\n\n" + GetTranslation("errorDetails.log").Replace("{0}", VRChatLogPath);
#endif
      return text;
    }

    // One line to quote when writing in, and to find the error in the log by
    // its number.
    private string BuildErrorReportLine(PlaybackErrorLog log, int max)
    {
      string players = PlayerLabel(log.GetPlayer(0));
      if (log.WillRetry(0) && log.GetNextPlayer(0) != log.GetPlayer(0)) players += "→" + PlayerLabel(log.GetNextPlayer(0));
      string host = UrlUtils.GetHostFromUrl(log.GetUrl(0));
      return $"KawaPlayer {_controller.Version} | #{log.GetNumber(0)} {log.GetError(0)} | {players} | retry {log.GetAttempt(0)}/{max} | {(string.IsNullOrEmpty(host) ? "-" : host)} | {PlatformLabel()} | {_controller.MaxResolution}p";
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
