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
    private SerializedProperty _controller;
    private SerializedProperty _ui;
    private SerializedProperty _redirectPool;
    private SerializedProperty _poolId;
    private SerializedProperty _poolBaseUrl;
    private SerializedProperty _poolSize;

    private void OnEnable()
    {
      ShowHeader = false;
      Title = "Playlist Loader";

      _controller = serializedObject.FindProperty("_controller");
      _ui = serializedObject.FindProperty("_ui");
      _redirectPool = serializedObject.FindProperty("_redirectPool");
      _poolId = serializedObject.FindProperty("_poolId");
      _poolBaseUrl = serializedObject.FindProperty("_poolBaseUrl");
      _poolSize = serializedObject.FindProperty("_poolSize");
    }

    public override void OnInspectorGUI()
    {
      base.OnInspectorGUI();
      serializedObject.Update();

      DrawReferences();
      EditorGUILayout.Space(SpaceMedium);
      DrawPoolSettings();
      EditorGUILayout.Space(SpaceMedium);
      DrawPoolStatus();
      EditorGUILayout.Space(SpaceMedium);
      DrawPoolActions();

      serializedObject.ApplyModifiedProperties();
    }

    private void DrawReferences()
    {
      EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
      EditorGUILayout.Space(SpaceSmall);

      EditorGUILayout.PropertyField(_controller, new GUIContent("Controller"));
      if (_controller.objectReferenceValue == null)
        EditorGUILayout.HelpBox("Controller が未設定です。YamaPlayer の Controller を割り当ててください。", MessageType.Error);

      EditorGUILayout.PropertyField(_ui, new GUIContent("UI (PlaylistLoaderUI)"));
      if (_ui.objectReferenceValue == null)
        EditorGUILayout.HelpBox("UI が未設定です。PlaylistLoaderUI を割り当ててください。", MessageType.Error);
    }

    private void DrawPoolSettings()
    {
      EditorGUILayout.LabelField("Pool Settings", EditorStyles.boldLabel);
      EditorGUILayout.Space(SpaceSmall);

      using (new EditorGUI.DisabledGroupScope(true))
      {
        EditorGUILayout.PropertyField(_poolBaseUrl, new GUIContent("Pool Base URL"));
        EditorGUILayout.PropertyField(_poolSize, new GUIContent("Pool Size"));
      }
      EditorGUILayout.PropertyField(_poolId, new GUIContent("Pool ID"));
      if (string.IsNullOrEmpty(_poolId.stringValue))
        EditorGUILayout.HelpBox("Pool ID が未設定です。サーバーの Pool ID を入力してください。", MessageType.Error);
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
      string baseUrl = _poolBaseUrl.stringValue;
      string poolId = _poolId.stringValue;
      int poolSize = _poolSize.intValue;

      if (string.IsNullOrEmpty(baseUrl) || !(baseUrl.StartsWith("http://") || baseUrl.StartsWith("https://")))
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

      var urls = new VRCUrl[poolSize];
      for (int i = 0; i < poolSize; i++)
      {
        urls[i] = new VRCUrl($"{baseUrl}/vrcurl/{poolId}/{i}");
      }

      Undo.RecordObject(target, "Generate PlaylistLoader Pool");
      var loader = (PlaylistLoader)target;
      var field = typeof(PlaylistLoader).GetField("_redirectPool",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      field.SetValue(loader, urls);
      EditorUtility.SetDirty(target);
      serializedObject.Update();

      sw.Stop();
      Debug.Log($"[PlaylistLoader] Generated {poolSize} VRCUrl entries in {sw.ElapsedMilliseconds}ms");
      EditorUtility.DisplayDialog("Success", $"Generated {poolSize} VRCUrl entries.\nTime: {sw.ElapsedMilliseconds}ms", "OK");
    }

    private void ValidatePool()
    {
      string poolId = _poolId.stringValue;
      int expectedSize = _poolSize.intValue;

      var loader = (PlaylistLoader)target;
      VRCUrl[] pool = loader.RedirectPool;
      int actualSize = pool != null ? pool.Length : 0;

      if (actualSize == 0)
      {
        EditorUtility.DisplayDialog("Validation", "Pool is empty. Generate a pool first.", "OK");
        return;
      }

      int errors = 0;

      if (actualSize != expectedSize)
      {
        Debug.LogWarning($"[PlaylistLoader] Validation: pool size mismatch. Expected {expectedSize}, actual {actualSize}");
        errors++;
      }

      string firstUrl = pool[0] != null ? pool[0].Get() : null;
      if (string.IsNullOrEmpty(firstUrl))
      {
        Debug.LogError("[PlaylistLoader] Validation: first entry has empty URL");
        EditorUtility.DisplayDialog("Validation", "Validation failed: first entry has empty URL.", "OK");
        return;
      }

      int vrcurlPos = firstUrl.IndexOf("/vrcurl/");
      if (vrcurlPos < 0)
      {
        Debug.LogError($"[PlaylistLoader] Validation: first entry does not contain /vrcurl/ pattern: {firstUrl}");
        EditorUtility.DisplayDialog("Validation", "Validation failed: URL pattern not recognized.", "OK");
        return;
      }

      string detectedBaseUrl = firstUrl.Substring(0, vrcurlPos);

      if (!(detectedBaseUrl.StartsWith("http://") || detectedBaseUrl.StartsWith("https://")))
      {
        Debug.LogWarning($"[PlaylistLoader] Validation: base URL does not start with http(s): {detectedBaseUrl}");
        errors++;
      }

      for (int i = 0; i < actualSize; i++)
      {
        string url = pool[i] != null ? pool[i].Get() : null;
        string expectedUrl = $"{detectedBaseUrl}/vrcurl/{poolId}/{i}";

        if (string.IsNullOrEmpty(url))
        {
          Debug.LogWarning($"[PlaylistLoader] Validation: index {i} has empty URL");
          errors++;
        }
        else if (url != expectedUrl)
        {
          Debug.LogWarning($"[PlaylistLoader] Validation: index {i} URL mismatch.\n  Expected: {expectedUrl}\n  Actual:   {url}");
          errors++;
          if (errors > 5) break;
        }
      }

      if (errors == 0)
      {
        string msg = $"Validation passed.\n\nPool Size: {actualSize}\nPool ID: {poolId}\nBase URL: {detectedBaseUrl}";
        EditorUtility.DisplayDialog("Validation", msg, "OK");
        Debug.Log($"[PlaylistLoader] Validation passed: {actualSize} entries, pool={poolId}, base={detectedBaseUrl}");
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
