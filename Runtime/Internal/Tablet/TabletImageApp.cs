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
  // closed app takes nothing from the world's shared image rate limit.
  [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
  public class TabletImageApp : YamaPlayerBehaviour
  {
    private const string EmptyKey = "tablet.image.empty";
    private const string LoadingKey = "tablet.image.loading";
    private const string FailedKey = "tablet.image.failed";

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
    [SerializeField] private Text _titleText;
    [SerializeField] private Text _hintText;
    // The fixed frame, and the part inside it that turns with the tablet.
    [SerializeField] private RectTransform _area;
    [SerializeField] private RectTransform _content;
    [SerializeField] private RawImage _picture;
    [SerializeField] private Text _statusText;
    [SerializeField] private Text _detailsButtonText;
    [SerializeField] private Text _detailsText;

    private VRCImageDownloader _downloader;
    private TextureInfo _textureInfo;
    private IVRCImageDownload _shown;
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

    // A download still running when the app closes may never report back,
    // so ask again when it opens.
    private void OnDisable()
    {
      if (_statusKey == LoadingKey) _requestedUrl = VRCUrl.Empty;
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
    // download.
    public void ShowUrl()
    {
      if (!gameObject.activeInHierarchy || !Utilities.IsValid(_screen)) return;
      VRCUrl url = _screen.ImageUrl;
      if (!Utilities.IsValid(url) || string.IsNullOrEmpty(url.Get())) return;
      if (url.Equals(_requestedUrl)) return;

      _requestedUrl = url;
      if (!Utilities.IsValid(_downloader)) _downloader = new VRCImageDownloader();
      _downloader.DownloadImage(url, null, (IUdonEventReceiver)this, GetTextureInfo());
      SetStatus(LoadingKey, "", "");
    }

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
      if (!result.Url.Equals(_requestedUrl))
      {
        result.Dispose();
        return;
      }
      // Only one picture is ever kept: each can take up to 16 MB.
      if (Utilities.IsValid(_shown) && _shown != result) _shown.Dispose();
      _shown = result;
      _picture.texture = result.Result;
      SetStatus("", "", "");
      Layout();
    }

    public override void OnImageLoadError(IVRCImageDownload result)
    {
      if (!result.Url.Equals(_requestedUrl)) return;

      // Nothing is shown for this URL now. Free the last picture, and let the
      // same URL be tried again.
      if (Utilities.IsValid(_shown))
      {
        _shown.Dispose();
        _shown = null;
      }
      _picture.texture = null;
      _requestedUrl = VRCUrl.Empty;

      string reason;
      string error;
      switch (result.Error)
      {
        case VRCImageDownloadError.AccessDenied:
          // Turned away by the allow list: the domain is not trusted and the
          // player has not allowed untrusted URLs.
          reason = "tablet.image.error.untrusted";
          error = "AccessDenied";
          break;
        case VRCImageDownloadError.InvalidURL:
          reason = "tablet.image.error.invalidUrl";
          error = "InvalidURL";
          break;
        case VRCImageDownloadError.InvalidImage:
          reason = "tablet.image.error.invalidImage";
          error = "InvalidImage";
          break;
        case VRCImageDownloadError.DownloadError:
          reason = "tablet.image.error.download";
          error = "DownloadError";
          break;
        case VRCImageDownloadError.TooManyRequests:
          reason = "tablet.image.error.busy";
          error = "TooManyRequests";
          break;
        default:
          reason = "";
          error = "Unknown";
          break;
      }
      SetStatus(FailedKey, reason, BuildDetails(error, result));
    }

    // For the developer a player sends a screenshot to: what VRChat reported,
    // left untranslated.
    private string BuildDetails(string error, IVRCImageDownload result)
    {
      string platform;
#if UNITY_ANDROID
      platform = "Android";
#elif UNITY_IOS
      platform = "iOS";
#else
      platform = "PC";
#endif
      return "Error: " + error
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
      SetTranslatedText(_titleText, "tablet.app.image");
      SetTranslatedText(_hintText, "tablet.image.inputHint");
      UpdateStatusView();
    }

    private string GetTranslation(string key) => Utilities.IsValid(_screen) ? _screen.GetTranslation(key) : string.Empty;

    // A missing key leaves the text the prefab was saved with.
    private void SetTranslatedText(Text text, string key)
    {
      if (!Utilities.IsValid(text)) return;
      string value = GetTranslation(key);
      if (!string.IsNullOrEmpty(value)) text.text = value;
    }

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
