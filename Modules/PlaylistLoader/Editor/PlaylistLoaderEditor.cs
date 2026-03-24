using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase;
using Yamadev.YamaStream.Editor;
using Debug = UnityEngine.Debug;

namespace Yamadev.YamaStream.Modules.PlaylistLoader.Editor
{
  [CustomEditor(typeof(PlaylistLoader))]
  public class PlaylistLoaderEditor : EditorBase
  {
    private SerializedProperty _redirectPool;
    private SerializedProperty _poolId;

    private string _poolBaseUrl = "https://api.example.com";
    private int _poolSize = 100000;

    private void OnEnable()
    {
      ShowHeader = false;
      Title = "Playlist Loader";

      _redirectPool = serializedObject.FindProperty("_redirectPool");
      _poolId = serializedObject.FindProperty("_poolId");
    }

    public override void OnInspectorGUI()
    {
      base.OnInspectorGUI();
      serializedObject.Update();

      DrawPoolSettings();
      EditorGUILayout.Space(SpaceMedium);
      DrawPoolStatus();
      EditorGUILayout.Space(SpaceMedium);
      DrawPoolActions();

      serializedObject.ApplyModifiedProperties();
    }

    private void DrawPoolSettings()
    {
      EditorGUILayout.LabelField("Pool Settings", EditorStyles.boldLabel);
      EditorGUILayout.Space(SpaceSmall);

      _poolBaseUrl = EditorGUILayout.TextField("Pool Base URL", _poolBaseUrl);
      EditorGUILayout.PropertyField(_poolId, new GUIContent("Pool ID"));
      _poolSize = EditorGUILayout.IntField("Pool Size", _poolSize);

      if (_poolSize < 1) _poolSize = 1;
      if (_poolSize > 200000) _poolSize = 200000;
    }

    private void DrawPoolStatus()
    {
      EditorGUILayout.LabelField("Pool Status", EditorStyles.boldLabel);
      EditorGUILayout.Space(SpaceSmall);

      int currentSize = _redirectPool != null ? _redirectPool.arraySize : 0;
      EditorGUILayout.LabelField("Current Pool Size", currentSize.ToString());

      if (currentSize > 0)
      {
        float estimatedMB = currentSize * 54f / (1024f * 1024f);
        EditorGUILayout.LabelField("Estimated File Size", $"~{estimatedMB:F2} MB");
      }
    }

    private void DrawPoolActions()
    {
      EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
      EditorGUILayout.Space(SpaceSmall);

      using (new EditorGUILayout.HorizontalScope())
      {
        if (GUILayout.Button("Generate Pool"))
        {
          GeneratePool();
        }
        if (GUILayout.Button("Validate Pool"))
        {
          ValidatePool();
        }
      }
    }

    private void GeneratePool()
    {
      string poolId = _poolId.stringValue;

      if (string.IsNullOrEmpty(_poolBaseUrl) || !(_poolBaseUrl.StartsWith("http://") || _poolBaseUrl.StartsWith("https://")))
      {
        EditorUtility.DisplayDialog("Error", "Pool Base URL must start with http:// or https://", "OK");
        return;
      }
      if (string.IsNullOrEmpty(poolId))
      {
        EditorUtility.DisplayDialog("Error", "Pool ID must not be empty.", "OK");
        return;
      }

      var sw = Stopwatch.StartNew();

      _redirectPool.arraySize = _poolSize;
      for (int i = 0; i < _poolSize; i++)
      {
        var element = _redirectPool.GetArrayElementAtIndex(i);
        var urlProp = element.FindPropertyRelative("url");
        if (urlProp != null)
        {
          urlProp.stringValue = $"{_poolBaseUrl}/vrcurl/{poolId}/{i}";
        }
      }

      serializedObject.ApplyModifiedProperties();
      EditorUtility.SetDirty(target);

      sw.Stop();
      Debug.Log($"[PlaylistLoader] Generated {_poolSize} VRCUrl entries in {sw.ElapsedMilliseconds}ms");
      EditorUtility.DisplayDialog("Success", $"Generated {_poolSize} VRCUrl entries.\nTime: {sw.ElapsedMilliseconds}ms", "OK");
    }

    private void ValidatePool()
    {
      string poolId = _poolId.stringValue;
      int poolSize = _redirectPool.arraySize;

      if (poolSize == 0)
      {
        EditorUtility.DisplayDialog("Validation", "Pool is empty. Generate a pool first.", "OK");
        return;
      }

      int errors = 0;
      string expectedPrefix = null;

      for (int i = 0; i < poolSize; i++)
      {
        var element = _redirectPool.GetArrayElementAtIndex(i);
        var urlProp = element.FindPropertyRelative("url");
        if (urlProp == null)
        {
          errors++;
          continue;
        }

        string url = urlProp.stringValue;
        string expectedSuffix = $"/vrcurl/{poolId}/{i}";

        if (i == 0)
        {
          int suffixStart = url.IndexOf("/vrcurl/");
          expectedPrefix = suffixStart >= 0 ? url.Substring(0, suffixStart) : null;
        }

        if (string.IsNullOrEmpty(url))
        {
          Debug.LogWarning($"[PlaylistLoader] Validation: index {i} has empty URL");
          errors++;
        }
        else if (!url.EndsWith(expectedSuffix))
        {
          Debug.LogWarning($"[PlaylistLoader] Validation: index {i} URL mismatch. Expected suffix: {expectedSuffix}");
          errors++;
        }
      }

      if (errors == 0)
      {
        string msg = $"Validation passed.\n\nPool Size: {poolSize}\nPool ID: {poolId}\nBase URL: {expectedPrefix}";
        EditorUtility.DisplayDialog("Validation", msg, "OK");
        Debug.Log($"[PlaylistLoader] Validation passed: {poolSize} entries, pool={poolId}");
      }
      else
      {
        string msg = $"Validation failed: {errors} error(s) found.\nCheck console for details.";
        EditorUtility.DisplayDialog("Validation", msg, "OK");
        Debug.LogError($"[PlaylistLoader] Validation failed: {errors} error(s)");
      }
    }
  }
}
