using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;

namespace Yamadev.YamaStream.Editor.Tests
{
  // Guards the boundary where editor input becomes process arguments. A quote
  // in the input used to close the quoting of the command line and let the
  // rest of it be read as yt-dlp options (issue #104).
  public class YtdlpArgumentTests
  {
    // Anything after this marker is positional, so the tests below check both
    // that the input is a single argv element and that it sits past the marker.
    private const string EndOfOptions = "--";

    // Opens with a dash and carries a quote, so it exercises both halves of
    // the fix at once: ArgumentList keeps it in one piece, the marker keeps it
    // from being read as an option.
    private const string Hostile = "-PL\" --exec \"x";

    [TestCase("PLBCF2DAC6FFB574DE")]
    [TestCase("PL1234567890abcdefghij_-")]
    [TestCase("UUZfGdEZlANJYFVLuXqhqYUw")]
    [TestCase("WL")]
    [TestCase("LL")]
    [TestCase("RDMMc0lUj2wrsFY")]
    public void IsValidPlaylistId_AcceptsRealIds(string playlistId)
    {
      Assert.IsTrue(YtdlpResolver.IsValidPlaylistId(playlistId));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("P")]
    [TestCase("PL123\" --print \"x")]
    [TestCase("PL123 --no-exec")]
    [TestCase("PL123'")]
    [TestCase("PL123\nPL456")]
    [TestCase("PL123\tPL456")]
    [TestCase("-PL123")]
    [TestCase("--exec")]
    [TestCase("PL123;whoami")]
    [TestCase("PL123&PL456")]
    [TestCase("PL123|PL456")]
    [TestCase("PL123$(x)")]
    [TestCase("PL/123")]
    [TestCase("https://www.youtube.com/playlist?list=PL123")]
    public void IsValidPlaylistId_RejectsEverythingElse(string playlistId)
    {
      Assert.IsFalse(YtdlpResolver.IsValidPlaylistId(playlistId));
    }

    [Test]
    public void IsValidPlaylistId_RejectsOverlyLongInput()
    {
      Assert.IsFalse(YtdlpResolver.IsValidPlaylistId(new string('a', 101)));
      Assert.IsTrue(YtdlpResolver.IsValidPlaylistId(new string('a', 100)));
    }

