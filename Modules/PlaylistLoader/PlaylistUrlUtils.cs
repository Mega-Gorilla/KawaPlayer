namespace Yamadev.YamaStream.Modules.PlaylistLoader
{
  // Static classification of VHub playlist URLs (issue #82), shared between
  // PlaylistLoader (with its pool config) and callers that have no loader
  // instance (DefaultUrl's null-loader guard).
  public static class PlaylistUrlUtils
  {
    public const int KindNotOurs = 0;
    public const int KindOwnPlaylist = 1;
    public const int KindOtherPool = 2;
    public const int KindWebPage = 3;
    public const int KindMalformed = 4;

    // The fork targets the VHub service; used when no loader (and therefore
    // no _poolBaseUrl config) is available.
    public const string DefaultPoolBaseUrl = "https://playlist.vrc-hub.com";

    // Accepts http and https so a pasted http playlist URL surfaces a
    // playlist-flavored error instead of a video-player error. Pass an empty
    // poolId to classify pool-agnostically (own pool then never matches).
    public static int Classify(string url, string poolBaseUrl, string poolId)
    {
      if (string.IsNullOrEmpty(url)) return KindNotOurs;
      string scheme = UrlUtils.GetProtocolFromUrl(url);
      if (scheme != "http" && scheme != "https") return KindNotOurs;
      string host = UrlUtils.GetHostFromUrl(url);
      if (string.IsNullOrEmpty(host) || host != UrlUtils.GetHostFromUrl(poolBaseUrl)) return KindNotOurs;

      string path = UrlUtils.GetPathFromUrl(url);
      while (path.EndsWith("/") && path.Length > 1) path = path.Substring(0, path.Length - 1);

      if (path.StartsWith("/playlists/")) return KindWebPage;
      if (path == "/r") return KindMalformed;
      if (!path.StartsWith("/r/")) return KindNotOurs;

      // path = /r/{pool}/{playlistId...}; any non-empty remainder counts as
      // the id (the server rejects ids it does not know).
      string rest = path.Substring(3);
      int slashIndex = rest.IndexOf('/');
      if (slashIndex <= 0 || slashIndex == rest.Length - 1) return KindMalformed;
      string pool = rest.Substring(0, slashIndex);
      return pool == poolId ? KindOwnPlaylist : KindOtherPool;
    }

    // Stable identity for a playlist URL, used to recognize a reload of the
    // same playlist (issue #88). Reduces /r/{pool}/{id} to "{pool}/{id}" so a
    // trailing slash or a different scheme does not read as a different
    // playlist and consume a second slot. Anything unrecognized falls back to
    // the path (or the raw URL), which still matches itself.
    public static string GetSourceKey(string url)
    {
      if (string.IsNullOrEmpty(url)) return string.Empty;

      string path = UrlUtils.GetPathFromUrl(url);
      while (path.EndsWith("/") && path.Length > 1) path = path.Substring(0, path.Length - 1);

      if (!path.StartsWith("/r/")) return path.Length > 1 ? path : url;

      string rest = path.Substring(3);
      int slashIndex = rest.IndexOf('/');
      if (slashIndex <= 0 || slashIndex == rest.Length - 1) return path;
      return rest;
    }
  }
}
