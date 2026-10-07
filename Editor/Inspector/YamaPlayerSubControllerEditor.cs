using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Components;
using Yamadev.YamaStream.Tablet;
using Yamadev.YamaStream.UI;

namespace Yamadev.YamaStream.Editor
{
  [CustomEditor(typeof(YamaPlayerSubController))]
  public class YamaPlayerSubControllerEditor : EditorBase
  {
    private YamaPlayerSubController _target;
    private SerializedProperty _yamaPlayer;

    private AppearanceSettings _appearanceSettings;
    private SerializedObject _appearanceSerializedObject;
    private SerializedProperty _defaultColorSet;

    private LocalizationSettings _localizationSettings;
    private SerializedObject _localizationSerializedObject;
    private SerializedProperty _defaultLanguage;
    private SerializedProperty _languages;

    private UIController _uiController;
    private SerializedObject _uiControllerSerializedObject;
    private SerializedProperty _idleScreenSprite;

    private VRCPickup _vrcPickup;
    private SerializedObject _vrcPickupSerializedObject;
    private SerializedProperty _pickupable;
    private bool _globalSync;

    private TabletPickup _tabletPickup;
    private SerializedObject _tabletPickupSerializedObject;
    private SerializedProperty _idleReturnSeconds;
    private TabletReturnButton _tabletReturnButton;
    private SerializedObject _visitorRecorderSerializedObject;
    private SerializedProperty _visitorPhotosEnabled;

    private void OnEnable()
    {
      _target = target as YamaPlayerSubController;
      _yamaPlayer = serializedObject.FindProperty("YamaPlayer");

      if (Application.isPlaying) return;

      _appearanceSettings = _target.GetComponentInChildren<AppearanceSettings>(true);
      if (_appearanceSettings != null)
      {
        _appearanceSerializedObject = new SerializedObject(_appearanceSettings);
        _defaultColorSet = _appearanceSerializedObject.FindProperty("defaultColorSet");
      }

      _localizationSettings = _target.GetComponentInChildren<LocalizationSettings>(true);
      if (_localizationSettings != null)
      {
        _localizationSerializedObject = new SerializedObject(_localizationSettings);
        _defaultLanguage = _localizationSerializedObject.FindProperty("defaultLanguage");
        _languages = _localizationSerializedObject.FindProperty("languages");
      }

      _uiController = _target.GetComponentInChildren<UIController>(true);
      if (_uiController != null)
      {
        _uiControllerSerializedObject = new SerializedObject(_uiController);
        _idleScreenSprite = _uiControllerSerializedObject.FindProperty("_idleScreenSprite");
      }

      _vrcPickup = _target.GetComponentInChildren<VRCPickup>(true);
      if (_vrcPickup != null)
      {
        _vrcPickupSerializedObject = new SerializedObject(_vrcPickup);
        _pickupable = _vrcPickupSerializedObject.FindProperty("pickupable");
      }

      _tabletPickup = _target.GetComponentInChildren<TabletPickup>(true);
      if (_tabletPickup != null)
      {
        _tabletPickupSerializedObject = new SerializedObject(_tabletPickup);
        _idleReturnSeconds = _tabletPickupSerializedObject.FindProperty("_idleReturnSeconds");
      }
      _tabletReturnButton = _target.GetComponentInChildren<TabletReturnButton>(true);
      var visitorRecorder = _target.GetComponentInChildren<TabletVisitorRecorder>(true);
      if (visitorRecorder != null)
      {
        _visitorRecorderSerializedObject = new SerializedObject(visitorRecorder);
        _visitorPhotosEnabled = _visitorRecorderSerializedObject.FindProperty("_photosEnabled");
      }
    }

    public override void OnInspectorGUI()
    {
      base.OnInspectorGUI();
      serializedObject.Update();

      if (Application.isPlaying)
      {
        EditorGUILayout.HelpBox(EditorLocalization.Get("msg.playModeNotAvailable"), MessageType.Info);
        return;
      }

      using (var check = new EditorGUI.ChangeCheckScope())
      {
        EditorGUILayout.PropertyField(_yamaPlayer, EditorLocalization.GetLayout("label.yamaPlayer"));
        if (check.changed)
        {
          serializedObject.ApplyModifiedProperties();
          ProcessYamaPlayerChange();
        }
      }

      EditorGUILayout.Space(SpaceMedium);

      if (_target.YamaPlayer == null)
      {
        EditorGUILayout.HelpBox(EditorLocalization.Get("msg.yamaPlayerRequired"), MessageType.Warning);
      }

      using (var check = new EditorGUI.ChangeCheckScope())
      {
        DrawAppearanceSettings();
        DrawLocalizationSettings();
        DrawUISettings();
        DrawPickupSettings();
        DrawVisitorSettings();

        ApplyModifiedProperties();
        if (check.changed) CopyToOtherUIs();
      }
    }

