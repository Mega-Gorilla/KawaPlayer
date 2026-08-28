using System.IO;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine.Networking;
using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

using Debug = UnityEngine.Debug;

namespace Yamadev.YamaStream.Editor
{
  // What the yt-dlp process actually reported, with no interpretation layered
  // on top. Callers need every field to tell a real failure apart from an
  // empty playlist: yt-dlp signals trouble through the exit code, through
  // stderr and through an empty stdout, and reading only one of them is how
  // a failed fetch used to reach the editor disguised as a valid result
  // (issue #101).
  public class YtdlpPlaylistResult
  {
    public bool Success;
    // Set alongside Success when the dump may be incomplete. Kept separate
    // from the counts because a shortfall is not always measurable: yt-dlp can
    // report trouble through the exit code alone.
    public bool IsPartial;
    public int ExitCode;
    public bool TimedOut;
    public bool Cancelled;
    public List<string> JsonLines = new List<string>();
    // n_entries from the flat playlist dump: how many items yt-dlp meant to
    // emit. Null when the field is absent, in which case a shortfall cannot
    // be measured and no partial-success claim is made.
    public int? ExpectedCount;
    // Raw stderr. Never parsed or translated, only forwarded to the Console.
    public string Diagnostics;
    // The request was turned away before yt-dlp ran because the id could not
    // be one (issue #104). Distinct from a failed fetch: nothing was tried.
    public bool InvalidInput;
  }

  public static class YtdlpResolver
  {
#if UNITY_EDITOR_WIN
    private const string FILENAME = "yt-dlp.exe";
#elif UNITY_EDITOR_OSX
    private const string FILENAME = "yt-dlp_macos";
#elif UNITY_EDITOR_LINUX
    private const string FILENAME = "yt-dlp_linux";
#else
    private const string FILENAME = "yt-dlp";
#endif

#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
    private const uint EXECUTABLE_PERMISSION = 0x100 | 0x40 | 0x80 | 0x20 | 0x8 | 0x4 | 0x1; // 0755
#endif

    private static readonly string DownloadUrl = $"https://github.com/yt-dlp/yt-dlp/releases/latest/download/{FILENAME}";
    public static readonly string ExecutablePath = Path.Combine(Path.GetTempPath(), FILENAME);

