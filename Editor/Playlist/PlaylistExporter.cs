using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Yamadev.YamaStream.Editor
{
  public static class PlaylistExporter
  {
    private class PlaylistsJson
    {
      public List<PlaylistData> playlists;
    }

    public static void Export(List<PlaylistItem> playlists)
    {
      string jsonStr = Serialize(playlists.Where(p => p != null).Select(p => new PlaylistData
      {
        active = p.gameObject.activeSelf,
        name = p.playlistName,
        tracks = p.tracks?.ToList() ?? new List<PlaylistTrack>(),
        youtubeListId = p.youtubePlaylistId,
        vhubPlaylistUrl = p.vhubPlaylistUrl
      }).ToList());
      string filePath = EditorUtility.SaveFilePanel("Export playlists", Application.dataPath, "playlists", "json");
      if (!string.IsNullOrEmpty(filePath)) File.WriteAllText(filePath, jsonStr);
    }

    // What Export writes, without the file panel, so that tests can read it
    // back with TryParse.
    public static string Serialize(List<PlaylistData> playlists)
    {
      var settings = new JsonSerializerSettings
      {
        Formatting = Formatting.Indented,
        ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
      };
      return JsonConvert.SerializeObject(new PlaylistsJson { playlists = playlists }, settings);
    }

    // Asks for a file and reads every playlist in it. Null when the file
    // panel is cancelled. A file is taken whole or not at all (issue #208):
    // when anything in it cannot be read, no playlists come back and error
    // says why, already localized, so a mistake halfway through does not
    // leave half of the file in the editor without a word.
    public static List<PlaylistData> Import(out string error)
    {
      error = null;
      string filePath = EditorUtility.OpenFilePanel("Import playlists", Application.dataPath, "json");
      if (string.IsNullOrEmpty(filePath)) return null;

      string json;
      try
      {
        json = File.ReadAllText(filePath);
      }
      catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
      {
        error = string.Format(EditorLocalization.Get("playlist.json.readError"), ex.Message);
        return new List<PlaylistData>();
      }
      return TryParse(json, out List<PlaylistData> playlists, out error) ? playlists : new List<PlaylistData>();
    }

    // The playlists in a file written by Export, by YamaPlayer, or by hand in
    // the same shape (the guide's playlists.md has it). Stops at the first
    // thing that cannot be read, gives back no playlists and says where.
    public static bool TryParse(string json, out List<PlaylistData> playlists, out string error)
    {
      playlists = new List<PlaylistData>();
      error = null;

      // Strings stay strings: by default Newtonsoft reads one that looks like
      // a date ("2026-10-20T04:00:00Z") as a date, and a playlist or track
      // named so would be refused as not a string. Reading on to the end
      // refuses anything after the root, as JToken.Parse does.
      JToken root;
      try
      {
        using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None })
        {
          root = JToken.Load(reader);
          while (reader.Read())
          {
          }
        }
      }
      catch (JsonReaderException ex)
      {
        error = string.Format(EditorLocalization.Get("playlist.json.parseError"), ex.LineNumber, ex.LinePosition);
        return false;
      }
      if (!(Find(root as JObject, "playlists") is JArray items))
      {
        error = EditorLocalization.Get("playlist.json.noPlaylists");
        return false;
      }

      var read = new List<PlaylistData>();
      for (int i = 0; i < items.Count; i++)
      {
        if (!TryReadPlaylist(items[i], i + 1, out PlaylistData data, out error)) return false;
        read.Add(data);
      }
      playlists = read;
      return true;
    }

    private static bool TryReadPlaylist(JToken token, int number, out PlaylistData data, out string error)
    {
      data = null;
      string where = string.Format(EditorLocalization.Get("playlist.json.at.playlist"), number);
      if (!(token is JObject item))
      {
        error = NotObject(where);
        return false;
      }
      if (!TryBool(item, "active", true, where, out bool active, out error)
        || !TryString(item, "name", where, out string name, out error)
        || !TryString(item, "youtubeListId", where, out string youtubeListId, out error)
        || !TryString(item, "vhubPlaylistUrl", where, out string vhubPlaylistUrl, out error)) return false;

      var tracks = new List<PlaylistTrack>();
      JToken tracksToken = Find(item, "tracks");
      if (tracksToken != null)
      {
        if (!(tracksToken is JArray trackItems))
        {
          error = BadValue(where, "tracks", "playlist.json.expect.array");
          return false;
        }
        for (int i = 0; i < trackItems.Count; i++)
        {
          if (!TryReadTrack(trackItems[i], number, i + 1, out PlaylistTrack track, out error)) return false;
          tracks.Add(track);
        }
      }

      data = new PlaylistData
      {
        active = active,
        name = name,
        tracks = tracks,
        youtubeListId = youtubeListId,
        vhubPlaylistUrl = vhubPlaylistUrl
      };
      return true;
    }

    private static bool TryReadTrack(JToken token, int playlistNumber, int number, out PlaylistTrack track, out string error)
    {
      track = null;
      error = null;
      string where = string.Format(EditorLocalization.Get("playlist.json.at.track"), playlistNumber, number);
      if (!(token is JObject item))
      {
        error = NotObject(where);
        return false;
      }

      // YamaPlayer's files call it mode. Left out, a track plays on AVPro, as
      // a URL entered in the world does (issue #207); a 0 written in the file
      // still means Unity.
      string key = "playerType";
      JToken typeToken = Find(item, key);
      if (typeToken == null)
      {
        key = "mode";
        typeToken = Find(item, key);
      }
      VideoPlayerType playerType = VideoPlayerType.AVProVideoPlayer;
      if (typeToken != null && !TryPlayerType(typeToken, out playerType))
      {
        error = BadValue(where, key, "playlist.json.expect.playerType");
        return false;
      }
      if (!TryString(item, "title", where, out string title, out error)
        || !TryString(item, "url", where, out string url, out error)) return false;

      track = new PlaylistTrack
      {
        playerType = playerType,
        title = title,
        url = url
      };
      return true;
    }

    // A number or a name, the name in any case: 1 or "AVProVideoPlayer". Only
    // the types there are: Enum.TryParse would also take "7", and a list
    // such as "UnityVideoPlayer, AVProVideoPlayer".
    private static bool TryPlayerType(JToken token, out VideoPlayerType playerType)
    {
      foreach (VideoPlayerType type in Enum.GetValues(typeof(VideoPlayerType)))
      {
        bool matches = token.Type == JTokenType.Integer
          ? ((JValue)token).Value is long number && number == (int)type
          : token.Type == JTokenType.String && string.Equals((string)token, type.ToString(), StringComparison.OrdinalIgnoreCase);
        if (!matches) continue;
        playerType = type;
        return true;
      }
      playerType = VideoPlayerType.AVProVideoPlayer;
      return false;
    }

    // A key as written, or with its first letter in capitals (Name, Tracks,
    // PlayerType): YamaPlayer's files and hand-written ones use both. A null
    // counts as left out.
    private static JToken Find(JObject item, string key)
    {
      if (item == null) return null;
      JToken token = item[key] ?? item[char.ToUpperInvariant(key[0]) + key.Substring(1)];
      return token == null || token.Type == JTokenType.Null ? null : token;
    }

    private static bool TryString(JObject item, string key, string where, out string value, out string error)
    {
      value = "";
      error = null;
      JToken token = Find(item, key);
      if (token == null) return true;
      if (token.Type != JTokenType.String)
      {
        error = BadValue(where, key, "playlist.json.expect.string");
        return false;
      }
      value = (string)token;
      return true;
    }

    private static bool TryBool(JObject item, string key, bool fallback, string where, out bool value, out string error)
    {
      value = fallback;
      error = null;
      JToken token = Find(item, key);
      if (token == null) return true;
      if (token.Type != JTokenType.Boolean)
      {
        error = BadValue(where, key, "playlist.json.expect.bool");
        return false;
      }
      value = (bool)token;
      return true;
    }

    private static string NotObject(string where) =>
      string.Format(EditorLocalization.Get("playlist.json.notObject"), where);

    private static string BadValue(string where, string key, string expectKey) =>
      string.Format(EditorLocalization.Get("playlist.json.badValue"), where, key, EditorLocalization.Get(expectKey));
  }
}
