using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace Yamadev.YamaStream.UI
{
  [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
  public class Modal : UdonSharpBehaviour
  {
    [SerializeField] private Text _titleText;
    [SerializeField] private Text _messageText;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(Close))] private Button _closeButton;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(Execute))] private Button _executeButton;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(Execute2))] private Button _execute2Button;
    [SerializeField] private Text _closeText, _executeText, _execute2Text;
    [SerializeField] private float _maxHeight = 400f;
    [SerializeField] private ScrollRect _scrollRect;
    private UdonSharpBehaviour _targetUdon;
    private string _closeEventName, _executeEventName, _execute2EventName;
    private bool _notifyingRefusal;
    private RectTransform _scrollRectTransform;

    private void Start()
    {
      if (Utilities.IsValid(_scrollRect))
      {
        _scrollRectTransform = _scrollRect.GetComponent<RectTransform>();
      }
    }

    // True while a dialog is up that someone is waiting on an answer from.
    // A message has nothing to answer and can be replaced freely; a question
    // cannot, because replacing it drops the events that would have ended
    // the wait, and the dialog does not cover the screen -- whatever is
    // behind it stays clickable while it is open (issue #130).
    public bool IsAwaitingAnswer =>
        gameObject.activeSelf && Utilities.IsValid(_targetUdon) &&
        (!string.IsNullOrEmpty(_closeEventName) ||
         !string.IsNullOrEmpty(_executeEventName) ||
         !string.IsNullOrEmpty(_execute2EventName));

    // Closes first, answers second. The other order left a dialog opened
    // from the callback to be hidden by the line after it -- and once
    // showing refuses to replace a question, that dialog would have been
    // refused instead, since this one is still up while its own callback
    // runs.
    private void ExecuteAndClose(string eventName)
    {
      var target = _targetUdon;
      _targetUdon = null;
      _closeEventName = "";
      _executeEventName = "";
      _execute2EventName = "";
      gameObject.SetActive(false);

      if (Utilities.IsValid(target) && !string.IsNullOrEmpty(eventName))
      {
        target.SendCustomEvent(eventName);
      }
    }

    public void Close() => ExecuteAndClose(_closeEventName);
    public void Execute() => ExecuteAndClose(_executeEventName);
    public void Execute2() => ExecuteAndClose(_execute2EventName);

    // Compiles against the same signature as before, but no longer always
    // shows. A caller that cannot see the refusal and is waiting for an
    // answer is told the same thing cancelling tells it, so it is never left
    // waiting for one that is not coming. Callers that want to know should
    // use TryShow.
    public void Show(string title, string message, string closeText, string executeText, UdonSharpBehaviour targetUdon, string closeEventName, string executeEventName)
    {
      Show(title, message, closeText, executeText, "", targetUdon, closeEventName, executeEventName, "");
    }

    public void Show(string title, string message, string closeText, string executeText, string execute2Text, UdonSharpBehaviour targetUdon, string closeEventName, string executeEventName, string execute2EventName)
    {
      if (TryShow(title, message, closeText, executeText, execute2Text, targetUdon, closeEventName, executeEventName, execute2EventName)) return;
      if (!Utilities.IsValid(targetUdon)) return;
      if (string.IsNullOrEmpty(executeEventName) && string.IsNullOrEmpty(execute2EventName)) return;

      // Only ever one deep: a close handler that shows something is refused
      // again, and answering that refusal too would not end.
      if (_notifyingRefusal) return;
      _notifyingRefusal = true;
      if (!string.IsNullOrEmpty(closeEventName)) targetUdon.SendCustomEvent(closeEventName);
      else Debug.LogWarning($"[Modal] Refused a dialog for {targetUdon.name} while another is waiting for an answer, and it has no close event to be told with.");
      _notifyingRefusal = false;
    }

    public bool TryShow(string title, string message, string closeText, string executeText, UdonSharpBehaviour targetUdon, string closeEventName, string executeEventName)
    {
      return TryShow(title, message, closeText, executeText, "", targetUdon, closeEventName, executeEventName, "");
    }

    // Returns whether the dialog was put up. A caller that changed something
    // in order to show it has to be able to put that back.
    public bool TryShow(string title, string message, string closeText, string executeText, string execute2Text, UdonSharpBehaviour targetUdon, string closeEventName, string executeEventName, string execute2EventName)
    {
      if (IsAwaitingAnswer) return false;

      if (Utilities.IsValid(_titleText)) _titleText.text = title;
      if (Utilities.IsValid(_messageText)) _messageText.text = message;
      if (Utilities.IsValid(_closeText)) _closeText.text = closeText;
      if (Utilities.IsValid(_executeText)) _executeText.text = executeText;
      if (Utilities.IsValid(_execute2Text)) _execute2Text.text = execute2Text;
      _targetUdon = targetUdon;
      _closeEventName = closeEventName;
      _executeEventName = executeEventName;
      _execute2EventName = execute2EventName;

      if (Utilities.IsValid(_executeButton)) _executeButton.gameObject.SetActive(Utilities.IsValid(targetUdon) && !string.IsNullOrEmpty(executeEventName));
      if (Utilities.IsValid(_execute2Button)) _execute2Button.gameObject.SetActive(Utilities.IsValid(targetUdon) && !string.IsNullOrEmpty(execute2EventName));

      gameObject.SetActive(true);
      SendCustomEventDelayedFrames(nameof(AdaptMaxHeight), 3);
      return true;
    }

    public void AdaptMaxHeight()
    {
      if (!Utilities.IsValid(_scrollRect) || !Utilities.IsValid(_scrollRect.content) || !Utilities.IsValid(_scrollRectTransform)) return;
      if (_maxHeight <= 0) return;

      float contentHeight = _scrollRect.content.sizeDelta.y;
      bool over = contentHeight > _maxHeight;
      _scrollRect.vertical = over;
      _scrollRectTransform.sizeDelta = new Vector2(_scrollRectTransform.sizeDelta.x, over ? _maxHeight : contentHeight);
      _scrollRect.verticalNormalizedPosition = 1f;
    }
  }
}