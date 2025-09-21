using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using Yamadev.YamaStream.Libraries.GenericDataContainer;
using System.Text.RegularExpressions;

namespace Yamadev.YamaStream
{
    public class YouTubePlaylist : UdonSharpBehaviour
    {
        public static YouTubePlaylist New(string name, DataList<Track> tracks)
        {
            object[] result = new object[] { name, tracks };
            return (YouTubePlaylist)(object)result;
        }

        public static YouTubePlaylist Empty()
        {
            return (YouTubePlaylist)(object)new object[] { string.Empty, DataList<Track>.New() };
        }
    }

    public static class YouTubePlaylistExtentions
    {
        public static string GetName(this YouTubePlaylist obj)
        {
            return (string)((object[])(object)obj)[0];
        }

        public static DataList<Track> GetTracks(this YouTubePlaylist obj)
        {
            return (DataList<Track>)((object[])(object)obj)[1];
        }
    }

    public class YouTubeCaptionTrack : UdonSharpBehaviour
    {
        public static YouTubeCaptionTrack New(string baseUrl, string languageCode)
        {
            object[] result = new object[] { baseUrl, languageCode };
            return (YouTubeCaptionTrack)(object)result;
        }
    }

    public static class YouTubeCaptionTrackExtentions
    {
        public static string GetBaseUrl(this YouTubeCaptionTrack obj)
        {
            return (string)((object[])(object)obj)[0];
        }

        public static string GetLanguageCode(this YouTubeCaptionTrack obj)
        {
            return (string)((object[])(object)obj)[1];
        }
    }

    public static class YouTube
    {
        public static YouTubePlaylist ParsePlaylist(string playlistJson)
        {
            if (string.IsNullOrEmpty(playlistJson) || !VRCJson.TryDeserializeFromJson(playlistJson, out var json))
            {
                return YouTubePlaylist.Empty();
            }

            var tracks = DataList<Track>.New();
            DataDictionary dict = json.DataDictionary["playlist"].DataDictionary;
            string playlistName = dict["title"].String;
            DataList contents = dict["contents"].DataList;

            for (int i = 0; i < contents.Count; i++)
            {
                if (contents[i].DataDictionary.TryGetValue("playlistPanelVideoRenderer", out var renderer))
                {
                    // Play both video and live in AVPro video player.
                    bool isLive = renderer.DataDictionary.TryGetValue("badges", out var badges) &&
                        badges.DataList.TryGetValue(0, out var badge) &&
                        badge.DataDictionary["metadataBadgeRenderer"].DataDictionary["icon"].DataDictionary["iconType"].String == "LIVE";
                    string title = renderer.DataDictionary["title"].DataDictionary["simpleText"].String;
                    string url = $"https://www.youtube.com/watch?v={renderer.DataDictionary["videoId"].String}";
                    tracks.Add(Track.New(VideoPlayerType.AVProVideoPlayer, title, VRCUrl.Empty, url));
                }
            }

            return YouTubePlaylist.New(playlistName, tracks);
        }

        public static DataList<Track> ParsePlaylistRenderer(string playlistJson)
        {
            var tracks = DataList<Track>.New();

            if (string.IsNullOrEmpty(playlistJson) || !VRCJson.TryDeserializeFromJson(playlistJson, out var json)) return tracks;
            DataList contents = json.DataDictionary["playlistVideoListRenderer"].DataDictionary["contents"].DataList;

            for (int i = 0; i < contents.Count; i++)
            {
                DataDictionary renderer = contents[i].DataDictionary["playlistVideoRenderer"].DataDictionary;
                // VRCUrl.TryCreateAllowlistedVRCUrl($"https://www.youtube.com/watch?v={renderer["videoId"].String}", out VRCUrl outputUrl);
                // Play both video and live in AVPro video player.
                bool isLive = renderer["thumbnailOverlays"].DataList[0].DataDictionary.TryGetValue("thumbnailOverlayTimeStatusRenderer", out var thu) &&
                    thu.DataDictionary["style"] == "LIVE";
                string title = renderer["title"].DataDictionary["runs"].DataList[0].DataDictionary["text"].String;
                string url = $"https://www.youtube.com/watch?v={renderer["videoId"].String}";
                tracks.Add(Track.New(VideoPlayerType.AVProVideoPlayer, title, VRCUrl.Empty, url));
            }

            return tracks;
        }

        public static YouTubePlaylist GetPlaylistFromHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return YouTubePlaylist.Empty();

            var dataMatch = Regex.Match(html, @"ytInitialData\s*=\s*(\{[\s\S]*?\});", RegexOptions.IgnoreCase);
            if (!dataMatch.Success) return YouTubePlaylist.Empty();

            string ytJson = dataMatch.Groups[1].Value;

            string playlistName = string.Empty;
            var nameMatch = Regex.Match(ytJson, @"""pageHeaderRenderer""[\s\S]*?""pageTitle""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (nameMatch.Success)
            {
                playlistName = nameMatch.Groups[1].Value;
            }
            else
            {
                var metaNameMatch = Regex.Match(ytJson, @"""playlistMetadataRenderer""[\s\S]*?""title""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                if (metaNameMatch.Success) playlistName = metaNameMatch.Groups[1].Value;
            }

