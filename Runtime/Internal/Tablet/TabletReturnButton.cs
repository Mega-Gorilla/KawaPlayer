using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace Yamadev.YamaStream.Tablet
{
  // A button the world can place anywhere, to bring back tablets that were
  // left behind. Each tablet listed goes back to where it started, unless
  // someone is holding it.
  [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
  public class TabletReturnButton : YamaPlayerBehaviour
  {
    [SerializeField] private TabletPickup[] _tablets = new TabletPickup[0];
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(ReturnTablets))] private Button _button;
    [SerializeField] private Text _label;

    private void Start()
    {
      if (!Utilities.IsValid(_label) || _tablets.Length == 0 || !Utilities.IsValid(_tablets[0])) return;

      // Says it in the first tablet's language and font. The translation
      // comes first: asking for it is what sets the font.
      string text = _tablets[0].GetTranslation("tablet.return");
      if (string.IsNullOrEmpty(text)) return;
      _label.text = text;
      Font font = _tablets[0].CurrentFont;
      if (Utilities.IsValid(font)) _label.font = font;
    }

    public void ReturnTablets()
    {
      foreach (var tablet in _tablets)
      {
        if (Utilities.IsValid(tablet)) tablet.ReturnToSpawn();
      }
    }
  }
}
