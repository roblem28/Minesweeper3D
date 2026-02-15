using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Minesweeper3D.Core;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Minesweeper3D.Unity
{
    /// <summary>
    /// Multi-platform input abstraction. Handles mouse/keyboard (PC) and
    /// touch gestures (mobile), fires high-level game events.
    /// Raycasts internally to resolve screen position to Coord3.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        // --- Events ---
        public event Action<Coord3> OnReveal;
        public event Action<Coord3> OnFlag;
        public event Action<int> OnSliceChange;
        public event Action<float> OnZoom;
        public event Action<Vector2> OnOrbit;

        // --- Config ---
        private Camera _cam;
        private bool _enabled = true;

        // --- Touch state ---
        private float _touchStartTime;
        private Vector2 _touchStartPos;
        private bool _touchMoved;
        private float _prevPinchDist;
        private bool _wasTwoFinger;

        // --- Constants ---
        private const float LongPressDuration = 0.5f;
        private const float TapMoveTolerance = 15f;
        private const float SwipeThreshold = 50f;
        private const float PinchThreshold = 20f;

        public void Init(Camera cam)
        {
            _cam = cam;
        }

        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
        }

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
        }

        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
        }

        private void Update()
        {
            if (!_enabled) return;

            if (Touch.activeTouches.Count > 0)
                HandleTouch();
            else
                HandleMouseKeyboard();
        }

        // ===== PC Input =====

        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void HandleMouseKeyboard()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            // --- Click: reveal / flag ---
            bool left = mouse.leftButton.wasPressedThisFrame;
            bool right = mouse.rightButton.wasPressedThisFrame;

            if (left || right)
            {
                if (IsPointerOverUI()) return;

                Vector2 pos = mouse.position.ReadValue();
                if (TryRaycastCell(pos, out Coord3 coord))
                {
                    if (left)
                        OnReveal?.Invoke(coord);
                    else
                        OnFlag?.Invoke(coord);
                }
            }

            // --- Scroll ---
            float scrollY = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) > 0.01f)
            {
                var kb = Keyboard.current;
                bool ctrl = kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed);

                if (ctrl)
                    OnZoom?.Invoke(Mathf.Sign(scrollY));
                else
                    OnSliceChange?.Invoke(scrollY > 0f ? 1 : -1);
            }

            // --- Orbit: middle mouse drag ---
            if (mouse.middleButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                if (delta.sqrMagnitude > 0.01f)
                    OnOrbit?.Invoke(delta);
            }
        }

        // ===== Mobile Touch Input =====

        private void HandleTouch()
        {
            int count = Touch.activeTouches.Count;

            if (count == 1)
                HandleOneFingerTouch(Touch.activeTouches[0]);
            else if (count >= 2)
                HandleTwoFingerTouch(Touch.activeTouches[0], Touch.activeTouches[1]);

            _wasTwoFinger = count >= 2;
        }

        private void HandleOneFingerTouch(Touch touch)
        {
            // Skip if just released from two-finger gesture
            if (_wasTwoFinger) return;

            switch (touch.phase)
            {
                case UnityEngine.InputSystem.TouchPhase.Began:
                    _touchStartTime = Time.time;
                    _touchStartPos = touch.screenPosition;
                    _touchMoved = false;
                    break;

                case UnityEngine.InputSystem.TouchPhase.Moved:
                {
                    float dist = Vector2.Distance(touch.screenPosition, _touchStartPos);
                    if (dist > TapMoveTolerance)
                    {
                        _touchMoved = true;
                        // One-finger drag on empty space = orbit
                        if (!TryRaycastCell(_touchStartPos, out _))
                        {
                            OnOrbit?.Invoke(touch.delta);
                        }
                        else
                        {
                            // Dragging on a cell — still orbit (don't reveal while dragging)
                            OnOrbit?.Invoke(touch.delta);
                        }
                    }
                    break;
                }

                case UnityEngine.InputSystem.TouchPhase.Ended:
                {
                    if (!_touchMoved && !IsPointerOverUI())
                    {
                        float duration = Time.time - _touchStartTime;
                        if (TryRaycastCell(touch.screenPosition, out Coord3 coord))
                        {
                            if (duration >= LongPressDuration)
                                OnFlag?.Invoke(coord);
                            else
                                OnReveal?.Invoke(coord);
                        }
                    }
                    break;
                }
            }
        }

        private void HandleTwoFingerTouch(Touch t0, Touch t1)
        {
            Vector2 pos0 = t0.screenPosition;
            Vector2 pos1 = t1.screenPosition;
            float currentDist = Vector2.Distance(pos0, pos1);

            if (!_wasTwoFinger)
            {
                // First frame of two-finger — initialize
                _prevPinchDist = currentDist;
                return;
            }

            // Pinch: distance change → zoom
            float distDelta = currentDist - _prevPinchDist;
            if (Mathf.Abs(distDelta) > PinchThreshold)
            {
                OnZoom?.Invoke(Mathf.Sign(distDelta));
                _prevPinchDist = currentDist;
            }

            // Two-finger vertical swipe: both fingers moving same direction
            if (t0.phase == UnityEngine.InputSystem.TouchPhase.Moved &&
                t1.phase == UnityEngine.InputSystem.TouchPhase.Moved)
            {
                Vector2 d0 = t0.delta;
                Vector2 d1 = t1.delta;

                // Both moving same vertical direction and vertically dominant
                if (Mathf.Sign(d0.y) == Mathf.Sign(d1.y) &&
                    Mathf.Abs(d0.y) > Mathf.Abs(d0.x) &&
                    Mathf.Abs(d1.y) > Mathf.Abs(d1.x))
                {
                    float avgY = (d0.y + d1.y) * 0.5f;
                    if (Mathf.Abs(avgY) > SwipeThreshold * Time.deltaTime * 60f)
                    {
                        OnSliceChange?.Invoke(avgY > 0f ? 1 : -1);
                    }
                }
            }

            _prevPinchDist = currentDist;
        }

        // ===== Raycast Helper =====

        private bool TryRaycastCell(Vector2 screenPos, out Coord3 coord)
        {
            coord = default;
            if (_cam == null) return false;

            Ray ray = _cam.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                var cell = hit.collider.GetComponent<CellView>();
                if (cell != null)
                {
                    coord = cell.Coord;
                    return true;
                }
            }
            return false;
        }
    }
}
