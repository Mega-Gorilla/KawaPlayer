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

    // The single definition of who may change the default URL. The settings
    // UI and OwnerDefaultUrlStorage both ask here, so the button state, the
    // write guard and the persistence guard cannot drift apart.
    //
    // Instance Owner only, deliberately. The saved URL lives in each player's
    // own VRCPlayerObject and is restored only for the instance owner, so
    // letting anyone else persist one would make the world's default depend
    // on who happens to be in the instance.
    public bool CanEditDefaultUrl()
    {
      if (!Utilities.IsValid(Networking.LocalPlayer)) return false;
      return Networking.LocalPlayer.isInstanceOwner;
    }

    public void SetDefaultUrl(VRCUrl url)
    {
      if (!CanEditDefaultUrl()) return;
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

      // Strict playlist-URL check (issue #82). When the loader is absent,
      // classify with the default VHub host and an empty pool: any
      // playlist-intent URL (/r/..., /playlists/...) stays a silent no-op
      // (nothing can load it), while /vrcurl/... slot URLs and lookalike
      // hosts fall through to the video path as ordinary video URLs.
      if (_playlistLoader == null)
      {
        if (Yamadev.YamaStream.Modules.PlaylistLoader.PlaylistUrlUtils.Classify(
                _defaultUrl.Get(),
                Yamadev.YamaStream.Modules.PlaylistLoader.PlaylistUrlUtils.DefaultPoolBaseUrl,
                "") != Yamadev.YamaStream.Modules.PlaylistLoader.PlaylistUrlUtils.KindNotOurs)
          return;
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
