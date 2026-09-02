using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Video;

namespace JellyfinForRayNeo
{
    internal interface IPlaybackEngine : IDisposable
    {
        event Action<string> Failed;
        event Action Completed;

        Texture OutputTexture { get; }
        bool FlipOutputVertically { get; }
        bool IsPrepared { get; }
        bool IsPlaying { get; }
        bool IsPaused { get; }
        bool CanSeek { get; }
        double PositionSeconds { get; }
        double DurationSeconds { get; }

        Task PrepareAndPlayAsync(JellyfinPlaybackPlan plan, CancellationToken cancellationToken);
        void Update();
        void Play();
        void Pause();
        void Seek(double seconds);
        void Stop();
    }

    internal sealed class UnityVideoPlaybackEngine : IPlaybackEngine
    {
        private readonly VideoPlayer _videoPlayer;
        private readonly AudioSource _audioSource;
        private readonly RenderTexture _renderTexture;
        private bool _preparing;
        private bool _prepared;
        private bool _disposed;

        public UnityVideoPlaybackEngine(GameObject owner)
        {
            _renderTexture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32)
            {
                name = "Jellyfin Hardware Video Surface",
                useMipMap = false,
                autoGenerateMips = false,
                antiAliasing = 1
            };
            _renderTexture.Create();

            _videoPlayer = owner.AddComponent<VideoPlayer>();
            _audioSource = owner.AddComponent<AudioSource>();
            _videoPlayer.playOnAwake = false;
            _videoPlayer.waitForFirstFrame = true;
            _videoPlayer.skipOnDrop = true;
            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            _videoPlayer.targetTexture = _renderTexture;
            _videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            _videoPlayer.errorReceived += HandleRuntimeError;
            _videoPlayer.loopPointReached += HandleCompleted;
        }

        public event Action<string> Failed;
        public event Action Completed;

        public Texture OutputTexture
        {
            get { return _renderTexture; }
        }

        public bool FlipOutputVertically
        {
            get { return false; }
        }

        public bool IsPrepared
        {
            get { return _prepared; }
        }

        public bool IsPlaying
        {
            get { return _prepared && _videoPlayer.isPlaying; }
        }

        public bool IsPaused
        {
            get { return _prepared && _videoPlayer.isPaused; }
        }

        public bool CanSeek
        {
            get { return _prepared && _videoPlayer.canSetTime; }
        }

        public double PositionSeconds
        {
            get
            {
                double value = _prepared ? _videoPlayer.time : 0d;
                return double.IsNaN(value) || value < 0d ? 0d : value;
            }
        }

        public double DurationSeconds
        {
            get { return _prepared ? _videoPlayer.length : 0d; }
        }

