using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace Yamadev.YamaStream.Modules.PlaylistLoader
{
  [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
  public class PlaylistLoader : YamaPlayerModule
  {
    [SerializeField] private PlaylistLoaderUI _ui;
    [SerializeField] private VRCUrl[] _redirectPool = new VRCUrl[0];
    [SerializeField] private string _poolId;
    [SerializeField] private string _poolBaseUrl = "https://api.example.com";
    [SerializeField, HideInInspector] private int _poolSize = 100000;

    private bool _isLoading;
    private VRCUrl _pendingResolveUrl;

    public VRCUrl[] RedirectPool => _redirectPool;
    public string PoolId => _poolId;
    public bool IsLoading => _isLoading;

    public void LoadPlaylistFromUrl(VRCUrl resolveUrl)
    {
      if (_isLoading)
      {
        PrintWarning("Already loading a playlist.");
        return;
      }

      _isLoading = true;
      _pendingResolveUrl = resolveUrl;
      if (Utilities.IsValid(_ui)) _ui.ShowLoading("Loading playlist...");
      VRCStringDownloader.LoadUrl(resolveUrl, (IUdonEventReceiver)this);
      PrintLog($"Downloading playlist from {resolveUrl.Get()}...");
    }

    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
      if (!Utilities.IsValid(_pendingResolveUrl) ||
          result.Url.Get() != _pendingResolveUrl.Get())
        return;

      _isLoading = false;

      if (!VRCJson.TryDeserializeFromJson(result.Result, out DataToken root)
          || root.TokenType != TokenType.DataDictionary)
      {
        PrintError("Failed to parse playlist response.");
        if (Utilities.IsValid(_ui)) _ui.ShowError("Failed to parse playlist response.");
        return;
      }

      var rootDict = root.DataDictionary;

      // "ok" チェック
      if (rootDict.TryGetValue("ok", out DataToken okToken)
          && okToken.TokenType == TokenType.Boolean
          && !okToken.Boolean)
      {
        string error = "Playlist server returned an error.";
        if (rootDict.TryGetValue("error", out DataToken errToken)
            && errToken.TokenType == TokenType.String)
          error = errToken.String;
        PrintError(error);
        if (Utilities.IsValid(_ui)) _ui.ShowError(error);
        return;
      }

      // "tracks" 配列を取得
      if (!rootDict.TryGetValue("tracks", out DataToken tracksToken)
          || tracksToken.TokenType != TokenType.DataList
          || tracksToken.DataList.Count == 0)
      {
        PrintWarning("No tracks found in playlist.");
        if (Utilities.IsValid(_ui)) _ui.ShowError("No tracks found in playlist.");
        return;
      }

      EnqueueFromIndexes(tracksToken.DataList);
    }

    public override void OnStringLoadError(IVRCStringDownload result)
    {
      if (!Utilities.IsValid(_pendingResolveUrl) ||
          result.Url.Get() != _pendingResolveUrl.Get())
        return;

      _isLoading = false;
      PrintError($"Failed to download playlist: {result.Error}");
      if (Utilities.IsValid(_ui)) _ui.ShowError("Playlist server is unavailable.");
    }

    private void EnqueueFromIndexes(DataList trackDicts)
    {
      var queue = _controller.Queue;
      if (!Utilities.IsValid(queue))
      {
        PrintError("Queue is not available.");
        if (Utilities.IsValid(_ui)) _ui.ShowError("Queue is not available.");
        return;
      }

      int totalCount = trackDicts.Count;
      object[][] tempTracks = new object[totalCount][];
      int addedCount = 0;
      int failedCount = 0;

      for (int i = 0; i < totalCount; i++)
      {
        if (trackDicts[i].TokenType != TokenType.DataDictionary)
        {
          failedCount++;
          continue;
        }
        var dict = trackDicts[i].DataDictionary;

        int index = TryGetInt(dict, "index", -1);
        if (index < 0 || index >= _redirectPool.Length)
        {
          failedCount++;
          continue;
        }

        int mode = TryGetInt(dict, "mode", 0);
        string title = "";
        if (dict.TryGetValue("title", out DataToken t)
            && t.TokenType == TokenType.String)
          title = t.String;

        VRCUrl redirectUrl = _redirectPool[index];
        tempTracks[addedCount] = TrackUtils.NewTrack(
            (VideoPlayerType)mode, title, redirectUrl);
        addedCount++;
      }

      if (addedCount == 0)
      {
        string msg = failedCount > 0
            ? $"No tracks could be added ({failedCount} skipped)"
            : "No valid tracks to add.";
        PrintWarning(msg);
        if (Utilities.IsValid(_ui)) _ui.ShowError(msg);
        return;
      }

      object[][] finalTracks = new object[addedCount][];
      for (int i = 0; i < addedCount; i++) finalTracks[i] = tempTracks[i];

      _controller.TakeOwnership();
      queue.AddTracks(finalTracks);

      var message = failedCount > 0
          ? $"Added {addedCount}/{totalCount} tracks ({failedCount} failed)"
          : $"Added {addedCount} tracks to queue";
      PrintLog(message);
      if (Utilities.IsValid(_ui)) _ui.ShowSuccess(message);
    }

    private int TryGetInt(DataDictionary dict, string key, int defaultValue)
    {
      if (!dict.TryGetValue(key, out DataToken token)) return defaultValue;
      if (token.TokenType == TokenType.Double) return (int)token.Double;
      if (token.TokenType == TokenType.Float) return (int)token.Float;
      if (token.TokenType == TokenType.Int) return token.Int;
      if (token.TokenType == TokenType.Long) return (int)token.Long;
      return defaultValue;
    }
  }
}
