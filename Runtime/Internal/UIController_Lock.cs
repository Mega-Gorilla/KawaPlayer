using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace Yamadev.YamaStream.UI
{
    public partial class UIController
    {
        private const int DEFAULT_LAYER = 0; // こっちはビームが出る
        private const int UI_LAYER = 5; // こっちはビームが出ない、ロック時はこっち
        
        [SerializeField] private InputController _inputController;
        [SerializeField] private RectTransform _uiShapeRect;
        [SerializeField] private Text _lockPercent;
        [SerializeField] private float _unlockHoldTime = 1f;
        
        private bool _locked = false;
        private bool _lastInput = false;
        private float _lastInputTime = 0f;
        private bool _pointerInside = false;

        public override void PostLateUpdate()
        {
            if (!Utilities.IsValid(_uiShapeRect) || !Utilities.IsValid(_inputController) || !_locked) return;

            Vector3 mousePosition = _inputController.GetMousePosition();
            
            if (mousePosition == Vector3.zero)
            {
                if (_pointerInside)
                {
                    _systemUIAnimator.SetTrigger("HideLockMessage");
                    _pointerInside = false;
                }
                _lockPercent.text = "";
                return;
            }
            
            Vector3 localPosition = _uiShapeRect.InverseTransformPoint(mousePosition);

            bool pointerInside = _uiShapeRect.rect.Contains(localPosition);
            if (pointerInside != _pointerInside)
            {
                if (pointerInside) _systemUIAnimator.SetTrigger("ShowLockMessage");
                else _systemUIAnimator.SetTrigger("HideLockMessage");
                _pointerInside = pointerInside;
            }

            if (!pointerInside)
            {
                _lockPercent.text = "";
                return;
            }

            if (_lastInputTime != 0f)
            {
                _lockPercent.text = $"{Mathf.Clamp01((Time.time - _lastInputTime) / _unlockHoldTime) * 100f:0}%";

                if (Time.time - _lastInputTime >= _unlockHoldTime)
                {
                    UnlockUI();
                    _lockPercent.text = "";
                    _pointerInside = false;
                    _systemUIAnimator.SetTrigger("HideLockMessage");
                }
            }

            if (!_lastInput)
            {
                _lockPercent.text = "";
            }
        }

        public void LockUI()
        {
            if (!Utilities.IsValid(_uiShapeRect)) return;
            _uiShapeRect.gameObject.layer = UI_LAYER;
            _userUIanimator.SetTrigger("ToggleUI");
            _locked = true;
        }

        public void UnlockUI()
        {
            if (!Utilities.IsValid(_uiShapeRect)) return;
            _uiShapeRect.gameObject.layer = DEFAULT_LAYER;
            _userUIanimator.SetTrigger("ToggleUI");
            _locked = false;
        }

        public override void InputUse(bool value, UdonInputEventArgs args)
        {
            _lastInput = value;

            if (value)
            {
                _lastInputTime = Time.time;
            }
            else
            {
                _lastInputTime = 0f;
            }
        }
    }
}