        public async Task PrepareAndPlayAsync(
            JellyfinPlaybackPlan plan,
            CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(UnityVideoPlaybackEngine));
            }
            if (plan == null || string.IsNullOrWhiteSpace(plan.Url))
            {
                throw new ArgumentException("A playback URL is required.", nameof(plan));
            }

            Stop();
            ConfigureAudioTracks(plan);
            _videoPlayer.url = plan.Url;
            _preparing = true;
            TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();
            VideoPlayer.EventHandler preparedHandler = source => completion.TrySetResult(true);
            VideoPlayer.ErrorEventHandler errorHandler = (source, message) =>
                completion.TrySetException(new InvalidOperationException(
                    string.IsNullOrWhiteSpace(message) ? "Android 视频解码器准备失败。" : message));
            _videoPlayer.prepareCompleted += preparedHandler;
            _videoPlayer.errorReceived += errorHandler;
            CancellationTokenRegistration registration = cancellationToken.Register(
                () => completion.TrySetCanceled(cancellationToken));

            try
            {
                _videoPlayer.Prepare();
                await completion.Task;
                cancellationToken.ThrowIfCancellationRequested();
                _prepared = true;
                if (plan.StartPositionTicks > 0L && _videoPlayer.canSetTime)
                {
                    _videoPlayer.time = plan.StartPositionTicks
                        / (double)AppConstants.TicksPerSecond;
                }
                _videoPlayer.Play();
            }
            catch
            {
                Stop();
                throw;
            }
            finally
            {
                registration.Dispose();
                _videoPlayer.prepareCompleted -= preparedHandler;
                _videoPlayer.errorReceived -= errorHandler;
                _preparing = false;
            }
        }

        public void Update()
        {
        }

        public void Play()
        {
            if (_prepared)
            {
                _videoPlayer.Play();
            }
        }

        public void Pause()
        {
            if (_prepared)
            {
                _videoPlayer.Pause();
            }
        }

        public void Seek(double seconds)
        {
            if (CanSeek)
            {
                _videoPlayer.time = Math.Max(0d, seconds);
            }
        }

        public void Stop()
        {
            _prepared = false;
            _preparing = false;
            if (_videoPlayer != null)
            {
                _videoPlayer.Stop();
                _videoPlayer.url = string.Empty;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            if (_videoPlayer != null)
            {
                _videoPlayer.errorReceived -= HandleRuntimeError;
                _videoPlayer.loopPointReached -= HandleCompleted;
            }
            Stop();
            _renderTexture.Release();
            UnityEngine.Object.Destroy(_renderTexture);
        }

        private void ConfigureAudioTracks(JellyfinPlaybackPlan plan)
        {
            int available = Math.Max(1, plan.LocalAudioTrackCount);
            int maximum = Math.Max(1, (int)VideoPlayer.controlledAudioTrackMaxCount);
            ushort count = (ushort)Math.Min(available, maximum);
            ushort selected = (ushort)Mathf.Clamp(
                plan.LocalAudioTrackIndex,
                0,
                count - 1);
            try
            {
                _videoPlayer.controlledAudioTrackCount = count;
                for (ushort index = 0; index < count; index++)
                {
                    bool enabled = index == selected;
                    _videoPlayer.EnableAudioTrack(index, enabled);
                    if (enabled)
                    {
                        _videoPlayer.SetTargetAudioSource(index, _audioSource);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Unable to preselect the requested audio track: " + exception.Message);
                _videoPlayer.controlledAudioTrackCount = 1;
                _videoPlayer.EnableAudioTrack(0, true);
                _videoPlayer.SetTargetAudioSource(0, _audioSource);
            }
        }

        private void HandleRuntimeError(VideoPlayer source, string message)
        {
            if (_preparing || !_prepared)
            {
                return;
            }
            _prepared = false;
            Failed?.Invoke(string.IsNullOrWhiteSpace(message)
                ? "Android 硬件播放器发生未知错误。"
                : message);
        }

        private void HandleCompleted(VideoPlayer source)
        {
            if (_prepared)
            {
                Completed?.Invoke();
            }
        }
    }

    internal sealed class LibVlcPlaybackEngine : IPlaybackEngine
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private const string NativeLibrary = "libvlc";
        private const int StatePlaying = 3;
        private const int StatePaused = 4;
        private const int StateEnded = 6;
        private const int StateError = 7;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr VideoLockCallback(IntPtr opaque, IntPtr planes);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void VideoUnlockCallback(IntPtr opaque, IntPtr picture, IntPtr planes);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void VideoDisplayCallback(IntPtr opaque, IntPtr picture);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate uint VideoFormatCallback(
            IntPtr opaquePointer,
            IntPtr chroma,
            ref uint width,
            ref uint height,
            ref uint pitches,
            ref uint lines);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void VideoCleanupCallback(IntPtr opaque);

        private static readonly VideoLockCallback LockCallback = HandleVideoLock;
        private static readonly VideoUnlockCallback UnlockCallback = HandleVideoUnlock;
        private static readonly VideoDisplayCallback DisplayCallback = HandleVideoDisplay;
        private static readonly VideoFormatCallback FormatCallback = HandleVideoFormat;
        private static readonly VideoCleanupCallback CleanupCallback = HandleVideoCleanup;

        private readonly object _frameLock = new object();
        private IntPtr _libVlc;
        private IntPtr _mediaPlayer;
        private IntPtr _frameBuffer;
        private int _frameBufferSize;
        private int _frameWidth;
        private int _frameHeight;
        private int _requestedAudioTrackOrdinal;
        private bool _frameReady;
        private bool _formatChanged;
        private bool _preparing;
        private bool _prepared;
        private bool _failureRaised;
        private bool _completionRaised;
        private float _prepareDeadline;
        private long _startPositionMilliseconds;
        private TaskCompletionSource<bool> _prepareCompletion;
        private GCHandle _selfHandle;
        private IntPtr _opaque;
        private Texture2D _texture;
#endif
        private readonly bool _forceSoftwareDecode;
        private bool _disposed;

        public LibVlcPlaybackEngine(bool forceSoftwareDecode)
        {
            _forceSoftwareDecode = forceSoftwareDecode;
        }

        public static bool IsNativeLibraryAvailable
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (Application.platform != RuntimePlatform.Android)
                {
                    return false;
                }
                try
                {
                    return libvlc_get_version() != IntPtr.Zero;
                }
                catch (DllNotFoundException)
                {
                    return false;
                }
                catch (EntryPointNotFoundException)
                {
                    return false;
                }
#else
                return false;
#endif
            }
        }

        public event Action<string> Failed;
        public event Action Completed;

        public Texture OutputTexture
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return _texture;
#else
                return null;
