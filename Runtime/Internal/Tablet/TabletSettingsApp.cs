using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace Yamadev.YamaStream.Tablet
{
  // The settings app: the player's own settings, and below them the playback
  // ones the modules add (issue #159). The controls are the tablet
  // UIController's own settings fields, wired in the prefab, so they act and
  // show their values just as they do on the player's screen. All this does
  // is name what the UIController does not, and keep out of sight a category
  // nothing was put in.
  [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
  public class TabletSettingsApp : YamaPlayerBehaviour
  {
    [SerializeField] private TabletScreen _screen;
    [SerializeField] private Text _titleText;
    [SerializeField] private Text _videoLabel;
    [SerializeField] private Text _audioLabel;
    [SerializeField] private Text _languageLabel;
    [SerializeField] private Text _playbackLabel;
    // Under each page of the player's own settings.
    [SerializeField] private Text[] _notes;
    // The playback page has no rows of its own: the DefaultUrl module adds
    // them when the world is built, and without it the category goes.
    [SerializeField] private GameObject _playbackCategory;
    [SerializeField] private Transform _playbackRows;

    private void Start()
    {
      if (Utilities.IsValid(_playbackCategory))
      {
        _playbackCategory.SetActive(Utilities.IsValid(_playbackRows) && _playbackRows.childCount > 0);
      }
      UpdateTranslation();
    }

    public void UpdateTranslation()
    {
      if (!Utilities.IsValid(_screen)) return;
      _screen.SetTranslatedText(_titleText, "menu.settings");
      _screen.SetTranslatedText(_videoLabel, "tablet.settings.video");
      _screen.SetTranslatedText(_audioLabel, "tablet.settings.audio");
      _screen.SetTranslatedText(_languageLabel, "label.languageSelect");
      _screen.SetTranslatedText(_playbackLabel, "tab.playback");
      if (Utilities.IsValid(_notes))
      {
        for (int i = 0; i < _notes.Length; i++) _screen.SetTranslatedText(_notes[i], "tablet.settings.note");
      }
    }
  }
}
