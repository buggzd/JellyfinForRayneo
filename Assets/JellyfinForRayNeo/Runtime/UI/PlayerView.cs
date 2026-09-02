using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[assembly: InternalsVisibleTo("JellyfinForRayNeo.PlayModeTests")]

namespace JellyfinForRayNeo
{
    public sealed class PlayerView : IDisposable
    {
        private const float ControlsAutoHideSeconds = 3.2f;
        private const float SeekConfirmationTimeoutSeconds = 2.5f;
        private const double SeekConfirmationToleranceSeconds = 1.5d;
        private const double RemoteSeekSeconds = 10d;

        private readonly GameObject _root;
        private readonly UiViewMotion _motion;
        private readonly RawImage _videoSurface;
        private readonly AspectRatioFitter _videoAspect;
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
        private readonly Button _backButton;
        private readonly Button _audioButton;
        private readonly Button _subtitleButton;
        private readonly Button _playPauseButton;
        private readonly Slider _progress;
        private readonly CanvasGroup _topControlsGroup;
        private readonly CanvasGroup _bottomControlsGroup;
        private readonly CanvasGroup _topEdgeGroup;
        private readonly CanvasGroup _bottomEdgeGroup;
        private readonly Transform _topControlsRoot;
        private readonly Transform _bottomControlsRoot;
        private readonly RectTransform _topControlsRect;
        private readonly RectTransform _bottomControlsRect;
        private readonly Vector2 _topControlsRestPosition;
        private readonly Vector2 _bottomControlsRestPosition;
        private readonly GameObject _seekFeedbackRoot;
        private readonly CanvasGroup _seekFeedbackGroup;
        private readonly RectTransform _seekFeedbackRect;
        private readonly Vector3 _seekFeedbackRestScale;
        private readonly Text _seekFeedbackLabel;
        private readonly GameObject _trackPanel;
        private UiViewMotion _trackMotion;
        private readonly GameObject _subtitleRoot;
        private readonly Text _subtitleText;
        private IPlaybackEngine _activeEngine;
        private JellyfinPlaybackPlan _plan;
        private SubtitleTrack _subtitleTrack;
        private long _lastPositionTicks;
        private long _pendingSeekTargetTicks = -1L;
        private float _pendingSeekDeadline;
        private bool _updatingSlider;
        private bool _disposed;
        private bool _controlsVisible;
        private float _hideControlsAt;
        private float _hideSeekFeedbackAt;

        public event Action BackRequested;
        public event Action<bool> PauseStateChanged;
        public event Action<string> PlaybackFailed;
        public event Action PlaybackCompleted;
        public event Action<int?, int?> TrackSelectionRequested;

        public Transform FocusRoot => _root.transform;

        public PlayerView(Transform parent)
            : this(parent, null, null, null)
        {
        }

        internal PlayerView(
            Transform parent,
            IPlaybackEngine unityEngine,
            IPlaybackEngine libVlcHardwareEngine,
            IPlaybackEngine softwareEngine)
        {
            Image rootImage = UiFactory.CreatePanel("Player Screen", parent, Color.black);
            UiFactory.Stretch(rootImage.rectTransform);
            _root = rootImage.gameObject;
            _motion = UiFactory.AddViewMotion(_root, 0f, 1f);

            _videoSurface = UiFactory.CreateRect("Video Surface", rootImage.transform)
                .gameObject.AddComponent<RawImage>();
            _videoSurface.color = Color.white;
            _videoSurface.raycastTarget = false;
            UiFactory.Stretch(_videoSurface.rectTransform);
            _videoAspect = _videoSurface.gameObject.AddComponent<AspectRatioFitter>();
            _videoAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            _videoAspect.aspectRatio = 16f / 9f;

            _unityEngine = unityEngine ?? new UnityVideoPlaybackEngine(rootImage.gameObject);
            _libVlcHardwareEngine = libVlcHardwareEngine ?? new LibVlcPlaybackEngine(false);
            _softwareEngine = softwareEngine ?? new LibVlcPlaybackEngine(true);
            _unityEngine.Failed += message => HandleEngineFailure(_unityEngine, message);
            _libVlcHardwareEngine.Failed += message =>
                HandleEngineFailure(_libVlcHardwareEngine, message);
            _softwareEngine.Failed += message => HandleEngineFailure(_softwareEngine, message);
            _unityEngine.Completed += () => HandleEngineCompleted(_unityEngine);
            _libVlcHardwareEngine.Completed += () =>
                HandleEngineCompleted(_libVlcHardwareEngine);
            _softwareEngine.Completed += () => HandleEngineCompleted(_softwareEngine);

            Image topEdgeGradient = UiFactory.CreateGradientPanel(
                "Top Edge Gradient",
                rootImage.transform,
                new Color(0f, 0f, 0f, 0f),
                new Color(0.004f, 0.006f, 0.012f, 0.88f));
            UiFactory.SetRect(
                topEdgeGradient.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 260f));
            _topEdgeGroup = topEdgeGradient.gameObject.AddComponent<CanvasGroup>();
            _topEdgeGroup.alpha = 0f;
            _topEdgeGroup.interactable = false;
            _topEdgeGroup.blocksRaycasts = false;