#endif
            }
        }

        public bool FlipOutputVertically
        {
            get { return true; }
        }

        public bool IsPrepared
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return _prepared;
#else
                return false;
#endif
            }
        }

        public bool IsPlaying
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return _prepared && GetState() == StatePlaying;
#else
                return false;
#endif
            }
        }

        public bool IsPaused
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return _prepared && GetState() == StatePaused;
#else
                return false;
#endif
            }
        }

        public bool CanSeek
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return _prepared
                    && _mediaPlayer != IntPtr.Zero
                    && libvlc_media_player_is_seekable(_mediaPlayer) != 0;
#else
                return false;
#endif
            }
        }

        public double PositionSeconds
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return _mediaPlayer == IntPtr.Zero
                    ? 0d
                    : Math.Max(0L, libvlc_media_player_get_time(_mediaPlayer)) / 1000d;
#else
                return 0d;
#endif
            }
        }

        public double DurationSeconds
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return _mediaPlayer == IntPtr.Zero
                    ? 0d
                    : Math.Max(0L, libvlc_media_player_get_length(_mediaPlayer)) / 1000d;
#else
                return 0d;
#endif
            }
        }

        public Task PrepareAndPlayAsync(
            JellyfinPlaybackPlan plan,
            CancellationToken cancellationToken)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(LibVlcPlaybackEngine));
            }
            if (!IsNativeLibraryAvailable)
            {
                throw new InvalidOperationException(
                    "LibVLC 本地解码库未安装，请运行 scripts/install-libvlc-android.sh 后重新构建。");
            }
            if (plan == null || string.IsNullOrWhiteSpace(plan.Url))
            {
                throw new ArgumentException("A playback URL is required.", nameof(plan));
            }

            Stop();
            EnsureInstance();
            IntPtr mediaUrl = AllocateUtf8(plan.Url);
            IntPtr media = IntPtr.Zero;
            try
            {
                media = libvlc_media_new_location(_libVlc, mediaUrl);
            }
            finally
            {
                Marshal.FreeHGlobal(mediaUrl);
            }
            if (media == IntPtr.Zero)
            {
                throw NativeException("LibVLC 无法打开媒体地址。");
            }

            try
            {
                AddMediaOption(media, _forceSoftwareDecode
                    ? ":avcodec-hw=none"
                    : ":avcodec-hw=any");
                AddMediaOption(media, ":no-spu");
                AddMediaOption(media, ":network-caching=1500");
                _mediaPlayer = libvlc_media_player_new_from_media(media);
            }
            finally
            {
                libvlc_media_release(media);
            }
            if (_mediaPlayer == IntPtr.Zero)
            {
                throw NativeException(DecoderLabel + "无法创建播放器。");
            }

            _selfHandle = GCHandle.Alloc(this);
            _opaque = GCHandle.ToIntPtr(_selfHandle);
            libvlc_video_set_callbacks(
                _mediaPlayer,
                LockCallback,
                UnlockCallback,
                DisplayCallback,
                _opaque);
            libvlc_video_set_format_callbacks(
                _mediaPlayer,
                FormatCallback,
                CleanupCallback);

            _preparing = true;
            _failureRaised = false;
            _completionRaised = false;
            _requestedAudioTrackOrdinal = Math.Max(0, plan.LocalAudioTrackIndex);
            _prepareDeadline = Time.realtimeSinceStartup + 25f;
            _startPositionMilliseconds = Math.Max(
                0L,
                plan.StartPositionTicks / (AppConstants.TicksPerSecond / 1000L));
            _prepareCompletion = new TaskCompletionSource<bool>();
            if (libvlc_media_player_play(_mediaPlayer) != 0)
            {
                Stop();
                throw NativeException(DecoderLabel + "启动失败。");
            }
            return AwaitPreparationAsync(_prepareCompletion, cancellationToken);
