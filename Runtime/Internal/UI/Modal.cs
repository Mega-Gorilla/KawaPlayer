using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace Yamadev.YamaStream.UI
{
  [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
  public class Modal : UdonSharpBehaviour
  {
    [SerializeField] private Text _titleText, _messageText;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(Close))] private Button _closeButton;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(Execute))] private Button _executeButton;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(Execute2))] private Button _execute2Button;
    [SerializeField] private Text _closeText, _executeText, _execute2Text;
    [SerializeField] private float _maxHeight;
    [SerializeField] private ScrollRect _scrollRect;
    private UdonSharpBehaviour _targetUdon;
    private string _closeEventName, _executeEventName, _execute2EventName;
    private RectTransform _scrollRectTransform;

    private void Start()
    {
      _scrollRectTransform = _scrollRect.GetComponent<RectTransform>();
    }

    private void Update() => AdaptMaxHeight();

    public void Close()
    {
      if (Utilities.IsValid(_targetUdon) && !string.IsNullOrEmpty(_closeEventName))
      {
        _targetUdon.SendCustomEvent(_closeEventName);
      }
      gameObject.SetActive(false);
    }

    public void Execute()
    {
      if (Utilities.IsValid(_targetUdon) && !string.IsNullOrEmpty(_executeEventName))
      {
        _targetUdon.SendCustomEvent(_executeEventName);
      }
      gameObject.SetActive(false);
    }
    public void Execute2()
    {
      if (Utilities.IsValid(_targetUdon) && !string.IsNullOrEmpty(_execute2EventName))
      {
        _targetUdon.SendCustomEvent(_execute2EventName);
      }
      gameObject.SetActive(false);
    }

    public void Show(string title, string message, string closeText, string executeText, UdonSharpBehaviour targetUdon, string closeEventName, string executeEventName)
    {
      Show(title, message, closeText, executeText, "", targetUdon, closeEventName, executeEventName, "");
    }

    public void Show(string title, string message, string closeText, string executeText, string execute2Text, UdonSharpBehaviour targetUdon, string closeEventName, string executeEventName, string execute2EventName)
    {
      _titleText.text = title;
      _messageText.text = message;
      _closeText.text = closeText;
      _executeText.text = executeText;
      _execute2Text.text = execute2Text;
      _targetUdon = targetUdon;
      _closeEventName = closeEventName;
      _executeEventName = executeEventName;
      _execute2EventName = execute2EventName;
      gameObject.SetActive(true);
    }

    public void AdaptMaxHeight()
    {
      float contentHeight = _scrollRect.content.sizeDelta.y;
      bool over = contentHeight > _maxHeight;
      _scrollRect.vertical = over;
      _scrollRectTransform.sizeDelta = new Vector2(_scrollRectTransform.sizeDelta.x, over ? _maxHeight : contentHeight);
    }
  }
}