            var tracks = DataList<Track>.New();

            var panelMatches = Regex.Matches(ytJson, @"""playlistPanelVideoRenderer""\s*:\s*(\{[\s\S]*?\})", RegexOptions.IgnoreCase);
            for (int i = 0; i < panelMatches.Count; i++)
            {
                string block = panelMatches[i].Groups[1].Value;
                var idMatch = Regex.Match(block, @"""videoId""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                var titleMatch = Regex.Match(block, @"""title""\s*:\s*\{[\s\S]*?""simpleText""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                if (idMatch.Success && titleMatch.Success)
                {
                    string url = "https://www.youtube.com/watch?v=" + idMatch.Groups[1].Value;
                    tracks.Add(Track.New(VideoPlayerType.AVProVideoPlayer, titleMatch.Groups[1].Value, VRCUrl.Empty, url));
                }
            }

            if (panelMatches.Count == 0)
            {
                var videoMatches = Regex.Matches(ytJson, @"""playlistVideoRenderer""\s*:\s*(\{[\s\S]*?\})", RegexOptions.IgnoreCase);
                for (int i = 0; i < videoMatches.Count; i++)
                {
                    string block = videoMatches[i].Groups[1].Value;
                    var idMatch = Regex.Match(block, @"""videoId""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                    var titleMatch = Regex.Match(block, @"""title""\s*:\s*\{[\s\S]*?""runs""\s*:\s*\[\s*\{[\s\S]*?""text""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                    if (idMatch.Success && titleMatch.Success)
                    {
                        string url = "https://www.youtube.com/watch?v=" + idMatch.Groups[1].Value;
                        tracks.Add(Track.New(VideoPlayerType.AVProVideoPlayer, titleMatch.Groups[1].Value, VRCUrl.Empty, url));
                    }
                }
            }

            return YouTubePlaylist.New(playlistName, tracks);
        }

        public static string GetTitleFromHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;

            var prMatch = Regex.Match(html, @"ytInitialPlayerResponse\s*=\s*(\{[\s\S]*?\});", RegexOptions.IgnoreCase);
            if (prMatch.Success)
            {
                string prJson = prMatch.Groups[1].Value;
                if (VRCJson.TryDeserializeFromJson(prJson, out var json) &&
                    json.TokenType == TokenType.DataDictionary &&
                    json.DataDictionary.TryGetValue("videoDetails", out var videoDetails) &&
                    videoDetails.TokenType == TokenType.DataDictionary &&
                    videoDetails.DataDictionary.TryGetValue("title", out var titleToken))
                {
                    return titleToken.String;
                }
                var direct = Regex.Match(prJson, @"""videoDetails""[\s\S]*?""title""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                if (direct.Success) return direct.Groups[1].Value;
            }

            return string.Empty;
        }

        public static DataList<YouTubeCaptionTrack> GetCaptionTracksFromHtml(string html)
        {
            var results = DataList<YouTubeCaptionTrack>.New();
            if (string.IsNullOrEmpty(html))
            {
                return results;
            }

            var prMatch = Regex.Match(html, @"ytInitialPlayerResponse\s*=\s*(\{[\s\S]*?\});", RegexOptions.IgnoreCase);
            if (!prMatch.Success)
            {
                return results;
            }

            string prJson = prMatch.Groups[1].Value;
            if (!VRCJson.TryDeserializeFromJson(prJson, out var pr) || pr.TokenType != TokenType.DataDictionary)
            {
                return results;
            }

            DataDictionary prDict = pr.DataDictionary;
            if (!prDict.TryGetValue("captions", out var captions) || captions.TokenType != TokenType.DataDictionary)
            {
                return results;
            }

            DataDictionary capDict = captions.DataDictionary;
            if (!capDict.TryGetValue("playerCaptionsTracklistRenderer", out var tracklist) || tracklist.TokenType != TokenType.DataDictionary)
            {
                return results;
            }

            if (!tracklist.DataDictionary.TryGetValue("captionTracks", out var tracksToken) || tracksToken.TokenType != TokenType.DataList)
            {
                return results;
            }

            DataList captionTracks = tracksToken.DataList;
            for (int i = 0; i < captionTracks.Count; i++)
            {
                if (captionTracks[i].TokenType != TokenType.DataDictionary) continue;
                DataDictionary item = captionTracks[i].DataDictionary;
                if (!item.TryGetValue("baseUrl", out var baseUrlTok) || !item.TryGetValue("languageCode", out var langTok)) continue;

                string baseUrl = baseUrlTok.String;
                string languageCode = langTok.String;
                if (!string.IsNullOrEmpty(baseUrl) && !string.IsNullOrEmpty(languageCode))
                {
                    results.Add(YouTubeCaptionTrack.New(baseUrl, languageCode));
                }
            }

            return results;
        }
    }
}