    [TestCase("PLBCF2DAC6FFB574DE", "PLBCF2DAC6FFB574DE")]
    [TestCase("  PLBCF2DAC6FFB574DE  ", "PLBCF2DAC6FFB574DE")]
    [TestCase("https://www.youtube.com/playlist?list=PLBCF2DAC6FFB574DE", "PLBCF2DAC6FFB574DE")]
    [TestCase("http://www.youtube.com/playlist?list=PLBCF2DAC6FFB574DE", "PLBCF2DAC6FFB574DE")]
    [TestCase("https://www.youtube.com/watch?v=abc&list=PLBCF2DAC6FFB574DE", "PLBCF2DAC6FFB574DE")]
    // A repeated parameter used to throw; now the first readable one wins.
    [TestCase("https://www.youtube.com/playlist?list=PLaaaaaaaaaa&list=PLbbbbbbbbbb", "PLaaaaaaaaaa")]
    [TestCase("https://www.youtube.com/playlist?list=%22evil&list=PLaaaaaaaaaa", "PLaaaaaaaaaa")]
    public void TryGetYoutubePlaylistId_ReadsWhatItCan(string input, string expected)
    {
      Assert.IsTrue(YtdlpResolver.TryGetYoutubePlaylistId(input, out string playlistId));
      Assert.AreEqual(expected, playlistId);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    // Used to be handed back unchanged and reach yt-dlp as options.
    [TestCase("--exec before_dl:whoami")]
    [TestCase("-PL123")]
    // Used to throw UriFormatException.
    [TestCase("https://")]
    // Used to throw ArgumentException before it could be read.
    [TestCase("https://www.youtube.com/playlist?list=%22a&list=%22b")]
    [TestCase("https://www.youtube.com/playlist")]
    [TestCase("https://www.youtube.com/playlist?list=")]
    // Percent-encoded quote is decoded before it is judged, then refused.
    [TestCase("https://www.youtube.com/playlist?list=PL%22evil")]
    [TestCase("ftp://example.com/?list=PLaaaaaaaaaa")]
    public void TryGetYoutubePlaylistId_RefusesTheRestWithoutThrowing(string input)
    {
      Assert.IsFalse(YtdlpResolver.TryGetYoutubePlaylistId(input, out string playlistId));
      Assert.AreEqual(string.Empty, playlistId);
    }

    [TestCase("PL123\" --print \"INJECTED")]
    [TestCase("PL123 --exec calc")]
    [TestCase("-PL123")]
    [TestCase("PL123\nPL456")]
    public void PlaylistArguments_KeepHostileInputAsOneElement(string playlistId)
    {
      List<string> arguments = YtdlpResolver.BuildPlaylistExtractionArguments(playlistId);

      Assert.AreEqual(playlistId, arguments[arguments.Count - 1],
        "the input must arrive as a single, unmodified argv element");
      Assert.AreEqual(1, arguments.FindAll(argument => argument == playlistId).Count,
        "the input must appear exactly once");
      Assert.AreEqual(EndOfOptions, arguments[arguments.Count - 2],
        "the input must sit past the end-of-options marker");
    }

    [Test]
    public void PlaylistArguments_FixOptionsAheadOfTheInput()
    {
      List<string> arguments = YtdlpResolver.BuildPlaylistExtractionArguments("PL123");
      int marker = arguments.IndexOf(EndOfOptions);

      Assert.Greater(marker, 0);
      foreach (string option in new[] { "--no-exec", "--ignore-config", "--flat-playlist", "-sij" })
      {
        int at = arguments.IndexOf(option);
        Assert.Greater(at, -1, option + " is missing");
        Assert.Less(at, marker, option + " must precede the end-of-options marker");
      }
    }

    // Setting both is an error at start time, and it would also mean the list
    // this suite checks is not the one that reaches the process.
    [Test]
    public void PlaylistProcess_UsesTheArgumentListAndNotTheArgumentString()
    {
      ProcessStartInfo startInfo = YtdlpResolver.CreatePlaylistExtractionProcess("PLBCF2DAC6FFB574DE");

      Assert.IsTrue(string.IsNullOrEmpty(startInfo.Arguments));
      Assert.AreEqual(
        YtdlpResolver.BuildPlaylistExtractionArguments("PLBCF2DAC6FFB574DE").Count,
        startInfo.ArgumentList.Count);
    }

    [TestCase("https://example.com/a\" --exec \"calc")]
    [TestCase("-https://example.com/a")]
    [TestCase("https://example.com/a b")]
    public void ResolveArguments_KeepHostileUrlAsOneElement(string url)
    {
      List<string> arguments = VideoPlayerResolver.BuildResolveArguments(url, 720);

      Assert.AreEqual(url, arguments[arguments.Count - 1]);
      Assert.AreEqual(1, arguments.FindAll(argument => argument == url).Count);
      Assert.AreEqual(EndOfOptions, arguments[arguments.Count - 2]);
    }

    [Test]
    public void ResolveArguments_KeepTheFormatSelectorInOnePiece()
    {
      List<string> arguments = VideoPlayerResolver.BuildResolveArguments("https://example.com/a", 1080);
      int selector = arguments.IndexOf("-f");

      Assert.Greater(selector, -1);
      Assert.AreEqual("(mp4/best)[height<=?1080][height>=?64][protocol^=http]", arguments[selector + 1]);
    }

    // The list assertions above stop at our own boundary. This one starts the
    // real process with the real arguments and reads back the argv yt-dlp
    // parsed, which is the only thing that covers the OS in between. It is
    // skipped when yt-dlp has not been downloaded, so it never fails a machine
    // that simply has not used the feature.
    [Test]
    public void PlaylistProcess_HandsHostileInputToYtdlpAsOneArgument()
    {
      if (!File.Exists(YtdlpResolver.ExecutablePath))
        Assert.Ignore("yt-dlp is not downloaded on this machine");

      ProcessStartInfo startInfo = YtdlpResolver.CreatePlaylistExtractionProcess(Hostile);
      // Verbose has to lead: anything appended would land past the marker and
      // be read as another positional argument.
      startInfo.ArgumentList.Insert(0, "-v");

      string diagnostics;
      using (Process process = Process.Start(startInfo))
      {
        diagnostics = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        Assert.IsTrue(process.WaitForExit(60000), "yt-dlp did not exit in time");
      }

      string argv = null;
      foreach (string line in diagnostics.Split('\n'))
        if (line.Contains("Command-line config")) argv = line.Trim();

      Assert.IsNotNull(argv, "yt-dlp did not report the command line it parsed");
      // yt-dlp prints the argv as a Python list. The value carries no single
      // quote, so it is reproduced verbatim between single quotes.
      StringAssert.EndsWith("'" + EndOfOptions + "', '" + Hostile + "']", argv);
      StringAssert.DoesNotContain("'--exec'", argv);
    }
  }
}
