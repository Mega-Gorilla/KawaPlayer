using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;
using Yamadev.YamaStream.UI;

namespace Yamadev.YamaStream.Modules.PlaylistLoader
{
  // Answers the search page (issue #145): takes what was entered in the
  // search box, asks VHub, and hands the results back as rows. A row pressed
  // comes back with its key and its number, and is loaded the way a URL
  // typed into the URL field is (PlaylistLoaderUI.LoadPlaylist).
  //
  // The box holds searchFieldUrl, the address of VHub's search with nothing
  // after "q=", and the player types after it: a world cannot make a URL
  // from text, so the text has to arrive inside one. What was entered is
  // checked before anything is fetched, as a fetch is one of the one-in-five-
  // seconds string loads, and the server takes all after "q=" as the words.
  //
  // Wired to each SearchPage of the player's UIs by PlaylistLoaderBuildProcess,
  // which also sets searchFieldUrl from the loader's server.
  [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
  public class PlaylistSearch : UdonSharpBehaviour
  {
    // As long as the server takes; longer is refused there, so not sent.
    private const int MaxQueryLength = 100;

    [SerializeField] private PlaylistLoader _loader;
    [SerializeField] private PlaylistLoaderUI _loaderUi;

    // Contract with SearchPage: it reads searchFieldUrl for its box, writes
    // searchSource and searchInput before OnSearchSubmitted, and searchSource,
    // searchPickKey and searchPickNumber before OnSearchResultPicked.
    [HideInInspector] public VRCUrl searchFieldUrl;
    [HideInInspector] public SearchPage searchSource;
    [HideInInspector] public VRCUrl searchInput;
    [HideInInspector] public string searchPickKey;
    [HideInInspector] public int searchPickNumber;

    // One search at a time: the view that asked, what it asked with and for.
    private bool _searching;
    private SearchPage _pendingView;
    private VRCUrl _pendingUrl;
    private string _pendingQuery = string.Empty;

    public void OnSearchSubmitted()
    {
      var view = searchSource;
      if (!Utilities.IsValid(view) || !Utilities.IsValid(_loader) || !Utilities.IsValid(searchInput)) return;
      var ui = view.Ui;
      if (!Utilities.IsValid(ui)) return;

      string entered = searchInput.Get();
      string prefix = Utilities.IsValid(searchFieldUrl) ? searchFieldUrl.Get() : string.Empty;

      // A playlist's own URL pasted into the box loads that playlist.
      if (_loader.IsOwnPlaylistUrl(entered))
      {
        if (Utilities.IsValid(_loaderUi)) _loaderUi.LoadPlaylist(ui, searchInput, _loader.GetExpectedKey(searchInput), false);
        return;
      }

      // The URL was deleted or changed: nothing of ours to send. The box has
      // its URL back already (the view puts it back after every entry).
      if (string.IsNullOrEmpty(prefix) || !entered.StartsWith(prefix))
      {
        view.ShowStatus(ui.GetTranslation("module.playlistLoader.searchKeepUrl"));
        return;
      }

      string query = entered.Substring(prefix.Length).Trim();
      if (query.Length == 0)
      {
        view.ShowStatus(ui.GetTranslation("module.playlistLoader.searchEmpty"));
        return;
      }
      // VRChat sends nothing after a "#", and refuses to load a URL with a
      // "%" that is not an escape. Either would search for something else,
      // or for nothing, so neither is sent.
      if (query.Contains("#") || query.Contains("%"))
      {
        view.ShowStatus(ui.GetTranslation("module.playlistLoader.searchBadCharacter"));
        return;
      }
      if (query.Length > MaxQueryLength)
      {
        view.ShowStatus(ui.GetTranslation("module.playlistLoader.searchTooLong"));
        return;
      }
      if (_searching)
      {
        view.ShowStatus(ui.GetTranslation("module.playlistLoader.searchBusy"));
        return;
      }

      _searching = true;
      _pendingView = view;
      _pendingUrl = searchInput;
      _pendingQuery = query;
      view.ShowQuery(query);
      view.ShowStatus(ui.GetTranslation("module.playlistLoader.searchSearching"));
      VRCStringDownloader.LoadUrl(searchInput, (IUdonEventReceiver)this);
    }

    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
      if (!_searching || !Utilities.IsValid(_pendingUrl) || result.Url.Get() != _pendingUrl.Get()) return;
      var view = _pendingView;
      string query = _pendingQuery;
      EndSearch();
      if (!Utilities.IsValid(view) || !Utilities.IsValid(view.Ui)) return;
      var ui = view.Ui;

      if (!VRCJson.TryDeserializeFromJson(result.Result, out DataToken root)
          || root.TokenType != TokenType.DataDictionary
          || !root.DataDictionary.TryGetValue("items", out DataToken itemsToken)
          || itemsToken.TokenType != TokenType.DataList)
      {
        ShowNothing(view, ui.GetTranslation("module.playlistLoader.searchFailed"));
        return;
      }

      var items = itemsToken.DataList;
      int total = items.Count;
      var titles = new string[total];
      var details = new string[total];
      var counts = new string[total];
      var keys = new string[total];
      var numbers = new int[total];
      string tracksWord = ui.GetTranslation("label.tracks");
      int count = 0;
      for (int i = 0; i < total; i++)
      {
        if (items[i].TokenType != TokenType.DataDictionary) continue;
        var item = items[i].DataDictionary;
        string id = GetString(item, "id");
        int number = GetInt(item, "resolveIndex", -1);
        // A result this world cannot fetch (a number outside what was baked
        // in) or cannot tell apart (no id) is left out rather than shown and
        // refused when pressed.
        if (string.IsNullOrEmpty(id) || !Utilities.IsValid(_loader.GetResolveUrl(number))) continue;

        titles[count] = GetString(item, "name");
        // Under the name: who made it, and the track that matched when the
        // name itself does not hold the words (why else is it here?).
        string detail = GetString(item, "ownerName");
        string matched = GetString(item, "matchedTrack");
        if (!string.IsNullOrEmpty(matched)) detail = string.IsNullOrEmpty(detail) ? $"♪ {matched}" : $"{detail}  ♪ {matched}";
        details[count] = detail;
        int trackCount = GetInt(item, "trackCount", 0);
        counts[count] = trackCount > 0 ? $"{trackCount} {tracksWord}" : string.Empty;
        keys[count] = PlaylistUrlUtils.MakeSourceKey(_loader.PoolId, id);
        numbers[count] = number;
        count++;
      }

      bool truncated = root.DataDictionary.TryGetValue("truncated", out DataToken truncatedToken)
          && truncatedToken.TokenType == TokenType.Boolean && truncatedToken.Boolean;
      string status = count == 0
          ? ui.GetTranslation("module.playlistLoader.searchNoResults")
          : ui.GetTranslation(truncated ? "module.playlistLoader.searchResultsTruncated" : "module.playlistLoader.searchResults")
              .Replace("{0}", count.ToString());
      view.ShowQuery(query);
      view.ShowResults(titles, details, counts, keys, numbers, count);
      view.ShowStatus(status);
    }

    public override void OnStringLoadError(IVRCStringDownload result)
    {
      if (!_searching || !Utilities.IsValid(_pendingUrl) || result.Url.Get() != _pendingUrl.Get()) return;
      var view = _pendingView;
      EndSearch();
      // The server's own words stay in the log; the view gets ours.
      Debug.LogWarning($"[PlaylistSearch] Search failed: {result.ErrorCode} {result.Error}");
      if (!Utilities.IsValid(view) || !Utilities.IsValid(view.Ui)) return;
      ShowNothing(view, view.Ui.GetTranslation(result.ErrorCode == 429
          ? "module.playlistLoader.searchRateLimited"
          : "module.playlistLoader.searchFailed"));
    }

    private void EndSearch()
    {
      _searching = false;
      _pendingView = null;
      _pendingUrl = null;
      _pendingQuery = string.Empty;
    }

    // A search that brought nothing clears the rows of the one before: they
    // would otherwise stand under words they do not answer.
    private void ShowNothing(SearchPage view, string status)
    {
      view.ShowResults(new string[0], new string[0], new string[0], new string[0], new int[0], 0);
      view.ShowStatus(status);
    }

    // A result's row was pressed. Its number fetches the playlist and its key
    // says which playlist that has to be: a number is used again, and if it
    // has come to stand for another playlist, nothing is loaded.
    public void OnSearchResultPicked()
    {
      var view = searchSource;
      if (!Utilities.IsValid(view) || !Utilities.IsValid(_loader) || !Utilities.IsValid(_loaderUi)) return;
      var url = _loader.GetResolveUrl(searchPickNumber);
      if (!Utilities.IsValid(url) || string.IsNullOrEmpty(searchPickKey)) return;
      _loaderUi.LoadPlaylist(view.Ui, url, searchPickKey, false);
    }

    private string GetString(DataDictionary dict, string key)
    {
      if (dict.TryGetValue(key, out DataToken token) && token.TokenType == TokenType.String) return token.String;
      return string.Empty;
    }

    private int GetInt(DataDictionary dict, string key, int defaultValue)
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
