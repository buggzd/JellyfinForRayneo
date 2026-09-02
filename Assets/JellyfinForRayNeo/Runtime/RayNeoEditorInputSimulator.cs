#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.EventSystems;

namespace JellyfinForRayNeo
{
    [DefaultExecutionOrder(-1000)]
    public sealed class RayNeoEditorInputSimulator : MonoBehaviour
    {
        private bool _previousSendNavigationEvents = true;
        private Air3SDisplayController _displayController;
        private EventSystem _eventSystem;
        private bool _showHelp;

        private void Awake()
        {
            ConfigureEventSystem();
        }

        private void Update()
        {
            if (_eventSystem == null)
            {
                ConfigureEventSystem();
            }

            if (Pressed(KeyCode.UpArrow, KeyCode.W))
            {
                CompanionRemoteInputRuntime.Submit(CompanionRemoteCommand.Up);
            }
            else if (Pressed(KeyCode.DownArrow, KeyCode.S))
            {
                CompanionRemoteInputRuntime.Submit(CompanionRemoteCommand.Down);
            }
            else if (Pressed(KeyCode.LeftArrow, KeyCode.A))
            {
                CompanionRemoteInputRuntime.Submit(CompanionRemoteCommand.Left);
            }
            else if (Pressed(KeyCode.RightArrow, KeyCode.D))
            {
                CompanionRemoteInputRuntime.Submit(CompanionRemoteCommand.Right);
            }

            if (Pressed(KeyCode.Return, KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                CompanionRemoteInputRuntime.Submit(CompanionRemoteCommand.Submit);
            }
            if (Pressed(KeyCode.Escape, KeyCode.Backspace))
            {
                CompanionRemoteInputRuntime.Submit(CompanionRemoteCommand.Back);
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                FindDisplayController()?.SetMode(Air3SDisplayMode.Mirror2D);
            }
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                FindDisplayController()?.SetMode(Air3SDisplayMode.StereoVirtualScreen);
            }
            if (Input.GetKeyDown(KeyCode.F1))
            {
                _showHelp = !_showHelp;
            }
        }

        private void OnDisable()
        {
            RestoreEventSystem();
        }

        private void OnDestroy()
        {
            RestoreEventSystem();
        }

        private void OnGUI()
        {
            if (!_showHelp)
            {
                GUI.Box(
                    new Rect(14f, Mathf.Max(8f, Screen.height - 34f), 132f, 24f),
                    "F1  调试说明");
                return;
            }

            Air3SDisplayController display = FindDisplayController();
            string displayMode = display != null
                && display.ActiveMode == Air3SDisplayMode.StereoVirtualScreen
                    ? "立体屏幕（SBS 预览）"
                    : "镜像 2D";
            float top = Mathf.Max(10f, Screen.height - 78f);
            GUI.Box(new Rect(12f, top, 920f, 66f), string.Empty);
            GUI.Label(
                new Rect(24f, top + 8f, 890f, 26f),
                "Air 3S 盲操调试：方向键/WASD 移动，Enter/Space 确认，Esc/Backspace 返回；1=镜像 2D，2=立体屏幕");
            GUI.Label(
                new Rect(24f, top + 34f, 520f, 24f),
                "当前显示模式：" + displayMode + "  ·  F1 隐藏说明");
        }

        private void ConfigureEventSystem()
        {
            _eventSystem = EventSystem.current;
            if (_eventSystem == null)
            {
                return;
            }

            _previousSendNavigationEvents = _eventSystem.sendNavigationEvents;
            _eventSystem.sendNavigationEvents = false;
        }

        private void RestoreEventSystem()
        {
            if (_eventSystem != null)
            {
                _eventSystem.sendNavigationEvents = _previousSendNavigationEvents;
            }
        }

        private static bool Pressed(KeyCode first, KeyCode second)
        {
            return Input.GetKeyDown(first) || Input.GetKeyDown(second);
        }

        private Air3SDisplayController FindDisplayController()
        {
            if (_displayController == null)
            {
                _displayController = Object.FindObjectOfType<Air3SDisplayController>();
            }
            return _displayController;
        }
    }
}
#endif
