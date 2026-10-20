using System.Collections.Generic;
using NUnit.Framework;

namespace Yamadev.YamaStream.Editor.Tests
{
  // Pins how the Playlist Editor reads a JSON file (issues #207, #208): a file
  // is taken whole or not at all, a track that names no player plays on AVPro,
  // and a player is written as a number or a name.
  public class PlaylistJsonImportTests
  {
    private static List<PlaylistData> Read(string json)
    {
      Assert.IsTrue(PlaylistExporter.TryParse(json, out List<PlaylistData> playlists, out string error), error);
      Assert.IsNull(error);
      return playlists;
    }

    private static void Refused(string json)
    {
      Assert.IsFalse(PlaylistExporter.TryParse(json, out List<PlaylistData> playlists, out string error), json);
      Assert.IsEmpty(playlists, json);
      Assert.IsFalse(string.IsNullOrEmpty(error), json);
    }

    [Test]
    public void ReadsWhatExportWrites()
    {
      var playlists = Read(@"{ ""playlists"": [ { ""active"": false, ""name"": ""A"",
        ""tracks"": [ { ""playerType"": 2, ""title"": ""t"", ""url"": ""u"" } ],
        ""youtubeListId"": ""y"", ""vhubPlaylistUrl"": ""v"" } ] }");
      Assert.AreEqual(1, playlists.Count);
      var playlist = playlists[0];
      Assert.IsFalse(playlist.active);
      Assert.AreEqual("A", playlist.name);
      Assert.AreEqual("y", playlist.youtubeListId);
      Assert.AreEqual("v", playlist.vhubPlaylistUrl);
      Assert.AreEqual(1, playlist.tracks.Count);
      Assert.AreEqual(VideoPlayerType.ImageViewer, playlist.tracks[0].playerType);
      Assert.AreEqual("t", playlist.tracks[0].title);
      Assert.AreEqual("u", playlist.tracks[0].url);
    }

    [Test]
    public void LeftOutKeysTakeTheirDefaults()
    {
      var playlist = Read(@"{ ""playlists"": [ { ""tracks"": [ { ""url"": ""u"" } ] }, {} ] }")[0];
      Assert.IsTrue(playlist.active);
      Assert.AreEqual("", playlist.name);
      // A URL entered in the world plays on AVPro; so does a track that does not say (issue #207).
      Assert.AreEqual(VideoPlayerType.AVProVideoPlayer, playlist.tracks[0].playerType);
      Assert.AreEqual("", playlist.tracks[0].title);
    }

    [Test]
    public void WrittenZeroStaysUnity()
    {
      var track = Read(@"{ ""playlists"": [ { ""tracks"": [ { ""playerType"": 0 } ] } ] }")[0].tracks[0];
      Assert.AreEqual(VideoPlayerType.UnityVideoPlayer, track.playerType);
    }

    [Test]
    public void PlayerByNameInAnyCase()
    {
      var tracks = Read(@"{ ""playlists"": [ { ""tracks"": [
        { ""playerType"": ""AVProVideoPlayer"" }, { ""playerType"": ""imageviewer"" }, { ""playerType"": ""UNITYVIDEOPLAYER"" } ] } ] }")[0].tracks;
      Assert.AreEqual(VideoPlayerType.AVProVideoPlayer, tracks[0].playerType);
      Assert.AreEqual(VideoPlayerType.ImageViewer, tracks[1].playerType);
      Assert.AreEqual(VideoPlayerType.UnityVideoPlayer, tracks[2].playerType);
    }

    [Test]
    public void ReadsYamaPlayerModeAndCapitalKeys()
    {
      var playlist = Read(@"{ ""Playlists"": [ { ""Name"": ""B"", ""Active"": true,
        ""Tracks"": [ { ""Mode"": 0, ""Title"": ""x"", ""Url"": ""y"" }, { ""mode"": 2 }, { ""PlayerType"": 2 } ] } ] }")[0];
      Assert.AreEqual("B", playlist.name);
      Assert.AreEqual(VideoPlayerType.UnityVideoPlayer, playlist.tracks[0].playerType);
      Assert.AreEqual("x", playlist.tracks[0].title);
      Assert.AreEqual("y", playlist.tracks[0].url);
      Assert.AreEqual(VideoPlayerType.ImageViewer, playlist.tracks[1].playerType);
      Assert.AreEqual(VideoPlayerType.ImageViewer, playlist.tracks[2].playerType);
    }

    [Test]
    public void NullCountsAsLeftOut()
    {
      var playlist = Read(@"{ ""playlists"": [ { ""name"": null, ""tracks"": [ { ""playerType"": null } ] } ] }")[0];
      Assert.AreEqual("", playlist.name);
      Assert.AreEqual(VideoPlayerType.AVProVideoPlayer, playlist.tracks[0].playerType);
    }

    [Test]
    public void DateLikeTextStaysText()
    {
      // Newtonsoft reads such strings as dates by default, and they were refused as not strings (PR #278, F1).
      var playlist = Read(@"{ ""playlists"": [ { ""name"": ""2026-10-20T04:00:00Z"",
        ""tracks"": [ { ""title"": ""2026-10-20T04:00:00+09:00"", ""url"": ""https://example.com/video.mp4"" }, { ""title"": ""\/Date(1198908717056)\/"" } ] } ] }")[0];
      Assert.AreEqual("2026-10-20T04:00:00Z", playlist.name);
      Assert.AreEqual("2026-10-20T04:00:00+09:00", playlist.tracks[0].title);
      Assert.AreEqual("/Date(1198908717056)/", playlist.tracks[1].title);
    }

    [Test]
    public void ReadsBackWhatExportWrites()
    {
      var written = new List<PlaylistData>
      {
        new PlaylistData
        {
          active = false, name = "2026-10-20T04:00:00Z", youtubeListId = "y", vhubPlaylistUrl = "v",
          tracks = new List<PlaylistTrack>
          {
            new PlaylistTrack { playerType = VideoPlayerType.UnityVideoPlayer, title = "2026-10-20", url = "https://example.com/a.mp4" },
            new PlaylistTrack { playerType = VideoPlayerType.ImageViewer, title = "picture", url = "https://example.com/b.png" },
          }
        },
        new PlaylistData { active = true, name = "empty", tracks = new List<PlaylistTrack>() },
      };
      var read = Read(PlaylistExporter.Serialize(written));
      Assert.AreEqual(written.Count, read.Count);
      for (int i = 0; i < written.Count; i++)
      {
        Assert.AreEqual(written[i].active, read[i].active);
        Assert.AreEqual(written[i].name, read[i].name);
        Assert.AreEqual(written[i].youtubeListId ?? "", read[i].youtubeListId);
        Assert.AreEqual(written[i].vhubPlaylistUrl ?? "", read[i].vhubPlaylistUrl);
        Assert.AreEqual(written[i].tracks.Count, read[i].tracks.Count);
        for (int j = 0; j < written[i].tracks.Count; j++)
        {
          Assert.AreEqual(written[i].tracks[j].playerType, read[i].tracks[j].playerType);
          Assert.AreEqual(written[i].tracks[j].title, read[i].tracks[j].title);
          Assert.AreEqual(written[i].tracks[j].url, read[i].tracks[j].url);
        }
      }
    }

    [Test]
    public void AnEmptyListIsRead()
    {
      Assert.IsEmpty(Read(@"{ ""playlists"": [] }"));
    }

    [Test]
    public void RefusesWhatIsNotJsonOrHasNoPlaylists()
    {
      Refused("{");
      Refused(@"{ ""playlists"": [] } x");
      Refused("[]");
      Refused("{}");
      Refused(@"{ ""playlists"": {} }");
    }

    [Test]
    public void RefusesAPlayerThatIsNotOne()
    {
      Refused(@"{ ""playlists"": [ { ""tracks"": [ { ""playerType"": 7 } ] } ] }");
      Refused(@"{ ""playlists"": [ { ""tracks"": [ { ""playerType"": ""7"" } ] } ] }");
      Refused(@"{ ""playlists"": [ { ""tracks"": [ { ""playerType"": ""UnityVideoPlayer, AVProVideoPlayer"" } ] } ] }");
      Refused(@"{ ""playlists"": [ { ""tracks"": [ { ""playerType"": 1.5 } ] } ] }");
      Refused(@"{ ""playlists"": [ { ""tracks"": [ { ""playerType"": ""avpro"" } ] } ] }");
    }

    [Test]
    public void RefusesValuesOfTheWrongType()
    {
      Refused(@"{ ""playlists"": [ 5 ] }");
      Refused(@"{ ""playlists"": [ { ""active"": ""true"" } ] }");
      Refused(@"{ ""playlists"": [ { ""name"": 123 } ] }");
      Refused(@"{ ""playlists"": [ { ""tracks"": {} } ] }");
      Refused(@"{ ""playlists"": [ { ""tracks"": [ 5 ] } ] }");
      Refused(@"{ ""playlists"": [ { ""tracks"": [ { ""url"": 1 } ] } ] }");
    }

    [Test]
    public void OneBadPlaylistTakesNothingFromTheFile()
    {
      // Before issue #208, the playlists before the bad one were added and the rest dropped.
      Refused(@"{ ""playlists"": [ { ""name"": ""ok"", ""tracks"": [ { ""playerType"": 1 } ] },
        { ""name"": ""bad"", ""tracks"": [ { ""playerType"": ""x"" } ] }, { ""name"": ""after"" } ] }");
    }
  }
}
