using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Yamadev.YamaStream.Editor;

namespace Yamadev.YamaStream.Modules.PlaylistLoader.Editor
{
  // Imports a VHub playlist into the Playlist Editor as a static snapshot
  // (issue #90). The snapshot is taken here, at import time -- the build only
  // bakes whatever was last saved, so a world build never touches the network.
  //
  // The response carries no real video URLs, only pool indices, so every track
  // has to be resolved through the target player's baked _redirectPool. That
  // makes the importer player-specific: it cannot run without a PlaylistLoader.
  public class VhubPlaylistImportSource : IPlaylistImportSource
  {
    private const string KeyPrefix = "module.playlistLoader.import.";

    public int Order => 0;
    public string TitleKey => KeyPrefix + "title";
    public string InputHintKey => KeyPrefix + "hint";

    private static string L(string suffix) => EditorLocalization.Get(KeyPrefix + suffix);

    public bool IsAvailable(YamaPlayer player, out string unavailableMessage)
    {
      unavailableMessage = string.Empty;
      if (player == null)
      {
        unavailableMessage = L("errorNoPlayer");
        return false;
      }
      var loader = FindLoader(player);
      if (loader == null)
      {
        unavailableMessage = L("errorNoLoader");
        return false;
      }
      if (loader.RedirectPool == null || loader.RedirectPool.Length == 0)
      {
        unavailableMessage = L("errorNoPool");
        return false;
      }
      return true;
    }

    public bool MatchesSource(PlaylistData existing, PlaylistImportResult result)
    {
      if (existing == null || result == null) return false;
      if (string.IsNullOrEmpty(existing.vhubPlaylistUrl) || string.IsNullOrEmpty(result.SourceKey)) return false;
      return PlaylistUrlUtils.GetSourceKey(existing.vhubPlaylistUrl) == result.SourceKey;
    }

    public async UniTask<PlaylistImportResult> ImportAsync(YamaPlayer player, string input)
    {
      var loader = player == null ? null : FindLoader(player);
      if (loader == null) return PlaylistImportResult.Failed(L("errorNoLoader"));

      string url = (input ?? string.Empty).Trim();
      if (loader.ClassifyUrl(url) != PlaylistLoader.UrlKindOwnPlaylist)
        return PlaylistImportResult.Failed(L("errorNotPlaylistUrl"));

      // Classify deliberately accepts http so a pasted http URL reads as a
      // playlist rather than a video (issue #82); the fetch itself is https
      // only, matching the runtime loader (issue #95).
      if (UrlUtils.GetProtocolFromUrl(url) != "https")
        return PlaylistImportResult.Failed(L("errorNotHttps"));

      string body;
      using (var request = UnityWebRequest.Get(url))
      {
        try
        {
          await request.SendWebRequest();
        }
        catch (System.Exception ex)
        {
          Debug.LogWarning($"[VhubPlaylistImport] {ex.Message}");
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
          Debug.LogError($"[VhubPlaylistImport] Failed to fetch playlist: {request.error}");
          return PlaylistImportResult.Failed(
            request.responseCode == 404 ? L("errorNotFound") : L("errorDownload"));
        }
        body = request.downloadHandler.text;
      }

      return BuildResult(loader, url, body);
    }

    private PlaylistImportResult BuildResult(PlaylistLoader loader, string url, string body)
    {
      JObject root;
      try
      {
        root = JObject.Parse(body);
      }
      catch (System.Exception ex)
      {
        Debug.LogError($"[VhubPlaylistImport] {ex.Message}");
        return PlaylistImportResult.Failed(L("errorInvalidResponse"));
      }

      if (root["ok"]?.Type == JTokenType.Boolean && root["ok"].Value<bool>() == false)
      {
        Debug.LogError($"[VhubPlaylistImport] {root["error"]?.Value<string>() ?? "server error"}");
        return PlaylistImportResult.Failed(L("errorServer"));
      }

      // A playlist resolved for a different pool would map indices into the
      // wrong redirect URLs. Reject an explicit mismatch; a missing pool field
      // is tolerated, exactly as the runtime loader does.
      string pool = root["pool"]?.Type == JTokenType.String ? root["pool"].Value<string>() : null;
      if (pool != null && pool != loader.PoolId)
      {
        Debug.LogError($"[VhubPlaylistImport] Pool mismatch: expected '{loader.PoolId}', got '{pool}'.");
        return PlaylistImportResult.Failed(L("errorPoolMismatch"));
      }

      if (!(root["tracks"] is JArray trackArray) || trackArray.Count == 0)
        return PlaylistImportResult.Failed(L("errorEmpty"));

      string playlistName = root["name"]?.Type == JTokenType.String ? root["name"].Value<string>() : string.Empty;
      if (string.IsNullOrEmpty(playlistName)) playlistName = EditorLocalization.Get("playlist.unnamed");

      var redirectPool = loader.RedirectPool;
      var tracks = new List<PlaylistTrack>();
      int skipped = 0;

      foreach (var token in trackArray)
      {
        if (!(token is JObject track)) { skipped++; continue; }

        int index = track["index"]?.Type == JTokenType.Integer ? track["index"].Value<int>() : -1;
        if (index < 0 || index >= redirectPool.Length) { skipped++; continue; }

        var slot = redirectPool[index];
        string slotUrl = slot == null ? string.Empty : slot.Get();
        if (string.IsNullOrEmpty(slotUrl)) { skipped++; continue; }

        int mode = track["mode"]?.Type == JTokenType.Integer ? track["mode"].Value<int>() : 0;
        string title = track["title"]?.Type == JTokenType.String ? track["title"].Value<string>() : string.Empty;

        tracks.Add(new PlaylistTrack
        {
          playerType = (VideoPlayerType)mode,
          title = title,
          url = slotUrl,
        });
      }

      // Every track unusable means the pool does not match this playlist, so
      // producing an empty playlist would only look like a successful import.
      if (tracks.Count == 0) return PlaylistImportResult.Failed(L("errorAllSkipped"));

      return new PlaylistImportResult
      {
        Success = true,
        Message = skipped > 0
          ? string.Format(L("resultPartial"), playlistName, tracks.Count, skipped)
          : string.Format(L("result"), playlistName, tracks.Count),
        SourceKind = "vhub",
        SourceKey = PlaylistUrlUtils.GetSourceKey(url),
        ImportedCount = tracks.Count,
        SkippedCount = skipped,
        Data = new PlaylistData
        {
          originalItem = null,
          active = true,
          name = playlistName,
          tracks = tracks,
          youtubeListId = "",
          vhubPlaylistUrl = url,
          isNameEditing = false,
        },
      };
    }

    private static PlaylistLoader FindLoader(YamaPlayer player)
    {
      return player.GetComponentInChildren<PlaylistLoader>(true);
    }
  }
}
