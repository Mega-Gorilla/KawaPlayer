using System;
using System.Linq;
using UnityEngine;

using Object = UnityEngine.Object;

namespace Yamadev.YamaStream.Editor
{
  // A player starts on the first of its video players, and a track whose own
  // player is missing, or refuses its URL, plays on the first that takes it;
  // an error during playback goes to each player's fallback instead, whatever
  // the order. AVPro comes first (issue #166): Unity Video Player cannot play
  // HLS and gets YouTube at 360p (issue #139). The inspector used to move the
  // player chosen there to the front and no longer offers that, so a scene
  // saved with another order could not be put back by hand: every player is
  // sorted here to AVPro, Unity, the image viewer, then any other in the
  // order it had. Runs at scene build and on play-mode entry.
  internal class VideoPlayerOrderBuildProcess : IYamaPlayerBuildProcess
  {
    private static readonly VideoPlayerType[] Order =
    {
      VideoPlayerType.AVProVideoPlayer,
      VideoPlayerType.UnityVideoPlayer,
      VideoPlayerType.ImageViewer,
    };

    public int callbackOrder => -2000;

    public void Process()
    {
      var controllers = Object.FindObjectsByType<Controller>(FindObjectsInactive.Include, FindObjectsSortMode.None);
      foreach (var controller in controllers)
      {
        if (controller == null) continue;
        var handlers = controller.GetProgramVariable("_videoPlayerHandlers") as PlayerHandler[];
        if (handlers == null || handlers.Length < 2) continue;
        // OrderBy keeps the order of players that rank the same.
        var sorted = handlers.OrderBy(Rank).ToArray();
        if (sorted.SequenceEqual(handlers)) continue;
        controller.SetProgramVariable("_videoPlayerHandlers", sorted);
      }
    }

    private static int Rank(PlayerHandler handler)
    {
      if (handler == null) return Order.Length;
      int rank = Array.IndexOf(Order, handler.Type);
      return rank < 0 ? Order.Length : rank;
    }
  }
}
