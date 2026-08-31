using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace JellyfinForRayNeo
{
    public sealed class PlayerView : IDisposable
    {
        private readonly GameObject _root;
        private readonly VideoPlayer _videoPlayer;
        private readonly AudioSource _audioSource;
        private readonly RenderTexture _renderTexture;
        private readonly Text _title;
        private readonly Text _status;
        private readonly Text _timeLabel;
        private readonly Text _playPauseLabel;
        private readonly Slider _progress;
        private JellyfinPlaybackPlan _plan;
        private bool _prepared;
        private bool _preparing;
        private bool _updatingSlider;
        private bool _disposed;

        public event Action BackRequested;
        public event Action<bool> PauseStateChanged;
        public event Action<string> PlaybackFailed;
        public event Action PlaybackCompleted;

        public PlayerView(Transform parent)
        {
            Image rootImage = UiFactory.CreatePanel("Player Screen", parent, Color.black);
            UiFactory.Stretch(rootImage.rectTransform);
            _root = rootImage.gameObject;

            RawImage videoSurface = UiFactory.CreateRect("Video Surface", rootImage.transform).gameObject.AddComponent<RawImage>();
            videoSurface.color = Color.white;
            videoSurface.raycastTarget = false;
            UiFactory.SetRect(videoSurface.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(1660f, 934f));

            _renderTexture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32)
            {
                name = "Jellyfin Video Surface",
                useMipMap = false,
                autoGenerateMips = false,
                antiAliasing = 1
            };
            _renderTexture.Create();
            videoSurface.texture = _renderTexture;

            _videoPlayer = rootImage.gameObject.AddComponent<VideoPlayer>();
            _audioSource = rootImage.gameObject.AddComponent<AudioSource>();
            _videoPlayer.playOnAwake = false;
            _videoPlayer.waitForFirstFrame = true;
            _videoPlayer.skipOnDrop = true;
            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            _videoPlayer.targetTexture = _renderTexture;
            _videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            _videoPlayer.SetTargetAudioSource(0, _audioSource);
            _videoPlayer.errorReceived += HandleRuntimeError;
            _videoPlayer.loopPointReached += HandleCompleted;

            Image topBar = UiFactory.CreatePanel("Top Controls", rootImage.transform, new Color(0.01f, 0.012f, 0.02f, 0.82f));
            UiFactory.SetRect(topBar.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 104f));

            Button back = UiFactory.CreateButton("Back", topBar.transform, "返回", UiTheme.SurfaceRaised, UiTheme.TextPrimary, 23);
            UiFactory.SetRect(back.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(128f, 58f));
            back.onClick.AddListener(() => BackRequested?.Invoke());

            _title = UiFactory.CreateText("Now Playing", topBar.transform, string.Empty, 30, UiTheme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.SetRect(_title.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(195f, 0f), new Vector2(-260f, 62f));

            _status = UiFactory.CreateText("Status", rootImage.transform, string.Empty, 30, UiTheme.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.SetRect(_status.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 90f));

            Image controlBar = UiFactory.CreatePanel("Playback Controls", rootImage.transform, new Color(0.01f, 0.012f, 0.02f, 0.88f));
            UiFactory.SetRect(controlBar.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 126f));

            Button playPause = UiFactory.CreateButton("Play Pause", controlBar.transform, "暂停", UiTheme.Accent, UiTheme.TextPrimary, 23);
            UiFactory.SetRect(playPause.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(44f, 0f), new Vector2(130f, 58f));
            _playPauseLabel = playPause.GetComponentInChildren<Text>();
            playPause.onClick.AddListener(TogglePlayPause);

            Image sliderBackground = UiFactory.CreatePanel("Progress", controlBar.transform, UiTheme.ProgressTrack);
            UiFactory.SetRect(sliderBackground.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(205f, 0f), new Vector2(-435f, 18f));
            _progress = sliderBackground.gameObject.AddComponent<Slider>();
            _progress.minValue = 0f;
            _progress.maxValue = 1f;
            _progress.direction = Slider.Direction.LeftToRight;

            Image fill = UiFactory.CreatePanel("Fill", sliderBackground.transform, UiTheme.AccentBright);
            fill.raycastTarget = false;
            UiFactory.Stretch(fill.rectTransform);
            _progress.fillRect = fill.rectTransform;

            Image handle = UiFactory.CreatePanel("Handle", sliderBackground.transform, UiTheme.TextPrimary);
            UiFactory.SetRect(handle.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 28f));
            _progress.handleRect = handle.rectTransform;
            _progress.targetGraphic = handle;
            _progress.onValueChanged.AddListener(SeekToNormalized);
            sliderBackground.gameObject.AddComponent<FocusScale>().FocusedScale = 1.03f;

            _timeLabel = UiFactory.CreateText("Time", controlBar.transform, "00:00 / 00:00", 22, UiTheme.TextSecondary, TextAnchor.MiddleRight);
            UiFactory.SetRect(_timeLabel.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-42f, 0f), new Vector2(360f, 52f));

            _root.SetActive(false);
        }

        public bool IsVisible
        {
            get { return _root.activeSelf; }
        }

        public bool IsPaused
        {
            get { return _prepared && !_videoPlayer.isPlaying; }
        }

        public long CurrentPositionTicks
        {
            get
            {
                if (_prepared && !double.IsNaN(_videoPlayer.time) && _videoPlayer.time >= 0d)
                {
                    return (long)(_videoPlayer.time * AppConstants.TicksPerSecond);
                }
                return _plan != null ? _plan.StartPositionTicks : 0L;
            }
        }

        public async Task PrepareAndPlayAsync(JellyfinPlaybackPlan plan, CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PlayerView));
            }

            StopVideoOnly();
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            _title.text = plan.Item != null ? plan.Item.Name : "正在播放";
            _status.text = "正在准备视频…";
            _playPauseLabel.text = "暂停";
            _videoPlayer.url = plan.Url;
            _preparing = true;

            TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();
            VideoPlayer.EventHandler preparedHandler = null;
            VideoPlayer.ErrorEventHandler errorHandler = null;
            preparedHandler = source => completion.TrySetResult(true);
            errorHandler = (source, message) => completion.TrySetException(new InvalidOperationException(message));
            _videoPlayer.prepareCompleted += preparedHandler;
            _videoPlayer.errorReceived += errorHandler;
            CancellationTokenRegistration registration = cancellationToken.Register(() =>
            {
                _videoPlayer.Stop();
                completion.TrySetCanceled(cancellationToken);
            });

            try
            {
                _videoPlayer.Prepare();
                await completion.Task;
                cancellationToken.ThrowIfCancellationRequested();
                _prepared = true;
                if (plan.StartPositionTicks > 0L && _videoPlayer.canSetTime)
                {
                    _videoPlayer.time = plan.StartPositionTicks / (double)AppConstants.TicksPerSecond;
                }
                _status.text = string.Empty;
                _videoPlayer.Play();
            }
            finally
            {
                registration.Dispose();
                _videoPlayer.prepareCompleted -= preparedHandler;
                _videoPlayer.errorReceived -= errorHandler;
                _preparing = false;
            }
        }

        public void Stop()
        {
            StopVideoOnly();
            _plan = null;
            _root.SetActive(false);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _videoPlayer.errorReceived -= HandleRuntimeError;
            _videoPlayer.loopPointReached -= HandleCompleted;
            StopVideoOnly();
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                UnityEngine.Object.Destroy(_renderTexture);
            }
        }

        public void Update()
        {
            if (!_root.activeSelf || !_prepared)
            {
                return;
            }

            double duration = _videoPlayer.length;
            double position = Math.Max(0d, _videoPlayer.time);
            if (duration > 0.01d && !double.IsInfinity(duration) && !double.IsNaN(duration))
            {
                _updatingSlider = true;
                _progress.SetValueWithoutNotify(Mathf.Clamp01((float)(position / duration)));
                _updatingSlider = false;
                _timeLabel.text = FormatTime(position) + " / " + FormatTime(duration);
            }
            else
            {
                _timeLabel.text = FormatTime(position);
            }
        }

        private void TogglePlayPause()
        {
            if (!_prepared)
            {
                return;
            }

            if (_videoPlayer.isPlaying)
            {
                _videoPlayer.Pause();
                _playPauseLabel.text = "播放";
                PauseStateChanged?.Invoke(true);
            }
            else
            {
                _videoPlayer.Play();
                _playPauseLabel.text = "暂停";
                PauseStateChanged?.Invoke(false);
            }
        }

        private void SeekToNormalized(float value)
        {
            if (_updatingSlider || !_prepared || !_videoPlayer.canSetTime)
            {
                return;
            }
            if (_videoPlayer.length > 0.01d && !double.IsInfinity(_videoPlayer.length))
            {
                _videoPlayer.time = value * _videoPlayer.length;
            }
        }

        private void StopVideoOnly()
        {
            if (_videoPlayer != null)
            {
                _videoPlayer.Stop();
                _videoPlayer.url = string.Empty;
            }
            _prepared = false;
            _preparing = false;
            _status.text = string.Empty;
            _timeLabel.text = "00:00 / 00:00";
            _progress.SetValueWithoutNotify(0f);
        }

        private void HandleRuntimeError(VideoPlayer source, string message)
        {
            if (_preparing)
            {
                return;
            }
            _status.text = "播放失败";
            PlaybackFailed?.Invoke(string.IsNullOrWhiteSpace(message) ? "视频播放器发生未知错误。" : message);
        }

        private void HandleCompleted(VideoPlayer source)
        {
            PlaybackCompleted?.Invoke();
        }

        private static string FormatTime(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0d)
            {
                seconds = 0d;
            }
            TimeSpan time = TimeSpan.FromSeconds(seconds);
            return time.TotalHours >= 1d
                ? string.Format("{0:00}:{1:00}:{2:00}", (int)time.TotalHours, time.Minutes, time.Seconds)
                : string.Format("{0:00}:{1:00}", time.Minutes, time.Seconds);
        }
    }
}
