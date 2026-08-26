using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Yamadev.YamaStream.Editor
{
  public class PlaylistEditorWindow : EditorWindow
  {
    private YamaPlayer _player;
    private List<PlaylistData> _playlists;
    private ReorderableList _playlistsTable;
    private ReorderableList _playlistTracksTable;
    private Vector2 _leftScrollPos, _rightScrollPos;
    private PlaylistData _selectedPlaylist;
    private bool _useYoutubePlaylistName;
    private VideoPlayerType _defaultTrackMode = VideoPlayerType.AVProVideoPlayer;
    // Toolbar entries open one panel at a time below the toolbar (issue #90).
    private enum ToolbarPanel { None, Ytdlp, ImportUrl, ImportJson, ExportJson }
    private ToolbarPanel _activePanel = ToolbarPanel.None;
    // URL import state. Sources are contributed by modules through
    // IPlaylistImportSource, so this is source-agnostic.
    private int _importSourceIndex;
    private string _importInput = "";
    private bool _importInFlight;
    private bool _isDirty;
    private bool IsDirty
    {
      get => _isDirty;
      set
      {
        _isDirty = value;
        hasUnsavedChanges = value;
        if (value)
        {
          saveChangesMessage = EditorLocalization.Get("msg.confirmSave");
        }
      }
    }

    public YamaPlayer YamaPlayer
    {
      get => _player;
      set
      {
        if (_player == value) return;
        if (!ConfirmSave()) return;
        _player = value;
        ReadPlaylists();
      }
    }

    [MenuItem("KawaPlayer/Edit Playlist")]
    public static void ShowPlaylistEditorWindow()
    {
      var window = GetWindow<PlaylistEditorWindow>(title: "KawaPlayer Playlist Editor");
      window.Show();
    }

    public static void ShowPlaylistEditorWindow(YamaPlayer player)
    {
      var window = GetWindow<PlaylistEditorWindow>(title: "KawaPlayer Playlist Editor");
      window.YamaPlayer = player;
      window.Show();
    }

    public static void ShowPlaylistEditorWindow(YamaPlayer player, PlaylistItem targetPlaylist)
    {
      var window = GetWindow<PlaylistEditorWindow>(title: "KawaPlayer Playlist Editor");
      window.YamaPlayer = player;
      window.SelectPlaylist(targetPlaylist);
      window.Show();
    }

    public void SelectPlaylist(PlaylistItem targetPlaylist)
    {
      if (_playlists == null || targetPlaylist == null) return;
      int index = _playlists.FindIndex(p => p.originalItem == targetPlaylist);
      if (index >= 0 && _playlistsTable != null)
      {
        _playlistsTable.index = index;
        GeneratePlaylistTracksView(_playlistsTable);
      }
    }

    private void OnEnable()
    {
      if (_player == null) return;
      ReadPlaylists();
    }

    public override void SaveChanges()
    {
      Save();
      base.SaveChanges();
    }

    public override void DiscardChanges()
    {
      RevertChanges();
      base.DiscardChanges();
    }

    private void ReadPlaylists()
    {
      PlaylistManager container = _player?.GetComponentInChildren<PlaylistManager>();
      if (container == null) return;

      var originalPlaylists = container.GetPlaylists();
      _playlists = originalPlaylists.Select(item => new PlaylistData(item)).ToList();
      _selectedPlaylist = null;
      _playlistTracksTable = null;
      IsDirty = false;
      GeneratePlaylistsView();
    }

    private void RevertChanges()
    {
      IsDirty = false;
      ReadPlaylists();
    }

    private void GeneratePlaylistsView()
    {
      _playlistsTable = new ReorderableList(_playlists, typeof(PlaylistData), true, false, true, true)
      {
        onAddCallback = (list) =>
        {
          _playlists.Add(new PlaylistData
          {
            originalItem = null,
            name = EditorLocalization.Get("playlist.new"),
            active = true,
            youtubeListId = "",
            vhubPlaylistUrl = "",
            tracks = new List<PlaylistTrack>(),
            isNameEditing = false
          });
          IsDirty = true;
        },
        onRemoveCallback = (list) =>
        {
          if (list.index < 0 || list.index >= _playlists.Count) return;
          _playlists.RemoveAt(list.index);
          list.index = _playlists.Count > 0 ? _playlists.Count - 1 : 0;
          GeneratePlaylistTracksView(list);
          IsDirty = true;
        },
        drawElementCallback = (rect, index, isActive, isFocused) =>
        {
          if (index >= _playlists.Count) return;
          var playlist = _playlists[index];

          rect.height = EditorGUIUtility.singleLineHeight;
          Rect nameRect = rect;
          nameRect.xMax = rect.width - 24;

          if (playlist.isNameEditing)
          {
            playlist.name = EditorGUI.TextField(nameRect, playlist.name);
          }
          else
          {
            EditorGUI.LabelField(nameRect, $"{playlist.name} ({playlist.tracks?.Count ?? 0})");
          }

          Rect btnRect = rect;
          btnRect.xMin = nameRect.xMax;
          if (playlist.isNameEditing)
          {
            if (GUI.Button(btnRect, EditorLocalization.Get("button.save")))
            {
              playlist.isNameEditing = false;
              IsDirty = true;
            }
          }
          else
          {
            if (GUI.Button(btnRect, EditorLocalization.Get("button.edit"))) playlist.isNameEditing = true;
          }

          rect.y += EditorGUIUtility.standardVerticalSpacing + EditorGUIUtility.singleLineHeight;
          Rect activeRect = rect;
          activeRect.xMax = rect.width;
          EditorGUI.LabelField(
            activeRect,
            playlist.active ? EditorLocalization.Get("button.active") : EditorLocalization.Get("button.inactive"),
            new GUIStyle() { normal = new GUIStyleState() { textColor = playlist.active ? Color.green : Color.red } }
          );

          Rect toggleRect = rect;
          toggleRect.xMin = activeRect.xMax;
          using (var check = new EditorGUI.ChangeCheckScope())
          {
            bool newActive = EditorGUI.Toggle(toggleRect, playlist.active);
            if (check.changed)
            {
              playlist.active = newActive;
              IsDirty = true;
            }
          }
        },
        onSelectCallback = GeneratePlaylistTracksView,
        onReorderCallback = (ReorderableList list) =>
        {
          IsDirty = true;
        },
        elementHeight = (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 2,
        showDefaultBackground = false,
      };
    }

    private void GeneratePlaylistTracksView(ReorderableList selected)
    {
      if (selected.index < 0 || selected.index >= _playlists.Count)
      {
        _selectedPlaylist = null;
        _playlistTracksTable = null;
        return;
      }
      _selectedPlaylist = _playlists[selected.index];
      if (_selectedPlaylist.tracks == null) _selectedPlaylist.tracks = new List<PlaylistTrack>();

      _playlistTracksTable = new ReorderableList(_selectedPlaylist.tracks, typeof(PlaylistTrack), true, false, true, true)
      {
        onAddCallback = (list) =>
        {
          _selectedPlaylist.tracks.Add(new PlaylistTrack
          {
            playerType = _defaultTrackMode,
            title = "",
            url = ""
          });
          IsDirty = true;
        },
        onRemoveCallback = (list) =>
        {
          if (list.index >= 0 && list.index < _selectedPlaylist.tracks.Count)
          {
            _selectedPlaylist.tracks.RemoveAt(list.index);
          }
          IsDirty = true;
        },
        drawElementCallback = (rect, index, isActive, isFocused) =>
        {
          if (index >= _selectedPlaylist.tracks.Count) return;
          var track = _selectedPlaylist.tracks[index];

          rect.height = EditorGUIUtility.singleLineHeight;
          float labelWidth = EditorGUIUtility.labelWidth;
          EditorGUIUtility.labelWidth = 80;

          using (var check = new EditorGUI.ChangeCheckScope())
          {
            Rect numberRect = rect;
            string number = $"#{index + 1}";
            numberRect.xMin = rect.width - number.Length * 8f + 20f;
            EditorGUI.LabelField(numberRect, number);

            Rect playerRect = rect;
            playerRect.xMax = 240;
            track.playerType = (VideoPlayerType)EditorGUI.EnumPopup(playerRect, new GUIContent(EditorLocalization.Get("settings.videoPlayerType.label")), track.playerType);

            rect.y += EditorGUIUtility.standardVerticalSpacing + EditorGUIUtility.singleLineHeight;
            track.title = EditorGUI.TextField(rect, new GUIContent(EditorLocalization.Get("label.title")), track.title);

            rect.y += EditorGUIUtility.standardVerticalSpacing + EditorGUIUtility.singleLineHeight;
            track.url = EditorGUI.TextField(rect, new GUIContent("Url"), track.url);

            if (check.changed)
            {
              IsDirty = true;
            }
          }
          EditorGUIUtility.labelWidth = labelWidth;
        },
        onReorderCallback = (ReorderableList list) =>
        {
          IsDirty = true;
        },
        elementHeightCallback = (index => (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 3),
        showDefaultBackground = false,
      };
    }

    private void OnGUI()
    {
      using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
      {
        YamaPlayer = EditorGUILayout.ObjectField(YamaPlayer, typeof(YamaPlayer), true) as YamaPlayer;

        // Every toolbar entry that leads somewhere opens its own panel below
        // the toolbar, so they all behave the same way. Save is the exception:
        // it is the action itself, not a way in to one.
        DrawPanelToggle(ToolbarPanel.Ytdlp, YtdlpResolver.IsAvailable ? "ytdlp.update" : "ytdlp.download");
        if (PlaylistImportSources.Get().Length > 0) DrawPanelToggle(ToolbarPanel.ImportUrl, "playlist.importFromUrl");
        DrawPanelToggle(ToolbarPanel.ImportJson, "playlist.importFromJson");
        DrawPanelToggle(ToolbarPanel.ExportJson, "playlist.exportToJson");
        using (new EditorGUI.DisabledGroupScope(!IsDirty))
        {
          var saveStyle = new GUIStyle(EditorStyles.toolbarButton);
          if (IsDirty)
          {
            saveStyle.normal.textColor = new Color(1f, 0.6f, 0.2f);
            saveStyle.hover.textColor = new Color(1f, 0.7f, 0.4f);
          }
          if (GUILayout.Button(EditorLocalization.Get("button.save"), saveStyle, GUILayout.ExpandWidth(false))) Save();
        }
      }
      if (_activePanel != ToolbarPanel.None) DrawActivePanel();
      using (new EditorGUILayout.HorizontalScope())
      {
        using (var vert = new EditorGUILayout.VerticalScope(GUILayout.MaxWidth(380)))
        {
          HandleDragEvent(vert.rect);
          EditorGUILayout.LabelField(EditorLocalization.Get("label.playlists"), Styles.Bold);
          _leftScrollPos = EditorGUILayout.BeginScrollView(_leftScrollPos, GUI.skin.box);
          if (_player != null) _playlistsTable?.DoLayoutList();
          GUILayout.FlexibleSpace();
          EditorGUILayout.HelpBox(EditorLocalization.Get("playlist.importFromPlayer"), MessageType.Info);
          EditorGUILayout.EndScrollView();
          DrawPlaylistSettings();
        }
        using (new EditorGUILayout.VerticalScope())
        {
          using (new EditorGUILayout.HorizontalScope())
          {
            using (new EditorGUI.DisabledScope(_selectedPlaylist == null))
            {
              EditorGUILayout.LabelField(EditorLocalization.Get("label.playlistTracks"), Styles.Bold);
              GUILayout.FlexibleSpace();
              if (GUILayout.Button(EditorLocalization.Get("playlist.reverse"), GUILayout.ExpandWidth(false)))
              {
                ReverseTracks();
              }
            }
          }
          _rightScrollPos = EditorGUILayout.BeginScrollView(_rightScrollPos, GUI.skin.box);
          if (_player != null) _playlistTracksTable?.DoLayoutList();
          GUILayout.FlexibleSpace();
          EditorGUILayout.EndScrollView();
        }
      }
    }

    private void ReverseTracks()
    {
      if (_selectedPlaylist == null) return;
      _selectedPlaylist.tracks?.Reverse();
      GeneratePlaylistTracksView(_playlistsTable);
      IsDirty = true;
    }

    private void HandleDragEvent(Rect rect)
    {
      switch (Event.current.type)
      {
        case EventType.DragUpdated:
        case EventType.DragPerform:
          if (!rect.Contains(Event.current.mousePosition)) break;
          DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
          if (Event.current.type == EventType.DragPerform)
          {
            DragAndDrop.AcceptDrag();
            if (_playlists == null)
            {
              EditorUtility.DisplayDialog("Import playlists", "Assign a YamaPlayer to the editor before importing.", "OK");
            }
            else
            {
              var imported = PlaylistImporter.ImportPlaylists(DragAndDrop.objectReferences);
              _playlists.AddRange(imported);
              if (imported.Count > 0) IsDirty = true;
            }
          }
          Event.current.Use();
          break;
      }
    }

    private void DrawPlaylistSettings()
    {
      EditorGUILayout.LabelField(EditorLocalization.Get("playlist.settings"), Styles.Bold);
      using (new GUILayout.VerticalScope(GUI.skin.box))
      {
        using (new EditorGUILayout.HorizontalScope())
        {
          _defaultTrackMode = (VideoPlayerType)EditorGUILayout.Popup(EditorLocalization.Get("settings.videoPlayerType.label"), (int)_defaultTrackMode, Enum.GetNames(typeof(VideoPlayerType)));
          using (new EditorGUI.DisabledScope(_selectedPlaylist == null))
          {
            if (GUILayout.Button(EditorLocalization.Get("playlist.applyForAll"), GUILayout.ExpandWidth(false)))
            {
              ApplyModeForAllTracks();
            }
          }
        }
        _useYoutubePlaylistName = EditorGUILayout.Toggle(EditorLocalization.Get("playlist.overwriteName"), _useYoutubePlaylistName);
        using (new EditorGUILayout.HorizontalScope())
        {
          // Disabled while any import is running so a second fetch cannot be
          // started against a playlist the first one is about to replace.
          using (new EditorGUI.DisabledScope(_selectedPlaylist == null || _importInFlight))
          {
            string currentYoutubeId = _selectedPlaylist?.youtubeListId ?? "";

            var youtubeListId = EditorGUILayout.TextField(currentYoutubeId);
            if (_selectedPlaylist != null && youtubeListId != currentYoutubeId)
            {
              _selectedPlaylist.youtubeListId = youtubeListId;
              IsDirty = true;
            }

            if (GUILayout.Button(EditorLocalization.Get("playlist.loadYoutube"), GUILayout.ExpandWidth(false)))
            {
              ReadYouTubePlaylist().Forget();
            }
          }
        }
        // Imported playlists carry the URL they came from. Showing it read-only
        // keeps that saved metadata visible without inviting hand edits, since
        // the URL only means anything alongside the tracks it produced.
        if (!string.IsNullOrEmpty(_selectedPlaylist?.vhubPlaylistUrl))
        {
          using (new EditorGUI.DisabledScope(true))
          {
            EditorGUILayout.TextField(EditorLocalization.Get("playlist.sourceUrl"), _selectedPlaylist.vhubPlaylistUrl);
          }
        }
      }
    }

    private void DrawPanelToggle(ToolbarPanel panel, string labelKey)
    {
      bool open = _activePanel == panel;
      bool next = GUILayout.Toggle(open, EditorLocalization.Get(labelKey), EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));
      if (next != open) _activePanel = next ? panel : ToolbarPanel.None;
    }

    private void DrawActivePanel()
    {
      using (new GUILayout.VerticalScope(GUI.skin.box))
      {
        switch (_activePanel)
        {
          case ToolbarPanel.Ytdlp: DrawYtdlpPanel(); break;
          case ToolbarPanel.ImportUrl: DrawImportUrlPanel(); break;
          case ToolbarPanel.ImportJson: DrawJsonPanel(false); break;
          case ToolbarPanel.ExportJson: DrawJsonPanel(true); break;
        }
      }
    }

    private void DrawYtdlpPanel()
    {
      EditorGUILayout.HelpBox(EditorLocalization.Get("playlist.panel.ytdlp"), MessageType.Info);
      using (new EditorGUILayout.HorizontalScope())
      {
        EditorGUILayout.LabelField(EditorLocalization.Get(
          YtdlpResolver.IsAvailable ? "playlist.panel.ytdlpInstalled" : "playlist.panel.ytdlpMissing"));
        if (GUILayout.Button(
          EditorLocalization.Get(YtdlpResolver.IsAvailable ? "ytdlp.update" : "ytdlp.download"),
          GUILayout.ExpandWidth(false)))
        {
          YtdlpResolver.DownloadYtdlpExecutable().Forget();
        }
      }
    }

    private void DrawJsonPanel(bool export)
    {
      EditorGUILayout.HelpBox(EditorLocalization.Get(
        export ? "playlist.panel.exportJson" : "playlist.panel.importJson"), MessageType.Info);
      using (new EditorGUILayout.HorizontalScope())
      {
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(
          EditorLocalization.Get(export ? "playlist.exportToJson" : "playlist.importFromJson"),
          GUILayout.ExpandWidth(false)))
        {
          if (export) Export(); else Import();
        }
      }
    }

    private void DrawImportUrlPanel()
    {
      var sources = PlaylistImportSources.Get();
      if (sources.Length == 0) return;

      if (sources.Length > 1)
      {
        _importSourceIndex = EditorGUILayout.Popup(
          _importSourceIndex,
          sources.Select(s => EditorLocalization.Get(s.TitleKey)).ToArray());
      }
      _importSourceIndex = Mathf.Clamp(_importSourceIndex, 0, sources.Length - 1);
      var source = sources[_importSourceIndex];

      if (!source.IsAvailable(_player, out string unavailable))
      {
        EditorGUILayout.HelpBox(unavailable, MessageType.Warning);
        return;
      }

      using (new EditorGUILayout.HorizontalScope())
      using (new EditorGUI.DisabledScope(_importInFlight))
      {
        if (sources.Length == 1) EditorGUILayout.LabelField(EditorLocalization.Get(source.TitleKey), GUILayout.Width(120));
        _importInput = EditorGUILayout.TextField(_importInput);
        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_importInput)))
        {
          if (GUILayout.Button(EditorLocalization.Get("playlist.importFromUrl"), GUILayout.ExpandWidth(false)))
          {
            RunImport(source, _importInput).Forget();
          }
        }
      }
      EditorGUILayout.HelpBox(EditorLocalization.Get(source.InputHintKey), MessageType.Info);
    }

    // The request outlives the GUI event that started it, so nothing is
    // applied until we confirm the window still exists and is still pointed
    // at the player the import was started for.
    private async UniTaskVoid RunImport(IPlaylistImportSource source, string input)
    {
      if (_importInFlight) return;
      var startedFor = _player;
      _importInFlight = true;

      PlaylistImportResult result;
      try
      {
        result = await source.ImportAsync(startedFor, input);
      }
      catch (Exception ex)
      {
        Debug.LogException(ex);
        result = PlaylistImportResult.Failed(ex.Message);
      }
      finally
      {
        _importInFlight = false;
      }

      if (this == null || _player != startedFor || _playlists == null) return;

      if (result == null || !result.Success || result.Data == null)
      {
        EditorUtility.DisplayDialog(
          EditorLocalization.Get("playlist.import.title"),
          result?.Message ?? "",
          EditorLocalization.Get("button.ok"));
        return;
      }

      ApplyImportResult(source, result);
      Repaint();
    }

    private void ApplyImportResult(IPlaylistImportSource source, PlaylistImportResult result)
    {
      var existing = _playlists.FirstOrDefault(p => p != null && source.MatchesSource(p, result));
      if (existing != null)
      {
        int choice = EditorUtility.DisplayDialogComplex(
          EditorLocalization.Get("playlist.import.title"),
          string.Format(EditorLocalization.Get("playlist.import.duplicate"), existing.name),
          EditorLocalization.Get("button.doUpdate"),
          EditorLocalization.Get("button.cancel"),
          EditorLocalization.Get("playlist.import.addNew"));
        if (choice == 1) return;
        if (choice == 0)
        {
          existing.name = result.Data.name;
          existing.tracks = result.Data.tracks ?? new List<PlaylistTrack>();
          // Provenance comes wholesale from the result so importing from one
          // source clears the identifiers belonging to any other.
          existing.youtubeListId = result.Data.youtubeListId ?? "";
          existing.vhubPlaylistUrl = result.Data.vhubPlaylistUrl ?? "";
          IsDirty = true;
          GeneratePlaylistsView();
          GeneratePlaylistTracksView(_playlistsTable);
          ShowImportSummary(result);
          return;
        }
      }

      _playlists.Add(result.Data);
      IsDirty = true;
      GeneratePlaylistsView();
      ShowImportSummary(result);
    }

    private void ShowImportSummary(PlaylistImportResult result)
    {
      if (string.IsNullOrEmpty(result.Message)) return;
      EditorUtility.DisplayDialog(
        EditorLocalization.Get("playlist.import.title"),
        result.Message,
        EditorLocalization.Get("button.ok"));
    }

    private void ApplyModeForAllTracks()
    {
      if (_selectedPlaylist?.tracks == null) return;

      foreach (var track in _selectedPlaylist.tracks)
      {
        track.playerType = _defaultTrackMode;
      }
      IsDirty = true;
    }

    // Nothing on the selected playlist is touched until the fetch has come
    // back successfully. A failed fetch used to reach here as an empty result
    // and wipe the tracks being edited, with no message anywhere (issue #101),
    // so both the normalized id and the tracks are held in locals until then.
    public async UniTask ReadYouTubePlaylist()
    {
      if (_selectedPlaylist == null || _importInFlight) return;

      var startedFor = _player;
      var target = _selectedPlaylist;
      var normalizedId = YtdlpResolver.GetYoutubePlaylistIdFromUrl(target.youtubeListId);

      PlaylistImportResult result;
      _importInFlight = true;
      try
      {
        result = await PlaylistImporter.GetYouTubePlaylistData(normalizedId);
      }
      finally
      {
        _importInFlight = false;
      }

      // The fetch outlives the GUI event that started it, so the playlist it
      // was started for has to still be the one on screen.
      if (this == null || _player != startedFor || _selectedPlaylist != target) return;

      if (result == null || !result.Success || result.Data == null)
      {
        if (!string.IsNullOrEmpty(result?.Message))
        {
          EditorUtility.DisplayDialog(
            EditorLocalization.Get("playlist.import.title"),
            result.Message,
            EditorLocalization.Get("button.ok"));
        }
        return;
      }

      if (result.SkippedCount > 0 && !EditorUtility.DisplayDialog(
        EditorLocalization.Get("playlist.import.title"),
        string.Format(EditorLocalization.Get("ytdlp.partialWarning"), result.ImportedCount),
        EditorLocalization.Get("button.yes"),
        EditorLocalization.Get("button.no")))
      {
        return;
      }

      target.youtubeListId = normalizedId;
      if (_useYoutubePlaylistName && !string.IsNullOrEmpty(result.Data.name))
      {
        target.name = result.Data.name;
      }

      target.tracks = result.Data.tracks ?? new List<PlaylistTrack>();
      // The tracks now come from YouTube, so a VHub source URL saved on this
      // playlist would misdescribe it (issue #90).
      target.vhubPlaylistUrl = "";

      IsDirty = true;
      GeneratePlaylistsView();
      GeneratePlaylistTracksView(_playlistsTable);
      Repaint();
    }

    private bool ConfirmSave()
    {
      if (!IsDirty) return true;

      int result = EditorUtility.DisplayDialogComplex(
        EditorLocalization.Get("msg.notSaved"),
        EditorLocalization.Get("msg.confirmSave"),
        EditorLocalization.Get("button.save"),
        EditorLocalization.Get("button.cancel"),
        EditorLocalization.Get("button.notSave")
      );

      switch (result)
      {
        case 0:
          Save();
          return true;
        case 1:
          return false;
        case 2:
          RevertChanges();
          return true;
        default:
          return true;
      }
    }

    private void Save()
    {
      PlaylistManager container = _player?.GetComponentInChildren<PlaylistManager>();
      if (container == null) return;

      var existingPlaylists = container.GetPlaylists();
      var processedOriginals = new HashSet<PlaylistItem>();

      for (int i = 0; i < _playlists.Count; i++)
      {
        var playlist = _playlists[i];

        PlaylistItem item;
        if (playlist.originalItem != null)
        {
          item = playlist.originalItem;
          processedOriginals.Add(item);
        }
        else
        {
          GameObject obj = new GameObject(playlist.name);
          obj.transform.SetParent(container.transform);
          item = obj.AddComponent<PlaylistItem>();
          playlist.originalItem = item;
        }

        var so = new SerializedObject(item);
        so.FindProperty("playlistName").stringValue = playlist.name;
        so.FindProperty("youtubePlaylistId").stringValue = playlist.youtubeListId;
        so.FindProperty("vhubPlaylistUrl").stringValue = playlist.vhubPlaylistUrl ?? "";

        var tracksProp = so.FindProperty("tracks");
        tracksProp.arraySize = playlist.tracks?.Count ?? 0;
        if (playlist.tracks != null)
        {
          for (int j = 0; j < playlist.tracks.Count; j++)
          {
            var track = playlist.tracks[j];
            var trackProp = tracksProp.GetArrayElementAtIndex(j);
            trackProp.FindPropertyRelative("playerType").intValue = (int)track.playerType;
            trackProp.FindPropertyRelative("title").stringValue = track.title;
            trackProp.FindPropertyRelative("url").stringValue = track.url;
          }
        }
        so.ApplyModifiedProperties();

        item.gameObject.name = playlist.name;
        item.gameObject.SetActive(playlist.active);
        item.transform.SetSiblingIndex(i + 1);

        EditorUtility.SetDirty(item);
        EditorUtility.SetDirty(item.gameObject);
      }

      foreach (var existingItem in existingPlaylists)
      {
        if (existingItem != null && !processedOriginals.Contains(existingItem))
        {
          GameObject.DestroyImmediate(existingItem.gameObject);
        }
      }

      if (_player != null)
      {
        EditorUtility.SetDirty(_player.gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(_player.gameObject.scene);
      }

      IsDirty = false;
    }

    private void Export()
    {
      var items = _playlists
        .Where(p => p.originalItem != null)
        .Select(p => p.originalItem)
        .ToList();
      PlaylistExporter.Export(items);
    }

    private void Import()
    {
      if (_playlists == null)
      {
        EditorUtility.DisplayDialog("Import playlists", "Assign a YamaPlayer to the editor before importing.", "OK");
        return;
      }

      var imported = PlaylistExporter.Import();
      _playlists.AddRange(imported);

      if (imported.Count > 0)
      {
        IsDirty = true;
        GeneratePlaylistsView();
      }
    }

  }
}
