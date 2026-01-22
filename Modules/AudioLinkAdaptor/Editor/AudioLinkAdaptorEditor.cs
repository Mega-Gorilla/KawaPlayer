using UnityEditor;
using UnityEngine;
using Yamadev.YamaStream.Editor;

#if AUDIOLINK_V1
using AudioLink.Editor;
#endif

namespace Yamadev.YamaStream.Modules.AudioLinkAdaptor.Editor
{
  [CustomEditor(typeof(AudioLinkAdaptor))]
  public class AudioLinkAdaptorEditor : EditorBase
  {
    private void OnEnable()
    {
      Title = EditorLocalization.Get("module.audioLinkAdaptor.name");
      ShowHeader = false;
    }

    public override void OnInspectorGUI()
    {
      base.OnInspectorGUI();
      Title = EditorLocalization.Get("module.audioLinkAdaptor.name");

#if AUDIOLINK_V1
      serializedObject.Update();

      var audioLinkProperty = serializedObject.FindProperty("_audioLink");
      EditorGUILayout.PropertyField(audioLinkProperty);

      if (audioLinkProperty.objectReferenceValue == null)
      {
        if (GUILayout.Button(EditorLocalization.Get("module.audioLinkAdaptor.addToScene")))
        {
          AudioLinkAssetManager.AddAudioLinkToScene();
          var audioLink = Object.FindFirstObjectByType<AudioLink.AudioLink>();
          if (audioLink != null)
          {
            audioLinkProperty.objectReferenceValue = audioLink;
            serializedObject.ApplyModifiedProperties();
          }
        }
      }

      EditorGUILayout.HelpBox(EditorLocalization.Get("module.audioLinkAdaptor.hint"), MessageType.Info);
      serializedObject.ApplyModifiedProperties();
#else
      EditorGUILayout.HelpBox(EditorLocalization.Get("module.audioLinkAdaptor.notInstalled"), MessageType.Warning);
#endif
    }
  }
}
