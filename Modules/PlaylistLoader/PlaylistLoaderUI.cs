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
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(OnLoadPlaylistClick))]
    private Button _loadPlaylistButton;

    private UIController _uiController;
    private VRCUrlInputField _mainUrlInput;

    private void Start()
    {
      _uiController = GetComponentInParent<UIController>();
      if (!Utilities.IsValid(_uiController) || !Utilities.IsValid(_loader)) return;

      _mainUrlInput = (VRCUrlInputField)_uiController.GetProgramVariable("_urlInputField");
    }

    public void OnLoadPlaylistClick()
    {
      if (!Utilities.IsValid(_mainUrlInput) || !Utilities.IsValid(_loader)) return;
      if (_loader.IsLoading) return;

      var url = _mainUrlInput.GetUrl();
      if (!Utilities.IsValid(url) || string.IsNullOrEmpty(url.Get()))
      {
        ShowError("URL is empty.");
        return;
      }

      _mainUrlInput.SetUrl(VRCUrl.Empty);
      _loader.LoadPlaylistFromUrl(url);
    }

    public void ShowLoading(string message)
    {
      // Playlist 読み込みは短時間 (~1秒) のため、共有 loading indicator は操作しない
      // 競合回避: UIController の _loadingIndicator / _statusMessageText に触れない
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
      // ShowMessage はモーダルなのでユーザーが閉じる。自動クリア不要
    }
  }
}
