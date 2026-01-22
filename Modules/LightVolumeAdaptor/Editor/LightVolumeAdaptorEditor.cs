using UnityEditor;
using Yamadev.YamaStream.Editor;

namespace Yamadev.YamaStream.Modules.LightVolumeAdaptor.Editor
{
  [CustomEditor(typeof(LightVolumeAdaptor))]
  public class LightVolumeAdaptorEditor : EditorBase
  {
    private void OnEnable()
    {
      Title = EditorLocalization.Get("module.lightVolumeAdaptor.name");
      ShowHeader = false;
    }

    public override void OnInspectorGUI()
    {
      base.OnInspectorGUI();
      Title = EditorLocalization.Get("module.lightVolumeAdaptor.name");

#if VRC_LIGHT_VOLUMES
      serializedObject.Update();
      EditorGUILayout.HelpBox(EditorLocalization.Get("module.lightVolumeAdaptor.description"), MessageType.Info);
      serializedObject.ApplyModifiedProperties();
#else
      EditorGUILayout.HelpBox(EditorLocalization.Get("module.lightVolumeAdaptor.notInstalled"), MessageType.Warning);
#endif
    }
  }
}