    private void ProcessYamaPlayerChange()
    {
      var yamaPlayer = _yamaPlayer.objectReferenceValue as YamaPlayer;
      if (yamaPlayer == null) return;

      var controller = yamaPlayer.GetComponentInChildren<Controller>(true);
      if (controller == null) return;

      // Every UIController under this object follows the same player: the
      // tablet has its own for the home screen beside the player's ScreenUI.
      foreach (var uiController in _target.GetComponentsInChildren<UIController>(true))
      {
        var so = uiController == _uiController ? _uiControllerSerializedObject : new SerializedObject(uiController);
        so.FindProperty("_controller").objectReferenceValue = controller;
        so.ApplyModifiedProperties();
      }

      var screens = _target.GetComponentsInChildren<YamaPlayerScreen>(true);
      foreach (var screen in screens)
      {
        var so = new SerializedObject(screen);
        so.FindProperty("controller").objectReferenceValue = controller;
        so.ApplyModifiedProperties();
      }

      var speakers = _target.GetComponentsInChildren<YamaPlayerSpeaker>(true);
      foreach (var speaker in speakers)
      {
        var so = new SerializedObject(speaker);
        so.FindProperty("controller").objectReferenceValue = controller;
        so.ApplyModifiedProperties();
      }
    }

    private void DrawAppearanceSettings()
    {
      if (_appearanceSettings == null) return;

      EditorGUILayout.LabelField(EditorLocalization.Get("appearance.title"), EditorStyles.boldLabel);

      using (new EditorGUILayout.HorizontalScope())
      {
        AppearanceSettingsEditor.DrawColorSetPopup(_appearanceSettings, _defaultColorSet, EditorLocalization.Get("appearance.defaultColorSet"));

        if (GUILayout.Button(EditorLocalization.Get("button.edit"), GUILayout.Width(60)))
        {
          Selection.activeObject = _appearanceSettings;
        }
      }
    }

    private void DrawLocalizationSettings()
    {
      if (_localizationSettings == null) return;

      using (new EditorGUILayout.HorizontalScope())
      {
        if (_languages != null && _languages.arraySize > 0)
        {
          var optionCodes = new List<string> { "" };
          var optionNames = new List<string> { EditorLocalization.Get("localization.defaultLanguage.auto") };

          for (int i = 0; i < _languages.arraySize; i++)
          {
            var language = _languages.GetArrayElementAtIndex(i);
            var displayNameProperty = language.FindPropertyRelative("displayName");
            var codeProperty = language.FindPropertyRelative("languageCode");
            var code = codeProperty != null ? codeProperty.stringValue : "";
            var displayName = displayNameProperty != null ? displayNameProperty.stringValue : $"Language {i}";
            optionCodes.Add(code);
            optionNames.Add($"{code} - {displayName}");
          }

          int selectedIndex = 0;
          if (!string.IsNullOrEmpty(_defaultLanguage?.stringValue))
          {
            selectedIndex = optionCodes.IndexOf(_defaultLanguage.stringValue);
            if (selectedIndex < 0) selectedIndex = 0;
          }

          using (var check = new EditorGUI.ChangeCheckScope())
          {
            int newIndex = EditorGUILayout.Popup(EditorLocalization.Get("localization.defaultLanguage"), selectedIndex, optionNames.ToArray());
            if (check.changed && _defaultLanguage != null && newIndex >= 0 && newIndex < optionCodes.Count)
            {
              _defaultLanguage.stringValue = optionCodes[newIndex];
            }
          }
        }
        else
        {
          EditorGUILayout.LabelField(EditorLocalization.Get("localization.noLanguages"));
        }

        if (GUILayout.Button(EditorLocalization.Get("button.edit"), GUILayout.Width(60)))
        {
          Selection.activeObject = _localizationSettings;
        }
      }
    }

    private void DrawUISettings()
    {
      if (_uiController == null) return;
      if (_idleScreenSprite != null)
      {
        EditorGUILayout.PropertyField(_idleScreenSprite, EditorLocalization.GetLayout("settings.ui.idleScreenSprite"));
      }
    }

