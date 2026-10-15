using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using Yamadev.YamaStream.UI;

namespace Yamadev.YamaStream.Modules.GlobalReload
{
  // The button a world creator places to reload everyone's video (issue
  // #236). It sends through its player's GlobalReload module, which the
  // build links it to; a button whose player has the module missing or
  // switched off is left out of the build (GlobalReloadBuildProcess, issue
  // #248). Its words, in English only, are pictures in LED dots, one for each
  // state: ready to press, reloading (just sent), no video (everyone
  // stopped), and denied, which also draws a red cross over the icon.
  [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
  public class GlobalReloadButton : UdonSharpBehaviour
  {
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(Press))] private Button _button;
    [SerializeField] private GameObject _ready;
    [SerializeField] private GameObject _busy;
    [SerializeField] private GameObject _idle;
    [SerializeField] private GameObject _denied;
    [SerializeField, HideInInspector] private GlobalReload _module;

    // The button's own UIController, which has nothing but the button, as
    // the Controller Bar has only its bar: the permission listener goes under
    // its Canvas/Modules, so a press is asked about as the screen's controls
    // are.
    private UIController _uiController;

    // One press sends: who may press it is settled by where it stands and,
    // with PermissionManagement, by Editor or above, as on VizVid's. Once
    // sent, it waits as long as each player waits before reloading a load it
    // began (Controller.RELOAD_WAIT_WHILE_LOADING): a second press sooner
    // would reload again only those whose load had finished.
    private const float PERMISSION_INTERVAL = 1f;
    private float _sentAt = -1000f;
    private float _checkedAt = -1000f;
    private bool _allowed = true;

    private void Start()
    {
      _uiController = GetComponentInParent<UIController>();
    }

    private void Update()
    {
      if (!Utilities.IsValid(_module)) return;
      UpdateState();
    }

    public void Press()
    {
      if (!Utilities.IsValid(_module) || !_module.CanSend) return;
      if (Time.time - _sentAt < Controller.RELOAD_WAIT_WHILE_LOADING) return;
      _checkedAt = Time.time;
      _allowed = AskPermission();
      if (_allowed)
      {
        _sentAt = Time.time;
        _module.Send();
      }
      UpdateState();
    }

    // The button asks as a press would, once a second, so a player who may not
    // press it sees the cross before trying, and sees it go when given the
    // permission. Its UIController has no dialog, so a refusal puts up no
    // message (PermissionManagement shows one only where there is a dialog).
    // It cannot be pressed then, nor while waiting after a send, nor while
    // everyone is stopped, when there is nothing to reload; its words say which.
    private void UpdateState()
    {
      float now = Time.time;
      if (now - _checkedAt >= PERMISSION_INTERVAL)
      {
        _checkedAt = now;
        _allowed = AskPermission();
      }
      bool denied = !_allowed;
      bool busy = !denied && now - _sentAt < Controller.RELOAD_WAIT_WHILE_LOADING;
      bool idle = !denied && !busy && !_module.CanSend;
      bool ready = !denied && !busy && !idle;
      if (_button.interactable != ready) _button.interactable = ready;
      Show(_ready, ready);
      Show(_busy, busy);
      Show(_idle, idle);
      Show(_denied, denied);
    }

    private bool AskPermission()
    {
      return !Utilities.IsValid(_uiController) || _uiController.InvokeBeforeEvent("BeforeUserGlobalReload");
    }

    private void Show(GameObject state, bool shown)
    {
      if (Utilities.IsValid(state) && state.activeSelf != shown) state.SetActive(shown);
    }
  }
}
