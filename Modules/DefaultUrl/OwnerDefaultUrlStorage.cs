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

    // Two conditions, not one. This is a VRCPlayerObject, so every player owns
    // their own copy and IsOwner is always true for the caller -- on its own it
    // says "this is mine", not "I am allowed to set a default URL". Without the
    // permission check a visitor could save a URL here and have it applied out
    // of nowhere in the next instance they create.
    private bool CanEdit()
    {
      if (!Networking.IsOwner(gameObject)) return false;
      if (_controller == null) return false;
      return _controller.CanEditDefaultUrl();
    }

    public void SaveDefaultUrl(VRCUrl url)
    {
      if (!CanEdit()) return;
      _ownerSavedUrl = url;
      RequestSerialization();
    }

    public void ClearSavedUrl()
    {
      if (!CanEdit()) return;
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
