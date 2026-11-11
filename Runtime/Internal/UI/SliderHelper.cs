using UdonSharp;
using UnityEngine;
using VRC.Udon.Common;
using static VRC.SDKBase.VRCPlayerApi;

namespace Yamadev.YamaStream.UI
{
  [RequireComponent(typeof(RectTransform))]
  [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
  public class SliderHelper : YamaPlayerBehaviour
  {
    [SerializeField] private RectTransform _tooltip;
    private RectTransform _rect;
    private float _percent = 0f;
    private bool _rightHand = true;
    private RaycastHit[] _rayHits = new RaycastHit[TrackingUtils.MAX_RAY_HITS];

    private void Start()
    {
      _rect = GetComponent<RectTransform>();
    }

    public float Percent => _percent;

    public override void PostLateUpdate()
    {
      var trackingDataType = IsInVR ? (_rightHand ? TrackingDataType.RightHand : TrackingDataType.LeftHand) : TrackingDataType.Head;
      Vector3 mousePosition = TrackingUtils.GetMousePosition(LocalPlayer, trackingDataType, _rayHits);

      if (mousePosition == Vector3.zero)
      {
        _tooltip.gameObject.SetActive(false);
        return;
      }

      Vector3 localPosition = _rect.InverseTransformPoint(mousePosition);

      if (!_rect.rect.Contains(localPosition))
      {
        _tooltip.gameObject.SetActive(false);
        return;
      }

      // A bar taller than it is wide (the volume bar in its panel, issue
      // #258) runs from the bottom up; any other, from left to right.
      bool vertical = _rect.rect.height > _rect.rect.width;
      if (vertical)
      {
        float localY = localPosition.y + (_rect.rect.height * _rect.pivot.y);
        _percent = Mathf.Clamp01(localY / _rect.rect.height);
      }
      else
      {
        float localX = localPosition.x + (_rect.rect.width * _rect.pivot.x);
        _percent = Mathf.Clamp01(localX / _rect.rect.width);
      }

      _tooltip.gameObject.SetActive(true);
      Vector3 pos = _tooltip.localPosition;
      if (vertical) pos.y = localPosition.y;
      else pos.x = localPosition.x;
      _tooltip.localPosition = pos;
    }

    public override void InputUse(bool value, UdonInputEventArgs args)
    {
      _rightHand = args.handType == HandType.RIGHT;
    }

    public override void InputGrab(bool value, UdonInputEventArgs args)
    {
      _rightHand = args.handType == HandType.RIGHT;
    }
  }
}