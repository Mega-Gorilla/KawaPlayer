using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Yamadev.YamaStream.Editor.Tests
{
  // Pins the order in which a rebuilt playlist list picks its selection. The
  // list, the highlight and the detail pane all follow whatever this returns,
  // so a wrong answer here shows up as a table pointing at one playlist while
  // the right hand pane shows another (issue #106).
  public class PlaylistSelectionTests
  {
    private readonly List<GameObject> _created = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
      foreach (var go in _created) Object.DestroyImmediate(go);
      _created.Clear();
    }

    private PlaylistItem NewItem(string name)
    {
      var go = new GameObject(name);
      _created.Add(go);
      return go.AddComponent<PlaylistItem>();
    }

    private PlaylistData Saved(string name)
    {
      return new PlaylistData { name = name, originalItem = NewItem(name) };
    }

    // Never saved, so there is no PlaylistItem to match it by.
    private PlaylistData Unsaved(string name)
    {
      return new PlaylistData { name = name, originalItem = null };
    }

    [Test]
    public void NoPlaylists_SelectsNothing()
    {
      Assert.AreEqual(-1, PlaylistEditorWindow.ResolvePlaylistSelection(
        new List<PlaylistData>(), null, -1));
      Assert.AreEqual(-1, PlaylistEditorWindow.ResolvePlaylistSelection(null, null, 2));
    }

    [Test]
    public void NoPreviousSelection_SelectsTheFirstRow()
    {
      var playlists = new List<PlaylistData> { Saved("a"), Saved("b"), Saved("c") };

      Assert.AreEqual(0, PlaylistEditorWindow.ResolvePlaylistSelection(playlists, null, -1));
    }

    [Test]
    public void SavedPlaylist_IsFoundByItsOriginalItem()
    {
      var playlists = new List<PlaylistData> { Saved("a"), Saved("b"), Saved("c") };

      Assert.AreEqual(1, PlaylistEditorWindow.ResolvePlaylistSelection(
        playlists, playlists[1].originalItem, 0));
    }

    // The point of matching on originalItem rather than the index: a revert
    // after a reorder must land on the same playlist, not the same row.
    [Test]
    public void ReorderedList_FollowsThePlaylistAndNotTheIndex()
    {
      var first = Saved("a");
      var second = Saved("b");
      var third = Saved("c");
      var reordered = new List<PlaylistData> { third, second, first };

      Assert.AreEqual(2, PlaylistEditorWindow.ResolvePlaylistSelection(
        reordered, first.originalItem, 0));
    }

    [Test]
    public void UnsavedPlaylist_FallsBackToItsOldPosition()
    {
      var playlists = new List<PlaylistData> { Saved("a"), Saved("b"), Saved("c") };

      Assert.AreEqual(1, PlaylistEditorWindow.ResolvePlaylistSelection(
        playlists, Unsaved("gone").originalItem, 1));
    }

    // The playlist was there a moment ago but is not in the rebuilt list, so
    // the old position stands in for it.
    [Test]
    public void MissingPlaylist_FallsBackToItsOldPosition()
    {
      var playlists = new List<PlaylistData> { Saved("a"), Saved("b"), Saved("c") };

      Assert.AreEqual(2, PlaylistEditorWindow.ResolvePlaylistSelection(
        playlists, NewItem("not in the list"), 2));
    }

    // Reverting away unsaved additions leaves a shorter list than the index
    // was taken from.
    [TestCase(3, 2)]
    [TestCase(9, 2)]
    public void FallbackPastTheEnd_IsClampedToTheLastRow(int fallbackIndex, int expected)
    {
      var playlists = new List<PlaylistData> { Saved("a"), Saved("b"), Saved("c") };

      Assert.AreEqual(expected, PlaylistEditorWindow.ResolvePlaylistSelection(
        playlists, null, fallbackIndex));
    }

    [Test]
    public void NullEntriesInTheList_AreSkippedRatherThanThrowing()
    {
      var playlists = new List<PlaylistData> { null, Saved("b") };

      Assert.AreEqual(1, PlaylistEditorWindow.ResolvePlaylistSelection(
        playlists, playlists[1].originalItem, -1));
    }
  }
}
