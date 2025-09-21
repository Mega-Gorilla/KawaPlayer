using System.Linq;
using UnityEngine;
using Yamadev.YamaStream.Script;

namespace Yamadev.YamaStream.Editor
{
    public class YamaPlayerScreenBuildProcess : IYamaPlayerBuildProcess
    {
        public int callbackOrder => -200;

        public void Process()
        {
            try
            {
                var screens = GameObject.FindObjectsOfType<YamaPlayerScreen>(true);
                foreach (var screen in screens)
                {
                    SetupScreen(screen);
                }

                AssignLTCGITexture();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error occurred during YamaPlayerScreenBuildProcess: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        private Controller TryFindController(YamaPlayerScreen screen)
        {
            if (screen.controller != null) return screen.controller;

            var parentPlayer = screen.GetComponentInParent<YamaPlayer>();
            if (parentPlayer != null)
            {
                return parentPlayer.GetComponentInChildren<Controller>();
            }

            var parentController = screen.GetComponentInParent<YamaPlayerController>();
            if (parentController != null && parentController.YamaPlayer != null)
            {
                return parentController.YamaPlayer.GetComponentInChildren<Controller>();
            }

            return null;
        }

        private void SetupScreen(YamaPlayerScreen screen)
        {
            if (screen == null)
            {
                Debug.LogWarning("YamaPlayerScreen is null, skipping setup.");
                return;
            }

            if (screen.controller == null)
            {
                var foundController = TryFindController(screen);
                if (foundController != null)
                {
                    screen.controller = foundController;
                }
                else
                {
                    Debug.LogWarning("YamaPlayerScreen's controller is null, skipping setup.");
                }
            }

            screen.controller.AddScreen(screen.Type, screen.Reference);
        }

        private void AssignLTCGITexture()
        {
#if USE_LTCGI
            try
            {
                var player = FindLTCGIActivedPlayer();
                var controller = player?.GetComponentInChildren<Controller>();
                if (controller != null)
                {
                    controller.AddScreen(ScreenType.Material, LTCGIUtility.CRT.material);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error occurred: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
#endif
        }

        private YamaPlayer FindLTCGIActivedPlayer()
        {
#if USE_LTCGI
            var players = GameObject.FindObjectsOfType<YamaPlayer>();
            var actived = players.Where(player => player.UseLTCGI);

            if (actived.Count() > 1)
            {
                Debug.LogWarning("More than one LTCGI controller is active, using the first one.");
            }
            return actived.FirstOrDefault();
#else
            return null;
#endif
        }
    }
}