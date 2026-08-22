using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Yamadev.YamaStream.Modules.DefaultUrl
{
  [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
  public class DefaultUrlController : YamaPlayerModule
  {
    [SerializeField] private Yamadev.YamaStream.Modules.PlaylistLoader.PlaylistLoader _playlistLoader;
    [SerializeField] private VideoPlayerType _videoPlayerType = VideoPlayerType.AVProVideoPlayer;

    [UdonSynced] private VRCUrl _defaultUrl = VRCUrl.Empty;

    public VRCUrl DefaultUrl => _defaultUrl;

    public void SetDefaultUrl(VRCUrl url)
    {
      if (!Utilities.IsValid(Networking.LocalPlayer)) return;
      if (!Networking.LocalPlayer.isInstanceOwner) return;
      if (!Networking.IsOwner(gameObject))
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
      _defaultUrl = url;
      RequestSerialization();
      TryAutoPlay();
    }

    public override void OnDeserialization()
    {
      TryAutoPlay();
    }

    private void TryAutoPlay()
    {
      if (_controller == null) return;
      if (!Networking.IsMaster && !_controller.IsLocal) return;

      if (!Utilities.IsValid(_defaultUrl)) return;
      if (string.IsNullOrEmpty(_defaultUrl.Get())) return;
      if (!_controller.Stopped) return;

      // Strict playlist-URL check via the loader (issue #82). When the
      // loader is absent, keep the old substring guard so a stored playlist
      // URL stays a silent no-op instead of falling into the video path.
      if (_playlistLoader == null)
      {
        if (_defaultUrl.Get().Contains("playlist.vrc-hub.com")) return;
        _controller.TakeOwnership();
        _controller.PlayTrack(TrackUtils.NewTrack(_videoPlayerType, "", _defaultUrl));
        return;
      }

      if (_playlistLoader.IsOwnPlaylistUrl(_defaultUrl.Get()))
      {
        if (_playlistLoader.IsLoading) return;
        _playlistLoader.LoadPlaylistFromUrl(_defaultUrl);
      }
      else
      {
        _controller.TakeOwnership();
        _controller.PlayTrack(TrackUtils.NewTrack(_videoPlayerType, "", _defaultUrl));
      }
    }

    // OnValidate warning was removed (#56 review feedback): the warning was a false positive when
    // viewing Modules/DefaultUrl/DefaultUrl.prefab standalone in Project view, since the canonical
    // path wires _playlistLoader via KawaPlayer.prefab override. The built-in nested instance is
    // pre-wired, and standalone scene placements rely on TryAutoPlay's defensive null guard
    // (silent no-op for playlist URLs when _playlistLoader is null) — no crash.
    // UdonSharp does not expose UnityEditor.PrefabUtility or Scene.IsValid() for in-Udon detection
    // of prefab-asset context, so we cannot conditionally suppress; removing OnValidate is cleaner
    // and matches existing modules (PermissionManagement etc.) which do not use OnValidate validation.
  }
}
