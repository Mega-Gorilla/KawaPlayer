using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Yamadev.YamaStream.Editor
{
  // Extension point that lets a module contribute a "paste a URL, get a
  // playlist" importer to the Playlist Editor (issue #90) without the core
  // editor assembly knowing anything about that module's service.
  //
  // Module editor assemblies reference Yamadev.YamaStream.Editor, never the
  // other way around, so the source lives in the module and the window only
  // ever talks to it through this interface.
  public interface IPlaylistImportSource
  {
    // Display order in the import bar. Lower comes first.
    int Order { get; }

    // EditorLocalization keys. A module supplies them from its own
    // Localization.Editor.json; the merged table resolves them either way.
    string TitleKey { get; }
    string InputHintKey { get; }

    // Whether this source can run against the given player. When false,
    // unavailableMessage explains why (already localized) and the window
    // shows it instead of the input field.
    bool IsAvailable(YamaPlayer player, out string unavailableMessage);

    UniTask<PlaylistImportResult> ImportAsync(YamaPlayer player, string input);

    // Whether an existing playlist in the editor came from the same source
    // item as this result. Normalizing the identity (scheme, trailing slash,
    // service-specific path shape) is the module's business, so the window
    // delegates the comparison rather than matching strings itself.
    bool MatchesSource(PlaylistData existing, PlaylistImportResult result);
  }

  public class PlaylistImportResult
  {
    public bool Success;
    // Already localized: shown verbatim in the result dialog.
    public string Message;
    public PlaylistData Data;
    // Free-form provenance tags owned by the source (e.g. "vhub").
    public string SourceKind;
    public string SourceKey;
    public int ImportedCount;
    public int SkippedCount;
    // The import succeeded but may be missing entries. Distinct from
    // SkippedCount because a source can know something went wrong without
    // being able to say how much (issue #101). A window applying the result
    // should confirm with the user before replacing anything.
    public bool IsPartial;

    public static PlaylistImportResult Failed(string message)
    {
      return new PlaylistImportResult { Success = false, Message = message };
    }
  }

  public static class PlaylistImportSources
  {
    private static IPlaylistImportSource[] _cached;

    // Found through TypeCache, as YamaPlayerBuildProcess finds its processors,
    // but this one feeds editor GUI code, so the instances are cached instead of
    // being made on every repaint. A domain reload clears the static.
    public static IPlaylistImportSource[] Get()
    {
      if (_cached != null) return _cached;

      _cached = TypeCache.GetTypesDerivedFrom<IPlaylistImportSource>()
        .Where(type => !type.IsInterface && !type.IsAbstract)
        .Select(CreateInstance)
        .Where(source => source != null)
        .OrderBy(source => source.Order)
        .ToArray();

      return _cached;
    }

    private static IPlaylistImportSource CreateInstance(Type type)
    {
      try
      {
        return Activator.CreateInstance(type) as IPlaylistImportSource;
      }
      catch (Exception ex)
      {
        Debug.LogError($"Failed to create playlist import source {type.Name}: {ex.Message}");
        return null;
      }
    }
  }
}
