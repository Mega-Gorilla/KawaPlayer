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

        // Pool ID 不一致検出: 生成済み Pool の Pool ID と現在の設定値を比較
        var loader = (PlaylistLoader)target;
        VRCUrl[] pool = loader.RedirectPool;
        if (pool != null && pool.Length > 0 && pool[0] != null)
        {
          string firstUrl = pool[0].Get();
          string expectedPrefix = $"/vrcurl/{_poolId.stringValue}/";
          if (!string.IsNullOrEmpty(firstUrl) && !firstUrl.Contains(expectedPrefix))
          {
            EditorGUILayout.HelpBox("Pool ID が生成済み Pool と一致しません。[Generate Pool] を再実行してください。", MessageType.Warning);
          }
        }
      }
    }

    private void DrawPoolActions()
    {
      EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
      EditorGUILayout.Space(SpaceSmall);

      if (GUILayout.Button("Generate Pool"))
      {
        GeneratePool();
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

      if (!ValidatePoolIdWithServer(baseUrl, poolId))
      {
        return;
      }

      var urls = new VRCUrl[poolSize];
      for (int i = 0; i < poolSize; i++)
      {
        urls[i] = new VRCUrl($"{baseUrl}/vrcurl/{poolId}/{i}");
      }

      var loader = (PlaylistLoader)target;
      var field = typeof(PlaylistLoader).GetField("_redirectPool",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      field.SetValue(loader, urls);
      EditorUtility.SetDirty(target);

      Debug.Log($"[PlaylistLoader] Generated {poolSize} VRCUrl entries");
      EditorUtility.DisplayDialog("Success", $"Generated {poolSize} VRCUrl entries.", "OK");
    }

    private bool ValidatePoolIdWithServer(string baseUrl, string poolId)
    {
      try
      {
        var request = System.Net.HttpWebRequest.Create($"{baseUrl}/r/{poolId}/_validate") as System.Net.HttpWebRequest;
        request.Timeout = 5000;

        try
        {
          using (var response = request.GetResponse() as System.Net.HttpWebResponse)
          using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
          {
            string body = reader.ReadToEnd();
            if (body.Contains("Unknown pool"))
            {
              EditorUtility.DisplayDialog("Error",
                $"Pool ID \"{poolId}\" はサーバーに存在しません。\n\nサーバー: {baseUrl}\nPool ID を確認してください。", "OK");
              return false;
            }
            return true;
          }
        }
        catch (System.Net.WebException ex) when (ex.Response is System.Net.HttpWebResponse httpRes)
        {
          // HTTP エラーレスポンス (404 等) — サーバーには接続できている
          using (var reader = new System.IO.StreamReader(httpRes.GetResponseStream()))
          {
            string body = reader.ReadToEnd();
            if (body.Contains("Unknown pool"))
            {
              EditorUtility.DisplayDialog("Error",
                $"Pool ID \"{poolId}\" はサーバーに存在しません。\n\nサーバー: {baseUrl}\nPool ID を確認してください。", "OK");
              return false;
            }
          }
          // Playlist not found 等 → Pool ID は有効
          return true;
        }
      }
      catch (System.Net.WebException)
      {
        // 接続自体ができない (DNS 解決失敗、タイムアウト等)
        return EditorUtility.DisplayDialog("Warning",
          $"サーバーに接続できませんでした。\n\nサーバー: {baseUrl}\nPool ID の有効性を確認できません。\n\nそのまま生成しますか？",
          "生成する", "キャンセル");
      }
      catch (System.Exception ex)
      {
        Debug.LogWarning($"[PlaylistLoader] Pool ID validation error: {ex.Message}");
        return EditorUtility.DisplayDialog("Warning",
          $"Pool ID の検証中にエラーが発生しました。\n\n{ex.Message}\n\nそのまま生成しますか？",
          "生成する", "キャンセル");
      }
    }
  }
}