            Image bottomEdgeGradient = UiFactory.CreateGradientPanel(
                "Bottom Edge Gradient",
                rootImage.transform,
                new Color(0.004f, 0.006f, 0.012f, 0.94f),
                new Color(0f, 0f, 0f, 0f));
            UiFactory.SetRect(
                bottomEdgeGradient.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                Vector2.zero,
                new Vector2(0f, 330f));
            _bottomEdgeGroup = bottomEdgeGradient.gameObject.AddComponent<CanvasGroup>();
            _bottomEdgeGroup.alpha = 0f;
            _bottomEdgeGroup.interactable = false;
            _bottomEdgeGroup.blocksRaycasts = false;

            Image topBar = UiFactory.CreateRoundedPanel(
                "Top Controls",
                rootImage.transform,
                new Color(0.028f, 0.034f, 0.049f, 0.88f));
            UiFactory.SetRect(
                topBar.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -28f),
                new Vector2(-64f, 80f));
            AddGlassTreatment(topBar, new Vector2(0f, -7f));
            _topControlsRoot = topBar.transform;
            _topControlsRect = topBar.rectTransform;
            _topControlsRestPosition = _topControlsRect.anchoredPosition;
            _topControlsGroup = topBar.gameObject.AddComponent<CanvasGroup>();

