using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace Yamadev.YamaStream.UI
{
  // Moves a track's title left while the row is pointed at, so that a title
  // longer than the row can be read whole (issue #352). At rest the title is
  // one line, cut at the edge of the list.
  //
  // It runs while its object is on: the row's button changes state through
  // the row's animator (TrackAction), whose Hover layer switches this object
  // on while the row is pointed at or pressed and off otherwise. A row with
  // an EventTrigger would know the same, but would also take the drags that
  // scroll the list.
  //
  // The title sits in a clip (_clip) from its own left edge, which is on only
  // while the title moves: a RectMask2D culls its graphics again every frame,
  // still or not (#325), so only the row pointed at has one on. It culls a
  // graphic by its rect, so the title's rect is made as wide as its text
  // while it moves.
  [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
  public class TitleMarquee : UdonSharpBehaviour
  {
    // In the title's parent's units a second: two to three characters.
    private const float Speed = 65f;
    // Seconds it waits before it moves, and at the end before it starts again.
    private const float StartWait = 1f;
    private const float EndWait = 1.5f;

    [SerializeField] private Text _title;
    [SerializeField] private RectMask2D _clip;

    private RectTransform _titleRect;
    private RectTransform _clipRect;
    private Vector2 _restPosition;
    private Vector2 _restSize;
    private bool _prepared;
    private bool _moving;
    private bool _loopQueued;
    private string _text;
    private float _distance;
    private float _offset;
    private float _waitUntil;

    private void OnEnable() => Begin();

    private void OnDisable() => Stop();

    // Remembers where the title rests: the first time it moves, as nothing
    // else moves or sizes it.
    private void Prepare()
    {
      if (_prepared) return;
      _prepared = true;
      _titleRect = _title.GetComponent<RectTransform>();
      _clipRect = _clip.GetComponent<RectTransform>();
      _restPosition = _titleRect.anchoredPosition;
      _restSize = _titleRect.sizeDelta;
    }

    private void Begin()
    {
      if (!Utilities.IsValid(_title) || !Utilities.IsValid(_clip)) return;
      Prepare();
      Rest();
      _text = _title.text;
      float scale = _titleRect.localScale.x;
      _distance = _title.preferredWidth * scale - _clipRect.rect.width;
      _moving = _distance > 0f;
      if (!_moving) return;
      _titleRect.sizeDelta = new Vector2(_title.preferredWidth, _restSize.y);
      _clip.enabled = true;
      _offset = 0f;
      _waitUntil = Time.time + StartWait;
      if (_loopQueued) return;
      _loopQueued = true;
      SendCustomEventDelayedFrames(nameof(_Step), 1);
    }

    private void Stop()
    {
      _moving = false;
      if (!_prepared) return;
      Rest();
    }

    private void Rest()
    {
      _titleRect.anchoredPosition = _restPosition;
      _titleRect.sizeDelta = _restSize;
      _clip.enabled = false;
    }

    // Runs each frame while the title moves, and stops itself once the row
    // is no longer pointed at.
    public void _Step()
    {
      _loopQueued = false;
      if (!_moving) return;
      // The list drew another track on this row (a scroll, a refresh).
      if (_title.text != _text)
      {
        Begin();
        return;
      }
      if (Time.time >= _waitUntil)
      {
        if (_offset >= _distance)
        {
          _offset = 0f;
          _waitUntil = Time.time + StartWait;
        }
        else
        {
          _offset = Mathf.Min(_offset + Speed * Time.deltaTime, _distance);
          if (_offset >= _distance) _waitUntil = Time.time + EndWait;
        }
        _titleRect.anchoredPosition = _restPosition - new Vector2(_offset, 0f);
      }
      _loopQueued = true;
      SendCustomEventDelayedFrames(nameof(_Step), 1);
    }
  }
}
