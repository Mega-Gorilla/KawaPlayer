using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Yamadev.YamaStream.Modules.DefaultUrl
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class DefaultUrlController : YamaPlayerListener
    {
        [SerializeField] private Yamadev.YamaStream.Modules.PlaylistLoader.PlaylistLoader _playlistLoader;
        [SerializeField] private Controller _controller;
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
            if (_controller.State != PlayerState.Idle) return;

            if (_defaultUrl.Get().Contains("playlist.vrc-hub.com"))
            {
                if (_playlistLoader == null) return;
                if (_playlistLoader.IsLoading) return;
                _playlistLoader.LoadPlaylistFromUrl(_defaultUrl);
            }
            else
            {
                _controller.PlayTrack(TrackUtils.NewTrack(_videoPlayerType, "", _defaultUrl));
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_playlistLoader == null)
                Debug.LogWarning("[DefaultUrlController] " + gameObject.name + ": _playlistLoader is not set.", this);
            if (_controller == null)
                Debug.LogWarning("[DefaultUrlController] " + gameObject.name + ": _controller is not set.", this);
        }
#endif
    }
}