#else
            throw new PlatformNotSupportedException("LibVLC playback is only available in Android builds.");
#endif
        }

        public void Update()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            UploadPendingFrame();
            if (_mediaPlayer == IntPtr.Zero)
            {
                return;
            }

            int state = GetState();
            if (_preparing)
            {
                if (state == StatePlaying || _texture != null && _frameReady)
                {
                    _preparing = false;
                    _prepared = true;
                    SelectAudioTrack(_requestedAudioTrackOrdinal);
                    if (_startPositionMilliseconds > 0L)
                    {
                        libvlc_media_player_set_time(_mediaPlayer, _startPositionMilliseconds);
                    }
                    _prepareCompletion?.TrySetResult(true);
                }
                else if (state == StateError)
                {
                    FailPreparation(DecoderLabel + "无法解码此媒体。");
                }
                else if (Time.realtimeSinceStartup >= _prepareDeadline)
                {
                    FailPreparation(DecoderLabel + "准备超时。");
                }
                return;
            }

            if (_prepared && state == StateError && !_failureRaised)
            {
                _failureRaised = true;
                _prepared = false;
                Failed?.Invoke(DecoderLabel + "失败。");
            }
            else if (_prepared && state == StateEnded && !_completionRaised)
            {
                _completionRaised = true;
                Completed?.Invoke();
            }
#endif
        }

        public void Play()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_mediaPlayer != IntPtr.Zero)
            {
                libvlc_media_player_set_pause(_mediaPlayer, 0);
            }
#endif
        }

        public void Pause()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_mediaPlayer != IntPtr.Zero)
            {
                libvlc_media_player_set_pause(_mediaPlayer, 1);
            }
#endif
        }

        public void Seek(double seconds)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (CanSeek)
            {
                libvlc_media_player_set_time(
                    _mediaPlayer,
                    (long)(Math.Max(0d, seconds) * 1000d));
            }
#endif
        }

        public void Stop()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _prepared = false;
            _preparing = false;
            _prepareCompletion?.TrySetCanceled();
            _prepareCompletion = null;
            if (_mediaPlayer != IntPtr.Zero)
            {
                libvlc_media_player_stop(_mediaPlayer);
                libvlc_media_player_release(_mediaPlayer);
                _mediaPlayer = IntPtr.Zero;
            }
            if (_selfHandle.IsAllocated)
            {
                _selfHandle.Free();
            }
            _opaque = IntPtr.Zero;
            ReleaseFrameResources();
