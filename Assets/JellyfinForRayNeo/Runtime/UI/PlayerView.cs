using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace JellyfinForRayNeo
{
    public sealed class PlayerView : IDisposable
    {
        private readonly GameObject _root;
        private readonly RawImage _videoSurface;
        private readonly IPlaybackEngine _unityEngine;
        private readonly IPlaybackEngine _libVlcHardwareEngine;
        private readonly IPlaybackEngine _softwareEngine;
        private readonly Text _title;
        private readonly Text _status;
        private readonly Text _modeLabel;
        private readonly Text _timeLabel;
        private readonly Text _playPauseLabel;
        private readonly Text _audioLabel;
        private readonly Text _subtitleButtonLabel;
        private readonly Slider _progress;
        private readonly GameObject _trackPanel;
        private readonly GameObject _subtitleRoot;
        private readonly Text _subtitleText;
        private IPlaybackEngine _activeEngine;
        private JellyfinPlaybackPlan _plan;
        private SubtitleTrack _subtitleTrack;
        private long _lastPositionTicks;
        private bool _updatingSlider;
        private bool _disposed;

        public event Action BackRequested;
        public event Action<bool> PauseStateChanged;
        public event Action<string> PlaybackFailed;
        public event Action PlaybackCompleted;
        public event Action<int?, int?> TrackSelectionRequested;

        public PlayerView(Transform parent)
        {
            Image rootImage = UiFactory.CreatePanel("Player Screen", parent, Color.black);
            UiFactory.Stretch(rootImage.rectTransform);
            _root = rootImage.gameObject;

            _videoSurface = UiFactory.CreateRect("Video Surface", rootImage.transform)
                .gameObject.AddComponent<RawImage>();
            _videoSurface.color = Color.white;
            _videoSurface.raycastTarget = false;
            UiFactory.SetRect(
                _videoSurface.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 18f),
                new Vector2(1660f, 934f));

            _unityEngine = new UnityVideoPlaybackEngine(rootImage.gameObject);
            _libVlcHardwareEngine = new LibVlcPlaybackEngine(false);
            _softwareEngine = new LibVlcPlaybackEngine(true);
            _unityEngine.Failed += message => HandleEngineFailure(_unityEngine, message);
            _libVlcHardwareEngine.Failed += message =>
                HandleEngineFailure(_libVlcHardwareEngine, message);
            _softwareEngine.Failed += message => HandleEngineFailure(_softwareEngine, message);
            _unityEngine.Completed += () => HandleEngineCompleted(_unityEngine);
            _libVlcHardwareEngine.Completed += () =>
                HandleEngineCompleted(_libVlcHardwareEngine);
            _softwareEngine.Completed += () => HandleEngineCompleted(_softwareEngine);

            Image topBar = UiFactory.CreatePanel(
                "Top Controls",
                rootImage.transform,
                new Color(0.01f, 0.012f, 0.02f, 0.84f));
            UiFactory.SetRect(
                topBar.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 104f));

            Button back = UiFactory.CreateButton(
                "Back",
                topBar.transform,
                "返回",
                UiTheme.SurfaceRaised,
                UiTheme.TextPrimary,
                23);
            UiFactory.SetRect(
                back.GetComponent<RectTransform>(),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(40f, 0f),
                new Vector2(128f, 58f));
            back.onClick.AddListener(() => BackRequested?.Invoke());

            _title = UiFactory.CreateText(
                "Now Playing",
                topBar.transform,
                string.Empty,
                30,
                UiTheme.TextPrimary,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            UiFactory.SetRect(
                _title.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(195f, 0f),
                new Vector2(-760f, 62f));

            _modeLabel = UiFactory.CreateText(
                "Decode Mode",
                topBar.transform,
                string.Empty,
                20,
                UiTheme.AccentBright,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            UiFactory.SetRect(
                _modeLabel.rectTransform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-474f, 0f),
                new Vector2(190f, 50f));

            Button audio = UiFactory.CreateButton(
                "Audio Tracks",
                topBar.transform,
                "音轨",
                UiTheme.SurfaceRaised,
                UiTheme.TextPrimary,
                20);
            UiFactory.SetRect(
                audio.GetComponent<RectTransform>(),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-276f, 0f),
                new Vector2(184f, 58f));
            _audioLabel = audio.GetComponentInChildren<Text>();
            audio.onClick.AddListener(() => ShowTrackMenu(true));

            Button subtitles = UiFactory.CreateButton(
                "Subtitle Tracks",
                topBar.transform,
                "字幕",
                UiTheme.SurfaceRaised,
                UiTheme.TextPrimary,
                20);
            UiFactory.SetRect(
                subtitles.GetComponent<RectTransform>(),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-78f, 0f),
                new Vector2(184f, 58f));
            _subtitleButtonLabel = subtitles.GetComponentInChildren<Text>();
            subtitles.onClick.AddListener(() => ShowTrackMenu(false));

            _status = UiFactory.CreateText(
                "Status",
                rootImage.transform,
                string.Empty,
                30,
                UiTheme.TextPrimary,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            UiFactory.SetRect(
                _status.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(1100f, 90f));

            Image subtitleBackdrop = UiFactory.CreateRoundedPanel(
                "Subtitle Overlay",
                rootImage.transform,
                new Color(0f, 0f, 0f, 0.76f));
            UiFactory.SetRect(
                subtitleBackdrop.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 164f),
                new Vector2(1480f, 126f));
            subtitleBackdrop.raycastTarget = false;
            _subtitleRoot = subtitleBackdrop.gameObject;
            _subtitleText = UiFactory.CreateText(
                "Subtitle Text",
                subtitleBackdrop.transform,
                string.Empty,
                34,
                UiTheme.TextPrimary,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            _subtitleText.verticalOverflow = VerticalWrapMode.Overflow;
            UiFactory.Stretch(_subtitleText.rectTransform, 34f, 34f, 16f, 16f);
            _subtitleRoot.SetActive(false);

            Image controlBar = UiFactory.CreatePanel(
                "Playback Controls",
                rootImage.transform,
                new Color(0.01f, 0.012f, 0.02f, 0.9f));
            UiFactory.SetRect(
                controlBar.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                Vector2.zero,
                new Vector2(0f, 126f));

            Button playPause = UiFactory.CreateButton(
                "Play Pause",
                controlBar.transform,
                "暂停",
                UiTheme.Accent,
                UiTheme.TextPrimary,
                23);
            UiFactory.SetRect(
                playPause.GetComponent<RectTransform>(),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(44f, 0f),
                new Vector2(130f, 58f));
            _playPauseLabel = playPause.GetComponentInChildren<Text>();
            playPause.onClick.AddListener(TogglePlayPause);

            Image sliderBackground = UiFactory.CreatePanel(
                "Progress",
                controlBar.transform,
                UiTheme.ProgressTrack);
            UiFactory.SetRect(
                sliderBackground.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(205f, 0f),
                new Vector2(-435f, 18f));
            _progress = sliderBackground.gameObject.AddComponent<Slider>();
            _progress.minValue = 0f;
            _progress.maxValue = 1f;
            _progress.direction = Slider.Direction.LeftToRight;

            Image fill = UiFactory.CreatePanel(
                "Fill",
                sliderBackground.transform,
                UiTheme.AccentBright);
            fill.raycastTarget = false;
            UiFactory.Stretch(fill.rectTransform);
            _progress.fillRect = fill.rectTransform;

            Image handle = UiFactory.CreatePanel(
                "Handle",
                sliderBackground.transform,
                UiTheme.TextPrimary);
            UiFactory.SetRect(
                handle.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(28f, 28f));
            _progress.handleRect = handle.rectTransform;
            _progress.targetGraphic = handle;
            _progress.onValueChanged.AddListener(SeekToNormalized);
            sliderBackground.gameObject.AddComponent<FocusScale>().FocusedScale = 1.03f;

            _timeLabel = UiFactory.CreateText(
                "Time",
                controlBar.transform,
                "00:00 / 00:00",
                22,
                UiTheme.TextSecondary,
                TextAnchor.MiddleRight);
            UiFactory.SetRect(
                _timeLabel.rectTransform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-42f, 0f),
                new Vector2(360f, 52f));

            Image trackPanel = UiFactory.CreateRoundedPanel(
                "Track Menu",
                rootImage.transform,
                new Color(0.055f, 0.061f, 0.086f, 0.985f));
            _trackPanel = trackPanel.gameObject;
            _trackPanel.SetActive(false);
            _root.SetActive(false);
        }

        public bool IsVisible
        {
            get { return _root.activeSelf; }
        }

        public bool IsPaused
        {
            get { return _activeEngine != null && _activeEngine.IsPaused; }
        }

        public long CurrentPositionTicks
        {
            get
            {
                if (_activeEngine != null && _activeEngine.IsPrepared)
                {
                    return Math.Max(
                        _lastPositionTicks,
                        (long)(_activeEngine.PositionSeconds * AppConstants.TicksPerSecond));
                }
                return _lastPositionTicks;
            }
        }

        public async Task PrepareAndPlayAsync(
            JellyfinPlaybackPlan plan,
            CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PlayerView));
            }

            StopVideoOnly();
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            _lastPositionTicks = plan.StartPositionTicks;
            switch (plan.Tier)
            {
                case PlaybackTier.HardwareLibVlcDirect:
                    _activeEngine = _libVlcHardwareEngine;
                    break;
                case PlaybackTier.SoftwareDirect:
                    _activeEngine = _softwareEngine;
                    break;
                default:
                    _activeEngine = _unityEngine;
                    break;
            }
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            _title.text = plan.Item != null ? plan.Item.Name : "正在播放";
            _modeLabel.text = plan.TierLabel;
            _status.text = PreparationMessage(plan.Tier);
            _playPauseLabel.text = "暂停";
            _videoSurface.texture = _activeEngine.OutputTexture;
            _videoSurface.uvRect = _activeEngine.FlipOutputVertically
                ? new Rect(0f, 1f, 1f, -1f)
                : new Rect(0f, 0f, 1f, 1f);
            UpdateTrackLabels();

            try
            {
                await _activeEngine.PrepareAndPlayAsync(plan, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                _status.text = string.Empty;
            }
            catch
            {
                _activeEngine.Stop();
                throw;
            }
        }

        public void SetSubtitleTrack(SubtitleTrack track)
        {
            _subtitleTrack = track;
            _subtitleText.text = string.Empty;
            _subtitleRoot.SetActive(false);
        }

        public void Stop()
        {
            StopVideoOnly();
            _plan = null;
            _activeEngine = null;
            _trackPanel.SetActive(false);
            _root.SetActive(false);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            StopVideoOnly();
            _unityEngine.Dispose();
            _libVlcHardwareEngine.Dispose();
            _softwareEngine.Dispose();
        }

        public void Update()
        {
            if (!_root.activeSelf || _activeEngine == null)
            {
                return;
            }

            _activeEngine.Update();
            if (_activeEngine.OutputTexture != null
                && _videoSurface.texture != _activeEngine.OutputTexture)
            {
                _videoSurface.texture = _activeEngine.OutputTexture;
            }
            if (!_activeEngine.IsPrepared)
            {
                return;
            }

            double duration = _activeEngine.DurationSeconds;
            double position = Math.Max(0d, _activeEngine.PositionSeconds);
            _lastPositionTicks = Math.Max(
                _lastPositionTicks,
                (long)(position * AppConstants.TicksPerSecond));
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
            UpdateSubtitle(position);
        }

        private void TogglePlayPause()
        {
            if (_activeEngine == null || !_activeEngine.IsPrepared)
            {
                return;
            }

            if (_activeEngine.IsPlaying)
            {
                _activeEngine.Pause();
                _playPauseLabel.text = "播放";
                PauseStateChanged?.Invoke(true);
            }
            else
            {
                _activeEngine.Play();
                _playPauseLabel.text = "暂停";
                PauseStateChanged?.Invoke(false);
            }
        }

        private void SeekToNormalized(float value)
        {
            if (_updatingSlider
                || _activeEngine == null
                || !_activeEngine.CanSeek
                || _activeEngine.DurationSeconds <= 0.01d)
            {
                return;
            }
            double seconds = value * _activeEngine.DurationSeconds;
            _activeEngine.Seek(seconds);
            _lastPositionTicks = (long)(seconds * AppConstants.TicksPerSecond);
        }

        private void ShowTrackMenu(bool audio)
        {
            if (_plan == null || _plan.MediaSource == null)
            {
                return;
            }

            List<JellyfinMediaStream> streams = PlaybackCapabilities.StreamsOfType(
                _plan.MediaSource,
                audio ? "Audio" : "Subtitle");
            int optionCount = streams.Count + (audio ? 0 : 1);
            float height = Mathf.Clamp(104f + optionCount * 66f, 180f, 760f);
            UiFactory.SetRect(
                _trackPanel.GetComponent<RectTransform>(),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-38f, -116f),
                new Vector2(520f, height));
            UiFactory.DestroyChildren(_trackPanel.transform);

            Text heading = UiFactory.CreateText(
                "Heading",
                _trackPanel.transform,
                audio ? "选择音轨" : "选择字幕",
                25,
                UiTheme.TextPrimary,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            UiFactory.SetRect(
                heading.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -12f),
                new Vector2(-44f, 62f));

            int row = 0;
            if (!audio)
            {
                bool selected = !_plan.SubtitleStreamIndex.HasValue
                    || _plan.SubtitleStreamIndex.Value < 0;
                AddTrackOption(
                    "Subtitle Off",
                    selected ? "✓ 关闭字幕" : "关闭字幕",
                    row++,
                    () => RequestTrackSelection(_plan.AudioStreamIndex, -1));
            }
            foreach (JellyfinMediaStream stream in streams.Take(9))
            {
                JellyfinMediaStream selectedStream = stream;
                bool selected = audio
                    ? _plan.AudioStreamIndex == stream.Index
                    : _plan.SubtitleStreamIndex == stream.Index;
                AddTrackOption(
                    (audio ? "Audio " : "Subtitle ") + stream.Index,
                    (selected ? "✓ " : string.Empty) + StreamLabel(stream),
                    row++,
                    () => RequestTrackSelection(
                        audio ? selectedStream.Index : _plan.AudioStreamIndex,
                        audio ? _plan.SubtitleStreamIndex : selectedStream.Index));
            }

            _trackPanel.SetActive(true);
            _trackPanel.transform.SetAsLastSibling();
        }

        private void AddTrackOption(string name, string label, int row, Action selected)
        {
            Button button = UiFactory.CreateButton(
                name,
                _trackPanel.transform,
                label,
                UiTheme.SurfaceRaised,
                UiTheme.TextPrimary,
                20);
            UiFactory.SetRect(
                button.GetComponent<RectTransform>(),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -76f - row * 66f),
                new Vector2(-32f, 56f));
            button.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
            button.onClick.AddListener(() => selected());
        }

        private void RequestTrackSelection(int? audioStreamIndex, int? subtitleStreamIndex)
        {
            _trackPanel.SetActive(false);
            _status.text = "正在切换音轨与字幕…";
            TrackSelectionRequested?.Invoke(audioStreamIndex, subtitleStreamIndex);
        }

        private void UpdateTrackLabels()
        {
            if (_plan == null || _plan.MediaSource == null)
            {
                _audioLabel.text = "音轨";
                _subtitleButtonLabel.text = "字幕";
                return;
            }
            JellyfinMediaStream audio = PlaybackCapabilities.ResolveAudioStream(
                _plan.MediaSource,
                _plan.AudioStreamIndex);
            JellyfinMediaStream subtitle = PlaybackCapabilities.ResolveSubtitleStream(
                _plan.MediaSource,
                _plan.SubtitleStreamIndex);
            _audioLabel.text = "音轨 · " + CompactStreamLabel(audio, "默认");
            _subtitleButtonLabel.text = _plan.SubtitleBurnedIn
                ? "字幕 · 烧录"
                : "字幕 · " + CompactStreamLabel(subtitle, "关闭");
        }

        private void UpdateSubtitle(double position)
        {
            string text = _subtitleTrack != null
                ? _subtitleTrack.TextAt(position)
                : string.Empty;
            if (string.Equals(_subtitleText.text, text, StringComparison.Ordinal))
            {
                return;
            }
            _subtitleText.text = text;
            _subtitleRoot.SetActive(!string.IsNullOrWhiteSpace(text));
        }

        private void StopVideoOnly()
        {
            _unityEngine.Stop();
            _libVlcHardwareEngine.Stop();
            _softwareEngine.Stop();
            _subtitleTrack = null;
            if (_subtitleText != null)
            {
                _subtitleText.text = string.Empty;
            }
            if (_subtitleRoot != null)
            {
                _subtitleRoot.SetActive(false);
            }
            if (_status != null)
            {
                _status.text = string.Empty;
            }
            if (_timeLabel != null)
            {
                _timeLabel.text = "00:00 / 00:00";
            }
            if (_progress != null)
            {
                _progress.SetValueWithoutNotify(0f);
            }
        }

        private void HandleEngineFailure(IPlaybackEngine engine, string message)
        {
            if (engine != _activeEngine)
            {
                return;
            }
            _status.text = "播放路径失败，正在自动降级…";
            PlaybackFailed?.Invoke(string.IsNullOrWhiteSpace(message)
                ? "播放器发生未知错误。"
                : message);
        }

        private void HandleEngineCompleted(IPlaybackEngine engine)
        {
            if (engine == _activeEngine)
            {
                PlaybackCompleted?.Invoke();
            }
        }

        private static string PreparationMessage(PlaybackTier tier)
        {
            switch (tier)
            {
                case PlaybackTier.HardwareDirect:
                    return "正在启动 Android 硬件解码…";
                case PlaybackTier.HardwareLibVlcDirect:
                    return "正在用 LibVLC 启动 MediaCodec 硬件优先解码…";
                case PlaybackTier.SoftwareDirect:
                    return "硬件路径不可用，正在启动本地软件解码…";
                default:
                    return "本地解码不可用，正在启动 Jellyfin 服务器转码…";
            }
        }

        private static string StreamLabel(JellyfinMediaStream stream)
        {
            if (stream == null)
            {
                return "未知轨道";
            }
            string title = !string.IsNullOrWhiteSpace(stream.DisplayTitle)
                ? stream.DisplayTitle.Trim()
                : !string.IsNullOrWhiteSpace(stream.Language)
                    ? stream.Language.Trim()
                    : "轨道 " + stream.Index;
            string codec = string.IsNullOrWhiteSpace(stream.Codec)
                ? string.Empty
                : "  ·  " + stream.Codec.ToUpperInvariant();
            return title + codec + (stream.IsForced ? "  ·  强制" : string.Empty);
        }

        private static string CompactStreamLabel(JellyfinMediaStream stream, string fallback)
        {
            if (stream == null)
            {
                return fallback;
            }
            string value = !string.IsNullOrWhiteSpace(stream.Language)
                ? stream.Language.Trim()
                : !string.IsNullOrWhiteSpace(stream.DisplayTitle)
                    ? stream.DisplayTitle.Trim()
                    : "轨道 " + stream.Index;
            return value.Length <= 10 ? value : value.Substring(0, 9) + "…";
        }

        private static string FormatTime(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0d)
            {
                seconds = 0d;
            }
            TimeSpan time = TimeSpan.FromSeconds(seconds);
            return time.TotalHours >= 1d
                ? string.Format(
                    "{0:00}:{1:00}:{2:00}",
                    (int)time.TotalHours,
                    time.Minutes,
                    time.Seconds)
                : string.Format("{0:00}:{1:00}", time.Minutes, time.Seconds);
        }
    }
}
