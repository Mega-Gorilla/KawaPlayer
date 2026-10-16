using UdonSharp;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace Yamadev.YamaStream.Modules.GlobalReload
{
  // Has every player reload its own video, as if each had pressed reload:
  // for a live event, where a stream is not placed by the clock and players
  // fall behind, each by their own amount (issue #236). The buttons a world
  // creator places send through it, and a player without it has none
  // (issue #248). Only the press is sent, and nothing synced changes. It
  // syncs nothing, but is Manual: a behaviour that does not sync sends no
  // network events.
  [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
  public class GlobalReload : YamaPlayerModule
  {
    // Whether a reload for everyone would do anything: the video is shared as
    // playing or paused. Each player then judges for itself, as for its own
    // reload, so a load of theirs still under way is left to finish.
    public bool CanSend => Utilities.IsValid(_controller) && _controller.SyncedState != PlayerState.Idle;

    // A local player has no one to send it to.
    public void Send()
    {
      if (!CanSend) return;
      if (_controller.IsLocal)
      {
        _controller.Reload();
        return;
      }
      SendCustomNetworkEvent(NetworkEventTarget.All, nameof(OnGlobalReload));
    }

    // Run on every player by Send. Its name has no leading underscore, as
    // Udon lets no network event call such a method.
    public void OnGlobalReload()
    {
      if (Utilities.IsValid(_controller)) _controller.Reload();
    }
  }
}
