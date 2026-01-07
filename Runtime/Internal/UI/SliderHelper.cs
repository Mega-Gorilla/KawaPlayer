using UdonSharp;
using UnityEngine;

namespace Yamadev.YamaStream.UI
{
    [RequireComponent(typeof(RectTransform))]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SliderHelper : UdonSharpBehaviour
    {
        [SerializeField] private InputController _inputController;
        [SerializeField] private RectTransform _tooltip;
        private RectTransform _trans;
        private float _percent = 0f;

        private void Start()
        {
            _trans = GetComponent<RectTransform>();
        }

        public float Percent => _percent;

        public override void PostLateUpdate()
        {
            Vector3 mousePosition = _inputController.GetMousePosition();
            
            if (mousePosition == Vector3.zero)
            {
                _tooltip.gameObject.SetActive(false);
                return;
            }
            
            Vector3 localPosition = _trans.InverseTransformPoint(mousePosition);
            
            if (!_trans.rect.Contains(localPosition))
            {
                _tooltip.gameObject.SetActive(false);
                return;
            }
            
            float localX = localPosition.x + (_trans.rect.width * _trans.pivot.x);
            _percent = Mathf.Clamp01(localX / _trans.rect.width);
            
            _tooltip.gameObject.SetActive(true);
            Vector2 pos = _tooltip.anchoredPosition;
            pos.x = _percent * _trans.rect.width;
            _tooltip.anchoredPosition = pos;
        }
    }
}