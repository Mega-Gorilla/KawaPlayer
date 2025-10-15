using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace Yamadev.YamaStream.UI
{
    public partial class UIController
    {
        [SerializeField] private InputController _inputController;
        [SerializeField] private RectTransform _detectRect;
        [SerializeField] private BoxCollider _canvasCollider;
        [SerializeField] private Text _lockPercent;
        [SerializeField] float _uploadHoldTime = 1f;
        private bool _locked = false;
        private bool _lastInput = false;
        private float _lastInputTime = 0f;
        private bool _pointerInside = false;

        public override void PostLateUpdate()
        {
            if (!Utilities.IsValid(_detectRect) || !Utilities.IsValid(_inputController) || !_locked) return;

            Vector3 mousePosition = _inputController.GetMousePosition();
            Vector3 localPosition = _detectRect.InverseTransformPoint(mousePosition);

            bool pointerInside = _detectRect.rect.Contains(localPosition);
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
                _lockPercent.text = $"{Mathf.Clamp01((Time.time - _lastInputTime) / _uploadHoldTime) * 100f:0}%";

                if (Time.time - _lastInputTime >= _uploadHoldTime)
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
            if (!Utilities.IsValid(_detectRect)) return;
            _canvasCollider.size = new Vector3(0, 0, 0);
            _userUIanimator.SetTrigger("ToggleUI");
            _locked = true;
        }

        public void UnlockUI()
        {
            if (!Utilities.IsValid(_detectRect)) return;
            var box = _detectRect.GetComponent<BoxCollider>();
            if (box)
            {
                _canvasCollider.size = new Vector3(box.size.x, box.size.y, box.size.z);
            }
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