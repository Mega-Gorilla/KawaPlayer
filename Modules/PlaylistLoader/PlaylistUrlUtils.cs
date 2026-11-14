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
    // /vrcurl/resolve/{i}: a number the search hands out for a playlist
    // (issue #145). The server reuses numbers, so it is never a URL a player
    // is given: the URL field turns it away, and only the loader's own
    // callers (a search result, a refresh) fetch it, with the playlist they
    // expect.
    public const int KindResolveSlot = 5;

    // The fork targets the VHub service; used when no loader (and therefore
    // no _poolBaseUrl config) is available.
    public const string DefaultPoolBaseUrl = "https://playlist.vrc-hub.com";

    // The pool the search's numbers are in, at /vrcurl/{ResolvePoolId}/{i}.
    public const string ResolvePoolId = "resolve";

    // The search, with nothing after "q=": the words are typed after it, and
    // the server takes all that follows as the words.
    public const string SearchPath = "/r/s?q=";

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

      string path = GetTrimmedPath(url);

      if (path.StartsWith("/playlists/")) return KindWebPage;
      if (IsResolvePath(path)) return KindResolveSlot;
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

      string path = GetTrimmedPath(url);

      if (!path.StartsWith("/r/")) return path.Length > 1 ? path : url;

      string rest = path.Substring(3);
      int slashIndex = rest.IndexOf('/');
      if (slashIndex <= 0 || slashIndex == rest.Length - 1) return path;
      return rest;
    }

    // Whether the URL is one of the search's numbers, on any host: the
    // loader asks this of a slot's own URL, which only it put there.
    public static bool IsResolveUrl(string url)
    {
      if (string.IsNullOrEmpty(url)) return false;
      return IsResolvePath(GetTrimmedPath(url));
    }

    // Which playlist a response is, as "{pool}/{id}": the same form
    // GetSourceKey gives a /r/{pool}/{id} URL, so a playlist entered by its
    // URL and one picked from the search are the same playlist.
    public static string MakeSourceKey(string pool, string id) => pool + "/" + id;

    // /vrcurl/resolve/{i}, with a number and nothing after it.
    private static bool IsResolvePath(string path)
    {
      string prefix = "/vrcurl/" + ResolvePoolId + "/";
      if (!path.StartsWith(prefix) || path.Length == prefix.Length) return false;
      for (int i = prefix.Length; i < path.Length; i++)
      {
        char c = path[i];
        if (c < '0' || c > '9') return false;
      }
      return true;
    }

    // The URL's path without its trailing slashes, but never shorter than "/".
    private static string GetTrimmedPath(string url)
    {
      string path = UrlUtils.GetPathFromUrl(url);
      while (path.EndsWith("/") && path.Length > 1) path = path.Substring(0, path.Length - 1);
      return path;
    }
  }
}
