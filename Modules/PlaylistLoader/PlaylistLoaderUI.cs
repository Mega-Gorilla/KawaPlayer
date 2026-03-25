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
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(OnLoadPlaylistClick))]
    private Button _loadPlaylistButton;

    private UIController _uiController;
    private VRCUrlInputField _mainUrlInput;
    private Text _statusMessageText;
    private GameObject _loadingIndicator;

    private bool _clearPending;

    private void Start()
    {
      _uiController = GetComponentInParent<UIController>();
      if (!Utilities.IsValid(_uiController) || !Utilities.IsValid(_loader)) return;

      _mainUrlInput = (VRCUrlInputField)_uiController.GetProgramVariable("_urlInputField");
      _statusMessageText = (Text)_uiController.GetProgramVariable("_statusMessageText");
      _loadingIndicator = (GameObject)_uiController.GetProgramVariable("_loadingIndicator");
    }

    public void OnLoadPlaylistClick()
    {
      if (!Utilities.IsValid(_mainUrlInput) || !Utilities.IsValid(_loader)) return;
      var url = _mainUrlInput.GetUrl();
      _mainUrlInput.SetUrl(VRCUrl.Empty);
      _loader.LoadPlaylistFromUrl(url);
    }

    public void ShowLoading(string message)
    {
      _clearPending = false;
      if (Utilities.IsValid(_loadingIndicator)) _loadingIndicator.SetActive(true);
      if (Utilities.IsValid(_statusMessageText)) _statusMessageText.text = message;
    }

    public void ShowSuccess(string message)
    {
      _clearPending = false;
      if (Utilities.IsValid(_loadingIndicator)) _loadingIndicator.SetActive(false);
      if (Utilities.IsValid(_statusMessageText)) _statusMessageText.text = message;
      _clearPending = true;
      SendCustomEventDelayedSeconds(nameof(ClearStatus), 5f);
    }

    public void ShowError(string message)
    {
      _clearPending = false;
      if (Utilities.IsValid(_loadingIndicator)) _loadingIndicator.SetActive(false);
      if (Utilities.IsValid(_statusMessageText)) _statusMessageText.text = message;
    }

    public void ClearStatus()
    {
      if (!_clearPending) return;
      _clearPending = false;
      if (Utilities.IsValid(_statusMessageText)) _statusMessageText.text = "";
    }
  }
}
