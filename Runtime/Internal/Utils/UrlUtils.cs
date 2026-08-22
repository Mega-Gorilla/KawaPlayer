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

    public static bool IsYouTubeUrl(string url)
    {
      string host = GetHostFromUrl(url);
      if (string.IsNullOrEmpty(host)) return false;
      return host == "youtube.com" || host.EndsWith(".youtube.com") || host == "youtu.be";
    }
  }
}