            _backButton = UiFactory.CreateButton(
                "Back",
                topBar.transform,
                "返回",
                UiTheme.SurfaceRaised,
                UiTheme.TextPrimary,
                23);
            UiFactory.SetRect(
                _backButton.GetComponent<RectTransform>(),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(18f, 0f),
                new Vector2(112f, 50f));
            _backButton.onClick.AddListener(() => BackRequested?.Invoke());

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
                new Vector2(150f, 0f),
                new Vector2(-850f, 56f));

            Image modeCapsule = UiFactory.CreateRoundedPanel(
                "Decode Status Capsule",
                topBar.transform,
                new Color(0.18f, 0.43f, 0.41f, 0.62f));
            UiFactory.SetRect(
                modeCapsule.rectTransform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-462f, 0f),
                new Vector2(172f, 46f));
            modeCapsule.raycastTarget = false;
            AddGlassOutline(modeCapsule, new Color(0.48f, 0.94f, 0.88f, 0.26f));
            _modeLabel = UiFactory.CreateText(
                "Decode Mode",
                modeCapsule.transform,
                string.Empty,
                17,
                UiTheme.AccentBright,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            UiFactory.Stretch(_modeLabel.rectTransform, 12f, 12f, 4f, 4f);

            _audioButton = UiFactory.CreateButton(
                "Audio Tracks",
                topBar.transform,
                "音轨",
                UiTheme.SurfaceRaised,
                UiTheme.TextPrimary,
                18);
            UiFactory.SetRect(
                _audioButton.GetComponent<RectTransform>(),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-270f, 0f),
                new Vector2(174f, 50f));
            _audioLabel = _audioButton.GetComponentInChildren<Text>();
            _audioButton.onClick.AddListener(() => ShowTrackMenu(true));

            _subtitleButton = UiFactory.CreateButton(
                "Subtitle Tracks",
                topBar.transform,
                "字幕",
                UiTheme.SurfaceRaised,
                UiTheme.TextPrimary,
                18);
            UiFactory.SetRect(
                _subtitleButton.GetComponent<RectTransform>(),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-80f, 0f),
                new Vector2(174f, 50f));
            _subtitleButtonLabel = _subtitleButton.GetComponentInChildren<Text>();
            _subtitleButton.onClick.AddListener(() => ShowTrackMenu(false));

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

            Image seekFeedback = UiFactory.CreateRoundedPanel(
                "Seek Feedback",
                rootImage.transform,
                Color.white);
            UiGradient seekGradient = seekFeedback.gameObject.AddComponent<UiGradient>();
            seekGradient.StartColor = new Color(0.035f, 0.16f, 0.17f, 0.94f);
            seekGradient.EndColor = new Color(0.13f, 0.075f, 0.21f, 0.94f);
            seekGradient.Horizontal = true;
            UiFactory.SetRect(
                seekFeedback.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(268f, 96f));
            AddGlassTreatment(seekFeedback, new Vector2(0f, -9f));
            _seekFeedbackRoot = seekFeedback.gameObject;
            _seekFeedbackGroup = seekFeedback.gameObject.AddComponent<CanvasGroup>();
            _seekFeedbackRect = seekFeedback.rectTransform;
            _seekFeedbackRestScale = _seekFeedbackRect.localScale;
            _seekFeedbackLabel = UiFactory.CreateText(
                "Seek Feedback Label",
                seekFeedback.transform,
                string.Empty,
                29,
                UiTheme.TextPrimary,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            UiFactory.Stretch(_seekFeedbackLabel.rectTransform, 18f, 18f, 8f, 8f);
            _seekFeedbackRoot.SetActive(false);

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

            Image controlBar = UiFactory.CreateRoundedPanel(
                "Playback Controls",
                rootImage.transform,
                new Color(0.028f, 0.034f, 0.049f, 0.91f));
            UiFactory.SetRect(
                controlBar.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 28f),
                new Vector2(-64f, 112f));
            AddGlassTreatment(controlBar, new Vector2(0f, 8f));
            _bottomControlsRoot = controlBar.transform;
            _bottomControlsRect = controlBar.rectTransform;
            _bottomControlsRestPosition = _bottomControlsRect.anchoredPosition;
            _bottomControlsGroup = controlBar.gameObject.AddComponent<CanvasGroup>();

            _playPauseButton = UiFactory.CreateButton(
                "Play Pause",
                controlBar.transform,
                "暂停",
                UiTheme.Accent,
                UiTheme.TextPrimary,
                23);
            UiFactory.SetRect(
                _playPauseButton.GetComponent<RectTransform>(),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(22f, 0f),
                new Vector2(124f, 56f));
            _playPauseLabel = _playPauseButton.GetComponentInChildren<Text>();
            _playPauseButton.onClick.AddListener(TogglePlayPause);

            Image sliderBackground = UiFactory.CreateRoundedPanel(
                "Progress",
                controlBar.transform,
                UiTheme.ProgressTrack);
            UiFactory.SetRect(
                sliderBackground.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(174f, 0f),
                new Vector2(-410f, 10f));
            _progress = sliderBackground.gameObject.AddComponent<Slider>();
            _progress.minValue = 0f;
            _progress.maxValue = 1f;
            _progress.direction = Slider.Direction.LeftToRight;

            Image fill = UiFactory.CreateRoundedPanel(
                "Fill",
                sliderBackground.transform,
                UiTheme.AccentBright);
            fill.raycastTarget = false;
            UiFactory.Stretch(fill.rectTransform);
            _progress.fillRect = fill.rectTransform;

            Image handle = UiFactory.CreateRoundedPanel(
                "Handle",
                sliderBackground.transform,
                UiTheme.TextPrimary);
            UiFactory.SetRect(
                handle.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(22f, 22f));
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
                new Vector2(-24f, 0f),
                new Vector2(340f, 52f));

            ConfigurePlayerNavigation();

            Image trackPanel = UiFactory.CreateRoundedPanel(
                "Track Menu",
                rootImage.transform,
                new Color(0.032f, 0.038f, 0.056f, 0.975f));
            AddGlassTreatment(trackPanel, new Vector2(-8f, -10f));
            _trackPanel = trackPanel.gameObject;
            _trackMotion = UiFactory.AddViewMotion(_trackPanel, 18f, 0.98f);
            _trackMotion.SetVisibleImmediately(false);
            _controlsVisible = false;
            _topControlsGroup.alpha = 0f;
            _bottomControlsGroup.alpha = 0f;
            _topEdgeGroup.alpha = 0f;
            _bottomEdgeGroup.alpha = 0f;
            _topControlsGroup.interactable = false;
            _bottomControlsGroup.interactable = false;
            _topControlsGroup.blocksRaycasts = false;
            _bottomControlsGroup.blocksRaycasts = false;
            _topControlsRect.anchoredPosition = HiddenTopControlsPosition;
            _bottomControlsRect.anchoredPosition = HiddenBottomControlsPosition;
            _motion.SetVisibleImmediately(false);
        }

        public bool IsVisible
        {
            get { return _motion.IsVisible; }
        }

        public bool IsPaused
        {
            get { return _activeEngine != null && _activeEngine.IsPaused; }
        }

        public bool ControlsVisible
        {
            get { return _controlsVisible; }
        }

        public long CurrentPositionTicks
        {
            get { return SamplePositionTicks(); }
        }

        private Vector2 HiddenTopControlsPosition =>
            _topControlsRestPosition + Vector2.up * 24f;

        private Vector2 HiddenBottomControlsPosition =>
            _bottomControlsRestPosition + Vector2.down * 28f;

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
            ClearPendingSeek();
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
            _root.transform.SetAsLastSibling();
            _motion.Show();
            _title.text = plan.Item != null ? plan.Item.Name : "正在播放";
            _modeLabel.text = plan.TierLabel;
            _status.text = PreparationMessage(plan.Tier);
            _playPauseLabel.text = "暂停";
            UpdateVideoSurface();
            UpdateTrackLabels();
            _seekFeedbackRoot.SetActive(false);
            ShowControls();

            try
            {
                await _activeEngine.PrepareAndPlayAsync(plan, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                _status.text = string.Empty;
                ShowControls();
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

        public bool HandleRemoteCommand(
            CompanionRemoteCommand command,
            DirectionalFocusNavigator navigator)
        {
            if (!IsVisible || navigator == null)
            {
                return false;
            }

            navigator.SetScope(FocusRoot);
            if (_trackMotion != null && _trackMotion.IsVisible)
            {
                navigator.SetScope(_trackPanel.transform);
                ShowControls();
                return navigator.Handle(command);
            }

            switch (command)
            {
                case CompanionRemoteCommand.Left:
                    SeekRelative(-RemoteSeekSeconds);
                    RefreshControlsDeadline();
                    return true;
                case CompanionRemoteCommand.Right:
                    SeekRelative(RemoteSeekSeconds);
                    RefreshControlsDeadline();
                    return true;
                case CompanionRemoteCommand.Up:
                case CompanionRemoteCommand.Down:
                {
                    bool wasVisible = _controlsVisible;
                    ShowControls();
                    if (!wasVisible || !HasPlayerSelection())
                    {
                        navigator.SelectPreferred(
                            command == CompanionRemoteCommand.Up
                                ? "Audio Tracks"
                                : "Play Pause");
                        return true;
                    }
                    return navigator.Handle(command);
                }
                case CompanionRemoteCommand.Submit:
                    if (!_controlsVisible || !HasPlayerSelection())
                    {
                        ShowControls();
                        navigator.SelectPreferred("Play Pause");
                        return true;
                    }
                    ShowControls();
                    return navigator.Handle(command);
                default:
                    return false;
            }
        }

        public bool CloseTransientUi()
        {
            if (_trackMotion == null || !_trackMotion.IsVisible)
            {
                return false;
            }

            _trackMotion.Hide();
            ShowControls();
            return true;
        }

        public void Stop()
        {
            StopVideoOnly();
            _plan = null;
            _activeEngine = null;
            _controlsVisible = false;
            _topControlsGroup.alpha = 0f;
            _bottomControlsGroup.alpha = 0f;
            _topEdgeGroup.alpha = 0f;
            _bottomEdgeGroup.alpha = 0f;
            _topControlsGroup.interactable = false;
            _bottomControlsGroup.interactable = false;
            _topControlsGroup.blocksRaycasts = false;
            _bottomControlsGroup.blocksRaycasts = false;
            _topControlsRect.anchoredPosition = HiddenTopControlsPosition;
            _bottomControlsRect.anchoredPosition = HiddenBottomControlsPosition;
            _seekFeedbackRoot.SetActive(false);
            _seekFeedbackRect.localScale = _seekFeedbackRestScale;
            if (_trackMotion != null)
            {
                _trackMotion.SetVisibleImmediately(false);
            }
            else
            {
                _trackPanel.SetActive(false);
            }
            _motion.Hide();
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
            UpdateVideoSurface();
            if (!_activeEngine.IsPrepared)
            {
                UpdateTransientUi();
                return;
            }

            double duration = _activeEngine.DurationSeconds;
            double position = SamplePositionTicks()
                / (double)AppConstants.TicksPerSecond;
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
            UpdateTransientUi();
        }

        private void ConfigurePlayerNavigation()
        {
            SetExplicitNavigation(
                _backButton,
                null,
                _playPauseButton,
                null,
                _audioButton);
            SetExplicitNavigation(
                _audioButton,
                null,
                _progress,
                _backButton,
                _subtitleButton);
            SetExplicitNavigation(
                _subtitleButton,
                null,
                _progress,
                _audioButton,
                null);
            SetExplicitNavigation(
                _playPauseButton,
                _backButton,
                null,
                null,
                _progress);
            SetExplicitNavigation(
                _progress,
                _audioButton,
                null,
                _playPauseButton,
                null);
        }

        private bool HasPlayerSelection()
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            GameObject selected = EventSystem.current.currentSelectedGameObject;
            return selected != null
                && selected.activeInHierarchy
                && selected.transform.IsChildOf(FocusRoot);
        }

        private static void SetExplicitNavigation(
            Selectable selectable,
            Selectable up,
            Selectable down,
            Selectable left,
            Selectable right)
        {
            if (selectable == null)
            {
                return;
            }

            Navigation navigation = selectable.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = up;
            navigation.selectOnDown = down;
            navigation.selectOnLeft = left;
            navigation.selectOnRight = right;
            selectable.navigation = navigation;
        }

        private void ShowControls(bool keepVisible = false)
        {
            _controlsVisible = true;
            _topControlsGroup.interactable = true;
            _bottomControlsGroup.interactable = true;
            _topControlsGroup.blocksRaycasts = true;
            _bottomControlsGroup.blocksRaycasts = true;
            _hideControlsAt = keepVisible || IsPaused
                ? float.PositiveInfinity
                : Time.unscaledTime + ControlsAutoHideSeconds;
        }

        private void RefreshControlsDeadline()
        {
            if (!_controlsVisible)
            {
                return;
            }

            _hideControlsAt = IsPaused
                ? float.PositiveInfinity
                : Time.unscaledTime + ControlsAutoHideSeconds;
        }

        private void HideControls()
        {
            if (!_controlsVisible)
            {
                return;
            }

            _controlsVisible = false;
            _topControlsGroup.interactable = false;
            _bottomControlsGroup.interactable = false;
            _topControlsGroup.blocksRaycasts = false;
            _bottomControlsGroup.blocksRaycasts = false;
            if (EventSystem.current != null)
            {
                GameObject selected = EventSystem.current.currentSelectedGameObject;
                if (selected != null
                    && (selected.transform.IsChildOf(_topControlsRoot)
                        || selected.transform.IsChildOf(_bottomControlsRoot)))
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
            }
        }

        private void UpdateTransientUi()
        {
            bool trackMenuVisible = _trackMotion != null && _trackMotion.IsVisible;
            if (_controlsVisible
                && !trackMenuVisible
                && !IsPaused
                && Time.unscaledTime >= _hideControlsAt)
            {
                HideControls();
            }

            float targetAlpha = _controlsVisible ? 1f : 0f;
            float step = Time.unscaledDeltaTime * 6.8f;
            _topControlsGroup.alpha = Mathf.MoveTowards(
                _topControlsGroup.alpha,
                targetAlpha,
                step);
            _bottomControlsGroup.alpha = Mathf.MoveTowards(
                _bottomControlsGroup.alpha,
                targetAlpha,
                step);
            _topEdgeGroup.alpha = Mathf.MoveTowards(
                _topEdgeGroup.alpha,
                targetAlpha,
                step * 0.82f);
            _bottomEdgeGroup.alpha = Mathf.MoveTowards(
                _bottomEdgeGroup.alpha,
                targetAlpha,
                step * 0.82f);
            float motionStep = Time.unscaledDeltaTime * 250f;
            _topControlsRect.anchoredPosition = Vector2.MoveTowards(
                _topControlsRect.anchoredPosition,
                _controlsVisible
                    ? _topControlsRestPosition
                    : HiddenTopControlsPosition,
                motionStep);
            _bottomControlsRect.anchoredPosition = Vector2.MoveTowards(
                _bottomControlsRect.anchoredPosition,
                _controlsVisible
                    ? _bottomControlsRestPosition
                    : HiddenBottomControlsPosition,
                motionStep);

            if (!_seekFeedbackRoot.activeSelf)
            {
                return;
            }

            if (Time.unscaledTime < _hideSeekFeedbackAt)
            {
                _seekFeedbackGroup.alpha = Mathf.MoveTowards(
                    _seekFeedbackGroup.alpha,
                    1f,
                    Time.unscaledDeltaTime * 9f);
                _seekFeedbackRect.localScale = Vector3.MoveTowards(
                    _seekFeedbackRect.localScale,
                    _seekFeedbackRestScale,
                    Time.unscaledDeltaTime * 1.8f);
                return;
            }

            _seekFeedbackGroup.alpha = Mathf.MoveTowards(
                _seekFeedbackGroup.alpha,
                0f,
                Time.unscaledDeltaTime * 5f);
            _seekFeedbackRect.localScale = Vector3.MoveTowards(
                _seekFeedbackRect.localScale,
                _seekFeedbackRestScale * 0.94f,
                Time.unscaledDeltaTime * 0.8f);
            if (_seekFeedbackGroup.alpha <= 0.01f)
            {
                _seekFeedbackRoot.SetActive(false);
                _seekFeedbackRect.localScale = _seekFeedbackRestScale;
            }
        }

        private void SeekRelative(double deltaSeconds)
        {
            if (_activeEngine == null || !_activeEngine.IsPrepared || !_activeEngine.CanSeek)
            {
                ShowSeekFeedback("当前视频不可跳转");
                return;
            }

            double duration = _activeEngine.DurationSeconds;
            double current = CurrentPositionTicks
                / (double)AppConstants.TicksPerSecond;
            double target = Math.Max(0d, current + deltaSeconds);
            if (duration > 0.01d && !double.IsInfinity(duration) && !double.IsNaN(duration))
            {
                target = Math.Min(duration, target);
            }

            _activeEngine.Seek(target);
            BeginPendingSeek(target);
            ShowSeekFeedback(deltaSeconds < 0d ? "‹‹  10 秒" : "10 秒  ››");
        }

        private long SamplePositionTicks()
        {
            if (_activeEngine == null || !_activeEngine.IsPrepared)
            {
                return _lastPositionTicks;
            }

            double engineSeconds = Math.Max(0d, _activeEngine.PositionSeconds);
            long engineTicks = (long)(engineSeconds * AppConstants.TicksPerSecond);
            if (_pendingSeekTargetTicks >= 0L)
            {
                long toleranceTicks = (long)(
                    SeekConfirmationToleranceSeconds * AppConstants.TicksPerSecond);
                bool confirmed = Math.Abs(engineTicks - _pendingSeekTargetTicks)
                    <= toleranceTicks;
                bool timedOut = Time.unscaledTime >= _pendingSeekDeadline;
                if (!confirmed && !timedOut)
                {
                    return _pendingSeekTargetTicks;
                }

                ClearPendingSeek();
                _lastPositionTicks = engineTicks;
                return _lastPositionTicks;
            }

            _lastPositionTicks = Math.Max(_lastPositionTicks, engineTicks);
            return _lastPositionTicks;
        }

        private void BeginPendingSeek(double targetSeconds)
        {
            _pendingSeekTargetTicks = (long)(
                Math.Max(0d, targetSeconds) * AppConstants.TicksPerSecond);
            _lastPositionTicks = _pendingSeekTargetTicks;
            _pendingSeekDeadline = Time.unscaledTime + SeekConfirmationTimeoutSeconds;
        }

        private void ClearPendingSeek()
        {
            _pendingSeekTargetTicks = -1L;
            _pendingSeekDeadline = 0f;
        }

        private void UpdateVideoSurface()
        {
            if (_activeEngine == null)
            {
                return;
            }

            Texture output = _activeEngine.OutputTexture;
            if (_videoSurface.texture != output)
            {
                _videoSurface.texture = output;
            }
            _videoSurface.uvRect = _activeEngine.FlipOutputVertically
                ? new Rect(0f, 1f, 1f, -1f)
                : new Rect(0f, 0f, 1f, 1f);
            if (output != null && output.width > 0 && output.height > 0)
            {
                _videoAspect.aspectRatio = output.width / (float)output.height;
            }
        }

        private void ShowSeekFeedback(string message)
        {
            _seekFeedbackLabel.text = message ?? string.Empty;
            _seekFeedbackGroup.alpha = 0f;
            _seekFeedbackRect.localScale = _seekFeedbackRestScale * 0.84f;
            _seekFeedbackRoot.SetActive(true);
            _seekFeedbackRoot.transform.SetAsLastSibling();
            _hideSeekFeedbackAt = Time.unscaledTime + 0.72f;
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
                ShowControls(true);
                PauseStateChanged?.Invoke(true);
            }
            else
            {
                _activeEngine.Play();
                _playPauseLabel.text = "暂停";
                ShowControls();
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
            BeginPendingSeek(seconds);
        }

        private void ShowTrackMenu(bool audio)
        {
            if (_plan == null || _plan.MediaSource == null)
            {
                return;
            }

            ShowControls(true);

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

            Image accent = UiFactory.CreateRoundedPanel(
                "Track Menu Accent",
                _trackPanel.transform,
                audio ? UiTheme.Accent : UiTheme.AccentSecondary);
            accent.raycastTarget = false;
            UiFactory.SetRect(
                accent.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(18f, -18f),
                new Vector2(5f, 42f));

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
                new Vector2(12f, -12f),
                new Vector2(-74f, 62f));

            int row = 0;
            if (!audio)
            {
                bool selected = !_plan.SubtitleStreamIndex.HasValue
                    || _plan.SubtitleStreamIndex.Value < 0;
                AddTrackOption(
                    "Subtitle Off",
                    selected ? "✓ 关闭字幕" : "关闭字幕",
                    row++,
                    selected,
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
                    selected,
                    () => RequestTrackSelection(
                        audio ? selectedStream.Index : _plan.AudioStreamIndex,
                        audio ? _plan.SubtitleStreamIndex : selectedStream.Index));
            }

            _trackPanel.transform.SetAsLastSibling();
            _trackMotion.RefreshRestState();
            _trackMotion.Show();
            if (EventSystem.current != null)
            {
                Selectable firstOption = _trackPanel
                    .GetComponentsInChildren<Selectable>(true)
                    .FirstOrDefault(option => option != null && option.IsInteractable());
                if (firstOption != null)
                {
                    EventSystem.current.SetSelectedGameObject(firstOption.gameObject);
                }
            }
        }

        private void AddTrackOption(
            string name,
            string label,
            int row,
            bool isSelected,
            Action selected)
        {
            Button button = UiFactory.CreateButton(
                name,
                _trackPanel.transform,
                label,
                isSelected
                    ? new Color(0.17f, 0.42f, 0.39f, 0.82f)
                    : UiTheme.SurfaceSoft,
                UiTheme.TextPrimary,
                20);
            UiFactory.SetRect(
                button.GetComponent<RectTransform>(),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -76f - row * 66f),
                new Vector2(-32f, 56f));
            Text optionLabel = button.GetComponentInChildren<Text>();
            optionLabel.alignment = TextAnchor.MiddleLeft;
            UiFactory.Stretch(optionLabel.rectTransform, 20f, 16f, 6f, 6f);
            AddGlassOutline(
                button.targetGraphic as Image,
                isSelected
                    ? new Color(0.48f, 0.94f, 0.88f, 0.34f)
                    : UiTheme.Border);
            UiFactory.AddItemReveal(button.gameObject, Mathf.Min(row * 0.035f, 0.24f));
            button.onClick.AddListener(() => selected());
        }

        private void RequestTrackSelection(int? audioStreamIndex, int? subtitleStreamIndex)
        {
            if (_trackMotion != null)
            {
                _trackMotion.Hide();
            }
            else
            {
                _trackPanel.SetActive(false);
            }
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
            _status.text = "正在切换音轨与字幕…";
            ShowControls();
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
            ClearPendingSeek();
            _unityEngine.Stop();
            _libVlcHardwareEngine.Stop();
            _softwareEngine.Stop();
            _subtitleTrack = null;
            _videoSurface.texture = null;
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

        private static void AddGlassTreatment(Image image, Vector2 shadowDistance)
        {
            if (image == null)
            {
                return;
            }

            Shadow shadow = image.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.48f);
            shadow.effectDistance = shadowDistance;
            shadow.useGraphicAlpha = true;
            AddGlassOutline(image, UiTheme.Border);
        }

        private static void AddGlassOutline(Image image, Color color)
        {
            if (image == null)
            {
                return;
            }

            Outline outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
        }
    }
}
