using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Playables;
using Yamadev.YamaStream.Editor;

namespace Yamadev.YamaStream.Modules.TimelineSync.Editor
{
  [CustomEditor(typeof(TimelineSync))]
  public class TimelineSyncEditor : EditorBase
  {
    private SerializedProperty _urlsProperty;
    private SerializedProperty _timelinesProperty;
    private ReorderableList _mappingList;

    private void OnEnable()
    {
      Title = EditorLocalization.Get("module.timelineSync.title");
      ShowHeader = false;

      _urlsProperty = serializedObject.FindProperty("_urls");
      _timelinesProperty = serializedObject.FindProperty("_timelines");

      SetupReorderableList();
    }

    private void SetupReorderableList()
    {
      int maxCount = Mathf.Max(_urlsProperty.arraySize, _timelinesProperty.arraySize);
      _urlsProperty.arraySize = maxCount;
      _timelinesProperty.arraySize = maxCount;

      _mappingList = new ReorderableList(serializedObject, _urlsProperty, true, true, true, true);

      _mappingList.drawHeaderCallback = (Rect rect) =>
      {
        float halfWidth = (rect.width - 20) / 2;
        EditorGUI.LabelField(new Rect(rect.x, rect.y, halfWidth, rect.height), EditorLocalization.Get("module.timelineSync.url"));
        EditorGUI.LabelField(new Rect(rect.x + halfWidth + 10, rect.y, halfWidth, rect.height), EditorLocalization.Get("module.timelineSync.timeline"));
      };

      _mappingList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
      {
        EnsureArraySize(index);

        float halfWidth = (rect.width - 10) / 2;
        float y = rect.y + 2;
        float height = EditorGUIUtility.singleLineHeight;

        SerializedProperty urlElement = _urlsProperty.GetArrayElementAtIndex(index);
        SerializedProperty timelineElement = _timelinesProperty.GetArrayElementAtIndex(index);

        EditorGUI.PropertyField(
          new Rect(rect.x, y, halfWidth, height),
          urlElement,
          GUIContent.none
        );

        EditorGUI.PropertyField(
          new Rect(rect.x + halfWidth + 10, y, halfWidth, height),
          timelineElement,
          GUIContent.none
        );
      };

      _mappingList.onAddCallback = (ReorderableList list) =>
      {
        int newIndex = _urlsProperty.arraySize;
        _urlsProperty.arraySize++;
        _timelinesProperty.arraySize++;

        _urlsProperty.GetArrayElementAtIndex(newIndex).stringValue = "";
        _timelinesProperty.GetArrayElementAtIndex(newIndex).objectReferenceValue = null;
      };

      _mappingList.onRemoveCallback = (ReorderableList list) =>
      {
        if (list.index >= 0 && list.index < _urlsProperty.arraySize)
        {
          _urlsProperty.DeleteArrayElementAtIndex(list.index);
          if (list.index < _timelinesProperty.arraySize)
          {
            if (_timelinesProperty.GetArrayElementAtIndex(list.index).objectReferenceValue != null)
            {
              _timelinesProperty.GetArrayElementAtIndex(list.index).objectReferenceValue = null;
            }
            _timelinesProperty.DeleteArrayElementAtIndex(list.index);
          }
        }
      };

      _mappingList.onReorderCallbackWithDetails = (ReorderableList list, int oldIndex, int newIndex) =>
      {
        _timelinesProperty.MoveArrayElement(oldIndex, newIndex);
      };

      _mappingList.elementHeight = EditorGUIUtility.singleLineHeight + 6;
    }

    private void EnsureArraySize(int index)
    {
      while (_urlsProperty.arraySize <= index)
      {
        _urlsProperty.arraySize++;
      }
      while (_timelinesProperty.arraySize <= index)
      {
        _timelinesProperty.arraySize++;
      }
    }

    public override void OnInspectorGUI()
    {
      base.OnInspectorGUI();

      Title = EditorLocalization.Get("module.timelineSync.title");

      serializedObject.Update();

      EditorGUILayout.LabelField(
        $"{EditorLocalization.Get("module.timelineSync.mappingList")} ({_urlsProperty.arraySize})",
        EditorStyles.boldLabel
      );
      EditorGUILayout.Space(SpaceSmall);

      _mappingList.DoLayoutList();

      EditorGUILayout.Space(SpaceMedium);
      EditorGUILayout.HelpBox(EditorLocalization.Get("module.timelineSync.hint"), MessageType.Info);

      serializedObject.ApplyModifiedProperties();
    }
  }
}
