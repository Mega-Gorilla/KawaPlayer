using UdonSharp;
using VRC.SDKBase;

namespace Yamadev.YamaStream
{
    public partial class Controller
    {
        [UdonSynced] VideoPlayerType _targetPlayer;
        [UdonSynced] string _title = string.Empty;
        [UdonSynced] VRCUrl _url = VRCUrl.Empty;
        [UdonSynced] string _originalUrl = string.Empty;
        Track _track;
        UdonEvent _resolveTrack;

        public Track Track
        {
            get
            {
                if (!Utilities.IsValid(_track))
                    _track = Track.Empty();
                return _track;
            }
            set
            {
                _track = value;
                foreach (Listener listener in EventListeners) listener.OnTrackUpdated();
            }
        }

        public UdonEvent ResolveTrack
        {
            get
            {
                if (!Utilities.IsValid(_resolveTrack))
                    _resolveTrack = UdonEvent.New(this, nameof(Resolve));
                return _resolveTrack;
            }
            set => _resolveTrack = value;
        }

        public void PlayTrack(Track track)
        {
            if (!track.GetUrl().IsValidUrl())
            {
                PrintError($"URL {track.GetUrl()} is not valid");
                return;
            }

            if (State == PlayerState.Playing && (Networking.IsOwner(gameObject) || _isLocal))
            {
                Stop();
            }

            _state = _slideMode ? (byte)PlayerState.Paused : (byte)PlayerState.Playing;
            LoadTrack(track);
        }

        private void LoadTrack(Track track, bool isReload = false)
        {
            if (!Utilities.IsValid(Handler))
            {
                PrintError("Handler is not valid");
                return;
            }

            _reloading = isReload;
            Handler.Stop();

            var currentPlayerType = track.GetPlayerType();
            if (!isReload && PlayerType != currentPlayerType)
            {
                var currentStatus = _state;
                PlayerType = track.GetPlayerType();
                _state = currentStatus;
            }
            Track = track;
            ResolveTrack.Invoke();

            if (Networking.IsOwner(gameObject) && !_isLocal && !isReload)
            {
                RequestSerialization();
            }
            foreach (Listener listener in EventListeners) listener.OnUrlChanged();
            PrintLog($"Load url: {track.GetUrl()}.");
        }

        public void Resolve() => Handler.LoadUrl(Track.GetVRCUrl());

        public void Reload()
        {
            if (!Stopped && !IsLoading) LoadTrack(Track, true);
        }

        public override void OnPreSerialization()
        {
            _targetPlayer = Track.GetPlayerType();
            _title = Track.GetTitle();
            _url = Track.GetVRCUrl();
            _originalUrl = Track.GetOriginalUrl();
        }
    }
}