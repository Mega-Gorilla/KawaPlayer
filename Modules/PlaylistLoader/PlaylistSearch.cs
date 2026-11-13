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
  // comes back with its key and its number: the playlist is fetched and its
  // tracks shown, and the page's add button then adds what was fetched, the
  // way the list's "+" adds a URL (PlaylistLoaderUI.LoadPlaylist): without
  // playing it, and saying only what went wrong.
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
    // searchSource and searchInput before OnSearchSubmitted, searchSource,
    // searchPickKey and searchPickNumber before OnSearchResultPicked and
    // OnSearchResultAddPicked, and
    // searchSource and searchPickKey before OnSearchAddPressed and
    // OnSearchPlaylistsChanged.
    [HideInInspector] public VRCUrl searchFieldUrl;
    [HideInInspector] public SearchPage searchSource;
    [HideInInspector] public VRCUrl searchInput;
    [HideInInspector] public string searchPickKey;
    [HideInInspector] public int searchPickNumber;

    // One fetch at a time, a search or a playlist to show: the view that
    // asked, what it asked with and for.
    private bool _searching;
    private bool _opening;
    // The playlist being fetched is to be added as it comes, not shown.
    private bool _addWhenFetched;
    private string _pendingKey = string.Empty;
    // The playlist last fetched to be shown: its response, kept so that
    // adding it fetches nothing, what fetched it, and which playlist it is.
    private string _shownJson;
    private VRCUrl _shownUrl;
    private string _shownKey = string.Empty;
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
        if (Utilities.IsValid(_loaderUi)) _loaderUi.LoadPlaylist(ui, searchInput, null, _loader.GetExpectedKey(searchInput), false);
        return;
      }

      // The URL was deleted or changed: nothing of ours to send. The box has
      // its URL back already (the view puts it back after every entry).
      if (string.IsNullOrEmpty(prefix) || !entered.StartsWith(prefix))
      {
        view.ShowNote(ui.GetTranslation("module.playlistLoader.searchKeepUrl"));
        return;
      }

      string query = entered.Substring(prefix.Length).Trim();
      if (query.Length == 0)
      {
        view.ShowNote(ui.GetTranslation("module.playlistLoader.searchEmpty"));
        return;
      }
      // VRChat sends nothing after a "#", and refuses to load a URL with a
      // "%" that is not an escape. Either would search for something else,
      // or for nothing, so neither is sent.
      if (query.Contains("#") || query.Contains("%"))
      {
        view.ShowNote(ui.GetTranslation("module.playlistLoader.searchBadCharacter"));
        return;
      }
      if (query.Length > MaxQueryLength)
      {
        view.ShowNote(ui.GetTranslation("module.playlistLoader.searchTooLong"));
        return;
      }
      if (_searching || _opening)
      {
        view.ShowNote(ui.GetTranslation("module.playlistLoader.searchBusy"));
        return;
      }

      _searching = true;
      _pendingView = view;
      _pendingUrl = searchInput;
      _pendingQuery = query;
      view.ShowQuery(query);
      view.ShowLoading(ui.GetTranslation("module.playlistLoader.searchSearching"));
      VRCStringDownloader.LoadUrl(searchInput, (IUdonEventReceiver)this);
    }

    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
      if (!Utilities.IsValid(_pendingUrl) || result.Url.Get() != _pendingUrl.Get()) return;
      if (_opening)
      {
        OnPlaylistFetched(result.Result);
        return;
      }
      if (!_searching) return;
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
      if (!Utilities.IsValid(_pendingUrl) || result.Url.Get() != _pendingUrl.Get()) return;
      if (_opening)
      {
        var asked = _pendingView;
        EndSearch();
        Debug.LogWarning($"[PlaylistSearch] Fetching a result failed: {result.ErrorCode} {result.Error}");
        NotOpened(asked, result.ErrorCode == 429 ? "module.playlistLoader.searchRateLimited" : "module.playlistLoader.searchOpenFailed");
        return;
      }
      if (!_searching) return;
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
      _opening = false;
      _addWhenFetched = false;
      _pendingView = null;
      _pendingUrl = null;
      _pendingQuery = string.Empty;
      _pendingKey = string.Empty;
    }

    // A search that brought nothing clears the rows of the one before: they
    // would otherwise stand under words they do not answer.
    private void ShowNothing(SearchPage view, string status)
    {
      view.ShowResults(new string[0], new string[0], new string[0], new string[0], new int[0], 0);
      view.ShowStatus(status);
    }

    // A result's row was pressed. Its number fetches the playlist, to be
    // shown before it is added, and its key says which playlist that has to
    // be: a number is used again, and if it has come to stand for another
    // playlist, nothing is shown.
    public void OnSearchResultPicked() => Fetch(false);

    // A result's own add button: the playlist is fetched the same way and
    // added as it comes, without being shown.
    public void OnSearchResultAddPicked() => Fetch(true);

    private void Fetch(bool addWhenFetched)
    {
      var view = searchSource;
      if (!Utilities.IsValid(view) || !Utilities.IsValid(_loader) || !Utilities.IsValid(view.Ui)) return;
      var ui = view.Ui;
      var url = _loader.GetResolveUrl(searchPickNumber);
      if (!Utilities.IsValid(url) || string.IsNullOrEmpty(searchPickKey)) return;
      if (_searching || _opening)
      {
        view.ShowNote(ui.GetTranslation("module.playlistLoader.searchBusy"));
        return;
      }

      // Nothing to fetch for one that is in the list already.
      if (addWhenFetched && _loader.HasPlaylist(searchPickKey))
      {
        view.ShowNote(ui.GetTranslation("module.playlistLoader.searchAlreadyAdded"));
        return;
      }

      _opening = true;
      _addWhenFetched = addWhenFetched;
      _pendingView = view;
      _pendingUrl = url;
      _pendingKey = searchPickKey;
      view.ShowLoading(ui.GetTranslation("module.playlistLoader.searchOpening"));
      VRCStringDownloader.LoadUrl(url, (IUdonEventReceiver)this);
    }

    // The playlist a row stands for came: read as the loader would read it,
    // and shown if it is the one the row meant. The response is kept, so
    // that adding it fetches nothing.
    private void OnPlaylistFetched(string json)
    {
      var view = _pendingView;
      var url = _pendingUrl;
      string key = _pendingKey;
      bool addNow = _addWhenFetched;
      EndSearch();
      if (!Utilities.IsValid(view) || !Utilities.IsValid(view.Ui)) return;
      var ui = view.Ui;

      if (!_loader.ReadResponse(json, out string playlistName, out string sourceKey, out string[] titles) || sourceKey != key)
      {
        NotOpened(view, "module.playlistLoader.searchOpenFailed");
        return;
      }

      _shownJson = json;
      _shownUrl = url;
      _shownKey = key;
      string name = string.IsNullOrEmpty(playlistName) ? PlaylistLoader.UnnamedPlaylistName : playlistName;
      if (addNow)
      {
        // The results come back, and the line under the box says it is
        // added once it is: at once, unless a question is asked first or
        // the load is refused, which the dialog then says.
        view.BackToResults();
        if (Utilities.IsValid(_loaderUi)) _loaderUi.LoadPlaylist(ui, url, json, key, true);
        if (_loader.HasPlaylist(key))
          view.ShowNote(ui.GetTranslation("module.playlistLoader.searchAddedNamed").Replace("{0}", name));
        return;
      }
      bool added = _loader.HasPlaylist(key);
      view.ShowPlaylist(name,
          $"{titles.Length} {ui.GetTranslation("label.tracks")}", titles, titles.Length, key, added);
      view.ShowNote(ui.GetTranslation(added ? "module.playlistLoader.searchAlreadyAdded" : "module.playlistLoader.searchAddHint"));
    }

    // A playlist that did not come: the results show again, with why.
    private void NotOpened(SearchPage view, string messageKey)
    {
      if (!Utilities.IsValid(view) || !Utilities.IsValid(view.Ui)) return;
      view.BackToResults();
      view.ShowNote(view.Ui.GetTranslation(messageKey));
    }

    // The add button of the playlist that shows. What was fetched to show it
    // is what is added, through the same permission, busy and replacement
    // checks as a URL from the list's "+", and like it without playing; a
    // failure is said in the dialog, and the button says when it is added.
    public void OnSearchAddPressed()
    {
      var view = searchSource;
      if (!Utilities.IsValid(view) || !Utilities.IsValid(_loader) || !Utilities.IsValid(_loaderUi) || !Utilities.IsValid(view.Ui)) return;
      // Another UI of this player has opened another playlist since: what
      // was fetched for this one is gone.
      if (string.IsNullOrEmpty(searchPickKey) || searchPickKey != _shownKey || string.IsNullOrEmpty(_shownJson))
      {
        NotOpened(view, "module.playlistLoader.searchOpenFailed");
        return;
      }
      _loaderUi.LoadPlaylist(view.Ui, _shownUrl, _shownJson, _shownKey, true);
      // Added at once unless a question was asked first, in which case
      // OnSearchPlaylistsChanged says so when it is.
      OnSearchPlaylistsChanged();
    }

    // The world's playlists changed while a view shows a playlist: whether
    // that one is among them now.
    public void OnSearchPlaylistsChanged()
    {
      var view = searchSource;
      if (!Utilities.IsValid(view) || !Utilities.IsValid(_loader) || !Utilities.IsValid(view.Ui) || string.IsNullOrEmpty(searchPickKey)) return;
      bool added = _loader.HasPlaylist(searchPickKey);
      if (added == view.Added) return;
      view.SetAdded(added);
      view.ShowNote(view.Ui.GetTranslation(added ? "module.playlistLoader.searchAdded" : "module.playlistLoader.searchAddHint"));
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
