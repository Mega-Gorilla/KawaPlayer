using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

namespace Yamadev.YamaStream.Modules.DefaultUrl
{
    [RequireComponent(typeof(VRCPlayerObject))]
    [RequireComponent(typeof(VRCEnablePersistence))]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class OwnerDefaultUrlStorage : YamaPlayerListener
    {
        [SerializeField] private DefaultUrlController _controller;

        [UdonSynced] private VRCUrl _ownerSavedUrl = VRCUrl.Empty;

        public VRCUrl OwnerSavedUrl => _ownerSavedUrl;

        public void SaveDefaultUrl(VRCUrl url)
        {
            if (!Networking.IsOwner(gameObject)) return;
            _ownerSavedUrl = url;
            RequestSerialization();
        }

        public void ClearSavedUrl()
        {
            if (!Networking.IsOwner(gameObject)) return;
            _ownerSavedUrl = VRCUrl.Empty;
            RequestSerialization();
        }

        public override void OnPlayerRestored(VRCPlayerApi player)
        {
            if (player != Networking.LocalPlayer) return;
            if (!player.isInstanceOwner) return;
            if (!Utilities.IsValid(_ownerSavedUrl)) return;
            if (string.IsNullOrEmpty(_ownerSavedUrl.Get())) return;
            if (_controller == null) return;

            _controller.SetDefaultUrl(_ownerSavedUrl);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_controller == null)
                Debug.LogWarning("[OwnerDefaultUrlStorage] " + gameObject.name + ": _controller is not set.", this);
        }
#endif
    }
}
