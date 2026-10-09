using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDK3.Image;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace Yamadev.YamaStream.Tablet
{
  // The image app: the picture at a URL, shown to everyone looking at the
  // tablet (issue #108, D2). TabletScreen syncs the URL; each player
  // downloads the picture themselves, and only while the app is open, so a
  // closed app takes nothing from the world's shared image rate limit. A
  // player whose download fails is told why and what to do next, and can
  // try again on their own when that may help (issue #178).
  [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
  public class TabletImageApp : YamaPlayerBehaviour
  {
    private const string EmptyKey = "tablet.image.empty";
    private const string LoadingKey = "tablet.image.loading";
    private const string FailedKey = "tablet.image.failed";

    // Why a download failed, each telling the player what to do next.
    private const string UntrustedKey = "tablet.image.error.untrusted";
    private const string InvalidUrlKey = "tablet.image.error.invalidUrl";
    private const string InvalidImageKey = "tablet.image.error.invalidImage";
    private const string RedirectKey = "tablet.image.error.redirect";
    private const string RefusedKey = "tablet.image.error.refused";
    private const string NotFoundKey = "tablet.image.error.notFound";
    private const string ServerKey = "tablet.image.error.server";
    private const string DownloadKey = "tablet.image.error.download";
    private const string BusyKey = "tablet.image.error.busy";
    private const string UnknownKey = "tablet.image.error.unknown";

    private const float OrientationCheckInterval = 0.2f;
    // Degrees a second the picture turns when the tablet does.
    private const float TurnSpeed = 540f;
    // How far past the last quarter turn the tablet must go before the
    // picture follows, so it does not flip back and forth around 45 degrees.
    private const float TurnThreshold = 55f;
    // Lying flatter than this, which way is up says nothing.
    private const float FlatLimit = 0.8f;

    [SerializeField] private TabletScreen _screen;
    [SerializeField, RegisterEvent(nameof(VRCUrlInputField.onEndEdit), nameof(SubmitUrl))] private VRCUrlInputField _urlInput;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(ToggleDetails))] private Button _detailsButton;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(Retry))] private Button _retryButton;
    [SerializeField] private Text _titleText;
    [SerializeField] private Text _hintText;
    // The fixed frame, and the part inside it that turns with the tablet.
    [SerializeField] private RectTransform _area;
    [SerializeField] private RectTransform _content;
    [SerializeField] private RawImage _picture;
    [SerializeField] private Text _statusText;
    [SerializeField] private Text _detailsButtonText;
    [SerializeField] private Text _retryButtonText;
    [SerializeField] private Text _detailsText;

    private VRCImageDownloader _downloader;
    private TextureInfo _textureInfo;
    private IVRCImageDownload _shown;
    // The download asked for last. Any other still running is let go when it
    // finishes, even one for the same URL.
    private IVRCImageDownload _pending;
    // Set while DownloadImage runs: a URL it cannot fetch is reported from
    // inside the call, before the download is handed back.
    private bool _starting;
    private VRCUrl _requestedUrl = VRCUrl.Empty;
    // What the status line says, kept as translation keys so a change of
    // language can say it again. An empty status key shows the picture.
    private string _statusKey = EmptyKey;
    private string _reasonKey = "";
    private string _details = "";
    private bool _detailsOpen;
    private float _targetAngle;
    private float _shownAngle;
    private float _nextOrientationCheck;

    private void OnEnable()
    {
      CheckOrientation(true);
      UpdateTranslation();
      ShowUrl();
    }

    // The downloader holds every picture it fetched: let them go with the
    // tablet, as VRChat's own image loading example does.
    private void OnDestroy()
    {
      if (Utilities.IsValid(_downloader)) _downloader.Dispose();
    }

    private void Update()
    {
      if (Time.time >= _nextOrientationCheck)
      {
        _nextOrientationCheck = Time.time + OrientationCheckInterval;
        CheckOrientation(false);
      }
      if (_shownAngle != _targetAngle)
      {
        _shownAngle = Mathf.MoveTowardsAngle(_shownAngle, _targetAngle, TurnSpeed * Time.deltaTime);
        Layout();
      }
    }

    public void SubmitUrl()
    {
      if (!Utilities.IsValid(_urlInput)) return;
      // Closing the keyboard with cancel raises this too: that is not a new
      // picture.
      if (_urlInput.wasCanceled) return;
      VRCUrl url = _urlInput.GetUrl();
      _urlInput.SetUrl(VRCUrl.Empty);
      if (!Utilities.IsValid(url) || string.IsNullOrEmpty(url.Get())) return;
      if (Utilities.IsValid(_screen)) _screen.SetImageUrl(url);
    }

    // Shows what the tablet's URL points at. TabletScreen calls this when the
    // URL changes; opening the app calls it too, since a closed app does not
    // download. A download already running when the app closes still reports
    // back while it is closed, so opening it again does not ask twice.
    public void ShowUrl()
    {
      if (!gameObject.activeInHierarchy || !Utilities.IsValid(_screen)) return;
      VRCUrl url = _screen.ImageUrl;
      if (!Utilities.IsValid(url) || string.IsNullOrEmpty(url.Get())) return;
      if (url.Equals(_requestedUrl)) return;

      _requestedUrl = url;
      _pending = null;
      // Before the download starts, so an error reported while it starts is
      // not covered over.
      SetStatus(LoadingKey, "", "");
      if (!Utilities.IsValid(_downloader)) _downloader = new VRCImageDownloader();
      _starting = true;
      IVRCImageDownload download = _downloader.DownloadImage(url, null, (IUdonEventReceiver)this, GetTextureInfo());
      _starting = false;
      // Unless it has failed already, this is the download to wait for.
      if (_statusKey == LoadingKey) _pending = download;
    }

    // During DownloadImage, the only result that can come is the one it is
    // starting.
    private bool IsCurrent(IVRCImageDownload result) => _starting || result == _pending;

    // Shown small on a UI: mipmaps keep it from shimmering, and clamping
    // keeps one edge from bleeding into the other.
    private TextureInfo GetTextureInfo()
    {
      if (_textureInfo == null)
      {
        _textureInfo = new TextureInfo();
        _textureInfo.GenerateMipMaps = true;
        _textureInfo.WrapModeU = TextureWrapMode.Clamp;
        _textureInfo.WrapModeV = TextureWrapMode.Clamp;
      }
      return _textureInfo;
    }

    public override void OnImageLoadSuccess(IVRCImageDownload result)
    {
      // A picture asked for before the current one: let it go.
      if (!IsCurrent(result))
      {
        result.Dispose();
        return;
      }
      _pending = null;
      // Only one picture is ever kept: at 2048 x 2048, one takes about 21 MB
      // with its mipmaps.
      if (Utilities.IsValid(_shown)) _shown.Dispose();
      _shown = result;
      _picture.texture = result.Result;
      SetStatus("", "", "");
      Layout();
    }

    public override void OnImageLoadError(IVRCImageDownload result)
    {
      if (!IsCurrent(result)) return;
      _pending = null;

      // Nothing is shown for this URL now. Free the last picture, and let the
      // same URL be tried again.
      if (Utilities.IsValid(_shown))
      {
        _shown.Dispose();
        _shown = null;
      }
      _picture.texture = null;
      _requestedUrl = VRCUrl.Empty;

      SetStatus(FailedKey, GetReason(result.Error, result.ErrorMessage), BuildDetails(result));
    }

    // What went wrong, as what the player can do about it. VRChat's message
    // adds what its error leaves out: a download error carries the site's
    // HTTP status, and the message names an untrusted domain or a redirect.
    private string GetReason(VRCImageDownloadError error, string message)
    {
      string lower = string.IsNullOrEmpty(message) ? "" : message.ToLower();
      // Turned away by the allow list: the domain is not trusted and the
      // player has not allowed untrusted URLs.
      if (error == VRCImageDownloadError.AccessDenied || lower.Contains("untrusted")) return UntrustedKey;
      // Image loading never follows a redirect.
      if (lower.Contains("redirect")) return RedirectKey;
      switch (error)
      {
        case VRCImageDownloadError.InvalidURL:
          return InvalidUrlKey;
        case VRCImageDownloadError.InvalidImage:
          return InvalidImageKey;
        case VRCImageDownloadError.TooManyRequests:
          return BusyKey;
        case VRCImageDownloadError.DownloadError:
          int status = GetHttpStatus(message);
          // Many sites turn away whatever is not a browser, so a picture a
          // browser shows can still be refused here.
          if (status == 401 || status == 403) return RefusedKey;
          if (status == 404 || status == 410) return NotFoundKey;
          if (status == 429 || (status >= 500 && status < 600)) return ServerKey;
          return DownloadKey;
        default:
          return UnknownKey;
      }
    }

    // The status in a message such as "HTTP/1.1 403 Forbidden", or 0.
    private int GetHttpStatus(string message)
    {
      if (string.IsNullOrEmpty(message)) return 0;
      int at = message.IndexOf("HTTP/");
      if (at < 0) return 0;
      int space = message.IndexOf(' ', at);
      if (space < 0 || space + 4 > message.Length) return 0;
      int status;
      return int.TryParse(message.Substring(space + 1, 3), out status) ? status : 0;
    }

    // Whether the same URL may load when tried again: once the player allows
    // untrusted URLs, or once a busy or unreachable site is back. A wrong URL,
    // something that is not a picture, or a site that refuses stays as it is.
    private bool CanRetry(string reasonKey)
    {
      return reasonKey == UntrustedKey || reasonKey == ServerKey || reasonKey == DownloadKey
        || reasonKey == BusyKey || reasonKey == UnknownKey;
    }

    // Loads the tablet's URL again for this player only: whether a picture
    // loads depends on each player's own settings.
    public void Retry()
    {
      if (_statusKey != FailedKey) return;
      ShowUrl();
    }

    // For the developer a player sends a screenshot to: what VRChat reported,
    // left untranslated.
    private string BuildDetails(IVRCImageDownload result)
    {
      string platform;
#if UNITY_ANDROID
      platform = "Android";
#elif UNITY_IOS
      platform = "iOS";
#else
      platform = "PC";
#endif
      return "Error: " + result.Error.ToString()
        + "\nMessage: " + result.ErrorMessage
        + "\nURL: " + result.Url.Get()
        + "\nPlatform: " + platform
        + "\nTime: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public void ToggleDetails()
    {
      _detailsOpen = !_detailsOpen;
      UpdateStatusView();
    }

    private void SetStatus(string statusKey, string reasonKey, string details)
    {
      _statusKey = statusKey;
      _reasonKey = reasonKey;
      _details = details;
      _detailsOpen = false;
      UpdateStatusView();
    }

    private void UpdateStatusView()
    {
      bool showingPicture = string.IsNullOrEmpty(_statusKey);
      if (Utilities.IsValid(_picture)) _picture.gameObject.SetActive(showingPicture && Utilities.IsValid(_picture.texture));
      if (Utilities.IsValid(_statusText))
      {
        _statusText.gameObject.SetActive(!showingPicture);
        string text = showingPicture ? "" : GetTranslation(_statusKey);
        if (!string.IsNullOrEmpty(_reasonKey)) text += "\n" + GetTranslation(_reasonKey);
        _statusText.text = text;
      }
      bool hasDetails = !showingPicture && !string.IsNullOrEmpty(_details);
      if (Utilities.IsValid(_retryButton)) _retryButton.gameObject.SetActive(_statusKey == FailedKey && CanRetry(_reasonKey));
      if (Utilities.IsValid(_retryButtonText)) _retryButtonText.text = GetTranslation("tablet.image.retry");
      if (Utilities.IsValid(_detailsButton)) _detailsButton.gameObject.SetActive(hasDetails);
      if (Utilities.IsValid(_detailsButtonText)) _detailsButtonText.text = GetTranslation(_detailsOpen ? "tablet.image.hideDetails" : "tablet.image.showDetails");
      if (Utilities.IsValid(_detailsText))
      {
        _detailsText.gameObject.SetActive(hasDetails && _detailsOpen);
        _detailsText.text = _details;
      }
    }

    public void UpdateTranslation()
    {
      if (Utilities.IsValid(_screen))
      {
        _screen.SetTranslatedText(_titleText, "tablet.app.image");
        _screen.SetTranslatedText(_hintText, "tablet.image.inputHint");
      }
      UpdateStatusView();
    }

    private string GetTranslation(string key) => Utilities.IsValid(_screen) ? _screen.GetTranslation(key) : string.Empty;

    // The quarter turn that keeps the picture upright: on the screen, from
    // the tablet's up to the world's.
    private void CheckOrientation(bool immediate)
    {
      Vector3 normal = transform.forward;
      float facing = Vector3.Dot(normal, Vector3.up);
      if (Mathf.Abs(facing) <= FlatLimit)
      {
        Vector3 up = Vector3.up - normal * facing;
        float angle = Vector3.SignedAngle(transform.up, up, normal);
        if (immediate || Mathf.Abs(Mathf.DeltaAngle(_targetAngle, angle)) >= TurnThreshold)
        {
          _targetAngle = Mathf.Round(angle / 90f) * 90f;
        }
      }
      if (immediate)
      {
        _shownAngle = _targetAngle;
        Layout();
      }
    }

    // Turned a quarter, the picture lies along the frame's other side and is
    // fitted to that instead; in between, the box blends from one to the
    // other as it turns.
    private void Layout()
    {
      if (!Utilities.IsValid(_area) || !Utilities.IsValid(_content)) return;
      Vector2 area = _area.rect.size;
      float turned = Mathf.Abs(Mathf.Sin(_shownAngle * Mathf.Deg2Rad));
      Vector2 box = Vector2.Lerp(area, new Vector2(area.y, area.x), turned);
      _content.sizeDelta = box;
      _content.localEulerAngles = new Vector3(0f, 0f, _shownAngle);

      if (!Utilities.IsValid(_picture)) return;
      Texture texture = _picture.texture;
      if (!Utilities.IsValid(texture) || texture.width <= 0 || texture.height <= 0) return;
      float scale = Mathf.Min(box.x / texture.width, box.y / texture.height);
      _picture.rectTransform.sizeDelta = new Vector2(texture.width * scale, texture.height * scale);
    }
  }
}
