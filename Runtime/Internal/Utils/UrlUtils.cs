using System;
using VRC.SDKBase;

namespace Yamadev.YamaStream
{
  public static class UrlUtils
  {
    public static string GetProtocolFromUrl(string url)
    {
      int index = url.IndexOf("://");
      if (index == -1) return string.Empty;
      return url.Substring(0, index).ToLower();
    }

    public static string Protocol(this VRCUrl url) => GetProtocolFromUrl(url.Get());

    public static bool IsValidUrl(string url)
    {
      if (string.IsNullOrEmpty(url)) return false;
      var protocols = new string[] { "http", "https", "rtsp", "rtmp", "rtspt", "rtspu", "rtmps", "rtsps" };
      return Array.IndexOf(protocols, GetProtocolFromUrl(url)) >= 0;
    }

    public static bool IsValidUrl(this VRCUrl url)
    {
      var urlString = url.Get();
      return IsValidUrl(urlString);
    }
  }
}
