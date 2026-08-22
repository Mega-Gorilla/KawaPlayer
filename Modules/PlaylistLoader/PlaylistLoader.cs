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
    // ClassifyUrl results (issue #82).
    public const int UrlKindNotOurs = 0;
    public const int UrlKindOwnPlaylist = 1;
    public const int UrlKindOtherPool = 2;
    public const int UrlKindWebPage = 3;
    public const int UrlKindMalformed = 4;

    // OnLoadResult codes reported to PlaylistLoaderUI (issue #82).
    public const int LoadResultSuccess = 0;
    public const int LoadResultPartial = 1;
    public const int LoadResultDownloadError = 2;
    public const int LoadResultInvalidResponse = 3;
    public const int LoadResultServerError = 4;
    public const int LoadResultEmpty = 5;
    public const int LoadResultPoolMismatch = 6;
    public const int LoadResultQueueUnavailable = 7;

    [SerializeField] private VRCUrl[] _redirectPool = new VRCUrl[0];
    [SerializeField] private string _poolId = "default";
    [SerializeField] private string _poolBaseUrl = "https://playlist.vrc-hub.com";
    [SerializeField] private int _poolSize = 100000;
    [SerializeField] private bool _autoLoadOnStart;
    [SerializeField] private VRCUrl _autoLoadUrl;
    [SerializeField, Range(0, 60)] private float _autoLoadDelay;

    private bool _isLoading;
    private VRCUrl _pendingResolveUrl;
    // Set only by LoadPlaylistFromUrlWithFeedback; notified exactly once per
    // load on every terminal path, then cleared. Null for DefaultUrl / auto
    // load, which keep the log-only behavior.
    private PlaylistLoaderUI _feedbackUi;

    public VRCUrl[] RedirectPool => _redirectPool;
    public string PoolId => _poolId;
    public bool IsLoading => _isLoading;

    // Classifies a user-entered URL against this loader's pool config
    // (issue #82). Accepts http and https so a pasted http playlist URL
    // surfaces a playlist-flavored error instead of a video-player error.
    public int ClassifyUrl(string url)
    {
      if (string.IsNullOrEmpty(url)) return UrlKindNotOurs;
      string scheme = UrlUtils.GetProtocolFromUrl(url);
      if (scheme != "http" && scheme != "https") return UrlKindNotOurs;
      string host = UrlUtils.GetHostFromUrl(url);
      if (string.IsNullOrEmpty(host) || host != UrlUtils.GetHostFromUrl(_poolBaseUrl)) return UrlKindNotOurs;

      string path = UrlUtils.GetPathFromUrl(url);
      while (path.EndsWith("/") && path.Length > 1) path = path.Substring(0, path.Length - 1);

      if (path.StartsWith("/playlists/")) return UrlKindWebPage;
      if (path == "/r") return UrlKindMalformed;
      if (!path.StartsWith("/r/")) return UrlKindNotOurs;

      // path = /r/{pool}/{playlistId...}; any non-empty remainder counts as
      // the id (the server rejects ids it does not know).
      string rest = path.Substring(3);
      int slashIndex = rest.IndexOf('/');
      if (slashIndex <= 0 || slashIndex == rest.Length - 1) return UrlKindMalformed;
      string pool = rest.Substring(0, slashIndex);
      return pool == _poolId ? UrlKindOwnPlaylist : UrlKindOtherPool;
    }

    public bool IsOwnPlaylistUrl(string url) => ClassifyUrl(url) == UrlKindOwnPlaylist;

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

    // Same as LoadPlaylistFromUrl, but reports the outcome back to the UI
    // (issue #82). The busy case is checked by the caller; guarding here too
    // keeps a stray call from leaking feedback into an unrelated load.
    public void LoadPlaylistFromUrlWithFeedback(VRCUrl resolveUrl, PlaylistLoaderUI feedbackUi)
    {
      if (_isLoading)
      {
        PrintWarning("Already loading a playlist.");
        return;
      }
      _feedbackUi = feedbackUi;
      LoadPlaylistFromUrl(resolveUrl);
    }

    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
      if (!Utilities.IsValid(_pendingResolveUrl) ||
          result.Url.Get() != _pendingResolveUrl.Get())
        return;

      _isLoading = false;

      if (!TryParseResponse(result.Result, out DataList tracks, out string playlistName, out int failCode))
      {
        NotifyResult(failCode, "", 0, 0, 0);
        return;
      }

      var builtTracks = BuildTracks(tracks, out int failedCount);
      if (builtTracks == null)
      {
        NotifyResult(LoadResultEmpty, playlistName, 0, failedCount, 0);
        return;
      }

      EnqueueAndPlay(builtTracks, tracks.Count, failedCount, playlistName);
    }

    public override void OnStringLoadError(IVRCStringDownload result)
    {
      if (!Utilities.IsValid(_pendingResolveUrl) ||
          result.Url.Get() != _pendingResolveUrl.Get())
        return;

      _isLoading = false;
      // Raw server error text stays in the log only; the UI shows a
      // localized message keyed off the HTTP status code.
      PrintError($"Failed to download playlist: {result.Error}");
      NotifyResult(LoadResultDownloadError, "", 0, 0, result.ErrorCode);
    }

    private void NotifyResult(int resultCode, string playlistName, int added, int skipped, int httpErrorCode)
    {
      if (_feedbackUi == null) return;
      var ui = _feedbackUi;
      _feedbackUi = null;
      if (Utilities.IsValid(ui)) ui.OnLoadResult(resultCode, playlistName, added, skipped, httpErrorCode);
    }

    private bool TryParseResponse(string json, out DataList tracks, out string playlistName, out int failCode)
    {
      tracks = null;
      playlistName = "";
      failCode = 0;

      if (!VRCJson.TryDeserializeFromJson(json, out DataToken root)
          || root.TokenType != TokenType.DataDictionary)
      {
        PrintError("Failed to parse playlist response.");
        failCode = LoadResultInvalidResponse;
        return false;
      }

      var rootDict = root.DataDictionary;

      if (rootDict.TryGetValue("ok", out DataToken okToken)
          && okToken.TokenType == TokenType.Boolean
          && !okToken.Boolean)
      {
        // Raw server error text stays in the log only (issue #82).
        string error = "Playlist server returned an error.";
        if (rootDict.TryGetValue("error", out DataToken errToken)
            && errToken.TokenType == TokenType.String)
          error = errToken.String;
        PrintError(error);
        failCode = LoadResultServerError;
        return false;
      }

      // A playlist resolved for a different pool would map indices into the
      // wrong redirect URLs, playing unrelated videos. Reject on an explicit
      // mismatch; a missing pool field is tolerated (server version drift).
      if (rootDict.TryGetValue("pool", out DataToken poolToken)
          && poolToken.TokenType == TokenType.String
          && poolToken.String != _poolId)
      {
        PrintError($"Playlist pool mismatch: expected '{_poolId}', got '{poolToken.String}'.");
        failCode = LoadResultPoolMismatch;
        return false;
      }

      if (rootDict.TryGetValue("name", out DataToken nameToken)
          && nameToken.TokenType == TokenType.String)
        playlistName = nameToken.String;

      if (!rootDict.TryGetValue("tracks", out DataToken tracksToken)
          || tracksToken.TokenType != TokenType.DataList
          || tracksToken.DataList.Count == 0)
      {
        PrintWarning("No tracks found in playlist.");
        failCode = LoadResultEmpty;
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

    private void EnqueueAndPlay(object[][] tracks, int totalCount, int failedCount, string playlistName)
    {
      var queue = _controller.Queue;
      if (!Utilities.IsValid(queue))
      {
        PrintError("Queue is not available.");
        NotifyResult(LoadResultQueueUnavailable, playlistName, 0, 0, 0);
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
      NotifyResult(failedCount > 0 ? LoadResultPartial : LoadResultSuccess,
          playlistName, tracks.Length, failedCount, 0);
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
