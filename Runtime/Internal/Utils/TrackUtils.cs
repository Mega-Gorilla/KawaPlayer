using VRC.SDKBase;

namespace Yamadev.YamaStream
{
    public static class TrackUtils
    {
        public static object[] NewTrack(VideoPlayerType player, string title, VRCUrl url)
        {
            return new object[] { player, title, url };
        }

        public static object[] CreateEmptyTrack()
        {
            return new object[] { 0, string.Empty, VRCUrl.Empty};
        }

        public static VideoPlayerType GetPlayerType(object[] track)
        {
            return (VideoPlayerType)track[0];
        }

        public static string GetTitle(object[] track)
        {
            return (string)track[1];
        }

        public static void SetTitle(object[] track, string title)
        {
            track[1] = title;
        }

        public static VRCUrl GetUrl(object[] track)
        {
            return (VRCUrl)track[2];
        }

        public static void SetUrl(object[] track, VRCUrl url)
        {
            track[2] = url;
        }
    }
}