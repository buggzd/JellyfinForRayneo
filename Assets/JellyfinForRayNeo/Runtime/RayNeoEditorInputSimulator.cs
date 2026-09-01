#if UNITY_EDITOR
using FfalconXR.InputModule;
using FfalconXR.Native;
using UnityEngine;
using UnityEngine.EventSystems;

namespace JellyfinForRayNeo
{
    [DefaultExecutionOrder(-1000)]
    public sealed class RayNeoEditorInputSimulator : MonoBehaviour
    {
        private const float MouseSensitivity = 2.25f;
        private const float MaximumPitch = 42f;
        private const float MaximumYaw = 65f;
        private const string OfficialDebugTypeName = "FfalconXR.Editor.DebugMono";

        private WindowsMessager _messenger;
        private Transform _laser;
        private RayNeoEditorInputModule _inputModule;
        private UnityInputKeyHandler _legacyKeyHandler;
        private bool _captured;
        private float _pitch;
        private float _yaw;
        private Quaternion _rayRotation = Quaternion.identity;

        public bool IsCaptured
        {
            get { return _captured; }
        }

        private void Awake()
        {
            ResolveRayTargets();
            ConfigureInput();
            DisableOfficialDebugController();
        }

        private void Update()
        {
            if (_inputModule == null || _legacyKeyHandler == null)
            {
                ConfigureInput();
            }

            if (_laser == null || _messenger == null)
            {
                ResolveRayTargets();
            }

            if (Input.GetKeyDown(KeyCode.LeftControl))
            {
                SetCaptured(!_captured);
            }

            if (_captured && Input.GetKeyDown(KeyCode.Escape))
            {
                SetCaptured(false);
            }

            if (!_captured)
            {
                return;
            }

            KeepCursorCaptured();
            _yaw = Mathf.Clamp(
                _yaw + Input.GetAxisRaw("Mouse X") * MouseSensitivity,
                -MaximumYaw,
                MaximumYaw);
            _pitch = Mathf.Clamp(
                _pitch - Input.GetAxisRaw("Mouse Y") * MouseSensitivity,
                -MaximumPitch,
                MaximumPitch);
            _rayRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            ApplyRayRotation();
        }

        private void LateUpdate()
        {
            if (_captured && _laser != null)
            {
                _laser.rotation = _rayRotation;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && _captured)
            {
                SetCaptured(false);
            }
        }

        private void OnDisable()
        {
            ReleaseCursor();
        }

        private void OnDestroy()
        {
            ReleaseCursor();
        }

        private void OnGUI()
        {
            string message = _captured
                ? "RayNeo Editor：移动鼠标瞄准，左键点击/拖拽，左 Ctrl 或 Esc 释放鼠标"
                : "RayNeo Editor：点击 Game View 后按左 Ctrl 捕获鼠标";
            GUI.Label(new Rect(18f, 14f, 720f, 28f), message);
        }

        private void ConfigureInput()
        {
            EventSystem currentEventSystem = EventSystem.current;
            if (currentEventSystem != null && _inputModule == null)
            {
                XRInputModule[] modules = currentEventSystem.GetComponents<XRInputModule>();
                foreach (XRInputModule module in modules)
                {
                    if (!(module is RayNeoEditorInputModule))
                    {
                        module.enabled = false;
                    }
                }

                _inputModule = currentEventSystem.GetComponent<RayNeoEditorInputModule>();
                if (_inputModule == null)
                {
                    _inputModule = currentEventSystem.gameObject.AddComponent<RayNeoEditorInputModule>();
                }
                _inputModule.useCustomRay = true;
                _inputModule.enabled = true;
            }

            if (_legacyKeyHandler == null)
            {
                _legacyKeyHandler = GetComponent<UnityInputKeyHandler>();
                if (_legacyKeyHandler == null)
                {
                    _legacyKeyHandler = gameObject.AddComponent<UnityInputKeyHandler>();
                }
            }
        }

        private void DisableOfficialDebugController()
        {
            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour != null && behaviour.GetType().FullName == OfficialDebugTypeName)
                {
                    behaviour.enabled = false;
                }
            }
        }

        private void ResolveRayTargets()
        {
            if (_laser == null)
            {
                GameObject laserObject = GameObject.Find("LaserBeam");
                _laser = laserObject != null ? laserObject.transform : null;
            }

            if (_messenger == null)
            {
                _messenger = NativeModule.Instance.GetMsger() as WindowsMessager;
            }
        }

        private void SetCaptured(bool captured)
        {
            _captured = captured;
            if (!_captured)
            {
                ReleaseCursor();
                return;
            }

            ResolveRayTargets();
            Vector3 initialEuler = Vector3.zero;
            if (_laser != null)
            {
                initialEuler = _laser.rotation.eulerAngles;
            }
            else if (_messenger != null)
            {
                initialEuler = NativeModule.Instance.GetMobileQualternion().eulerAngles;
            }
            _pitch = Mathf.Clamp(NormalizeAngle(initialEuler.x), -MaximumPitch, MaximumPitch);
            _yaw = Mathf.Clamp(NormalizeAngle(initialEuler.y), -MaximumYaw, MaximumYaw);
            _rayRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            KeepCursorCaptured();
            ApplyRayRotation();
        }

        private void KeepCursorCaptured()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void ApplyRayRotation()
        {
            if (_messenger != null)
            {
                _messenger.m_windowsMouseQuaternion = _rayRotation;
            }
            if (_laser != null)
            {
                _laser.rotation = _rayRotation;
            }
        }

        private static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }
    }

    public sealed class RayNeoEditorInputModule : XRInputModule
    {
        public override bool ShouldActivateModule()
        {
            return isActiveAndEnabled;
        }

        protected override void ProcessDrag(PointerEventData pointerEvent)
        {
            if (!pointerEvent.IsPointerMoving() || pointerEvent.pointerDrag == null)
            {
                return;
            }

            if (!pointerEvent.dragging
                && ShouldStartDrag(
                    pointerEvent.pressPosition,
                    pointerEvent.position,
                    eventSystem.pixelDragThreshold,
                    pointerEvent.useDragThreshold))
            {
                ExecuteEvents.Execute(
                    pointerEvent.pointerDrag,
                    pointerEvent,
                    ExecuteEvents.beginDragHandler);
                pointerEvent.dragging = true;
            }

            if (pointerEvent.dragging)
            {
                if (pointerEvent.pointerPress != pointerEvent.pointerDrag)
                {
                    ExecuteEvents.Execute(
                        pointerEvent.pointerPress,
                        pointerEvent,
                        ExecuteEvents.pointerUpHandler);
                    pointerEvent.eligibleForClick = false;
                    pointerEvent.pointerPress = null;
                    pointerEvent.rawPointerPress = null;
                }

                ExecuteEvents.Execute(
                    pointerEvent.pointerDrag,
                    pointerEvent,
                    ExecuteEvents.dragHandler);
            }
        }

        private static bool ShouldStartDrag(
            Vector2 pressPosition,
            Vector2 currentPosition,
            float threshold,
            bool useDragThreshold)
        {
            if (!useDragThreshold)
            {
                return true;
            }

            return (pressPosition - currentPosition).sqrMagnitude >= threshold * threshold;
        }
    }
}
#endif
