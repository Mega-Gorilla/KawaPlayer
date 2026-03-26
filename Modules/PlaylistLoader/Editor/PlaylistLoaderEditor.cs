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

    private void ValidatePool()
    {
      string poolId = _poolId.stringValue;
      int expectedSize = _poolSize.intValue;

      var loader = (PlaylistLoader)target;
      VRCUrl[] pool = loader.RedirectPool;
      int actualSize = pool != null ? pool.Length : 0;

      if (actualSize == 0)
      {
        EditorUtility.DisplayDialog("Validate Pool",
          "Pool が空です。\n\n[Generate Pool] を先に実行してください。", "OK");
        return;
      }

      var issues = new System.Collections.Generic.List<string>();

      // サイズチェック
      if (actualSize != expectedSize)
      {
        issues.Add($"Pool サイズが不一致です。\n  設定値: {expectedSize}\n  実際: {actualSize}\n  → [Generate Pool] を再実行してください。");
      }

      // 先頭 URL チェック
      string firstUrl = pool[0] != null ? pool[0].Get() : null;
      if (string.IsNullOrEmpty(firstUrl))
      {
        issues.Add("先頭エントリの URL が空です。\n  → [Generate Pool] を再実行してください。");
        ShowValidationResult(issues);
        return;
      }

      int vrcurlPos = firstUrl.IndexOf("/vrcurl/");
      if (vrcurlPos < 0)
      {
        issues.Add($"URL パターンが不正です。\n  先頭URL: {firstUrl}\n  → /vrcurl/ を含む URL が必要です。");
        ShowValidationResult(issues);
        return;
      }

      string detectedBaseUrl = firstUrl.Substring(0, vrcurlPos);

      // Pool ID 一致チェック (先頭のみ)
      string expectedFirst = $"{detectedBaseUrl}/vrcurl/{poolId}/0";
      if (firstUrl != expectedFirst)
      {
        // Pool ID 不一致の可能性を検出
        string actualPoolId = firstUrl.Substring(vrcurlPos + "/vrcurl/".Length);
        int slashPos = actualPoolId.IndexOf('/');
        if (slashPos >= 0) actualPoolId = actualPoolId.Substring(0, slashPos);

        issues.Add($"Pool ID が一致しません。\n  設定値: {poolId}\n  Pool 内の値: {actualPoolId}\n  → Pool ID を変更した場合は [Generate Pool] を再実行してください。");
      }

      if (issues.Count == 0)
      {
        EditorUtility.DisplayDialog("Validate Pool",
          $"Validation passed.\n\nPool Size: {actualSize}\nPool ID: {poolId}\nBase URL: {detectedBaseUrl}", "OK");
      }
      else
      {
        ShowValidationResult(issues);
      }
    }

    private void ShowValidationResult(System.Collections.Generic.List<string> issues)
    {
      var sb = new System.Text.StringBuilder();
      sb.AppendLine($"Validation failed: {issues.Count} 件の問題が見つかりました。\n");
      for (int i = 0; i < issues.Count; i++)
      {
        sb.AppendLine($"[{i + 1}] {issues[i]}");
      }
      EditorUtility.DisplayDialog("Validate Pool", sb.ToString(), "OK");
    }
  }
}
