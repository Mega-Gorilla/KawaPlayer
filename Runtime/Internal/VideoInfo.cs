using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;
using System.Text.RegularExpressions;

namespace Yamadev.YamaStream
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class VideoInfo : Listener
    {
        [SerializeField] private Controller _controller;
        private DataDictionary _info = new DataDictionary();

        private DataDictionary[] _providers = new DataDictionary[]
        {
            new DataDictionary() {
                {"provider", "YouTube"},
                {"pattern", @"^https?:\/\/(?:www\.)?(?:youtube\.com|youtu\.be)\/"}
            },
            new DataDictionary() {
                {"provider", "Twitch"},
                {"pattern", @"^https?:\/\/(?:www\.)?twitch\.tv\/"}
            }
        };

        private void Start() => _controller.AddListener(this);

        public string GetVideoInfo(VRCUrl url)
        {
            if (string.IsNullOrEmpty(url.Get()))
            {
                return string.Empty;
            }

            if (_info.TryGetValue(url.Get(), TokenType.String, out var info))
            {
                return info.String;
            }

            DownloadVideoInfo(url);

            return string.Empty;
        }

        private string GetProviderFromUrl(string url)
        {
            foreach (var provider in _providers)
            {
                if (Regex.IsMatch(url, provider["pattern"].String, RegexOptions.IgnoreCase))
                {
                    return provider["provider"].String;
                }
            }
            return string.Empty;
        }

        private void DownloadVideoInfo(VRCUrl url)
        {
            if (!url.IsValid() || string.IsNullOrEmpty(GetProviderFromUrl(url.Get()))) return;
            VRCStringDownloader.LoadUrl(url, (IUdonEventReceiver)this);
        }

        public override void OnStringLoadSuccess(IVRCStringDownload result)
        {
            string urlStr = result.Url.Get();
            string html = result.Result;

            string provider = GetProviderFromUrl(urlStr);
            string title = string.Empty;

            switch (provider)
            {
                case "YouTube":
                    title = YouTube.GetTitleFromHtml(html);
                    break;
                case "Twitch":
                    title = GetTwitchTitleFromTwitch(html);
                    break;
                default:
                    title = string.Empty;
                    return;
            }

            _info.SetValue(urlStr, title);
            PrintLog($"Loaded video info from {provider}: {title} ({urlStr})");

            if (Utilities.IsValid(_controller.Track) && !_controller.Track.HasTitle())
            {
                _controller.Track.SetTitle(title);
            }

            _controller.SendCustomVideoEvent(nameof(OnVideoInfoLoaded));
        }

        public override void OnStringLoadError(IVRCStringDownload result)
        {
            string urlStr = result.Url.Get();
            _info.SetValue(urlStr, string.Empty);
        }

        public override void OnUrlChanged()
        {
            if (_controller.Track.GetTitle() == string.Empty)
            {
                DownloadVideoInfo(_controller.Track.GetVRCUrl());
            }
        }

        #region HTML parser
        public string GetTwitchTitleFromTwitch(string html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;

            var ldMatches = Regex.Matches(html,
                @"<script[^>]*type\s*=\s*[""']application/ld\+json[""'][^>]*>([\s\S]*?)</script>",
                RegexOptions.IgnoreCase);

            for (int i = 0; i < ldMatches.Count; i++)
            {
                string jsonString = ldMatches[i].Groups[1].Value;
                if (VRCJson.TryDeserializeFromJson(jsonString, out var json))
                {
                    if (json.TokenType == TokenType.DataDictionary)
                    {
                        var dict = json.DataDictionary;
                        if (dict.TryGetValue("@graph", out var graph) &&
                            graph.TokenType == TokenType.DataList &&
                            graph.DataList.Count > 0)
                        {
                            var first = graph.DataList[0];
                            if (first.TokenType == TokenType.DataDictionary &&
                                first.DataDictionary.TryGetValue("name", out var nameFromGraph))
                                return nameFromGraph.String;
                        }
                        if (dict.TryGetValue("name", out var directName))
                        {
                            return directName.String;
                        }
                    }
                    else if (json.TokenType == TokenType.DataList && json.DataList.Count > 0)
                    {
                        var first = json.DataList[0];
                        if (first.TokenType == TokenType.DataDictionary &&
                            first.DataDictionary.TryGetValue("name", out var nameFromArray))
                            return nameFromArray.String;
                    }
                }
                else
                {
                    var nameMatch = Regex.Match(jsonString, @"""name""\s*:\s*""([^""]+)""",
                        RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (nameMatch.Success)
                    {
                        return nameMatch.Groups[1].Value;
                    }
                }
            }

            return string.Empty;
        }
        #endregion
    }
}