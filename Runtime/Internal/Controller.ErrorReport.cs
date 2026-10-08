using VRC.SDK3.Components.Video;

namespace Yamadev.YamaStream
{
  // What the last playback error was, for the details a viewer can send in
  // when a video will not play (issue #168). Taken before HandleErrorRetry,
  // which may switch to the fallback handler for the next try, so these
  // describe the try that failed. Local, like the errors themselves.
  public partial class Controller
  {
    private int _errorNumber;
    private VideoPlayerType _lastErrorPlayerType;
    private int _lastErrorAttempt;
    private string _lastErrorDetail = string.Empty;

    // Every error since this player joined is numbered, so the details and
    // the log line of one error carry the same number.
    public int ErrorNumber => _errorNumber;
    public VideoPlayerType LastErrorPlayerType => _lastErrorPlayerType;
    // 0 for the first try, n for the nth retry.
    public int LastErrorAttempt => _lastErrorAttempt;
    // What the handler knows beyond the error type; empty when nothing.
    public string LastErrorDetail => _lastErrorDetail;
    public int MaxErrorRetry => _maxErrorRetry;
    // Retries scheduled for the current track. After an error it is above
    // LastErrorAttempt only when another try is coming.
    public int ErrorRetryCount => _errorRetryCount;

    private void RecordError(VideoError videoError)
    {
      var handler = ActiveHandler;
      _errorNumber++;
      _lastErrorPlayerType = handler.Type;
      _lastErrorAttempt = _errorRetryCount;
      _lastErrorDetail = handler.ErrorDetail;
      PrintLog($"Error #{_errorNumber}: {handler.Type.GetString()}: Video error {videoError}.");
    }
  }
}
