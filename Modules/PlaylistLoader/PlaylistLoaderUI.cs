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
    [SerializeField, RegisterEvent(nameof(VRCUrlInputField.onEndEdit), nameof(OnPlaylistUrlSubmit))]
    private VRCUrlInputField _playlistUrlInput;
    [SerializeField] private Text _statusText;
    [SerializeField] private GameObject _loadingIndicator;

    private bool _clearPending;

    public void OnPlaylistUrlSubmit()
    {
      if (!Utilities.IsValid(_playlistUrlInput) || !Utilities.IsValid(_loader)) return;
      var url = _playlistUrlInput.GetUrl();
      _playlistUrlInput.SetUrl(VRCUrl.Empty);
      _loader.LoadPlaylistFromUrl(url);
    }

    public void ShowLoading(string message)
    {
      _clearPending = false;
      if (Utilities.IsValid(_loadingIndicator)) _loadingIndicator.SetActive(true);
      if (Utilities.IsValid(_statusText)) _statusText.text = message;
    }

    public void ShowSuccess(string message)
    {
      _clearPending = false;
      if (Utilities.IsValid(_loadingIndicator)) _loadingIndicator.SetActive(false);
      if (Utilities.IsValid(_statusText)) _statusText.text = message;
      _clearPending = true;
      SendCustomEventDelayedSeconds(nameof(ClearStatus), 5f);
    }

    public void ShowError(string message)
    {
      _clearPending = false;
      if (Utilities.IsValid(_loadingIndicator)) _loadingIndicator.SetActive(false);
      if (Utilities.IsValid(_statusText)) _statusText.text = message;
    }

    public void ClearStatus()
    {
      if (!_clearPending) return;
      _clearPending = false;
      if (Utilities.IsValid(_statusText)) _statusText.text = "";
    }
  }
}
