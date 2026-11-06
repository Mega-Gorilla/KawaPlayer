using VRC.SDKBase;

namespace Yamadev.YamaStream
{
  public static class UrlUtils
  {
    public static string GetProtocolFromUrl(string url)
    {
      if (string.IsNullOrEmpty(url)) return string.Empty;
      int index = url.IndexOf("://");
      if (index == -1) return string.Empty;
      return url.Substring(0, index).ToLower();
    }

    public static string GetProtocol(this VRCUrl url) => GetProtocolFromUrl(url.Get());

    public static string GetHostFromUrl(string url)
    {
      if (string.IsNullOrEmpty(url)) return string.Empty;
      int schemeEnd = url.IndexOf("://");
      if (schemeEnd == -1) return string.Empty;
      string authority = url.Substring(schemeEnd + 3);
      int cut = authority.Length;
      int slashIndex = authority.IndexOf('/');
      if (slashIndex != -1 && slashIndex < cut) cut = slashIndex;
      int queryIndex = authority.IndexOf('?');
      if (queryIndex != -1 && queryIndex < cut) cut = queryIndex;
      int fragmentIndex = authority.IndexOf('#');
      if (fragmentIndex != -1 && fragmentIndex < cut) cut = fragmentIndex;
      authority = authority.Substring(0, cut);
      // Userinfo (`user:pass@host`) ends at the last `@` of the authority.
      int atIndex = authority.LastIndexOf('@');
      if (atIndex != -1) authority = authority.Substring(atIndex + 1);
      int portIndex = authority.IndexOf(':');
      if (portIndex != -1) authority = authority.Substring(0, portIndex);
      while (authority.EndsWith(".")) authority = authority.Substring(0, authority.Length - 1);
      return authority.ToLower();
    }

    public static string GetHost(this VRCUrl url) => GetHostFromUrl(url.Get());

    public static string GetPathFromUrl(string url)
    {
      if (string.IsNullOrEmpty(url)) return string.Empty;
      int schemeEnd = url.IndexOf("://");
      if (schemeEnd == -1) return string.Empty;
      string rest = url.Substring(schemeEnd + 3);
      // The path ends at the first `?` or `#`; a `?`/`#` before any `/`
      // means the authority has no path component at all.
      int cut = rest.Length;
      int queryIndex = rest.IndexOf('?');
      if (queryIndex != -1 && queryIndex < cut) cut = queryIndex;
      int fragmentIndex = rest.IndexOf('#');
      if (fragmentIndex != -1 && fragmentIndex < cut) cut = fragmentIndex;
      rest = rest.Substring(0, cut);
      int slashIndex = rest.IndexOf('/');
      if (slashIndex == -1) return "/";
      return rest.Substring(slashIndex);
    }

    // The translation key of the name of a site whose videos failed when tried
    // on VRChat (issue #361), or empty for any other URL. Only the forms of URL
    // that were tried: it is read after a video has failed, so a site that
    // later plays simply never gets here, and a world keeps the list it was
    // built with.
    public static string GetFailingSiteKey(string url)
    {
      string host = GetHostFromUrl(url);
      if (host == "www.nicovideo.jp") return "site.niconico";
      if (host == "vimeo.com" || host == "www.vimeo.com") return "site.vimeo";
      if (host == "www.tiktok.com") return "site.tiktok";
      if (host == "www.dailymotion.com") return "site.dailymotion";
      if (host == "v.youku.com") return "site.youku";
      if (host == "twitcasting.tv" && GetPathFromUrl(url).Contains("/movie/")) return "site.twitcastingRecording";
      return string.Empty;
    }

    // What a Vimeo video's address, vimeo.com/<number>, is on Vimeo's player,
    // which VRChat's yt-dlp can read without logging in (issue #361). Empty
    // when the URL names no video by its number alone, as a channel's does.
    public static string GetVimeoPlayerUrl(string url)
    {
      string id = GetPathFromUrl(url);
      if (id.EndsWith("/")) id = id.Substring(0, id.Length - 1);
      if (id.Length < 2) return string.Empty;
      id = id.Substring(1);
      for (int i = 0; i < id.Length; i++)
      {
        if (id[i] < '0' || id[i] > '9') return string.Empty;
      }
      return "https://player.vimeo.com/video/" + id;
    }

    public static bool IsYouTubeUrl(string url)
    {
      string host = GetHostFromUrl(url);
      if (string.IsNullOrEmpty(host)) return false;
      return host == "youtube.com" || host.EndsWith(".youtube.com") || host == "youtu.be";
    }
  }
}