#endif
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            Stop();
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_libVlc != IntPtr.Zero)
            {
                libvlc_release(_libVlc);
                _libVlc = IntPtr.Zero;
            }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private async Task AwaitPreparationAsync(
            TaskCompletionSource<bool> completion,
            CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(
                       () => completion.TrySetCanceled(cancellationToken)))
            {
                await completion.Task;
            }
        }

        private void EnsureInstance()
        {
            if (_libVlc != IntPtr.Zero)
            {
                return;
            }
            string[] arguments =
            {
                _forceSoftwareDecode ? "--avcodec-hw=none" : "--avcodec-hw=any",
                "--no-video-title-show",
                "--no-stats",
                "--network-caching=1500"
            };
            IntPtr[] strings = new IntPtr[arguments.Length];
            IntPtr array = Marshal.AllocHGlobal(IntPtr.Size * arguments.Length);
            try
            {
                for (int index = 0; index < arguments.Length; index++)
                {
                    strings[index] = AllocateUtf8(arguments[index]);
                    Marshal.WriteIntPtr(array, index * IntPtr.Size, strings[index]);
                }
                _libVlc = libvlc_new(arguments.Length, array);
            }
            finally
            {
                foreach (IntPtr value in strings)
                {
                    if (value != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(value);
                    }
                }
                Marshal.FreeHGlobal(array);
            }
            if (_libVlc == IntPtr.Zero)
            {
                throw NativeException(DecoderLabel + "初始化失败。");
            }
        }

        private void AddMediaOption(IntPtr media, string option)
        {
            IntPtr value = AllocateUtf8(option);
            try
            {
                libvlc_media_add_option(media, value);
            }
            finally
            {
                Marshal.FreeHGlobal(value);
            }
        }

        private void SelectAudioTrack(int requestedOrdinal)
        {
            if (_mediaPlayer == IntPtr.Zero)
            {
                return;
            }

            IntPtr descriptions = libvlc_audio_get_track_description(_mediaPlayer);
            if (descriptions == IntPtr.Zero)
            {
                return;
            }

            try
            {
                int ordinal = 0;
                IntPtr current = descriptions;
                while (current != IntPtr.Zero)
                {
                    LibVlcTrackDescription description =
                        Marshal.PtrToStructure<LibVlcTrackDescription>(current);
                    if (description.Id >= 0)
                    {
                        if (ordinal == requestedOrdinal)
                        {
                            if (libvlc_audio_set_track(_mediaPlayer, description.Id) != 0)
                            {
                                Debug.LogWarning(
                                    "LibVLC could not select audio track ordinal "
                                    + requestedOrdinal + ".");
                            }
                            return;
                        }
                        ordinal++;
                    }
                    current = description.Next;
                }

                Debug.LogWarning(
                    "LibVLC audio track ordinal is unavailable: " + requestedOrdinal + ".");
            }
            finally
            {
                libvlc_track_description_list_release(descriptions);
            }
        }

        private void UploadPendingFrame()
        {
            lock (_frameLock)
            {
                if (_formatChanged)
                {
                    if (_texture != null)
                    {
                        UnityEngine.Object.Destroy(_texture);
                    }
                    _texture = new Texture2D(
                        _frameWidth,
                        _frameHeight,
                        TextureFormat.RGBA32,
                        false,
                        false)
                    {
                        name = "Jellyfin LibVLC Software Frame",
                        wrapMode = TextureWrapMode.Clamp,
                        filterMode = FilterMode.Bilinear
                    };
                    _formatChanged = false;
                }
                if (!_frameReady
                    || _texture == null
                    || _frameBuffer == IntPtr.Zero
                    || _frameBufferSize <= 0)
                {
                    return;
                }
                _texture.LoadRawTextureData(_frameBuffer, _frameBufferSize);
                _texture.Apply(false, false);
                _frameReady = false;
            }
        }

        private void ConfigureFrameBuffer(int width, int height)
        {
            lock (_frameLock)
            {
                if (_frameBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_frameBuffer);
                }
                _frameWidth = Math.Max(1, width);
                _frameHeight = Math.Max(1, height);
                _frameBufferSize = checked(_frameWidth * _frameHeight * 4);
                _frameBuffer = Marshal.AllocHGlobal(_frameBufferSize);
                _frameReady = false;
                _formatChanged = true;
            }
        }

        private void ReleaseFrameResources()
        {
            lock (_frameLock)
            {
                if (_frameBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_frameBuffer);
                    _frameBuffer = IntPtr.Zero;
                }
                _frameBufferSize = 0;
                _frameReady = false;
                _formatChanged = false;
                if (_texture != null)
                {
                    UnityEngine.Object.Destroy(_texture);
                    _texture = null;
                }
            }
        }

        private void FailPreparation(string message)
        {
            _preparing = false;
            _prepared = false;
            _prepareCompletion?.TrySetException(new InvalidOperationException(message));
        }

        private int GetState()
        {
            return _mediaPlayer == IntPtr.Zero
                ? 0
                : libvlc_media_player_get_state(_mediaPlayer);
        }

        private string DecoderLabel
        {
            get
            {
                return _forceSoftwareDecode
                    ? "LibVLC 软件解码"
                    : "LibVLC MediaCodec 硬件优先解码";
            }
        }

        private static LibVlcPlaybackEngine FromOpaque(IntPtr opaque)
        {
            if (opaque == IntPtr.Zero)
            {
                return null;
            }
            try
            {
                return GCHandle.FromIntPtr(opaque).Target as LibVlcPlaybackEngine;
            }
            catch
            {
                return null;
            }
        }

        [AOT.MonoPInvokeCallback(typeof(VideoLockCallback))]
        private static IntPtr HandleVideoLock(IntPtr opaque, IntPtr planes)
        {
            LibVlcPlaybackEngine engine = FromOpaque(opaque);
            if (engine == null)
            {
                return IntPtr.Zero;
            }
            Monitor.Enter(engine._frameLock);
            if (engine._frameBuffer != IntPtr.Zero)
            {
                Marshal.WriteIntPtr(planes, engine._frameBuffer);
            }
            return IntPtr.Zero;
        }

        [AOT.MonoPInvokeCallback(typeof(VideoUnlockCallback))]
        private static void HandleVideoUnlock(IntPtr opaque, IntPtr picture, IntPtr planes)
        {
            LibVlcPlaybackEngine engine = FromOpaque(opaque);
            if (engine != null)
            {
                Monitor.Exit(engine._frameLock);
            }
        }

        [AOT.MonoPInvokeCallback(typeof(VideoDisplayCallback))]
        private static void HandleVideoDisplay(IntPtr opaque, IntPtr picture)
        {
            LibVlcPlaybackEngine engine = FromOpaque(opaque);
            if (engine == null)
            {
                return;
            }
            lock (engine._frameLock)
            {
                engine._frameReady = true;
            }
        }

        [AOT.MonoPInvokeCallback(typeof(VideoFormatCallback))]
        private static uint HandleVideoFormat(
            IntPtr opaquePointer,
            IntPtr chroma,
            ref uint width,
            ref uint height,
            ref uint pitches,
            ref uint lines)
        {
            IntPtr opaque = Marshal.ReadIntPtr(opaquePointer);
            LibVlcPlaybackEngine engine = FromOpaque(opaque);
            if (engine == null || width == 0 || height == 0)
            {
                return 0;
            }
            Marshal.Copy(new[] { (byte)'R', (byte)'G', (byte)'B', (byte)'A' }, 0, chroma, 4);
            pitches = width * 4;
            lines = height;
            engine.ConfigureFrameBuffer((int)width, (int)height);
            return 1;
        }

        [AOT.MonoPInvokeCallback(typeof(VideoCleanupCallback))]
        private static void HandleVideoCleanup(IntPtr opaque)
        {
        }

        private static IntPtr AllocateUtf8(string value)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes((value ?? string.Empty) + "\0");
            IntPtr pointer = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            return pointer;
        }

        private static Exception NativeException(string message)
        {
            IntPtr error = libvlc_errmsg();
            string detail = error != IntPtr.Zero ? Marshal.PtrToStringAnsi(error) : null;
            return new InvalidOperationException(
                string.IsNullOrWhiteSpace(detail) ? message : message + " " + detail);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LibVlcTrackDescription
        {
            public int Id;
            public IntPtr Name;
            public IntPtr Next;
        }

        [DllImport(NativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr libvlc_get_version();

        [DllImport(NativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr libvlc_new(int argumentCount, IntPtr arguments);

        [DllImport(NativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void libvlc_release(IntPtr instance);

        [DllImport(NativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr libvlc_errmsg();

        [DllImport(NativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr libvlc_media_new_location(IntPtr instance, IntPtr url);

        [DllImport(NativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void libvlc_media_add_option(IntPtr media, IntPtr option);

        [DllImport(NativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void libvlc_media_release(IntPtr media);

        [DllImport(NativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr libvlc_media_player_new_from_media(IntPtr media);

        [DllImport(NativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void libvlc_media_player_release(IntPtr mediaPlayer);

        [DllImport(NativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern int libvlc_media_player_play(IntPtr mediaPlayer);

        [DllImport(NativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void libvlc_media_player_stop(IntPtr mediaPlayer);

        [DllImport(NativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void libvlc_media_player_set_pause(IntPtr mediaPlayer, int paused);

        [DllImport(NativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern int libvlc_media_player_get_state(IntPtr mediaPlayer);

        [DllImport(NativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern long libvlc_media_player_get_time(IntPtr mediaPlayer);

        [DllImport(NativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void libvlc_media_player_set_time(IntPtr mediaPlayer, long milliseconds);

        [DllImport(NativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern long libvlc_media_player_get_length(IntPtr mediaPlayer);

        [DllImport(NativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern int libvlc_media_player_is_seekable(IntPtr mediaPlayer);

        [DllImport(NativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr libvlc_audio_get_track_description(IntPtr mediaPlayer);

        [DllImport(NativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern int libvlc_audio_set_track(IntPtr mediaPlayer, int trackId);

        [DllImport(NativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void libvlc_track_description_list_release(IntPtr descriptions);

        [DllImport(NativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void libvlc_video_set_callbacks(
            IntPtr mediaPlayer,
            VideoLockCallback lockCallback,
            VideoUnlockCallback unlockCallback,
            VideoDisplayCallback displayCallback,
            IntPtr opaque);

        [DllImport(NativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void libvlc_video_set_format_callbacks(
            IntPtr mediaPlayer,
            VideoFormatCallback setupCallback,
            VideoCleanupCallback cleanupCallback);
#endif
    }
}
