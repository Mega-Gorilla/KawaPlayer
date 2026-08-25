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
    // ClassifyUrl results (issue #82); canonical values live in
    // PlaylistUrlUtils, aliased here for callers holding a loader reference.
    public const int UrlKindNotOurs = PlaylistUrlUtils.KindNotOurs;
    public const int UrlKindOwnPlaylist = PlaylistUrlUtils.KindOwnPlaylist;
    public const int UrlKindOtherPool = PlaylistUrlUtils.KindOtherPool;
    public const int UrlKindWebPage = PlaylistUrlUtils.KindWebPage;
    public const int UrlKindMalformed = PlaylistUrlUtils.KindMalformed;

    // OnLoadResult codes reported to PlaylistLoaderUI (issue #82).
    public const int LoadResultSuccess = 0;
    public const int LoadResultPartial = 1;
    public const int LoadResultDownloadError = 2;
    public const int LoadResultInvalidResponse = 3;
    public const int LoadResultServerError = 4;
    public const int LoadResultEmpty = 5;
    public const int LoadResultPoolMismatch = 6;
    // No dynamic playlist slot to load into (issue #88). The numeric value
    // is kept so PlaylistLoaderUI's existing mapping still applies.
    public const int LoadResultQueueUnavailable = 7;

    [SerializeField] private VRCUrl[] _redirectPool = new VRCUrl[0];
    [SerializeField] private string _poolId = "default";
    [SerializeField] private string _poolBaseUrl = "https://playlist.vrc-hub.com";
    [SerializeField] private int _poolSize = 100000;
    // Instance-lifetime playlist slots this loader may fill (issue #88).
    // Wired at build time by PlaylistLoaderBuildProcess from the Controller
    // hierarchy, so worlds get whatever the prefab ships with.
    [SerializeField, HideInInspector] private DynamicPlaylist[] _dynamicPlaylists = new DynamicPlaylist[0];
    // Bounds one playlist by track count. 200 tracks of VHub redirect URLs
    // plus titles is roughly 36KB, so in practice the byte budget below is
    // what usually stops a large playlist first.
    [SerializeField, Range(1, 500)] private int _maxTracks = 200;
    // Track count alone does not bound the payload: titles dominate it and
    // vary wildly. This is a KawaPlayer-side estimate for keeping bandwidth
    // and late-joiner latency reasonable, NOT a platform ceiling -- Udon's
    // hard limit is ~280,496 bytes per manual serialization. The binding
    // constraint is the ~11KB/s that all Udon in the world shares, which
    // already makes 32KB take about three seconds to go out.
    // https://creators.vrchat.com/worlds/udon/networking/network-details/
    [SerializeField, Range(4096, 65536)] private int _maxSyncBytes = 32768;
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
    // (issue #82).
    public int ClassifyUrl(string url) => PlaylistUrlUtils.Classify(url, _poolBaseUrl, _poolId);

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

      LoadIntoSlot(builtTracks, tracks.Count, failedCount, playlistName);
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

      int estimatedBytes = 0;

      for (int i = 0; i < totalCount; i++)
      {
        // Everything past a cap is reported as skipped, which surfaces
        // through the existing partial-load message.
        if (addedCount >= _maxTracks)
        {
          failedCount += totalCount - i;
          PrintWarning($"Playlist truncated to {_maxTracks} tracks ({totalCount - i} dropped).");
          break;
        }

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

        // Rough per-track sync cost. VRChat bills a networked string at
        // 2 bytes per character, so a Japanese title costs the same per
        // character as an ASCII URL; 16 covers the player type and the
        // per-element array overhead.
        int trackBytes = (_redirectPool[index].Get().Length + title.Length) * 2 + 16;
        if (addedCount > 0 && estimatedBytes + trackBytes > _maxSyncBytes)
        {
          failedCount += totalCount - i;
          PrintWarning($"Playlist truncated at {addedCount} tracks: ~{estimatedBytes + trackBytes} bytes exceeds the {_maxSyncBytes} byte sync budget ({totalCount - i} dropped).");
          break;
        }
        estimatedBytes += trackBytes;

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

    // Loads into an instance-lifetime playlist slot rather than the queue
    // (issue #88). The queue is destructive — playing a track removes it —
    // so a playlist loaded there vanished as it played and could not be
    // browsed or replayed. A slot behaves like any built-in playlist:
    // browsable, replayable, and looping at the end via Controller.Forward.
    private void LoadIntoSlot(object[][] tracks, int totalCount, int failedCount, string playlistName)
    {
      // Normalized so http/https, a trailing slash, or a re-pasted link all
      // land on the slot that already holds this playlist.
      string sourceKey = Utilities.IsValid(_pendingResolveUrl)
          ? PlaylistUrlUtils.GetSourceKey(_pendingResolveUrl.Get())
          : string.Empty;

      var slot = SelectSlot(sourceKey);
      if (!Utilities.IsValid(slot))
      {
        PrintError("No dynamic playlist slot is available.");
        NotifyResult(LoadResultQueueUnavailable, playlistName, 0, 0, 0);
        return;
      }

      _controller.TakeOwnership();
      slot.Fill(sourceKey, playlistName, tracks, NextSequence());

      // 自動再生仕様:
      // - プレイヤーが停止中 (Stopped) の場合のみ自動再生する
      // - 停止中は読み込んだプレイリストの先頭 (シャッフル時はランダム) から再生
      // - 既に再生中・一時停止中の場合は読み込むのみ
      // Queue には触れないため、ユーザーが手動で積んだキューの挙動は変わらない。
      // ただし Queue に曲がある間は Forward() が Queue を優先し、
      // PlayTrack(object[]) が ClearPlaylistIndexes() を呼ぶ (Controller.cs:452)
      // ため、Queue 消化後にプレイリストへは自動復帰しない (上流と同じ挙動)。
      if (_controller.Stopped)
      {
        if (_controller.ShufflePlay) _controller.PlayRandomTrack(slot.Playlist);
        else _controller.PlayTrack(slot.Playlist, 0);
      }

      var message = failedCount > 0
          ? $"Loaded {tracks.Length}/{totalCount} tracks ({failedCount} skipped)"
          : $"Loaded {tracks.Length} tracks as a playlist";
      PrintLog(message);
      NotifyResult(failedCount > 0 ? LoadResultPartial : LoadResultSuccess,
          playlistName, tracks.Length, failedCount, 0);
    }

    // Reloading the same playlist reuses its slot; otherwise an empty slot
    // is taken. When every slot is full the one filled longest ago is
    // replaced, so loading never dead-ends in a world where nobody can free
    // a slot. Note this is least-recently-LOADED, not least-recently-played:
    // _sequence only moves on a load, so the slot being played right now is
    // still a candidate. Overwriting it leaves the current track playing and
    // the next Forward() wraps to the new contents.
    private DynamicPlaylist SelectSlot(string sourceKey)
    {
      DynamicPlaylist empty = null;
      DynamicPlaylist oldest = null;
      int oldestSequence = 0;
      bool hasSourceKey = !string.IsNullOrEmpty(sourceKey);

      for (int i = 0; i < _dynamicPlaylists.Length; i++)
      {
        var slot = _dynamicPlaylists[i];
        if (!Utilities.IsValid(slot)) continue;

        if (hasSourceKey && slot.SourceKey == sourceKey) return slot;
        if (!Utilities.IsValid(empty) && slot.IsEmpty) empty = slot;
        if (!Utilities.IsValid(oldest) || slot.Sequence < oldestSequence)
        {
          oldest = slot;
          oldestSequence = slot.Sequence;
        }
      }

      return Utilities.IsValid(empty) ? empty : oldest;
    }

    private int NextSequence()
    {
      int max = 0;
      for (int i = 0; i < _dynamicPlaylists.Length; i++)
      {
        var slot = _dynamicPlaylists[i];
        if (Utilities.IsValid(slot) && slot.Sequence > max) max = slot.Sequence;
      }
      return max + 1;
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
