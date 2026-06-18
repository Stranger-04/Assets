using UnityEngine;
using UnityEngine.InputSystem;

namespace Mine.CamController
{
    /// <summary>
    /// 统一相机控制：自动检测第三人称 / 自由飞行模式。
    /// </summary>
    ///
    /// <remarks>
    /// 模式由 transform.parent 自动判定：
    /// <br/>• 有父级 → 第三人称：父物体为焦点目标，WASD 由 RigidbodyMover 接管，
    ///              滚轮缩放 Camera.z，Camera 始终 LookAt 本节点（焦点）。
    /// <br/>• 无父级 → 自由飞行：WASD 沿视线方向移动本节点。
    ///
    /// <code>
    /// 第三人称层级：                 自由飞行层级：
    /// Target (RigidbodyMover)       CameraRig (本脚本)
    /// └── FocusPoint (本脚本)         └── YawPivot
    ///     └── YawPivot                   └── PitchPivot
    ///         └── PitchPivot                 └── Camera
    ///             └── Camera
    /// </code>
    /// </remarks>
    public class CamController : MonoBehaviour
    {
        // ── 层级引用 ──────────────────────────────────────────────

        [Header("层级引用")]
        [SerializeField] private Transform _yawPivot;
        [SerializeField] private Transform _pitchPivot;

        // ── 旋转 ──────────────────────────────────────────────────

        [Header("旋转")]
        [SerializeField] private float _mouseSensitivity = 2f;
        [SerializeField] private float _rotationLerp     = 12f;
        [SerializeField] [Range(-90f, 0f)] private float _pitchMin = -89f;
        [SerializeField] [Range(0f, 90f)]  private float _pitchMax =  89f;

        // ── 焦点（第三人称） ───────────────────────────────────────

        [Header("焦点")]
        [SerializeField] private Vector3 _focusOffset = new Vector3(0f, 1.5f, 0f);

        // ── 缩放（第三人称） ───────────────────────────────────────

        [Header("缩放")]
        [SerializeField] private float _zoomSensitivity = 2f;
        [SerializeField] [Range(0f, 10f)] private float _zoom    = 6f;
        [SerializeField] private float _zoomMin = 0f;
        [SerializeField] private float _zoomMax = 10f;

        // ── 移动（自由模式） ────────────────────────────────────────

        [Header("移动")]
        [SerializeField] private float _moveSpeed = 5f;

        // ── 光标 ──────────────────────────────────────────────────

        [Header("光标")]
        [SerializeField] private bool _lockCursorOnClick = true;

        // ── 状态 ──────────────────────────────────────────────────

        private Camera _cam;
        private float  _targetYaw, _targetPitch;
        private float  _appliedYaw, _appliedPitch;
        private bool   _thirdPerson;

        // ════════════════════════════════════════════════════════════

        private void Awake()
        {
            _cam         = GetComponentInChildren<Camera>();
            _thirdPerson = transform.parent != null;

            TryAutoResolve();
            CaptureInitialAngles();

            if (_thirdPerson)
            {
                transform.localPosition = _focusOffset;
                if (_cam != null)
                    _zoom = Mathf.Abs(_cam.transform.localPosition.z);
            }
        }

        private void Start()
        {
            LockCursor(true);
        }

        private void LateUpdate()
        {
            HandleCursorInput();
            if (Cursor.lockState == CursorLockMode.Locked)
                HandleRotation();

            HandleZoom();

            if (_thirdPerson)
            {
                LookAtFocus();
            }
            else
            {
                HandleMovement();
            }
        }

        // ════════════════════════════════════════════════════════════
        //  初始化
        // ════════════════════════════════════════════════════════════

        private void TryAutoResolve()
        {
            if (_yawPivot == null && transform.childCount > 0)
                _yawPivot = transform.GetChild(0);
            if (_pitchPivot == null && _yawPivot != null && _yawPivot.childCount > 0)
                _pitchPivot = _yawPivot.GetChild(0);
            if (_cam == null)
                _cam = GetComponentInChildren<Camera>();
        }

        private void CaptureInitialAngles()
        {
            if (_yawPivot != null)
                _appliedYaw = _targetYaw = _yawPivot.rotation.eulerAngles.y;
            if (_pitchPivot != null)
                _appliedPitch = _targetPitch = _pitchPivot.localRotation.eulerAngles.x;
        }

        // ════════════════════════════════════════════════════════════
        //  光标
        // ════════════════════════════════════════════════════════════

        private void HandleCursorInput()
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
                LockCursor(false);
            if (_lockCursorOnClick && Mouse.current.leftButton.wasPressedThisFrame)
                LockCursor(true);
        }

        private void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible   = !locked;
        }

        // ════════════════════════════════════════════════════════════
        //  旋转
        // ════════════════════════════════════════════════════════════

        private void HandleRotation()
        {
            if (_yawPivot == null || _pitchPivot == null) return;

            float mx = Mouse.current.delta.x.ReadValue() * _mouseSensitivity;
            float my = Mouse.current.delta.y.ReadValue() * _mouseSensitivity;

            _targetYaw   += mx;
            _targetPitch -= my;
            _targetPitch  = Mathf.Clamp(_targetPitch, _pitchMin, _pitchMax);

            float t = 1f - Mathf.Exp(-_rotationLerp * Time.deltaTime);
            _appliedYaw   = Mathf.LerpAngle(_appliedYaw,   _targetYaw,   t);
            _appliedPitch = Mathf.LerpAngle(_appliedPitch, _targetPitch, t);

            _yawPivot.rotation        = Quaternion.Euler(0f, _appliedYaw, 0f);
            _pitchPivot.localRotation = Quaternion.Euler(_appliedPitch, 0f, 0f);
        }

        // ════════════════════════════════════════════════════════════
        //  缩放（第三人称）— Camera.z 沿视线收缩
        // ════════════════════════════════════════════════════════════

        private void HandleZoom()
        {
            if (_cam == null) return;

            float scroll = Mouse.current.scroll.y.ReadValue() * _zoomSensitivity * 0.01f;
            if (Mathf.Approximately(scroll, 0f)) return;

            _zoom -= scroll;
            _zoom  = Mathf.Clamp(_zoom, _zoomMin, _zoomMax);

            var lp = _cam.transform.localPosition;
            lp.z = -_zoom;
            _cam.transform.localPosition = lp;
        }

        // ════════════════════════════════════════════════════════════
        //  注视焦点（第三人称）
        // ════════════════════════════════════════════════════════════

        private void LookAtFocus()
        {
            if (_cam == null) return;
            _cam.transform.LookAt(transform);
        }

        // ════════════════════════════════════════════════════════════
        //  移动（自由模式）— WASD 沿视线方向
        // ════════════════════════════════════════════════════════════

        private void HandleMovement()
        {
            if (_cam == null) return;

            var kb = Keyboard.current;
            float h = 0f, v = 0f;

            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed)  h += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)   h -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)     v += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)   v -= 1f;

            Vector3 move = _cam.transform.forward * v + _cam.transform.right * h;
            if (move.magnitude > 1f) move.Normalize();

            transform.position += move * (_moveSpeed * Time.deltaTime);
        }
    }
}