    private void DrawPickupSettings()
    {
      if (_vrcPickup == null || _pickupable == null) return;

      EditorGUILayout.Space(SpaceMedium);
      EditorGUILayout.LabelField(EditorLocalization.Get("settings.pickUp.label"), EditorStyles.boldLabel);

      EditorGUILayout.PropertyField(_pickupable, EditorLocalization.GetLayout("settings.pickUp.label", "settings.pickUp.tooltip"));

      if (_pickupable.boolValue)
      {
        VRCObjectSync objectSync = _vrcPickup.gameObject.GetComponent<VRCObjectSync>();
        _globalSync = objectSync != null;

        using (var check = new EditorGUI.ChangeCheckScope())
        {
          _globalSync = EditorGUILayout.Toggle(EditorLocalization.GetLayout("settings.globalSync.label", "settings.globalSync.tooltip"), _globalSync);

          if (check.changed)
          {
            if (_globalSync && objectSync == null) _vrcPickup.gameObject.AddComponent<VRCObjectSync>();
            if (!_globalSync && objectSync != null) GameObject.DestroyImmediate(objectSync);
          }
        }

        DrawTabletReturnSettings();
      }
    }

    // The visitors app's photos (issue #153). The world keeps one record for
    // all its tablets, so turning them off on any tablet turns them off.
    private void DrawVisitorSettings()
    {
      if (_visitorPhotosEnabled == null) return;
      EditorGUILayout.Space(SpaceMedium);
      EditorGUILayout.LabelField(EditorLocalization.Get("settings.visitors.label"), EditorStyles.boldLabel);
      _visitorRecorderSerializedObject.Update();
      EditorGUILayout.PropertyField(_visitorPhotosEnabled, EditorLocalization.GetLayout("settings.visitorPhotos.label", "settings.visitorPhotos.tooltip"));
    }

    // The tablet's two ways back to where it was placed (issue #108, D1).
    private void DrawTabletReturnSettings()
    {
      if (_idleReturnSeconds != null)
      {
        _tabletPickupSerializedObject.Update();
        EditorGUILayout.PropertyField(_idleReturnSeconds, EditorLocalization.GetLayout("settings.idleReturn.label", "settings.idleReturn.tooltip"));
      }

      if (_tabletReturnButton == null) return;
      var buttonObject = _tabletReturnButton.gameObject;
      using (var check = new EditorGUI.ChangeCheckScope())
      {
        bool show = EditorGUILayout.Toggle(EditorLocalization.GetLayout("settings.returnButton.label", "settings.returnButton.tooltip"), buttonObject.activeSelf);
        if (check.changed)
        {
          Undo.RecordObject(buttonObject, "Toggle Tablet Return Button");
          buttonObject.SetActive(show);
        }
      }
    }

    private void ApplyModifiedProperties()
    {
      serializedObject.ApplyModifiedProperties();
      _appearanceSerializedObject?.ApplyModifiedProperties();
      _localizationSerializedObject?.ApplyModifiedProperties();
      _uiControllerSerializedObject?.ApplyModifiedProperties();
      _vrcPickupSerializedObject?.ApplyModifiedProperties();
      _tabletPickupSerializedObject?.ApplyModifiedProperties();
      _visitorRecorderSerializedObject?.ApplyModifiedProperties();
    }

    // The fields above edit the first UI found. When there are more -- the
    // tablet's home screen and the player's ScreenUI -- the others take the
    // same colour set, language and idle image, so the object looks and reads
    // the same on every screen.
    private void CopyToOtherUIs()
    {
      if (_defaultColorSet != null)
      {
        foreach (var settings in _target.GetComponentsInChildren<AppearanceSettings>(true))
        {
          if (settings != _appearanceSettings) CopyString(settings, "defaultColorSet", _defaultColorSet.stringValue);
        }
      }
      if (_defaultLanguage != null)
      {
        foreach (var settings in _target.GetComponentsInChildren<LocalizationSettings>(true))
        {
          if (settings != _localizationSettings) CopyString(settings, "defaultLanguage", _defaultLanguage.stringValue);
        }
      }
      if (_idleScreenSprite != null)
      {
        foreach (var uiController in _target.GetComponentsInChildren<UIController>(true))
        {
          if (uiController == _uiController) continue;
          var so = new SerializedObject(uiController);
          var property = so.FindProperty("_idleScreenSprite");
          if (property == null || property.objectReferenceValue == _idleScreenSprite.objectReferenceValue) continue;
          property.objectReferenceValue = _idleScreenSprite.objectReferenceValue;
          so.ApplyModifiedProperties();
        }
      }
    }

    private static void CopyString(Object target, string propertyName, string value)
    {
      var so = new SerializedObject(target);
      var property = so.FindProperty(propertyName);
      if (property == null || property.stringValue == value) return;
      property.stringValue = value;
      so.ApplyModifiedProperties();
    }
  }
}
