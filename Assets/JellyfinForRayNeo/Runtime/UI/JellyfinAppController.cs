using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace JellyfinForRayNeo
{
    public sealed class JellyfinAppController : MonoBehaviour
    {
        private enum BrowseHistoryMode
        {
            Reset,
            Push,
            Replace,
            Pop
        }

        private enum DetailHistoryMode
        {
            Reset,
            Push,
            Replace,
            Pop
        }

        private static readonly TimeSpan QuickConnectTimeout = TimeSpan.FromMinutes(5d);
        private static readonly TimeSpan QuickConnectPollInterval = TimeSpan.FromSeconds(1.5d);

        private JellyfinSessionStore _sessionStore;
        private JellyfinApiClient _api;
        private JellyfinImageCache _imageCache;
        private HomeCatalogService _catalog;
        private BrowseCatalogService _browseCatalog;
        private PlaybackReporter _playbackReporter;
        private PlaybackCapabilities _playbackCapabilities;
        private CompanionLoginBridge _companionBridge;
        private DirectionalFocusNavigator _focusNavigator;
        private LoginView _loginView;
        private HomeView _homeView;
        private BrowseView _browseView;
        private DetailView _detailView;
        private PlayerView _playerView;
        private GameObject _loadingOverlay;
        private UiViewMotion _loadingMotion;
        private Text _loadingLabel;
        private GameObject _toast;
        private UiViewMotion _toastMotion;
        private Text _toastLabel;
        private float _toastHideAt;
        private float _nextProgressCheck;
        private bool _stoppingPlayback;
        private bool _recoveringPlayback;
        private bool _loginInProgress;
        private string _pendingServerUrl = "http://";
        private string _pendingUserName = string.Empty;
        private JellyfinPlaybackPlan _currentPlan;
        private JellyfinItem _playingItem;
        private JellyfinPlaybackSelection _playbackSelection;
        private CancellationTokenSource _lifetime;
        private CancellationTokenSource _operation;
        private CancellationTokenSource _homeImages;
        private CancellationTokenSource _browseImages;
        private CancellationTokenSource _detailImages;
        private readonly List<JellyfinBrowseState> _browseHistory =
            new List<JellyfinBrowseState>();
        private readonly List<JellyfinItem> _detailHistory =
            new List<JellyfinItem>();
        private bool _detailReturnsToBrowse;

        private void Start()
        {
#if UNITY_EDITOR
            if (GetComponent<RayNeoEditorInputSimulator>() == null)
            {
                gameObject.AddComponent<RayNeoEditorInputSimulator>();
            }
#endif
            _lifetime = new CancellationTokenSource();
            _companionBridge = new CompanionLoginBridge();
            _companionBridge.LoginRequested += HandleCompanionLoginRequested;
            _companionBridge.QuickConnectRequested += HandleCompanionQuickConnectRequested;
            _companionBridge.QuickConnectCancelRequested += HandleCompanionQuickConnectCancelRequested;
            _companionBridge.SessionAvailable += HandleCompanionSessionAvailable;
            _companionBridge.RemoteCommandReceived += HandleRemoteCommand;
            _companionBridge.PublishState(
                CompanionLoginState.Initializing,
                "正在启动 Jellyfin 客户端…",
                false,
                _pendingServerUrl,
                _pendingUserName);
            InitializeAsync().Forget(HandleFatalError);
        }

        private async Task InitializeAsync()
        {
            _sessionStore = new JellyfinSessionStore();
            string deviceId = _sessionStore.GetOrCreateDeviceId();
            _api = new JellyfinApiClient(deviceId);
            _imageCache = new JellyfinImageCache();
            _catalog = new HomeCatalogService(_api);
            _browseCatalog = new BrowseCatalogService(_api);
            _playbackReporter = new PlaybackReporter(_api);
            _playbackCapabilities = PlaybackCapabilities.Detect();

            Camera camera = UiFactory.EnsureMainCamera();
            UiFactory.EnsureEventSystem();
            Canvas canvas = UiFactory.CreateWorldSpaceCanvas(camera);
            Image appBackground = UiFactory.CreatePanel("App Background", canvas.transform, UiTheme.Background);
            appBackground.raycastTarget = true;
            UiFactory.Stretch(appBackground.rectTransform);

            _loginView = new LoginView(appBackground.transform);
            _homeView = new HomeView(appBackground.transform, _api, _imageCache);
            _browseView = new BrowseView(appBackground.transform, _api, _imageCache);
            _detailView = new DetailView(appBackground.transform);
            _playerView = new PlayerView(appBackground.transform);
            _focusNavigator = new DirectionalFocusNavigator();
            CreateOverlays(appBackground.transform);
            WireEvents();

            _homeView.Show(false);
            _browseView.Hide();
            _detailView.Hide();
            _loginView.Show(true);
            _focusNavigator.SetScope(_loginView.FocusRoot);

            JellyfinSession saved;
            if (!_sessionStore.TryLoad(out saved))
            {
                ShowLogin(
                    "请在手机端选择自动发现的 Jellyfin 服务器，或手动输入地址。",
                    false,
                    "http://",
                    string.Empty);
                return;
            }

            _pendingServerUrl = saved.ServerUrl;
            _pendingUserName = saved.UserName;
            _api.SetSession(saved);
            _loginInProgress = true;
            _loginView.SetBusy(true);
            _companionBridge.PublishState(
                CompanionLoginState.Connecting,
                "正在恢复登录并同步媒体库…",
                false,
                saved.ServerUrl,
                saved.UserName);
            ShowLoading(true, "正在恢复登录并同步媒体库…");
            try
            {
                await LoadHomeAsync(saved, _lifetime.Token);
            }
            catch (JellyfinApiException exception) when (exception.IsUnauthorized)
            {
                _sessionStore.ClearSession();
                _api.ClearSession();
                _companionBridge.ClearNativeSession();
                ShowLogin("登录会话已失效，请重新输入密码。", true);
            }
            catch (Exception exception)
            {
                ShowLogin("自动连接失败：" + UserMessage(exception), true);
            }
            finally
            {
                _loginInProgress = false;
                _loginView.SetBusy(false);
                ShowLoading(false);
            }
        }

        private void WireEvents()
        {
            _homeView.ItemSelected += OpenItem;
            _homeView.SearchRequested += OpenSearch;
            _homeView.FavoritesRequested += OpenFavorites;
            _homeView.RefreshRequested += () => RefreshHomeAsync().Forget(HandleFatalError);
            _homeView.LogoutRequested += Logout;
            _browseView.ItemSelected += OpenItem;
            _browseView.BackRequested += NavigateBrowseBack;
            _browseView.HomeRequested += CloseBrowseToHome;
            _browseView.SearchModeRequested += OpenSearch;
            _browseView.FavoritesRequested += OpenFavorites;
            _browseView.SearchSubmitted += query => SubmitSearchAsync(query).Forget(HandleFatalError);
            _browseView.SortRequested += () => CycleBrowseSortAsync().Forget(HandleFatalError);
            _browseView.FilterRequested += () => CycleBrowseFilterAsync().Forget(HandleFatalError);
            _browseView.PreviousPageRequested += () => MoveBrowsePageAsync(-1).Forget(HandleFatalError);
            _browseView.NextPageRequested += () => MoveBrowsePageAsync(1).Forget(HandleFatalError);
            _detailView.CloseRequested += CloseDetails;
            _detailView.PlayRequested += (item, position) => PlayAsync(item, position).Forget(HandleFatalError);
            _detailView.FavoriteStateChangeRequested += (item, isFavorite) =>
                SetFavoriteStateAsync(item, isFavorite).Forget(HandleFatalError);
            _detailView.PlayedStateChangeRequested += (item, isPlayed) =>
                SetPlayedStateAsync(item, isPlayed).Forget(HandleFatalError);
            _detailView.RelatedItemSelected += OpenRelatedItem;
            _playerView.BackRequested += () => StopPlaybackAsync(false, null).Forget(HandleFatalError);
            _playerView.PauseStateChanged += paused => ReportForcedProgressAsync(paused).Forget(HandleNonFatalError);
            _playerView.PlaybackFailed += message =>
                RecoverPlaybackAsync(message).Forget(HandleFatalError);
            _playerView.PlaybackCompleted += () => StopPlaybackAsync(false, null, true).Forget(HandleFatalError);
            _playerView.TrackSelectionRequested += (audioIndex, subtitleIndex) =>
                ChangeTracksAsync(audioIndex, subtitleIndex).Forget(HandleFatalError);
        }

        private void HandleRemoteCommand(CompanionRemoteCommand command)
        {
            if (command == CompanionRemoteCommand.Back)
            {
                HandleRemoteBack();
                return;
            }

            if (_playerView != null && _playerView.IsVisible)
            {
                _playerView.HandleRemoteCommand(command, _focusNavigator);
                return;
            }

            _focusNavigator?.Handle(command);
        }

        private void SelectInScope(Transform scope, params string[] preferredNames)
        {
            if (_focusNavigator == null)
            {
                return;
            }

            _focusNavigator.SetScope(scope);
            _focusNavigator.SelectPreferred(preferredNames);
        }

        private void HandleRemoteBack()
        {
            if (_playerView != null && _playerView.IsVisible)
            {
                if (_playerView.CloseTransientUi())
                {
                    SelectInScope(
                        _playerView.FocusRoot,
                        "Audio Tracks",
                        "Subtitle Tracks",
                        "Play Pause");
                    return;
                }

                StopPlaybackAsync(false, null).Forget(HandleFatalError);
                return;
            }

            if (_detailView != null && _detailView.IsVisible)
            {
                CloseDetails();
                return;
            }

            if (_browseView != null && _browseView.IsVisible)
            {
                NavigateBrowseBack();
            }
        }

        private void CloseDetails()
        {
            if (_detailHistory.Count > 1)
            {
                JellyfinItem previous = _detailHistory[_detailHistory.Count - 2];
                ShowDetailsAsync(previous, DetailHistoryMode.Pop).Forget(HandleFatalError);
                return;
            }

            _detailView?.Hide();
            CancelAndDispose(ref _detailImages);
            _detailHistory.Clear();
            if (_detailReturnsToBrowse && _browseView != null)
            {
                _browseView.Show();
                SelectInScope(
                    _browseView.FocusRoot,
                    "Poster Card",
                    "Search Input",
                    "Browse Back");
            }
            else
            {
                _homeView?.Show(true);
                SelectInScope(
                    _homeView.FocusRoot,
                    "Hero Action",
                    "Poster Card",
                    "Home Search");
            }
        }

        private void OpenItem(JellyfinItem item)
        {
            if (item == null)
            {
                return;
            }

            if (item.IsBrowsableContainer)
            {
                JellyfinBrowseState state = string.Equals(
                    item.Type,
                    "CollectionFolder",
                    StringComparison.OrdinalIgnoreCase)
                    ? JellyfinBrowseState.ForLibrary(item)
                    : JellyfinBrowseState.ForFolder(item);
                BrowseHistoryMode historyMode = _browseView != null && _browseView.IsVisible
                    ? BrowseHistoryMode.Push
                    : BrowseHistoryMode.Reset;
                ShowBrowseAsync(state, historyMode).Forget(HandleFatalError);
                return;
            }

            ShowDetailsAsync(item, DetailHistoryMode.Reset).Forget(HandleFatalError);
        }

        private void OpenRelatedItem(JellyfinItem item)
        {
            if (item == null)
            {
                return;
            }

            if (item.IsBrowsableContainer)
            {
                JellyfinBrowseState state = JellyfinBrowseState.ForFolder(item);
                _detailView.Hide();
                ShowBrowseAsync(state, BrowseHistoryMode.Push).Forget(HandleFatalError);
                return;
            }

            ShowDetailsAsync(item, DetailHistoryMode.Push).Forget(HandleFatalError);
        }

        private void OpenSearch()
        {
            JellyfinBrowseState current = _browseView != null
                ? _browseView.CurrentState
                : null;
            if (_browseView != null && _browseView.IsVisible && current != null && current.IsSearch)
            {
                SelectInScope(_browseView.FocusRoot, "Search Input", "Submit Search");
                return;
            }

            BrowseHistoryMode historyMode = _browseView != null && _browseView.IsVisible
                ? BrowseHistoryMode.Push
                : BrowseHistoryMode.Reset;
            ShowBrowseAsync(
                JellyfinBrowseState.ForSearch(),
                historyMode).Forget(HandleFatalError);
        }

        private void OpenFavorites()
        {
            BrowseHistoryMode historyMode = _browseView != null && _browseView.IsVisible
                ? BrowseHistoryMode.Push
                : BrowseHistoryMode.Reset;
            ShowBrowseAsync(
                JellyfinBrowseState.ForFavorites(),
                historyMode).Forget(HandleFatalError);
        }

        private void NavigateBrowseBack()
        {
            if (_browseHistory.Count <= 1)
            {
                CloseBrowseToHome();
                return;
            }

            JellyfinBrowseState target = _browseHistory[_browseHistory.Count - 2].Clone();
            ShowBrowseAsync(target, BrowseHistoryMode.Pop).Forget(HandleFatalError);
        }

        private void CloseBrowseToHome()
        {
            CancelAndDispose(ref _operation);
            CancelAndDispose(ref _browseImages);
            _browseHistory.Clear();
            _detailHistory.Clear();
            _browseView?.Hide();
            _detailView?.Hide();
            _homeView?.Show(true);
            _detailReturnsToBrowse = false;
            SelectInScope(_homeView.FocusRoot, "Hero Action", "Poster Card", "Home Search");
        }

        private async Task SubmitSearchAsync(string query)
        {
            JellyfinBrowseState state = _browseView != null
                ? _browseView.CurrentState
                : JellyfinBrowseState.ForSearch();
            if (state == null || !state.IsSearch)
            {
                state = JellyfinBrowseState.ForSearch();
            }
            state.SearchTerm = query != null ? query.Trim() : string.Empty;
            state.Title = string.IsNullOrWhiteSpace(state.SearchTerm)
                ? "搜索"
                : "搜索 · " + state.SearchTerm;
            state.StartIndex = 0;
            await ShowBrowseAsync(state, BrowseHistoryMode.Replace);
        }

        private async Task CycleBrowseSortAsync()
        {
            JellyfinBrowseState state = _browseView != null
                ? _browseView.CurrentState
                : null;
            if (state == null)
            {
                return;
            }

            state.Sort = (JellyfinBrowseSort)(((int)state.Sort + 1) % 3);
            state.StartIndex = 0;
            await ShowBrowseAsync(state, BrowseHistoryMode.Replace);
        }

        private async Task CycleBrowseFilterAsync()
        {
            JellyfinBrowseState state = _browseView != null
                ? _browseView.CurrentState
                : null;
            if (state == null)
            {
                return;
            }

            if (string.Equals(state.Title, "我的收藏", StringComparison.Ordinal)
                && state.Filter == JellyfinBrowseFilter.Favorite)
            {
                ShowToast("收藏页已经只显示收藏内容。", false);
                return;
            }

            state.Filter = (JellyfinBrowseFilter)(((int)state.Filter + 1) % 4);
            state.StartIndex = 0;
            await ShowBrowseAsync(state, BrowseHistoryMode.Replace);
        }

        private async Task MoveBrowsePageAsync(int direction)
        {
            JellyfinBrowseState state = _browseView != null
                ? _browseView.CurrentState
                : null;
            if (state == null || direction == 0)
            {
                return;
            }

            state.StartIndex = Math.Max(
                0,
                state.StartIndex + Math.Sign(direction) * Math.Max(1, state.PageSize));
            await ShowBrowseAsync(state, BrowseHistoryMode.Replace);
        }

        private async Task ShowBrowseAsync(
            JellyfinBrowseState state,
            BrowseHistoryMode historyMode)
        {
            if (state == null || _api.Session == null)
            {
                return;
            }

            CancellationToken token = BeginOperation();
            ShowLoading(true, state.IsSearch ? "正在搜索 Jellyfin…" : "正在加载媒体库…");
            try
            {
                JellyfinQueryResult result = await _browseCatalog.LoadPageAsync(state, token);
                token.ThrowIfCancellationRequested();
                ReplaceBrowseImageToken();
                ApplyBrowseHistory(state, historyMode);
                _homeView.Show(false);
                _detailView.Hide();
                _browseView.SetPage(state, result, _browseImages.Token);
                _detailReturnsToBrowse = false;
                SelectInScope(
                    _browseView.FocusRoot,
                    state.IsSearch && string.IsNullOrWhiteSpace(state.SearchTerm)
                        ? "Search Input"
                        : "Poster Card",
                    "Search Input",
                    "Browse Back");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                ShowToast(UserMessage(exception), true);
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private void ApplyBrowseHistory(
            JellyfinBrowseState state,
            BrowseHistoryMode historyMode)
        {
            JellyfinBrowseState snapshot = state.Clone();
            switch (historyMode)
            {
                case BrowseHistoryMode.Reset:
                    _browseHistory.Clear();
                    _browseHistory.Add(snapshot);
                    break;
                case BrowseHistoryMode.Push:
                    _browseHistory.Add(snapshot);
                    break;
                case BrowseHistoryMode.Pop:
                    if (_browseHistory.Count > 1)
                    {
                        _browseHistory.RemoveAt(_browseHistory.Count - 1);
                    }
                    if (_browseHistory.Count == 0)
                    {
                        _browseHistory.Add(snapshot);
                    }
                    else
                    {
                        _browseHistory[_browseHistory.Count - 1] = snapshot;
                    }
                    break;
                default:
                    if (_browseHistory.Count == 0)
                    {
                        _browseHistory.Add(snapshot);
                    }
                    else
                    {
                        _browseHistory[_browseHistory.Count - 1] = snapshot;
                    }
                    break;
            }
        }

        private void HandleCompanionLoginRequested(CompanionLoginRequest request)
        {
            if (request == null)
            {
                return;
            }

            if (_loginInProgress)
            {
                _companionBridge.PublishState(
                    CompanionLoginState.Connecting,
                    "连接正在进行，请稍候…",
                    false,
                    _pendingServerUrl,
                    _pendingUserName);
                return;
            }

            LoginAsync(request.ServerUrl, request.UserName, request.Password).Forget(HandleFatalError);
        }

        private void HandleCompanionQuickConnectRequested(CompanionQuickConnectRequest request)
        {
            if (request == null)
            {
                return;
            }

            if (_loginInProgress)
            {
                _companionBridge.PublishState(
                    CompanionLoginState.Connecting,
                    "连接正在进行，请稍候…",
                    false,
                    _pendingServerUrl,
                    _pendingUserName);
                return;
            }

            QuickConnectAsync(request.ServerUrl).Forget(HandleFatalError);
        }

        private void HandleCompanionQuickConnectCancelRequested()
        {
            CancelAndDispose(ref _operation);
            _loginInProgress = false;
            _loginView.SetBusy(false);
            ShowLogin(
                "已取消快速登录，你可以重新申请登录码或使用帐号密码。",
                false,
                _pendingServerUrl,
                _pendingUserName);
        }

        private void HandleCompanionSessionAvailable(CompanionSessionRequest request)
        {
            if (request == null || _api == null || _sessionStore == null)
            {
                return;
            }

            JellyfinSession session = request.ToSession();
            JellyfinSession current = _api.Session;
            if (current != null
                && string.Equals(
                    current.AccessToken,
                    session.AccessToken,
                    StringComparison.Ordinal))
            {
                return;
            }

            AdoptCompanionSessionAsync(session).Forget(HandleFatalError);
        }

        private async Task AdoptCompanionSessionAsync(JellyfinSession session)
        {
            _loginInProgress = true;
            _pendingServerUrl = session.ServerUrl;
            _pendingUserName = session.UserName ?? string.Empty;
            CancellationToken token = BeginOperation();
            _loginView.SetBusy(true);
            _loginView.SetMessage("手机端已登录，正在同步媒体库…", false);
            _companionBridge.PublishState(
                CompanionLoginState.Connecting,
                "登录成功，正在为眼镜同步媒体库…",
                false,
                session.ServerUrl,
                session.UserName);
            ShowLoading(true, "正在同步海报墙与观看记录…");

            try
            {
                _api.SetSession(session);
                _sessionStore.Save(session);
                await LoadHomeAsync(session, token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (JellyfinApiException exception) when (exception.IsUnauthorized)
            {
                _sessionStore.ClearSession();
                _api.ClearSession();
                _companionBridge.ClearNativeSession();
                ShowLogin("手机端会话已失效，请重新登录。", true, session.ServerUrl, session.UserName);
            }
            catch (Exception exception)
            {
                ShowLogin(
                    "手机已登录，但媒体库同步失败：" + UserMessage(exception),
                    true,
                    session.ServerUrl,
                    session.UserName);
            }
            finally
            {
                _loginInProgress = false;
                _loginView.SetBusy(false);
                ShowLoading(false);
            }
        }

        private async Task LoginAsync(string serverInput, string username, string password)
        {
            _loginInProgress = true;
            _pendingServerUrl = serverInput != null ? serverInput.Trim() : string.Empty;
            _pendingUserName = username != null ? username.Trim() : string.Empty;
            CancellationToken token = BeginOperation();
            _loginView.SetBusy(true);
            _loginView.SetMessage("正在验证服务器与用户…", false);
            _companionBridge.PublishState(
                CompanionLoginState.Connecting,
                "正在验证服务器与用户…",
                false,
                _pendingServerUrl,
                _pendingUserName);
            try
            {
                string serverUrl = JellyfinUrl.NormalizeServerUrl(serverInput);
                _pendingServerUrl = serverUrl;
                JellyfinPublicSystemInfo publicInfo = await _api.GetPublicSystemInfoAsync(serverUrl, token);
                JellyfinAuthenticationResult authentication = await _api.AuthenticateAsync(serverUrl, username, password, token);
                JellyfinSession session = CreateSession(
                    serverUrl,
                    publicInfo,
                    authentication,
                    "/Users/AuthenticateByName");

                _api.SetSession(session);
                _sessionStore.Save(session);
                await LoadHomeAsync(session, token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                ShowLogin(
                    UserMessage(exception),
                    true,
                    _pendingServerUrl,
                    _pendingUserName);
            }
            finally
            {
                _loginInProgress = false;
                _loginView.SetBusy(false);
            }
        }

        private async Task QuickConnectAsync(string serverInput)
        {
            _loginInProgress = true;
            _pendingServerUrl = serverInput != null ? serverInput.Trim() : string.Empty;
            _pendingUserName = string.Empty;
            CancellationToken token = BeginOperation();
            _loginView.SetBusy(true);
            _loginView.SetMessage("正在检查 Jellyfin 快速登录…", false);
            _companionBridge.PublishState(
                CompanionLoginState.Connecting,
                "正在检查服务器是否支持快速登录…",
                false,
                _pendingServerUrl,
                string.Empty);

            try
            {
                string serverUrl = JellyfinUrl.NormalizeServerUrl(serverInput);
                _pendingServerUrl = serverUrl;
                JellyfinPublicSystemInfo publicInfo =
                    await _api.GetPublicSystemInfoAsync(serverUrl, token);
                bool enabled = await _api.GetQuickConnectEnabledAsync(serverUrl, token);
                if (!enabled)
                {
                    throw new JellyfinApiException(
                        "此 Jellyfin 服务器未启用快速连接，请在服务器设置中开启或使用帐号密码。",
                        0,
                        "/QuickConnect/Enabled");
                }

                JellyfinQuickConnectResult request =
                    await _api.InitiateQuickConnectAsync(serverUrl, token);
                if (request == null
                    || string.IsNullOrWhiteSpace(request.Secret)
                    || string.IsNullOrWhiteSpace(request.Code))
                {
                    throw new JellyfinApiException(
                        "服务器未返回有效的快速登录码。",
                        0,
                        "/QuickConnect/Initiate");
                }

                string code = request.Code.Trim();
                _loginView.SetMessage(
                    "快速登录码：" + code + "\n请在手机上的 Jellyfin 中授权。",
                    false);
                _companionBridge.PublishState(
                    CompanionLoginState.QuickConnectWaiting,
                    "请在已登录的 Jellyfin App 或网页中授权此登录码。",
                    false,
                    serverUrl,
                    string.Empty,
                    code);

                DateTime expiresAt = DateTime.UtcNow.Add(QuickConnectTimeout);
                JellyfinQuickConnectResult state = request;
                while (!state.Authenticated)
                {
                    if (DateTime.UtcNow >= expiresAt)
                    {
                        throw new JellyfinApiException(
                            "快速登录码已过期，请在手机端重新申请。",
                            408,
                            "/QuickConnect/Connect");
                    }

                    await Task.Delay(QuickConnectPollInterval, token);
                    state = await _api.GetQuickConnectStateAsync(
                        serverUrl,
                        request.Secret,
                        token);
                    if (state == null)
                    {
                        throw new JellyfinApiException(
                            "服务器未返回快速登录状态。",
                            0,
                            "/QuickConnect/Connect");
                    }
                }

                _companionBridge.PublishState(
                    CompanionLoginState.Connecting,
                    "登录码已授权，正在同步媒体库…",
                    false,
                    serverUrl,
                    string.Empty);
                JellyfinAuthenticationResult authentication =
                    await _api.AuthenticateWithQuickConnectAsync(
                        serverUrl,
                        request.Secret,
                        token);
                JellyfinSession session = CreateSession(
                    serverUrl,
                    publicInfo,
                    authentication,
                    "/Users/AuthenticateWithQuickConnect");

                _api.SetSession(session);
                _sessionStore.Save(session);
                await LoadHomeAsync(session, token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (JellyfinApiException exception) when (exception.StatusCode == 404)
            {
                ShowLogin(
                    "快速登录码已失效，请重新申请。",
                    true,
                    _pendingServerUrl,
                    string.Empty);
            }
            catch (Exception exception)
            {
                ShowLogin(
                    UserMessage(exception),
                    true,
                    _pendingServerUrl,
                    string.Empty);
            }
            finally
            {
                _loginInProgress = false;
                _loginView.SetBusy(false);
            }
        }

        private JellyfinSession CreateSession(
            string serverUrl,
            JellyfinPublicSystemInfo publicInfo,
            JellyfinAuthenticationResult authentication,
            string endpoint)
        {
            if (authentication == null
                || authentication.User == null
                || string.IsNullOrWhiteSpace(authentication.User.Id)
                || string.IsNullOrWhiteSpace(authentication.AccessToken))
            {
                throw new JellyfinApiException(
                    "服务器未返回有效登录会话。",
                    0,
                    endpoint);
            }

            return new JellyfinSession
            {
                ServerUrl = serverUrl,
                ServerName = publicInfo != null ? publicInfo.ServerName : null,
                ServerVersion = publicInfo != null ? publicInfo.Version : null,
                ServerId = !string.IsNullOrWhiteSpace(authentication.ServerId)
                    ? authentication.ServerId
                    : publicInfo != null ? publicInfo.Id : null,
                AccessToken = authentication.AccessToken,
                UserId = authentication.User.Id,
                UserName = authentication.User.Name,
                DeviceId = _sessionStore.GetOrCreateDeviceId()
            };
        }

        private async Task LoadHomeAsync(JellyfinSession session, CancellationToken cancellationToken)
        {
            ShowLoading(true, "正在加载海报墙与观看记录…");
            List<JellyfinHomeSection> sections = await _catalog.LoadHomeAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            ReplaceHomeImageToken();
            _homeView.SetHeader(session);
            _homeView.SetSections(sections, _homeImages.Token);
            _loginView.Show(false);
            _browseView.Hide();
            _detailView.Hide();
            _homeView.Show(true);
            _browseHistory.Clear();
            _detailHistory.Clear();
            _detailReturnsToBrowse = false;
            SelectInScope(_homeView.FocusRoot, "Hero Action", "Poster Card", "Refresh");
            _pendingServerUrl = session.ServerUrl;
            _pendingUserName = session.UserName;
            _companionBridge.PublishState(
                CompanionLoginState.Ready,
                "已连接，媒体库正在眼镜中显示。",
                false,
                session.ServerUrl,
                session.UserName);
            ShowLoading(false);
        }

        private async Task RefreshHomeAsync()
        {
            if (_api.Session == null)
            {
                return;
            }

            CancellationToken token = BeginOperation();
            try
            {
                await LoadHomeAsync(_api.Session, token);
                ShowToast("媒体库已刷新", false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                ShowLoading(false);
                ShowToast(UserMessage(exception), true);
            }
        }

        private async Task ShowDetailsAsync(
            JellyfinItem item,
            DetailHistoryMode historyMode = DetailHistoryMode.Replace)
        {
            if (item == null)
            {
                return;
            }

            bool returnsToBrowse = historyMode == DetailHistoryMode.Reset
                ? _browseView != null && _browseView.IsVisible
                : _detailReturnsToBrowse;
            CancellationToken token = BeginOperation();
            ShowLoading(true, "正在加载详情与剧集…");
            try
            {
                JellyfinItem details = await _api.GetItemAsync(item.Id, token);
                token.ThrowIfCancellationRequested();
                JellyfinItem resolvedItem = details ?? item;
                Task<List<JellyfinItem>> episodesTask =
                    GetEpisodesForDetailAsync(resolvedItem, token);
                Task<List<JellyfinItem>> seasonsTask =
                    GetSeasonsForDetailAsync(resolvedItem, token);
                Task<List<JellyfinItem>> similarTask =
                    GetSimilarForDetailAsync(resolvedItem, token);
                await Task.WhenAll(episodesTask, seasonsTask, similarTask);
                token.ThrowIfCancellationRequested();
                ReplaceDetailImageToken();
                _detailReturnsToBrowse = returnsToBrowse;
                ApplyDetailHistory(resolvedItem, historyMode);
                _homeView.Show(false);
                _browseView.Hide();
                _detailView.Show(
                    resolvedItem,
                    _api,
                    _imageCache,
                    _detailImages.Token,
                    episodesTask.Result,
                    seasonsTask.Result,
                    similarTask.Result);
                SelectInScope(_detailView.FocusRoot, "Continue", "From Start", "Close");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                ShowToast(UserMessage(exception), true);
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private async Task<List<JellyfinItem>> GetSeasonsForDetailAsync(
            JellyfinItem item,
            CancellationToken cancellationToken)
        {
            string seriesId = null;
            if (item != null && string.Equals(item.Type, "Series", StringComparison.OrdinalIgnoreCase))
            {
                seriesId = item.Id;
            }
            else if (item != null
                && (string.Equals(item.Type, "Season", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Type, "Episode", StringComparison.OrdinalIgnoreCase)))
            {
                seriesId = item.SeriesId;
            }
            if (string.IsNullOrWhiteSpace(seriesId))
            {
                return new List<JellyfinItem>();
            }

            try
            {
                JellyfinQueryResult result = await _api.GetSeasonsAsync(seriesId, cancellationToken);
                return result != null && result.Items != null
                    ? result.Items
                    : new List<JellyfinItem>();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                HandleNonFatalError(exception);
                return new List<JellyfinItem>();
            }
        }

        private async Task<List<JellyfinItem>> GetSimilarForDetailAsync(
            JellyfinItem item,
            CancellationToken cancellationToken)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id) || item.IsBrowsableContainer)
            {
                return new List<JellyfinItem>();
            }

            try
            {
                JellyfinQueryResult result = await _api.GetSimilarItemsAsync(
                    item.Id,
                    16,
                    cancellationToken);
                return result != null && result.Items != null
                    ? result.Items
                    : new List<JellyfinItem>();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                HandleNonFatalError(exception);
                return new List<JellyfinItem>();
            }
        }

        private void ApplyDetailHistory(
            JellyfinItem item,
            DetailHistoryMode historyMode)
        {
            switch (historyMode)
            {
                case DetailHistoryMode.Reset:
                    _detailHistory.Clear();
                    _detailHistory.Add(item);
                    break;
                case DetailHistoryMode.Push:
                    _detailHistory.Add(item);
                    break;
                case DetailHistoryMode.Pop:
                    if (_detailHistory.Count > 1)
                    {
                        _detailHistory.RemoveAt(_detailHistory.Count - 1);
                    }
                    if (_detailHistory.Count == 0)
                    {
                        _detailHistory.Add(item);
                    }
                    else
                    {
                        _detailHistory[_detailHistory.Count - 1] = item;
                    }
                    break;
                default:
                    if (_detailHistory.Count == 0)
                    {
                        _detailHistory.Add(item);
                    }
                    else
                    {
                        _detailHistory[_detailHistory.Count - 1] = item;
                    }
                    break;
            }
        }

        private async Task<List<JellyfinItem>> GetEpisodesForDetailAsync(
            JellyfinItem item,
            CancellationToken cancellationToken)
        {
            string seriesId = null;
            string seasonId = null;
            if (item != null && string.Equals(item.Type, "Series", StringComparison.OrdinalIgnoreCase))
            {
                seriesId = item.Id;
            }
            else if (item != null && string.Equals(item.Type, "Season", StringComparison.OrdinalIgnoreCase))
            {
                seriesId = item.SeriesId;
                seasonId = item.Id;
            }
            else if (item != null && string.Equals(item.Type, "Episode", StringComparison.OrdinalIgnoreCase))
            {
                seriesId = item.SeriesId;
                seasonId = item.SeasonId;
            }
            if (string.IsNullOrWhiteSpace(seriesId))
            {
                return new List<JellyfinItem>();
            }

            JellyfinQueryResult result = await _api.GetEpisodesAsync(
                seriesId,
                seasonId,
                500,
                cancellationToken);
            return result != null && result.Items != null
                ? result.Items
                : new List<JellyfinItem>();
        }

        private async Task SetFavoriteStateAsync(JellyfinItem item, bool isFavorite)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
            {
                return;
            }

            CancellationToken token = BeginOperation();
            _detailView.SetUserActionBusy(true);
            try
            {
                JellyfinUserData userData = await _api.SetFavoriteAsync(item.Id, isFavorite, token);
                userData = await ResolveUserDataAsync(item, userData, token);
                token.ThrowIfCancellationRequested();
                if (IsCurrentDetail(item))
                {
                    _detailView.ApplyUserData(userData);
                    ShowToast(isFavorite ? "已加入收藏" : "已取消收藏", false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                ShowToast("收藏状态同步失败：" + UserMessage(exception), true);
            }
            finally
            {
                if (IsCurrentDetail(item))
                {
                    _detailView.SetUserActionBusy(false);
                }
            }
        }

        private async Task SetPlayedStateAsync(JellyfinItem item, bool isPlayed)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
            {
                return;
            }

            CancellationToken token = BeginOperation();
            _detailView.SetUserActionBusy(true);
            try
            {
                JellyfinUserData userData = await _api.SetPlayedAsync(item.Id, isPlayed, token);
                userData = await ResolveUserDataAsync(item, userData, token);
                token.ThrowIfCancellationRequested();
                if (IsCurrentDetail(item))
                {
                    _detailView.ApplyUserData(userData);
                    ShowToast(isPlayed ? "已标记为看完" : "已标记为未看", false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                ShowToast("观看状态同步失败：" + UserMessage(exception), true);
            }
            finally
            {
                if (IsCurrentDetail(item))
                {
                    _detailView.SetUserActionBusy(false);
                }
            }
        }

        private async Task<JellyfinUserData> ResolveUserDataAsync(
            JellyfinItem item,
            JellyfinUserData userData,
            CancellationToken cancellationToken)
        {
            if (userData != null)
            {
                return userData;
            }

            JellyfinItem refreshed = await _api.GetItemAsync(item.Id, cancellationToken);
            return refreshed != null && refreshed.UserData != null
                ? refreshed.UserData
                : item.UserData ?? new JellyfinUserData();
        }

        private bool IsCurrentDetail(JellyfinItem item)
        {
            JellyfinItem current = _detailView != null ? _detailView.CurrentItem : null;
            return current != null
                && item != null
                && string.Equals(current.Id, item.Id, StringComparison.OrdinalIgnoreCase);
        }

        private async Task PlayAsync(JellyfinItem item, long startPositionTicks)
        {
            if (item == null || !item.IsPlayable)
            {
                ShowToast("请选择一部电影或具体剧集。", true);
                return;
            }

            CancellationToken token = BeginOperation();
            ShowLoading(true, "正在与 Jellyfin 协商播放格式…");
            _playingItem = item;
            _playbackSelection = new JellyfinPlaybackSelection();
            try
            {
                List<JellyfinPlaybackPlan> plans = await _api.GetPlaybackPlansAsync(
                    item,
                    startPositionTicks,
                    _playbackSelection,
                    _playbackCapabilities,
                    token);
                ShowLoading(false);
                await StartFirstWorkingPlanAsync(plans, null, token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                _playerView.Stop();
                _currentPlan = null;
                _playingItem = null;
                _playbackSelection = null;
                SelectInScope(_detailView.FocusRoot, "Continue", "From Start", "Close");
                ShowToast("无法播放：" + UserMessage(exception), true);
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private async Task StartFirstWorkingPlanAsync(
            IEnumerable<JellyfinPlaybackPlan> plans,
            PlaybackTier? afterTier,
            CancellationToken cancellationToken)
        {
            Exception lastError = null;
            foreach (JellyfinPlaybackPlan plan in plans ?? Enumerable.Empty<JellyfinPlaybackPlan>())
            {
                if (plan == null || afterTier.HasValue && plan.Tier <= afterTier.Value)
                {
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    _currentPlan = plan;
                    _playbackReporter.Reset();
                    _playerView.SetSubtitleTrack(null);
                    _focusNavigator.SetScope(_playerView.FocusRoot);
                    _focusNavigator.ClearSelection();
                    await _playerView.PrepareAndPlayAsync(plan, cancellationToken);
                    SelectInScope(
                        _playerView.FocusRoot,
                        "Play Pause",
                        "Audio Tracks",
                        "Subtitle Tracks");
                    try
                    {
                        await _playbackReporter.StartAsync(
                            plan,
                            _playerView.CurrentPositionTicks,
                            cancellationToken);
                    }
                    catch (Exception reportException)
                    {
                        HandleNonFatalError(reportException);
                    }
                    _nextProgressCheck = Time.unscaledTime + 2f;
                    LoadSubtitleAsync(plan, cancellationToken).Forget(HandleNonFatalError);
                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    lastError = exception;
                    Debug.LogWarning(
                        plan.TierLabel + " playback path failed before start: "
                        + exception.Message);
                    _playerView.Stop();
                    _currentPlan = null;
                    _playbackReporter.Reset();
                }
            }

            throw lastError ?? new InvalidOperationException("没有剩余的播放降级路径。");
        }

        private async Task LoadSubtitleAsync(
            JellyfinPlaybackPlan plan,
            CancellationToken cancellationToken)
        {
            if (plan == null
                || plan.SubtitleBurnedIn
                || string.IsNullOrWhiteSpace(plan.SubtitleUrl))
            {
                _playerView.SetSubtitleTrack(null);
                return;
            }

            try
            {
                string content = await _api.GetSubtitleTextAsync(plan, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                _playerView.SetSubtitleTrack(
                    SubtitleParser.Parse(content, plan.SubtitleCodec));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _playerView.SetSubtitleTrack(null);
                HandleNonFatalError(exception);
                ShowToast("字幕加载失败，视频将继续播放。", true);
            }
        }

        private async Task RecoverPlaybackAsync(string errorMessage)
        {
            if (_recoveringPlayback || _currentPlan == null || _playingItem == null)
            {
                return;
            }

            _recoveringPlayback = true;
            long positionTicks = _playerView.CurrentPositionTicks;
            PlaybackTier failedTier = _currentPlan.Tier;
            CancellationToken token = BeginOperation();
            ShowLoading(
                true,
                failedTier == PlaybackTier.HardwareDirect
                    ? "系统硬件路径失败，正在切换兼容容器硬件解码…"
                    : failedTier == PlaybackTier.HardwareLibVlcDirect
                        ? "兼容硬件路径失败，正在切换本地软件解码…"
                        : "本地解码失败，正在请求 Jellyfin 服务器转码…");
            try
            {
                await StopCurrentReportForFallbackAsync(positionTicks, true, token);
                _playerView.Stop();
                _currentPlan = null;
                List<JellyfinPlaybackPlan> plans = await _api.GetPlaybackPlansAsync(
                    _playingItem,
                    positionTicks,
                    _playbackSelection,
                    _playbackCapabilities,
                    token);
                await StartFirstWorkingPlanAsync(plans, failedTier, token);
                ShowToast("已自动切换到" + _currentPlan.TierLabel + "。", false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                _playerView.Stop();
                _currentPlan = null;
                _playingItem = null;
                _playbackSelection = null;
                _playbackReporter.Reset();
                SelectInScope(_detailView.FocusRoot, "Continue", "From Start", "Close");
                string detail = string.IsNullOrWhiteSpace(errorMessage)
                    ? UserMessage(exception)
                    : errorMessage + "；" + UserMessage(exception);
                ShowToast("所有播放路径均失败：" + detail, true);
            }
            finally
            {
                ShowLoading(false);
                _recoveringPlayback = false;
            }
        }

        private async Task ChangeTracksAsync(
            int? audioStreamIndex,
            int? subtitleStreamIndex)
        {
            if (_recoveringPlayback || _currentPlan == null || _playingItem == null)
            {
                return;
            }

            _recoveringPlayback = true;
            long positionTicks = _playerView.CurrentPositionTicks;
            JellyfinPlaybackSelection previous = _playbackSelection;
            JellyfinPlaybackSelection requested = new JellyfinPlaybackSelection
            {
                MediaSourceId = _currentPlan.MediaSource != null
                    ? _currentPlan.MediaSource.Id
                    : null,
                AudioStreamIndex = audioStreamIndex,
                SubtitleStreamIndex = subtitleStreamIndex
            };
            CancellationToken token = BeginOperation();
            ShowLoading(true, "正在切换音轨与字幕…");
            try
            {
                await StopCurrentReportForFallbackAsync(positionTicks, false, token);
                _playerView.Stop();
                _currentPlan = null;
                _playbackSelection = requested;
                List<JellyfinPlaybackPlan> plans = await _api.GetPlaybackPlansAsync(
                    _playingItem,
                    positionTicks,
                    requested,
                    _playbackCapabilities,
                    token);
                await StartFirstWorkingPlanAsync(plans, null, token);
                ShowToast("音轨与字幕已切换。", false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                _playbackSelection = previous;
                _playerView.Stop();
                _currentPlan = null;
                _playbackReporter.Reset();
                try
                {
                    List<JellyfinPlaybackPlan> restorePlans = await _api.GetPlaybackPlansAsync(
                        _playingItem,
                        positionTicks,
                        previous,
                        _playbackCapabilities,
                        token);
                    await StartFirstWorkingPlanAsync(restorePlans, null, token);
                    ShowToast(
                        "切换失败，已恢复原轨道：" + UserMessage(exception),
                        true);
                }
                catch (Exception restoreException)
                {
                    _playerView.Stop();
                    _currentPlan = null;
                    _playingItem = null;
                    _playbackReporter.Reset();
                    SelectInScope(_detailView.FocusRoot, "Continue", "From Start", "Close");
                    ShowToast(
                        "切换失败且无法恢复：" + UserMessage(restoreException),
                        true);
                }
            }
            finally
            {
                ShowLoading(false);
                _recoveringPlayback = false;
            }
        }

        private async Task StopCurrentReportForFallbackAsync(
            long positionTicks,
            bool failed,
            CancellationToken cancellationToken)
        {
            if (_currentPlan != null)
            {
                try
                {
                    await _playbackReporter.StopAsync(
                        positionTicks,
                        failed,
                        cancellationToken);
                }
                catch (Exception exception)
                {
                    HandleNonFatalError(exception);
                }
            }
            _playbackReporter.Reset();
        }

        private async Task StopPlaybackAsync(bool failed, string errorMessage, bool refreshAfter = false)
        {
            if (_stoppingPlayback)
            {
                return;
            }

            _stoppingPlayback = true;
            CancelAndDispose(ref _operation);
            _recoveringPlayback = false;
            long position = _playerView.CurrentPositionTicks;
            try
            {
                if (_currentPlan != null)
                {
                    try
                    {
                        await _playbackReporter.StopAsync(position, failed, _lifetime.Token);
                    }
                    catch (Exception exception)
                    {
                        HandleNonFatalError(exception);
                    }
                }
            }
            finally
            {
                _playerView.Stop();
                _currentPlan = null;
                _playingItem = null;
                _playbackSelection = null;
                _playbackReporter.Reset();
                _stoppingPlayback = false;
                SelectInScope(_detailView.FocusRoot, "Continue", "From Start", "Close");
            }

            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                ShowToast("播放失败：" + errorMessage, true);
            }
            if (refreshAfter)
            {
                RefreshHomeAsync().Forget(HandleNonFatalError);
            }
        }

        private async Task ReportForcedProgressAsync(bool paused)
        {
            if (_currentPlan == null)
            {
                return;
            }
            await _playbackReporter.ReportProgressIfDueAsync(paused, _playerView.CurrentPositionTicks, true, _lifetime.Token);
        }

        private void Logout()
        {
            JellyfinSession activeSession = _api.Session;
            if (activeSession != null)
            {
                _pendingServerUrl = activeSession.ServerUrl;
                _pendingUserName = activeSession.UserName;
            }
            if (_playerView.IsVisible)
            {
                StopPlaybackAsync(false, null).Forget(HandleNonFatalError);
            }
            CancelAndDispose(ref _operation);
            CancelAndDispose(ref _homeImages);
            CancelAndDispose(ref _browseImages);
            CancelAndDispose(ref _detailImages);
            _sessionStore.ClearSession();
            _api.ClearSession();
            _companionBridge.ClearNativeSession();
            _detailView.Hide();
            _browseView.Hide();
            _homeView.Show(false);
            _browseHistory.Clear();
            _detailHistory.Clear();
            ShowLogin(
                "已退出当前 Jellyfin 用户，请在手机端重新连接。",
                false,
                _pendingServerUrl,
                _pendingUserName);
        }

        private void ShowLogin(
            string message,
            bool isError,
            string serverUrl = null,
            string userName = null)
        {
            if (!string.IsNullOrWhiteSpace(serverUrl))
            {
                _pendingServerUrl = serverUrl;
            }
            if (userName != null)
            {
                _pendingUserName = userName;
            }

            ShowLoading(false);
            _homeView.Show(false);
            _browseView.Hide();
            _detailView.Hide();
            _browseHistory.Clear();
            _detailHistory.Clear();
            _loginView.Show(true);
            _focusNavigator?.SetScope(_loginView.FocusRoot);
            _focusNavigator?.ClearSelection();
            _loginView.SetMessage(message, isError);
            _companionBridge.PublishState(
                CompanionLoginState.LoginRequired,
                message,
                isError,
                _pendingServerUrl,
                _pendingUserName);
        }

        private void Update()
        {
            _companionBridge?.Pump();

            if (_playerView == null)
            {
                return;
            }

            _playerView.Update();
            if (_playerView.IsVisible && _currentPlan != null && Time.unscaledTime >= _nextProgressCheck)
            {
                _nextProgressCheck = Time.unscaledTime + 2f;
                _playbackReporter.ReportProgressIfDueAsync(
                    _playerView.IsPaused,
                    _playerView.CurrentPositionTicks,
                    false,
                    _lifetime.Token).Forget(HandleNonFatalError);
            }

            if (_toastMotion != null && _toastMotion.IsVisible && Time.unscaledTime >= _toastHideAt)
            {
                _toastMotion.Hide();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && _currentPlan != null && _playbackReporter != null)
            {
                _playbackReporter.ReportProgressIfDueAsync(
                    true,
                    _playerView.CurrentPositionTicks,
                    true,
                    _lifetime.Token).Forget(HandleNonFatalError);
            }
        }

        private void OnDestroy()
        {
            if (_currentPlan != null && _api != null)
            {
                _api.ReportPlaybackStoppedAsync(
                    _currentPlan,
                    _playerView != null ? _playerView.CurrentPositionTicks : 0L,
                    false,
                    CancellationToken.None).Forget();
            }

            CancelAndDispose(ref _operation);
            CancelAndDispose(ref _homeImages);
            CancelAndDispose(ref _browseImages);
            CancelAndDispose(ref _detailImages);
            if (_companionBridge != null)
            {
                _companionBridge.LoginRequested -= HandleCompanionLoginRequested;
                _companionBridge.QuickConnectRequested -= HandleCompanionQuickConnectRequested;
                _companionBridge.QuickConnectCancelRequested -= HandleCompanionQuickConnectCancelRequested;
                _companionBridge.SessionAvailable -= HandleCompanionSessionAvailable;
                _companionBridge.RemoteCommandReceived -= HandleRemoteCommand;
                _companionBridge.PublishState(
                    CompanionLoginState.Offline,
                    "Jellyfin 客户端已停止。",
                    false,
                    _pendingServerUrl,
                    _pendingUserName);
                _companionBridge.Dispose();
                _companionBridge = null;
            }
            if (_lifetime != null)
            {
                _lifetime.Cancel();
                _lifetime.Dispose();
                _lifetime = null;
            }
            _playerView?.Dispose();
            _imageCache?.Dispose();
        }

        private CancellationToken BeginOperation()
        {
            CancelAndDispose(ref _operation);
            _operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            return _operation.Token;
        }

        private void ReplaceHomeImageToken()
        {
            CancelAndDispose(ref _homeImages);
            _homeImages = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        }

        private void ReplaceBrowseImageToken()
        {
            CancelAndDispose(ref _browseImages);
            _browseImages = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        }

        private void ReplaceDetailImageToken()
        {
            CancelAndDispose(ref _detailImages);
            _detailImages = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        }

        private static void CancelAndDispose(ref CancellationTokenSource source)
        {
            if (source == null)
            {
                return;
            }
            source.Cancel();
            source.Dispose();
            source = null;
        }

        private void CreateOverlays(Transform parent)
        {
            Image loading = UiFactory.CreatePanel(
                "Loading Overlay",
                parent,
                new Color(0.004f, 0.007f, 0.014f, 0.74f));
            UiFactory.Stretch(loading.rectTransform);
            _loadingOverlay = loading.gameObject;
            Image loadingCard = UiFactory.CreateRoundedPanel(
                "Loading Card",
                loading.transform,
                UiTheme.SurfaceGlass);
            UiFactory.SetRect(
                loadingCard.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(760f, 190f));
            Outline loadingOutline = loadingCard.gameObject.AddComponent<Outline>();
            loadingOutline.effectColor = UiTheme.Border;
            loadingOutline.effectDistance = new Vector2(1f, -1f);

            _loadingLabel = UiFactory.CreateText(
                "Loading Label",
                loadingCard.transform,
                "正在加载…",
                31,
                UiTheme.TextPrimary,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            UiFactory.SetRect(
                _loadingLabel.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 30f),
                new Vector2(680f, 64f));

            RectTransform pulse = UiFactory.CreateRect("Loading Pulse", loadingCard.transform);
            UiFactory.SetRect(
                pulse,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -38f),
                new Vector2(112f, 28f));
            for (int index = 0; index < 3; index++)
            {
                Image dot = UiFactory.CreateRoundedPanel(
                    "Pulse Dot " + (index + 1),
                    pulse,
                    index == 1 ? UiTheme.AccentSecondary : UiTheme.AccentBright);
                UiFactory.SetRect(
                    dot.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2((index - 1) * 38f, 0f),
                    new Vector2(16f, 16f));
                dot.raycastTarget = false;
            }
            pulse.gameObject.AddComponent<UiLoadingPulse>();
            _loadingMotion = UiFactory.AddViewMotion(_loadingOverlay, 0f, 1f);
            _loadingMotion.SetVisibleImmediately(false);

            Image toast = UiFactory.CreateRoundedPanel(
                "Toast",
                parent,
                new Color(0.055f, 0.066f, 0.089f, 0.98f));
            UiFactory.SetRect(toast.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(1080f, 82f));
            _toast = toast.gameObject;
            _toastLabel = UiFactory.CreateText("Toast Label", toast.transform, string.Empty, 24, UiTheme.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Stretch(_toastLabel.rectTransform, 20f, 20f, 8f, 8f);
            Outline toastOutline = toast.gameObject.AddComponent<Outline>();
            toastOutline.effectColor = UiTheme.Border;
            toastOutline.effectDistance = new Vector2(1f, -1f);
            _toastMotion = UiFactory.AddViewMotion(_toast, 24f, 0.98f);
            _toastMotion.SetVisibleImmediately(false);
        }

        private void ShowLoading(bool visible, string message = null)
        {
            if (_loadingOverlay == null)
            {
                return;
            }
            if (!string.IsNullOrWhiteSpace(message))
            {
                _loadingLabel.text = message;
            }
            if (visible)
            {
                _loadingOverlay.transform.SetAsLastSibling();
                _loadingMotion.Show();
            }
            else
            {
                _loadingMotion.Hide();
            }
        }

        private void ShowToast(string message, bool isError)
        {
            if (_toast == null)
            {
                return;
            }
            _toastLabel.text = message ?? string.Empty;
            _toastLabel.color = isError ? new Color(1f, 0.72f, 0.76f, 1f) : UiTheme.TextPrimary;
            _toast.GetComponent<Image>().color = isError
                ? new Color(0.32f, 0.06f, 0.10f, 0.98f)
                : new Color(0.08f, 0.09f, 0.13f, 0.98f);
            _toast.transform.SetAsLastSibling();
            _toastMotion.Show();
            _toastHideAt = Time.unscaledTime + 5f;
        }

        private void HandleFatalError(Exception exception)
        {
            ShowLoading(false);
            ShowToast(UserMessage(exception), true);
            Debug.LogError("Jellyfin for RayNeo error: " + exception.GetType().Name + ": " + exception.Message);
        }

        private void HandleNonFatalError(Exception exception)
        {
            if (exception is OperationCanceledException)
            {
                return;
            }
            Debug.LogWarning("Jellyfin sync warning: " + exception.GetType().Name + ": " + exception.Message);
        }

        private static string UserMessage(Exception exception)
        {
            if (exception == null)
            {
                return "发生未知错误。";
            }
            if (exception is JellyfinApiException || exception is ArgumentException || exception is InvalidOperationException)
            {
                return exception.Message;
            }
            return "连接或处理数据时发生错误，请稍后重试。";
        }
    }
}
