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
    [SerializeField] private VRCUrl[] _redirectPool = new VRCUrl[0];
    [SerializeField] private string _poolId = "default";
    [SerializeField] private string _poolBaseUrl = "https://playlist.vrc-hub.com";
    [SerializeField] private int _poolSize = 100000;
    [SerializeField] private bool _autoLoadOnStart;
    [SerializeField] private VRCUrl _autoLoadUrl;
    [SerializeField, Range(0, 60)] private float _autoLoadDelay;

    private bool _isLoading;
    private VRCUrl _pendingResolveUrl;

    public VRCUrl[] RedirectPool => _redirectPool;
    public string PoolId => _poolId;
    public bool IsLoading => _isLoading;

    public override void Start()
    {
      base.Start();
      if (!Utilities.IsValid(_controller)) return;
      if (!Networking.IsMaster && !_controller.IsLocal) return;
      if (!_autoLoadOnStart) return;
      if (string.IsNullOrEmpty(_autoLoadUrl.Get())) return;
      if (_redirectPool.Length == 0) return;
      SendCustomEventDelayedSeconds(nameof(LoadDefaultPlaylist), _autoLoadDelay);
    }

    public void LoadDefaultPlaylist()
    {
      if (_isLoading || _controller.IsLoading) return;
      LoadPlaylistFromUrl(_autoLoadUrl);
    }

    public void LoadPlaylistFromUrl(VRCUrl resolveUrl)
    {
      if (_isLoading)
      {
        PrintWarning("Already loading a playlist.");
        return;
      }

      _isLoading = true;
      _pendingResolveUrl = resolveUrl;
      VRCStringDownloader.LoadUrl(resolveUrl, (IUdonEventReceiver)this);
      PrintLog($"Downloading playlist from {resolveUrl.Get()}...");
    }

    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
      if (!Utilities.IsValid(_pendingResolveUrl) ||
          result.Url.Get() != _pendingResolveUrl.Get())
        return;

      _isLoading = false;

      if (!TryParseResponse(result.Result, out DataList tracks)) return;

      var builtTracks = BuildTracks(tracks, out int failedCount);
      if (builtTracks == null) return;

      EnqueueAndPlay(builtTracks, tracks.Count, failedCount);
    }

    public override void OnStringLoadError(IVRCStringDownload result)
    {
      if (!Utilities.IsValid(_pendingResolveUrl) ||
          result.Url.Get() != _pendingResolveUrl.Get())
        return;

      _isLoading = false;
      PrintError($"Failed to download playlist: {result.Error}");
    }

    private bool TryParseResponse(string json, out DataList tracks)
    {
      tracks = null;

      if (!VRCJson.TryDeserializeFromJson(json, out DataToken root)
          || root.TokenType != TokenType.DataDictionary)
      {
        PrintError("Failed to parse playlist response.");
        return false;
      }

      var rootDict = root.DataDictionary;

      if (rootDict.TryGetValue("ok", out DataToken okToken)
          && okToken.TokenType == TokenType.Boolean
          && !okToken.Boolean)
      {
        string error = "Playlist server returned an error.";
        if (rootDict.TryGetValue("error", out DataToken errToken)
            && errToken.TokenType == TokenType.String)
          error = errToken.String;
        PrintError(error);
        return false;
      }

      if (!rootDict.TryGetValue("tracks", out DataToken tracksToken)
          || tracksToken.TokenType != TokenType.DataList
          || tracksToken.DataList.Count == 0)
      {
        PrintWarning("No tracks found in playlist.");
        return false;
      }

      tracks = tracksToken.DataList;
      return true;
    }

    private object[][] BuildTracks(DataList trackDicts, out int failedCount)
    {
      failedCount = 0;
      int totalCount = trackDicts.Count;
      var tempTracks = new object[totalCount][];
      int addedCount = 0;

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

        // Optional provider field (issue #72): only "youtube" is defined in
        // v1. Missing, null, non-string, or unknown values get no extension.
        string provider = "";
        if (dict.TryGetValue("provider", out DataToken pv)
            && pv.TokenType == TokenType.String)
          provider = pv.String;

        tempTracks[addedCount] = provider == "youtube"
            ? TrackUtils.NewTrackWithExtension((VideoPlayerType)mode, title, _redirectPool[index],
                TrackProviderUtils.BuildProviderExtension(TrackProviderUtils.ProviderYouTube))
            : TrackUtils.NewTrack((VideoPlayerType)mode, title, _redirectPool[index]);
        addedCount++;
      }

      if (addedCount == 0)
      {
        string msg = failedCount > 0
            ? $"No tracks could be added ({failedCount} skipped)"
            : "No valid tracks to add.";
        PrintWarning(msg);
        return null;
      }

      var result = new object[addedCount][];
      for (int i = 0; i < addedCount; i++) result[i] = tempTracks[i];
      return result;
    }

    private void EnqueueAndPlay(object[][] tracks, int totalCount, int failedCount)
    {
      var queue = _controller.Queue;
      if (!Utilities.IsValid(queue))
      {
        PrintError("Queue is not available.");
        return;
      }

      _controller.TakeOwnership();
      queue.AddTracks(tracks);

      // 自動再生仕様:
      // - プレイヤーが停止中 (Stopped) の場合のみ自動再生する
      // - Forward() は Queue 先頭を取り出して再生する
      // - 既に再生中・一時停止中の場合はキューに追加するのみ
      if (_controller.Stopped)
      {
        _controller.Forward();
      }

      var message = failedCount > 0
          ? $"Added {tracks.Length}/{totalCount} tracks ({failedCount} failed)"
          : $"Added {tracks.Length} tracks to queue";
      PrintLog(message);
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
