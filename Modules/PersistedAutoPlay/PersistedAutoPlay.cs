using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;

namespace Yamadev.YamaStream.Modules.PersistedAutoPlay
{
    [RequireComponent(typeof(VRCPlayerObject))]
    [RequireComponent(typeof(VRCEnablePersistence))]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistedAutoPlay : YamaPlayerListener
    {
        [SerializeField] private Yamadev.YamaStream.Modules.PlaylistLoader.PlaylistLoader _loader;

        [UdonSynced] private VRCUrl _persistedUrl = VRCUrl.Empty;

        public bool HasPersistedUrl => Utilities.IsValid(_persistedUrl) && !string.IsNullOrEmpty(_persistedUrl.Get());
        public VRCUrl PersistedUrl => _persistedUrl;

        public void SaveCurrentUrl(VRCUrl url)
        {
            if (!Networking.IsOwner(gameObject)) return;
            _persistedUrl = url;
            RequestSerialization();
        }

        public void ClearSavedUrl()
        {
            if (!Networking.IsOwner(gameObject)) return;
            _persistedUrl = VRCUrl.Empty;
            RequestSerialization();
        }

        public override void OnPlayerRestored(VRCPlayerApi player)
        {
            if (player != Networking.LocalPlayer) return;
            if (!Utilities.IsValid(_persistedUrl)) return;
            if (string.IsNullOrEmpty(_persistedUrl.Get())) return;
            if (!Utilities.IsValid(_loader) || _loader.IsLoading) return;
            if (_loader.RedirectPool == null || _loader.RedirectPool.Length == 0) return;
            _loader.LoadPlaylistFromUrl(_persistedUrl);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_loader == null)
                Debug.LogWarning("[PersistedAutoPlay] " + gameObject.name + ": _loader (PlaylistLoader) is not set.", this);
        }
#endif
    }
}
