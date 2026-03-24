using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDKBase;

namespace Yamadev.YamaStream.Modules.PlaylistLoader
{
  [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
  public class PlaylistLoaderUI : YamaPlayerListener
  {
    [SerializeField] private PlaylistLoader _loader;
    [SerializeField] private VRCUrlInputField _playlistUrlInput;
    [SerializeField] private Text _statusText;
    [SerializeField] private GameObject _loadingIndicator;

    public void OnPlaylistUrlSubmit()
    {
      if (!Utilities.IsValid(_playlistUrlInput) || !Utilities.IsValid(_loader)) return;
      var url = _playlistUrlInput.GetUrl();
      _playlistUrlInput.SetUrl(VRCUrl.Empty);
      _loader.LoadPlaylistFromUrl(url);
    }

    public void ShowLoading(string message)
    {
      if (Utilities.IsValid(_loadingIndicator)) _loadingIndicator.SetActive(true);
      if (Utilities.IsValid(_statusText)) _statusText.text = message;
    }

    public void ShowSuccess(string message)
    {
      if (Utilities.IsValid(_loadingIndicator)) _loadingIndicator.SetActive(false);
      if (Utilities.IsValid(_statusText)) _statusText.text = message;
      SendCustomEventDelayedSeconds(nameof(ClearStatus), 5f);
    }

    public void ShowError(string message)
    {
      if (Utilities.IsValid(_loadingIndicator)) _loadingIndicator.SetActive(false);
      if (Utilities.IsValid(_statusText)) _statusText.text = message;
    }

    public void ClearStatus()
    {
      if (Utilities.IsValid(_statusText)) _statusText.text = "";
    }
  }
}
