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
  // tablet (issue #108, D2). It has two screens (issue #181): a list of the
  // last pictures shown, with the URL field above it, and the picture full
  // size. TabletScreen syncs the list, the picture and which screen is up;
  // each player downloads the picture themselves, and only while the picture
  // is up, so a closed app takes nothing from the world's shared image rate
  // limit. A player whose download fails is told why and what to do next,
  // and can try again on their own when that may help (issue #178).
  //
  // A downloaded picture is copied, made smaller, into a RenderTexture and
  // let go of at once: one is up to 2048 x 2048 and, kept as it comes, can
  // take far more memory than a Quest can spare. The list's thumbnails are
  // made from that copy, and only for pictures this player has loaded. What
  // is drawn into a RenderTexture can be lost; the picture is then loaded
  // again, and a lost thumbnail goes back to the site's name.
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

    // How often to see whether a RenderTexture has lost what was drawn in it.
    private const float TextureCheckInterval = 0.5f;

    // The longest side a picture is kept at. A Quest has less memory to
    // spare; at most this halves a 2048 picture, which a plain copy can do
    // without shimmering.
#if UNITY_ANDROID || UNITY_IOS
    private const int PictureSize = 1024;
#else
    private const int PictureSize = 2048;
#endif
    // The box a thumbnail fits in, keeping the picture's shape.
    private const int ThumbnailWidth = 256;
    private const int ThumbnailHeight = 144;
    // One more than the list holds, so the picture just loaded always has
    // one to go to before the list takes it in.
    private const int ThumbnailCount = 6;
    // How long the back button stays after the picture is touched.
    private const float ControlsTime = 4f;

    [SerializeField] private TabletScreen _screen;

    [Header("List")]
    [SerializeField] private GameObject _listScreen;
    [SerializeField, RegisterEvent(nameof(VRCUrlInputField.onEndEdit), nameof(SubmitUrl))] private VRCUrlInputField _urlInput;
    [SerializeField] private Text _titleText;
    [SerializeField] private Text _hintText;
    [SerializeField] private Text _listEmptyText;
    // One per place in the list, newest first.
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(SelectImage0))] private Button _listButton0;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(SelectImage1))] private Button _listButton1;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(SelectImage2))] private Button _listButton2;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(SelectImage3))] private Button _listButton3;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(SelectImage4))] private Button _listButton4;
    [SerializeField] private RawImage[] _listPictures = new RawImage[0];
    // Shown instead of a picture this player has not loaded yet.
    [SerializeField] private GameObject[] _listPlaceholders = new GameObject[0];
    [SerializeField] private Text[] _listSites = new Text[0];

    [Header("Picture")]
    [SerializeField] private GameObject _viewScreen;
    // The whole picture area: touching it shows or hides the back button.
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(ToggleControls))] private Button _areaButton;
    [SerializeField] private GameObject _controls;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(BackToList))] private Button _backButton;
    [SerializeField] private Text _backButtonText;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(ToggleDetails))] private Button _detailsButton;
    [SerializeField, RegisterEvent(nameof(Button.onClick), nameof(Retry))] private Button _retryButton;
    // The frame, and the part inside it the picture and the messages sit in.
    // The whole screen turns with the tablet (TabletScreen, issue #183).
    [SerializeField] private RectTransform _area;
    [SerializeField] private RectTransform _content;
    [SerializeField] private RawImage _picture;
    [SerializeField] private Text _statusText;
    [SerializeField] private Text _detailsButtonText;
    [SerializeField] private Text _retryButtonText;
    [SerializeField] private Text _detailsText;

    private VRCImageDownloader _downloader;
    private TextureInfo _textureInfo;
    // The download asked for last. Any other still running is let go when it
    // finishes, even one for the same URL.
    private IVRCImageDownload _pending;
    // Set while DownloadImage runs: a URL it cannot fetch is reported from
    // inside the call, before the download is handed back.
    private bool _starting;
    private VRCUrl _requestedUrl = VRCUrl.Empty;
    // The download for what this player entered last: the picture joins the
    // list when this download succeeds, and no other one, not even a retry.
    private IVRCImageDownload _enteredDownload;
    private RenderTexture _view;
    private RenderTexture[] _thumbnails;
    private string[] _thumbnailUrls;
    private Button[] _listButtons;
    // What the status line says, kept as translation keys so a change of
    // language can say it again. An empty status key shows the picture.
    private string _statusKey = EmptyKey;
    private string _reasonKey = "";
    private string _details = "";
    private bool _detailsOpen;
    private bool _viewing;
    private bool _controlsOpen;
    private float _controlsUntil;
    private float _nextTextureCheck;

    private void OnEnable()
    {
      Layout();
      UpdateTranslation();
      ShowImageState();
    }

    // The downloader holds every picture it fetched, and the copies live on
    // the graphics card: let them go with the tablet, as VRChat's own image
    // loading example does.
    private void OnDestroy()
    {
      if (Utilities.IsValid(_downloader)) _downloader.Dispose();
      ReleaseView();
      if (_thumbnails == null) return;
      foreach (RenderTexture thumbnail in _thumbnails)
      {
        if (!Utilities.IsValid(thumbnail)) continue;
        thumbnail.Release();
        Destroy(thumbnail);
      }
    }

    private void Update()
    {
      if (Time.time < _nextTextureCheck) return;
      _nextTextureCheck = Time.time + TextureCheckInterval;
      CheckLostTextures();
    }

    // The screen turned between landscape and portrait: the frames have new
    // sizes, so fit the picture and the thumbnails to them again.
    public void OnLayoutChanged()
    {
      Layout();
      if (gameObject.activeInHierarchy) UpdateList();
    }

    public void SubmitUrl()
    {
      if (!Utilities.IsValid(_urlInput)) return;
      // Closing the keyboard with cancel raises this too: that is not a new
      // picture.
      if (_urlInput.wasCanceled) return;
      VRCUrl url = _urlInput.GetUrl();
      _urlInput.SetUrl(VRCUrl.Empty);
      if (!Utilities.IsValid(url) || string.IsNullOrEmpty(url.Get()) || !Utilities.IsValid(_screen)) return;
      _enteredDownload = null;
      _screen.SetImageUrl(url);
      // The picture up already: nothing is downloaded, so put it in the list
      // now. Otherwise wait for the download it started, or the one already
      // running for the same URL.
      if (IsShowing(url)) AddToList(url);
      else _enteredDownload = _pending;
    }

    public void SelectImage0() => SelectImage(0);

    public void SelectImage1() => SelectImage(1);

    public void SelectImage2() => SelectImage(2);

    public void SelectImage3() => SelectImage(3);

    public void SelectImage4() => SelectImage(4);

    private void SelectImage(int index)
    {
      if (!Utilities.IsValid(_screen)) return;
      VRCUrl[] list = _screen.ImageList;
      if (index < 0 || index >= list.Length || !Utilities.IsValid(list[index])) return;
      _screen.SetImageUrl(list[index]);
    }

    public void BackToList()
    {
      _controlsOpen = false;
      if (Utilities.IsValid(_screen)) _screen.ShowImageList();
    }

    // Shows what the tablet's state says: the list, or the picture. Opening
    // the app calls this, and TabletScreen whenever the state changes.
    public void ShowImageState()
    {
      if (!gameObject.activeInHierarchy || !Utilities.IsValid(_screen)) return;
      bool viewing = _screen.ImageViewing;
      // Coming to the picture, show the way back for a moment.
      if (viewing && !_viewing) OpenControls();
      _viewing = viewing;
      if (Utilities.IsValid(_listScreen)) _listScreen.SetActive(!viewing);
      if (Utilities.IsValid(_viewScreen)) _viewScreen.SetActive(viewing);
      CheckLostTextures();
      UpdateList();
      if (viewing) ShowUrl();
      UpdateControls();
    }

    // What was drawn into a RenderTexture can be lost, and the downloaded
    // picture is gone by then: load the picture again, and let a lost
    // thumbnail go, so its place shows the site's name until the picture is
    // loaded again.
    private void CheckLostTextures()
    {
      if (Utilities.IsValid(_view) && !_view.IsCreated())
      {
        ReleaseView();
        _requestedUrl = VRCUrl.Empty;
        if (_viewing && string.IsNullOrEmpty(_statusKey)) ShowUrl();
      }
      if (_thumbnails == null) return;
      bool lost = false;
      for (int i = 0; i < _thumbnails.Length; i++)
      {
        if (!Utilities.IsValid(_thumbnails[i]) || _thumbnails[i].IsCreated()) continue;
        _thumbnails[i].Release();
        Destroy(_thumbnails[i]);
        _thumbnails[i] = null;
        _thumbnailUrls[i] = "";
        lost = true;
      }
      if (lost) UpdateList();
    }

    // Loads what the tablet's URL points at. A download already running when
    // the app closes still reports back while it is closed, so opening it
    // again does not ask twice.
    private void ShowUrl()
    {
      VRCUrl url = _screen.ImageUrl;
      if (!Utilities.IsValid(url) || string.IsNullOrEmpty(url.Get())) return;
      if (url.Equals(_requestedUrl)) return;

      _requestedUrl = url;
      _pending = null;
      // Let the last copy go before the next picture comes. While a picture
      // is copied, it and its copy are held together for a moment.
      ReleaseView();
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

    private bool IsShowing(VRCUrl url) => string.IsNullOrEmpty(_statusKey) && url.Equals(_requestedUrl);

    // During DownloadImage, the only result that can come is the one it is
    // starting.
    private bool IsCurrent(IVRCImageDownload result) => _starting || result == _pending;

    // No mipmaps: the picture is only copied, and the copy makes its own.
    private TextureInfo GetTextureInfo()
    {
      if (_textureInfo == null)
      {
        _textureInfo = new TextureInfo();
        _textureInfo.GenerateMipMaps = false;
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
      VRCUrl url = result.Url;
      CopyPicture(result.Result);
      // The copy is all that is kept.
      result.Dispose();
      MakeThumbnail(url.Get());
      if (result == _enteredDownload)
      {
        _enteredDownload = null;
        AddToList(url);
      }
      SetStatus("", "", "");
      UpdateList();
      Layout();
    }

    public override void OnImageLoadError(IVRCImageDownload result)
    {
      if (!IsCurrent(result)) return;
      _pending = null;
      if (result == _enteredDownload) _enteredDownload = null;
      // Let the same URL be tried again.
      _requestedUrl = VRCUrl.Empty;
      SetStatus(FailedKey, GetReason(result.Error, result.ErrorMessage), BuildDetails(result));
    }

    // Only the player who entered a picture puts it in the list, once it has
    // loaded for them, and only while it is still the tablet's picture and
    // they still own the screen: a success that comes after someone else
    // changed the picture must not write over what they did. Having gone back
    // to the list since does not matter; the list stays up.
    private void AddToList(VRCUrl url)
    {
      if (!Utilities.IsValid(_screen) || !Networking.IsOwner(_screen.gameObject)) return;
      if (!url.Equals(_screen.ImageUrl)) return;
      _screen.AddToImageList(url);
    }

    // The picture, made no larger than PictureSize along its longest side.
    private void CopyPicture(Texture2D source)
    {
      ReleaseView();
      if (!Utilities.IsValid(source) || source.width <= 0 || source.height <= 0) return;
      float scale = Mathf.Min(1f, (float)PictureSize / Mathf.Max(source.width, source.height));
      int width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
      int height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
      _view = NewTexture(width, height);
      VRCGraphics.Blit(source, _view);
      if (Utilities.IsValid(_picture)) _picture.texture = _view;
    }

    // No depth buffer: on Quest, VRCGraphics.Blit draws only into a
    // RenderTexture without one (or with a shader that ignores depth).
    private RenderTexture NewTexture(int width, int height)
    {
      RenderTexture texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
      // Shown smaller than it is: mipmaps keep it from shimmering, and
      // clamping keeps one edge from bleeding into the other.
      texture.useMipMap = true;
      texture.autoGenerateMips = true;
      texture.filterMode = FilterMode.Trilinear;
      texture.wrapMode = TextureWrapMode.Clamp;
      texture.Create();
      return texture;
    }

    private void ReleaseView()
    {
      if (!Utilities.IsValid(_view)) return;
      if (Utilities.IsValid(_picture) && _picture.texture == _view) _picture.texture = null;
      _view.Release();
      Destroy(_view);
      _view = null;
    }

    // The picture just loaded, whole and with its own shape, so a wide logo
    // or a square icon still looks like itself. It is made from the smaller
    // copy, whose mipmaps keep the thumbnail from shimmering.
    private void MakeThumbnail(string url)
    {
      if (!Utilities.IsValid(_view)) return;
      if (_thumbnails == null)
      {
        _thumbnails = new RenderTexture[ThumbnailCount];
        _thumbnailUrls = new string[ThumbnailCount];
        for (int i = 0; i < ThumbnailCount; i++) _thumbnailUrls[i] = "";
      }
      int slot = FindThumbnail(url);
      if (slot < 0) slot = FindFreeThumbnail();
      if (slot < 0) return;

      float scale = Mathf.Min(1f, Mathf.Min((float)ThumbnailWidth / _view.width, (float)ThumbnailHeight / _view.height));
      int width = Mathf.Max(1, Mathf.RoundToInt(_view.width * scale));
      int height = Mathf.Max(1, Mathf.RoundToInt(_view.height * scale));
      RenderTexture thumbnail = _thumbnails[slot];
      if (Utilities.IsValid(thumbnail) && (thumbnail.width != width || thumbnail.height != height))
      {
        thumbnail.Release();
        Destroy(thumbnail);
        thumbnail = null;
      }
      if (!Utilities.IsValid(thumbnail))
      {
        thumbnail = NewTexture(width, height);
        _thumbnails[slot] = thumbnail;
      }
      VRCGraphics.Blit(_view, thumbnail);
      _thumbnailUrls[slot] = url;
    }

    private int FindThumbnail(string url)
    {
      if (_thumbnailUrls == null) return -1;
      for (int i = 0; i < _thumbnailUrls.Length; i++)
      {
        if (_thumbnailUrls[i] == url) return i;
      }
      return -1;
    }

    // A thumbnail no picture in the list uses.
    private int FindFreeThumbnail()
    {
      VRCUrl[] list = Utilities.IsValid(_screen) ? _screen.ImageList : new VRCUrl[0];
      for (int i = 0; i < _thumbnailUrls.Length; i++)
      {
        if (!IsListed(list, _thumbnailUrls[i])) return i;
      }
      return -1;
    }

    private bool IsListed(VRCUrl[] list, string url)
    {
      if (string.IsNullOrEmpty(url)) return false;
      foreach (VRCUrl item in list)
      {
        if (Utilities.IsValid(item) && item.Get() == url) return true;
      }
      return false;
    }

    // The list as the tablet has it. A picture this player has not loaded
    // shows its site's name in place of a thumbnail: nothing is downloaded
    // just for the list.
    private void UpdateList()
    {
      if (!Utilities.IsValid(_screen)) return;
      if (_listButtons == null) _listButtons = new Button[] { _listButton0, _listButton1, _listButton2, _listButton3, _listButton4 };
      VRCUrl[] list = _screen.ImageList;
      int shown = 0;
      for (int i = 0; i < _listButtons.Length; i++)
      {
        if (!Utilities.IsValid(_listButtons[i])) continue;
        bool used = i < list.Length && Utilities.IsValid(list[i]) && !string.IsNullOrEmpty(list[i].Get());
        _listButtons[i].gameObject.SetActive(used);
        if (!used) continue;
        shown++;
        string url = list[i].Get();
        int slot = FindThumbnail(url);
        RenderTexture thumbnail = slot >= 0 ? _thumbnails[slot] : null;
        if (i < _listPictures.Length && Utilities.IsValid(_listPictures[i]))
        {
          RawImage picture = _listPictures[i];
          picture.texture = thumbnail;
          picture.gameObject.SetActive(Utilities.IsValid(thumbnail));
          if (Utilities.IsValid(thumbnail)) FitInFrame(picture.rectTransform, thumbnail);
        }
        if (i < _listPlaceholders.Length && Utilities.IsValid(_listPlaceholders[i])) _listPlaceholders[i].SetActive(!Utilities.IsValid(thumbnail));
        if (i < _listSites.Length && Utilities.IsValid(_listSites[i])) _listSites[i].text = UrlUtils.GetHostFromUrl(url);
      }
      if (Utilities.IsValid(_listEmptyText)) _listEmptyText.gameObject.SetActive(shown == 0);
    }

    // A thumbnail as large as its frame allows, keeping its shape.
    private void FitInFrame(RectTransform picture, Texture texture)
    {
      RectTransform frame = (RectTransform)picture.parent;
      Vector2 box = frame.rect.size;
      float scale = Mathf.Min(box.x / texture.width, box.y / texture.height);
      picture.anchorMin = new Vector2(0.5f, 0.5f);
      picture.anchorMax = new Vector2(0.5f, 0.5f);
      picture.sizeDelta = new Vector2(texture.width * scale, texture.height * scale);
    }

    // The back button shows for a while after the picture is touched, and
    // stays while there is no picture to look at.
    public void ToggleControls()
    {
      if (_controlsOpen) _controlsOpen = false;
      else OpenControls();
      UpdateControls();
    }

    private void OpenControls()
    {
      _controlsOpen = true;
      _controlsUntil = Time.time + ControlsTime;
      SendCustomEventDelayedSeconds(nameof(_CloseControls), ControlsTime);
    }

    // Touches since the controls opened push the time out; only the last
    // one closes them.
    public void _CloseControls()
    {
      if (Time.time < _controlsUntil - 0.05f) return;
      _controlsOpen = false;
      UpdateControls();
    }

    private void UpdateControls()
    {
      if (!Utilities.IsValid(_controls)) return;
      _controls.SetActive(_controlsOpen || !string.IsNullOrEmpty(_statusKey));
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
      ShowImageState();
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
      UpdateControls();
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
        _screen.SetTranslatedText(_listEmptyText, "tablet.image.listEmpty");
        _screen.SetTranslatedText(_backButtonText, "tablet.image.back");
      }
      UpdateStatusView();
    }

    private string GetTranslation(string key) => Utilities.IsValid(_screen) ? _screen.GetTranslation(key) : string.Empty;

    // The picture as large as the frame allows, keeping its shape.
    private void Layout()
    {
      if (!Utilities.IsValid(_area) || !Utilities.IsValid(_content)) return;
      Vector2 box = _area.rect.size;
      _content.sizeDelta = box;
      _content.localEulerAngles = Vector3.zero;

      if (!Utilities.IsValid(_picture)) return;
      Texture texture = _picture.texture;
      if (!Utilities.IsValid(texture) || texture.width <= 0 || texture.height <= 0) return;
      float scale = Mathf.Min(box.x / texture.width, box.y / texture.height);
      _picture.rectTransform.sizeDelta = new Vector2(texture.width * scale, texture.height * scale);
    }
  }
}
