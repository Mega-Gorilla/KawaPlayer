using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;
using System.Text.RegularExpressions;

namespace Yamadev.YamaStream.Modules.VideoInfoDownloader
{
  [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
  public class VideoInfoDownloader : YamaPlayerModule
  {
    private DataDictionary _info = new DataDictionary();
    private DataDictionary _pending = new DataDictionary();

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

    public string GetVideoInfo(VRCUrl url)
    {
      if (!Utilities.IsValid(url) || string.IsNullOrEmpty(url.Get()))
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
      if (string.IsNullOrEmpty(url)) return string.Empty;

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
      if (!Utilities.IsValid(url)) return;

      string urlStr = url.Get();
      if (string.IsNullOrEmpty(urlStr)) return;
      if (string.IsNullOrEmpty(GetProviderFromUrl(urlStr))) return;

      if (_pending.ContainsKey(urlStr)) return;

      _pending.SetValue(urlStr, true);
      VRCStringDownloader.LoadUrl(url, (IUdonEventReceiver)this);
    }

    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
      string urlStr = result.Url.Get();
      string html = result.Result;

      _pending.Remove(urlStr);

      string provider = GetProviderFromUrl(urlStr);
      string title = string.Empty;

      switch (provider)
      {
        case "YouTube":
          title = YouTubeHtmlParser.GetTitleFromHtml(html);
          break;
        case "Twitch":
          title = TwitchHtmlParser.GetTitleFromHtml(html);
          break;
        default:
          return;
      }

      if (string.IsNullOrEmpty(title)) return;

      _info.SetValue(urlStr, title);
      PrintLog($"Loaded video info from {provider}: {title} ({urlStr})");

      if (!Utilities.IsValid(_controller)) return;

      var track = _controller.Track;
      if (track == null || track.Length == 0) return;

      var currentTitle = TrackUtils.GetTitle(track);
      if (string.IsNullOrEmpty(currentTitle))
      {
        TrackUtils.SetTitle(track, title);
        _controller.SendCustomVideoEvent(nameof(AfterTrackUpdated));
      }
    }

    public override void OnStringLoadError(IVRCStringDownload result)
    {
      string urlStr = result.Url.Get();
      _pending.Remove(urlStr);
      PrintLog($"Failed to load video info: {urlStr}");
    }

    public override void AfterTrackLoaded()
    {
      if (!Utilities.IsValid(_controller)) return;

      var track = _controller.Track;
      if (track == null || track.Length == 0) return;

      var title = TrackUtils.GetTitle(track);
      if (string.IsNullOrEmpty(title))
      {
        DownloadVideoInfo(TrackUtils.GetUrl(track));
      }
    }
  }
}
