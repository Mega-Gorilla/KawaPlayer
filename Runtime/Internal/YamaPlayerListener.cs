using UnityEngine;
using VRC.SDK3.Components.Video;

namespace Yamadev.YamaStream
{
  public abstract class YamaPlayerListener : YamaPlayerBehaviour
  {
    #region Core Events

    public virtual void AfterVideoReady() { }
    public virtual void AfterVideoStarted() { }
    public virtual void AfterVideoErrorOccurred(VideoError videoError) { }
    public virtual void AfterVideoLooped() { }
    public virtual void AfterVideoPlayed() { }
    public virtual void AfterVideoPaused() { }
    public virtual void AfterVideoEnded() { }
    public virtual void AfterVideoStopped() { }
    public virtual void AfterVideoReloaded() { }

    #endregion

    #region Playback Events

    public virtual void AfterPlayerHandlerChanged(VideoPlayerType playerType) { }
    public virtual void AfterTimeChanged(float time) { }
    public virtual void AfterLoopChanged(bool loop) { }
    public virtual void AfterShufflePlayChanged(bool shufflePlay) { }
    public virtual void AfterSpeedChanged(float speed) { }
    public virtual void AfterRepeatChanged(ulong repeat) { }
    public virtual void AfterLocalDelayChanged(float localDelay) { }

    #endregion

    #region Screen Events

    public virtual void AfterTextureUpdated(Texture texture) { }
    public virtual void AfterMirrorFlipChanged(bool mirrorFlip) { }
    public virtual void AfterBrightnessChanged(float brightness) { }
    public virtual void AfterMaxResolutionChanged(int maxResolution) { }

    #endregion

    #region Audio Events

    public virtual void AfterVolumeChanged(float volume) { }
    public virtual void AfterMuteChanged(bool mute) { }

    #endregion

    #region Other Events

    public virtual void AfterTrackUpdated() { }
    public virtual void AfterQueueUpdated() { }
    public virtual void AfterHistoryUpdated() { }
    public virtual void AfterPlaylistsUpdated() { }
    public virtual void AfterTrackSynced() { }
    public virtual void AfterTrackLoaded() { }
    public virtual void AfterOwnerChanged() { }
    public virtual void AfterVideoRetry() { }

    #endregion

    #region User Events

    public virtual void BeforeUserChangePlayerHandler() { }
    public virtual void BeforeUserPlayTrack() { }
    public virtual void BeforeUserPlayVideo() { }
    public virtual void BeforeUserPauseVideo() { }
    public virtual void BeforeUserStopVideo() { }
    public virtual void BeforeUserSetTime() { }
    public virtual void BeforeUserBackward() { }
    public virtual void BeforeUserForward() { }
    public virtual void BeforeUserReloadVideo() { }
    public virtual void BeforeUserChangeLoop() { }
    public virtual void BeforeUserChangeShufflePlay() { }
    public virtual void BeforeUserChangeSpeed() { }
    public virtual void BeforeUserChangeRepeat() { }
    public virtual void BeforeUserChangeLocalDelay() { }
    public virtual void BeforeUserChangeMirrorFlip() { }
    public virtual void BeforeUserChangeBrightness() { }
    public virtual void BeforeUserChangeMaxResolution() { }
    public virtual void BeforeUserChangeVolume() { }
    public virtual void BeforeUserChangeMute() { }
    public virtual void BeforeUserChangeLanguage() { }
    public virtual void BeforeUserAddTrackToQueue() { }
    public virtual void BeforeUserRemoveTrackFromQueue() { }
    public virtual void BeforeUserMoveTrackUp() { }
    public virtual void BeforeUserMoveTrackDown() { }

    #endregion
  }
}
