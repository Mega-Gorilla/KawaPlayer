using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using Yamadev.YamaStream.UI;

namespace Yamadev.YamaStream.Modules.PlaylistLoader
{
  [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
  public class PlaylistLoaderUI : YamaPlayerListener
  {
    [SerializeField] private PlaylistLoader _loader;
    [SerializeField, RegisterEvent(nameof(VRCUrlInputField.onEndEdit), nameof(OnPlaylistUrlSubmit))]
    private VRCUrlInputField _playlistUrlInput;

    private UIController _uiController;

    private void Start()
    {
      _uiController = GetComponentInParent<UIController>();
    }

    public void OnPlaylistUrlSubmit()
    {
      if (!Utilities.IsValid(_playlistUrlInput) || !Utilities.IsValid(_loader)) return;
      if (_loader.IsLoading) return;
      var url = _playlistUrlInput.GetUrl();
      if (!Utilities.IsValid(url) || string.IsNullOrEmpty(url.Get())) return;
      _playlistUrlInput.SetUrl(VRCUrl.Empty);
      _loader.LoadPlaylistFromUrl(url);
    }

    public void ShowNotification(string message)
    {
      if (!Utilities.IsValid(_uiController)) return;
      _uiController.ShowMessage("Playlist Loader", message);
    }
  }
}
