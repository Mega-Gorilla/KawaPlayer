using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDKBase;
using Yamadev.YamaStream.UI;

namespace Yamadev.YamaStream.Modules.PlaylistLoader
{
  [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
  public class PlaylistLoaderUI : YamaPlayerListener
  {
    [SerializeField] private PlaylistLoader _loader;
    [SerializeField] private VRCUrlInputField _playlistUrlInput;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(OnLoadPlaylistClick))]
    private Button _loadPlaylistButton;

    private UIController _uiController;

    private void Start()
    {
      _uiController = GetComponentInParent<UIController>();
    }

    public void OnLoadPlaylistClick()
    {
      if (!Utilities.IsValid(_playlistUrlInput) || !Utilities.IsValid(_loader)) return;
      if (_loader.IsLoading) return;

      var url = _playlistUrlInput.GetUrl();
      if (!Utilities.IsValid(url) || string.IsNullOrEmpty(url.Get()))
      {
        ShowError("URL is empty.");
        return;
      }

      _playlistUrlInput.SetUrl(VRCUrl.Empty);
      _loader.LoadPlaylistFromUrl(url);
    }

    public void ShowLoading(string message)
    {
    }

    public void ShowSuccess(string message)
    {
      if (!Utilities.IsValid(_uiController)) return;
      _uiController.ShowMessage("Playlist Loader", message);
    }

    public void ShowError(string message)
    {
      if (!Utilities.IsValid(_uiController)) return;
      _uiController.ShowMessage("Playlist Loader", message);
    }

    public void ClearStatus()
    {
    }
  }
}
