using Prototipo.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Prototipo.Visual
{
    /// <summary>
    /// Controla a camera tatica sobre o tabuleiro com rotacao suave de 180 graus ao trocar de turno
    /// (AutoFlipEnabled), rotacao manual em Q/E e zoom via scroll do mouse (New Input System).
    /// </summary>
    public sealed class CameraRigController : MonoBehaviour
    {
        private const float DefaultPitchDegrees = 48f;
        private const float DefaultDistance = 10.2f;
        private const float MinDistance = 6.2f;
        private const float MaxDistance = 16.0f;
        private const float ManualRotateSpeed = 95f;
        private const float ScrollZoomSpeed = 0.012f;
        private const float SmoothTime = 0.18f;

        private Camera _camera;
        private Vector3 _targetCenter;

        private float _targetYaw;
        private float _currentYaw;
        private float _yawVelocity;

        private float _targetDistance = DefaultDistance;
        private float _currentDistance = DefaultDistance;
        private float _distanceVelocity;

        public bool AutoFlipEnabled { get; private set; } = true;
        public bool InputBlocked { get; set; }

        /// <summary>
        /// Inicializa a camera tatica olhando para o centro do tabuleiro.
        /// </summary>
        public void Initialize(Camera targetCamera, Vector3 boardCenter, PlayerTeam initialTeam)
        {
            _camera = targetCamera != null ? targetCamera : Camera.main;
            _targetCenter = boardCenter;
            _targetDistance = DefaultDistance;
            _currentDistance = DefaultDistance;

            SetTeamOrientation(initialTeam, true);
            ApplyTransformImmediate();
        }

        public void SetAutoFlip(bool enabled)
        {
            AutoFlipEnabled = enabled;
        }

        /// <summary>
        /// Alterna o Auto-Flip da camera entre Player 1 e Player 2 e retorna o novo estado.
        /// </summary>
        public bool ToggleAutoFlip(PlayerTeam currentActiveTeam)
        {
            AutoFlipEnabled = !AutoFlipEnabled;
            if (AutoFlipEnabled)
            {
                SetTeamOrientation(currentActiveTeam, false);
            }

            return AutoFlipEnabled;
        }

        /// <summary>
        /// Orienta a camera para o lado do jogador ativo se AutoFlipEnabled estiver ativo (ou se immediate for true).
        /// </summary>
        public void SetTeamOrientation(PlayerTeam team, bool immediate = false)
        {
            if (!AutoFlipEnabled && !immediate)
            {
                return;
            }

            float desiredYaw = team == PlayerTeam.Player1 ? 0f : 180f;
            _targetYaw = desiredYaw;

            if (immediate)
            {
                _currentYaw = desiredYaw;
                _yawVelocity = 0f;
                ApplyTransformImmediate();
            }
        }

        private void LateUpdate()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return;
                }
            }

            HandleInput();

            _currentYaw = Mathf.SmoothDampAngle(_currentYaw, _targetYaw, ref _yawVelocity, SmoothTime);
            _currentDistance = Mathf.SmoothDamp(_currentDistance, _targetDistance, ref _distanceVelocity, SmoothTime);

            ApplyTransformImmediate();
        }

        private void HandleInput()
        {
            if (InputBlocked)
            {
                return;
            }

            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                float rotateDelta = 0f;
                if (kb.qKey.isPressed)
                {
                    rotateDelta -= 1f;
                }

                if (kb.eKey.isPressed)
                {
                    rotateDelta += 1f;
                }

                if (Mathf.Abs(rotateDelta) > 0.01f)
                {
                    _targetYaw += rotateDelta * ManualRotateSpeed * Time.deltaTime;
                }
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                if (!overUi)
                {
                    float scrollY = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(scrollY) > 0.01f)
                    {
                        _targetDistance = Mathf.Clamp(
                            _targetDistance - scrollY * ScrollZoomSpeed,
                            MinDistance,
                            MaxDistance);
                    }
                }
            }
        }

        private void ApplyTransformImmediate()
        {
            if (_camera == null)
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(DefaultPitchDegrees, _currentYaw, 0f);
            Vector3 offset = rotation * new Vector3(0f, 0f, -_currentDistance);
            _camera.transform.position = _targetCenter + offset;
            _camera.transform.LookAt(_targetCenter);
        }
    }
}