    // Generous enough for a playlist with a few thousand entries, but finite:
    // yt-dlp retries ten times per request by default, so an unreachable host
    // would otherwise hold the progress bar open indefinitely.
    private const int ExtractionTimeoutSeconds = 90;
    private const int PollIntervalMilliseconds = 100;

#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
    [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
    private static extern int SetFilePermissions(string path, uint mode);
#endif

    public static bool IsAvailable => File.Exists(ExecutablePath);

    private static bool ShowDownloadConfirmationDialog()
    {
      var title = IsAvailable ? EditorLocalization.Get("ytdlp.update") : EditorLocalization.Get("ytdlp.download");
      var message = IsAvailable ? EditorLocalization.Get("ytdlp.updateMessage") : EditorLocalization.Get("ytdlp.downloadMessage");
      return EditorUtility.DisplayDialog(
          title,
          message,
          EditorLocalization.Get("button.yes"),
          EditorLocalization.Get("button.no")
      );
    }

    public static async UniTask<bool> EnsureYtdlpAvailable()
    {
      if (IsAvailable) return true;

      return await DownloadYtdlpExecutable();
    }

    public static async UniTask<YtdlpPlaylistResult> GetPlaylist(string playlistId)
    {
      if (string.IsNullOrEmpty(playlistId))
      {
        Debug.LogError(EditorLocalization.Get("ytdlp.urlEmpty"));
        return new YtdlpPlaylistResult();
      }

      if (!IsValidPlaylistId(playlistId))
      {
        Debug.LogError($"{EditorLocalization.Get("ytdlp.invalidPlaylistId")} ({playlistId})");
        return new YtdlpPlaylistResult { InvalidInput = true };
      }

      if (!await EnsureYtdlpAvailable())
      {
        Debug.LogError(EditorLocalization.Get("ytdlp.notAvailable"));
        return new YtdlpPlaylistResult();
      }

      return await ExecutePlaylistExtraction(playlistId);
    }

    // YouTube playlist ids are URL-safe base64 and always open with a letter
    // or a digit. Turning anything else away keeps quotes, whitespace and
    // leading dashes from ever reaching the argument boundary, and it costs
    // nothing: no real id is excluded by this (issue #104).
    private static readonly Regex PlaylistIdPattern =
      new Regex(@"^[A-Za-z0-9][A-Za-z0-9_-]{1,99}$", RegexOptions.CultureInvariant);

    public static bool IsValidPlaylistId(string playlistId)
    {
      return !string.IsNullOrEmpty(playlistId) && PlaylistIdPattern.IsMatch(playlistId);
    }

    public static async UniTask<bool> DownloadYtdlpExecutable()
    {
      if (!ShowDownloadConfirmationDialog())
      {
        Debug.LogWarning(EditorLocalization.Get("ytdlp.cancelledByUser"));
        return false;
      }

      var progressTitle = EditorLocalization.Get("ytdlp.downloading");

      try
      {
        EditorUtility.DisplayProgressBar(progressTitle, EditorLocalization.Get("ytdlp.downloadingExecutable"), 0.5f);

        using (var request = UnityWebRequest.Get(DownloadUrl))
        {
          var downloadHandler = new DownloadHandlerFile(ExecutablePath)
          {
            removeFileOnAbort = true
          };
          request.downloadHandler = downloadHandler;

          await request.SendWebRequest();

          if (request.result != UnityWebRequest.Result.Success)
          {
            Debug.LogError($"{EditorLocalization.Get("ytdlp.downloadFailed")}: {request.error}");
            return false;
          }
        }

        if (!SetExecutablePermissions())
        {
          Debug.LogWarning(EditorLocalization.Get("ytdlp.permissionFailed"));
        }

        Debug.Log(EditorLocalization.Get("ytdlp.downloadSuccess"));
        return true;
      }
      catch (Exception ex)
      {
        Debug.LogError($"{EditorLocalization.Get("ytdlp.exception")}: {ex.Message}");
        return false;
      }
      finally
      {
        EditorUtility.ClearProgressBar();
      }
    }

    private static bool SetExecutablePermissions()
    {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
      try
      {
        int result = SetFilePermissions(ExecutablePath, EXECUTABLE_PERMISSION);
        return result == 0;
      }
      catch (Exception ex)
      {
        Debug.LogWarning($"{EditorLocalization.Get("ytdlp.permissionFailed")}: {ex.Message}");
        return false;
      }
#else
      return true;
#endif
    }

    private static async UniTask<YtdlpPlaylistResult> ExecutePlaylistExtraction(string playlistUrl)
    {
      var progressTitle = EditorLocalization.Get("ytdlp.extracting");
      var result = new YtdlpPlaylistResult();

      try
      {
        var processInfo = CreatePlaylistExtractionProcess(playlistUrl);

        using (var process = Process.Start(processInfo))
        {
          if (process == null)
          {
            throw new InvalidOperationException(EditorLocalization.Get("ytdlp.startFailed"));
          }

          var outputTask = process.StandardOutput.ReadToEndAsync();
          var errorTask = process.StandardError.ReadToEndAsync();
          var reads = System.Threading.Tasks.Task.WhenAll(outputTask, errorTask);

          // Waiting on the reads alone would hand the window to yt-dlp with no
          // way back: it retries ten times per request by default and nothing
          // here bounds the total. Polling instead lets the progress bar offer
          // Cancel and gives the wait a deadline. UniTask drives its player
          // loop from EditorApplication.update outside play mode, so the delay
          // ticks while the editor is idle.
          double deadline = EditorApplication.timeSinceStartup + ExtractionTimeoutSeconds;
          while (!reads.IsCompleted)
          {
            if (EditorUtility.DisplayCancelableProgressBar(progressTitle, $"{progressTitle}: {playlistUrl}", 0.5f))
            {
              result.Cancelled = true;
              break;
            }
            if (EditorApplication.timeSinceStartup > deadline)
            {
              result.TimedOut = true;
              break;
            }
            await UniTask.Delay(PollIntervalMilliseconds, DelayType.Realtime);
          }

          if (result.Cancelled || result.TimedOut)
          {
            // Killing the process releases the pending reads. Whatever they
            // hold is a truncated dump, so it is never used.
            KillProcess(process);
            try { await reads.AsUniTask(); } catch (Exception) { }
            if (result.TimedOut) Debug.LogError($"{EditorLocalization.Get("ytdlp.extractFailed")}: {EditorLocalization.Get("ytdlp.timeout")}");
            return result;
          }

          await reads.AsUniTask();
          await UniTask.RunOnThreadPool(() => process.WaitForExit());

          result.ExitCode = process.ExitCode;
          result.Diagnostics = errorTask.Result;
          result.JsonLines = ParsePlaylistOutput(outputTask.Result);
          result.ExpectedCount = ReadExpectedCount(result.JsonLines);
          // A dump with no entries is treated as a failure even when yt-dlp
          // exited cleanly. That misreads a genuinely empty playlist, but the
          // alternative is overwriting the playlist being edited with nothing.
          result.Success = result.JsonLines.Count > 0;
          // Entries came back, but something still went wrong along the way:
          // --ignore-errors lets yt-dlp finish a playlist it could not read in
          // full and report that only through a non-zero exit code, so the
          // shortfall is not always countable. Either signal is enough to stop
          // and ask before the result replaces anything.
          result.IsPartial = result.Success
            && (result.ExitCode != 0
              || (result.ExpectedCount.HasValue && result.JsonLines.Count < result.ExpectedCount.Value));

          LogDiagnostics(result);
          return result;
        }
      }
      catch (Exception ex)
      {
        Debug.LogError($"{EditorLocalization.Get("ytdlp.extractException")}: {ex.Message}");
        return result;
      }
      finally
      {
        EditorUtility.ClearProgressBar();
      }
    }

    private static void KillProcess(Process process)
    {
      try
      {
        if (!process.HasExited) process.Kill();
      }
      catch (Exception ex)
      {
        Debug.LogWarning($"{EditorLocalization.Get("ytdlp.extractException")}: {ex.Message}");
      }
    }

    // yt-dlp writes its own messages in the console code page, which is CP932
    // on a Japanese Windows, while the JSON is pure ASCII either way. Forcing
    // both sides to UTF-8 is what keeps the "YouTube said: ..." text readable
    // once it reaches the Console.
    public static ProcessStartInfo CreatePlaylistExtractionProcess(string playlistId)
    {
      var startInfo = new ProcessStartInfo(ExecutablePath)
      {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        StandardOutputEncoding = new UTF8Encoding(false),
        StandardErrorEncoding = new UTF8Encoding(false),
        CreateNoWindow = true,
        WorkingDirectory = Path.GetTempPath()
      };

      foreach (var argument in BuildPlaylistExtractionArguments(playlistId))
        startInfo.ArgumentList.Add(argument);

      return startInfo;
    }

    // One list entry per argv element. Building a single command line instead
    // let a quote in the id close the quoting and append options of its own
    // (issue #104); ArgumentList hands the value to the process as it stands,
    // on every platform.
    public static List<string> BuildPlaylistExtractionArguments(string playlistId)
    {
      return new List<string>
      {
        "--extractor-args", "youtube:lang=ja",
        "--flat-playlist",
        "--no-write-playlist-metafiles",
        "--no-exec",
        "--ignore-config",
        "--encoding", "utf-8",
        "-sij",
        // Everything past this is positional, so even an id that opened with
        // a dash could not be read as an option. yt-dlp honours it.
        "--",
        playlistId,
      };
    }

    // The diagnostics go to the Console verbatim. Part of the text comes from
    // YouTube itself and is already localized by the lang extractor argument,
    // so matching phrases against it would be guesswork; the user-facing
    // wording is chosen upstream from a fixed set instead.
    private static void LogDiagnostics(YtdlpPlaylistResult result)
    {
      if (string.IsNullOrWhiteSpace(result.Diagnostics)) return;

      var lines = result.Diagnostics
        .Split('\n')
        .Select(line => line.Trim())
        .Where(line => line.Length > 0)
        .ToList();
      if (lines.Count == 0) return;

      // The Console list only shows the first line of an entry, so the last
      // line yt-dlp wrote is repeated up there: it reports the failure that
      // ended the run, after any warnings it passed along the way. Picking by
      // position keeps this free of any judgement about what the text says.
      var message = $"[yt-dlp] exit={result.ExitCode} {lines[lines.Count - 1]}\n{string.Join("\n", lines)}";
      if (result.Success) Debug.LogWarning(message);
      else Debug.LogError(message);
    }

    private static List<string> ParsePlaylistOutput(string output)
    {
      if (string.IsNullOrEmpty(output)) return new List<string>();

      return output
        .Split('\n')
        .Where(line => !string.IsNullOrWhiteSpace(line) && line.Trim().StartsWith("{"))
        .ToList();
    }

    [Serializable]
    private struct EntryCount
    {
      public int n_entries;
    }

    // n_entries is how many items yt-dlp set out to emit, which is what a
    // short dump should be measured against. playlist_count is the size of the
    // whole playlist and does not match once a range is requested. Absent or
    // zero means the shortfall cannot be measured.
    private static int? ReadExpectedCount(List<string> jsonLines)
    {
      if (jsonLines.Count == 0) return null;

      try
      {
        var count = UnityEngine.JsonUtility.FromJson<EntryCount>(jsonLines[0]).n_entries;
        return count > 0 ? count : (int?)null;
      }
      catch
      {
        return null;
      }
    }

    // Accepts a bare playlist id or a YouTube address carrying one, and never
    // throws: a malformed url, a repeated list parameter and stray whitespace
    // all just mean "no id here". The old version returned anything that was
    // not an https url unchanged, so an option-looking string reached yt-dlp
    // intact, and it threw on two inputs a person can easily paste (issue
    // #104): a url with two list parameters, and a truncated https url.
    public static bool TryGetYoutubePlaylistId(string input, out string playlistId)
    {
      playlistId = string.Empty;
      if (string.IsNullOrEmpty(input)) return false;

      string trimmed = input.Trim();
      if (IsValidPlaylistId(trimmed))
      {
        playlistId = trimmed;
        return true;
      }

      if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri uri)) return false;
      if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;

      foreach (string pair in uri.Query.TrimStart('?').Split('&'))
      {
        int separator = pair.IndexOf('=');
        if (separator < 0) continue;
        if (pair.Substring(0, separator) != "list") continue;

        // Decoded before it is judged, so a percent-encoded quote is seen for
        // what it is. A repeated list parameter loses instead of throwing:
        // the first one that reads as an id wins.
        string candidate = Uri.UnescapeDataString(pair.Substring(separator + 1)).Trim();
        if (!IsValidPlaylistId(candidate)) continue;

        playlistId = candidate;
        return true;
      }

      return false;
    }

    public static string GetYoutubePlaylistIdFromUrl(string url)
    {
      return TryGetYoutubePlaylistId(url, out string playlistId) ? playlistId : string.Empty;
    }
  }
}