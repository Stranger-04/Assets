using UnityEngine;
using UnityEngine.InputSystem;

namespace Mine.CamController
{
    /// <summary>
    /// WASD 控制独立刚体移动，受力驱动（FixedUpdate）。
    /// 输入方向基于相机朝向投影到水平面。
    /// </summary>
    public class RigidbodyMover : MonoBehaviour
    {
        // ── 参数 ──────────────────────────────────────────────────────

        [Header("移动")]
        [SerializeField] private float _moveForce  = 20f;
        [SerializeField] private float _maxSpeed   = 8f;
        [SerializeField] private float _stopDamping = 0.9f;   // 松手后减速系数

        [Header("参考")]
        [SerializeField] private Transform _cameraTransform;

        // ── 内部 ──────────────────────────────────────────────────────

        private Rigidbody _rb;

        // ════════════════════════════════════════════════════════════
        //  初始化
        // ════════════════════════════════════════════════════════════

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();

            if (_cameraTransform == null && Camera.main != null)
                _cameraTransform = Camera.main.transform;
        }

        // ════════════════════════════════════════════════════════════
        //  物理驱动 — FixedUpdate
        // ════════════════════════════════════════════════════════════

        private void FixedUpdate()
        {
            if (_rb == null || _cameraTransform == null) return;
            if (GetComponentInChildren<Camera>() == null) return;  // 无相机子级则不响应

            Vector2 input = ReadMovementInput();
            Vector3 force = ComputeForce(input);

            if (input != Vector2.zero)
            {
                _rb.AddForce(force, ForceMode.Force);

                // 限速
                Vector3 flatVel = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
                if (flatVel.magnitude > _maxSpeed)
                {
                    flatVel = flatVel.normalized * _maxSpeed;
                    _rb.linearVelocity = new Vector3(flatVel.x, _rb.linearVelocity.y, flatVel.z);
                }
            }
            else
            {
                // 无输入时：水平方向阻尼减速
                Vector3 v = _rb.linearVelocity;
                v.x *= _stopDamping;
                v.z *= _stopDamping;
                _rb.linearVelocity = v;
            }
        }

        // ════════════════════════════════════════════════════════════
        //  输入读取
        // ════════════════════════════════════════════════════════════

        private Vector2 ReadMovementInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;

            float x = 0f;
            float y = 0f;

            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed)  x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)   x -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)     y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)   y -= 1f;

            return new Vector2(x, y);
        }

        // ════════════════════════════════════════════════════════════
        //  力方向计算 — 基于相机朝向投影至水平面
        // ════════════════════════════════════════════════════════════

        private Vector3 ComputeForce(Vector2 input)
        {
            Vector3 forward = _cameraTransform.forward;
            Vector3 right   = _cameraTransform.right;

            forward.y = 0f;
            right.y   = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 direction = forward * input.y + right * input.x;
            return direction * _moveForce;
        }
    }
}
