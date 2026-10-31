using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace Yamadev.YamaStream.UI
{
  // The settings, one panel for every UI that has them: the screen's
  // settings page and the tablet's settings app (issue #309). The controls
  // are the UIController's own settings fields, wired in each UI, so they act
  // and show their values the same everywhere, and modules add their rows
  // and their category when the world is built (ModuleUISlot.inSettings).
  // All this does is name what the UIController does not, and on a tablet
  // held upright, lay the categories out as tabs along the top.
  [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
  public class SettingsPanel : YamaPlayerBehaviour
  {
    [SerializeField] private Text _titleText;
    [SerializeField] private Text _playbackLabel;
    [SerializeField] private Text _videoLabel;
    [SerializeField] private Text _audioLabel;
    [SerializeField] private Text _languageLabel;
    [SerializeField] private Text _noteText;
    // The category toggles. Lying flat, its layout group stacks them on the
    // left; upright, TabletScreen switches the group off and this places
    // them, a category a module added among them.
    [SerializeField] private RectTransform _categories;
    [SerializeField] private float _tabGap = 12f;

    private UIController _uiController;
    private bool _portrait;
    private int _tabCount = -1;

    private void Start()
    {
      _uiController = GetComponentInParent<UIController>();
      if (Utilities.IsValid(_uiController)) _uiController.AddListener(this);
      UpdateTranslation();
    }

    public void AfterLanguageChanged() => UpdateTranslation();

    private void UpdateTranslation()
    {
      if (!Utilities.IsValid(_uiController)) return;
      SetText(_titleText, "menu.settings");
      SetText(_playbackLabel, "tab.playback");
      SetText(_videoLabel, "settings.video");
      SetText(_audioLabel, "settings.audio");
      SetText(_languageLabel, "label.languageSelect");
      SetText(_noteText, "settings.note");
    }

    private void SetText(Text text, string key)
    {
      if (!Utilities.IsValid(text)) return;
      string value = _uiController.GetTranslation(key);
      if (!string.IsNullOrEmpty(value)) text.text = value;
    }

    // Upright, the categories share the width as tabs, the icon over the
    // name; lying flat, the icon is beside the name. The tab's own size and
    // place lying flat are its layout group's.
    public void SetPortrait(bool portrait)
    {
      _portrait = portrait;
      LayOutCategories();
    }

    // A module's category can come and go while the settings are open (the
    // permission one shows only to those who may change permissions), so
    // upright the tabs are placed again when the number shown changes.
    private void Update()
    {
      if (_portrait && Utilities.IsValid(_categories) && CountTabs() != _tabCount) LayOutCategories();
    }

    private int CountTabs()
    {
      int count = 0;
      for (int i = 0; i < _categories.childCount; i++)
      {
        if (_categories.GetChild(i).gameObject.activeSelf) count++;
      }
      return count;
    }

    private void LayOutCategories()
    {
      if (!Utilities.IsValid(_categories)) return;
      _tabCount = CountTabs();
      int index = 0;
      for (int i = 0; i < _categories.childCount; i++)
      {
        RectTransform tab = _categories.GetChild(i).GetComponent<RectTransform>();
        if (!Utilities.IsValid(tab)) continue;
        // A hidden tab is placed inside too, so it is right when it shows.
        PlaceInTab(tab.Find("Icon"), tab.Find("Label"), _portrait);
        if (!tab.gameObject.activeSelf) continue;
        if (_portrait)
        {
          tab.anchorMin = new Vector2((float)index / _tabCount, 0f);
          tab.anchorMax = new Vector2((float)(index + 1) / _tabCount, 1f);
          tab.pivot = new Vector2(0.5f, 0.5f);
          tab.anchoredPosition = Vector2.zero;
          tab.sizeDelta = new Vector2(-_tabGap, 0f);
        }
        index++;
      }
    }

    private void PlaceInTab(Transform icon, Transform label, bool portrait)
    {
      if (Utilities.IsValid(icon))
      {
        RectTransform rect = icon.GetComponent<RectTransform>();
        if (portrait) Place(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(56f, 56f));
        else Place(rect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(56f, 0f), new Vector2(64f, 64f));
      }
      if (Utilities.IsValid(label))
      {
        RectTransform rect = label.GetComponent<RectTransform>();
        Text text = label.GetComponent<Text>();
        if (portrait)
        {
          Place(rect, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 30f), new Vector2(-16f, 44f));
          if (Utilities.IsValid(text)) text.alignment = TextAnchor.MiddleCenter;
        }
        else
        {
          // From 20 after the icon to 12 short of the button's end: the
          // longest name (Spanish's "Reproducción", 218 at 36) fits a
          // category 340 wide (#337).
          Place(rect, Vector2.zero, Vector2.one, new Vector2(48f, 0f), new Vector2(-120f, 0f));
          if (Utilities.IsValid(text)) text.alignment = TextAnchor.MiddleLeft;
        }
      }
    }

    private void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
      if (!Utilities.IsValid(rect)) return;
      rect.anchorMin = anchorMin;
      rect.anchorMax = anchorMax;
      rect.anchoredPosition = position;
      rect.sizeDelta = size;
    }
  }
}
