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
        // --- Core game events ---
        public event Action<Coord3> OnReveal;
        public event Action<Coord3> OnFlag;
        public event Action<int> OnSliceChange;
        public event Action<float> OnZoom;
        public event Action<Vector2> OnOrbit;

        // --- Highlight events ---
        public event Action<Coord3> OnHoverEnter;
        public event Action OnHoverExit;
        public event Action<Coord3> OnHighlightStart;  // mobile long press
        public event Action OnHighlightEnd;             // mobile release

        // --- Chord events ---
        public event Action<Coord3> OnDoubleClick;      // desktop
        public event Action<Coord3> OnDoubleTap;        // mobile

        // --- Press events (for visual feedback) ---
        public event Action<Coord3> OnPressDown;
        public event Action OnPressUp;

        // --- Config ---
        private Camera _cam;
        private bool _enabled = true;

        // --- Desktop hover state ---
        private Coord3? _lastHoveredCell;

        // --- Pending tap/click (delayed to detect double) ---
        private float _pendingTapTime;
        private Coord3? _pendingTapCoord;
        private bool _pendingIsDesktop;

        // --- Touch state ---
        private float _touchStartTime;
        private Vector2 _touchStartPos;
        private bool _touchMoved;
        private float _prevPinchDist;
        private bool _wasTwoFinger;
        private bool _isHighlighting;

        // --- Constants ---
        private const float HighlightPressDuration = 0.3f;
        private const float LongPressFlagDuration = 0.5f;
        private const float TapMoveTolerance = 15f;
        private const float SwipeThreshold = 50f;
        private const float PinchThreshold = 20f;
        private const float DoubleTapWindow = 0.25f;

        public void Init(Camera cam)
        {
            _cam = cam;
        }

        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            if (!enabled)
            {
                if (_lastHoveredCell.HasValue)
                {
                    _lastHoveredCell = null;
                    OnHoverExit?.Invoke();
                }
                if (_isHighlighting)
                {
                    _isHighlighting = false;
                    OnHighlightEnd?.Invoke();
                }
                _pendingTapCoord = null;
            }
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

            // Flush pending tap/click if double-tap window expired
            if (_pendingTapCoord.HasValue && Time.time - _pendingTapTime >= DoubleTapWindow)
            {
                OnReveal?.Invoke(_pendingTapCoord.Value);
                _pendingTapCoord = null;
            }

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

            bool left = mouse.leftButton.wasPressedThisFrame;
            bool right = mouse.rightButton.wasPressedThisFrame;
            bool leftUp = mouse.leftButton.wasReleasedThisFrame;

            // --- Press feedback ---
            if (left && !IsPointerOverUI())
            {
                Vector2 pressPos = mouse.position.ReadValue();
                if (TryRaycastCell(pressPos, out Coord3 pressCoord))
                    OnPressDown?.Invoke(pressCoord);
            }
            if (leftUp)
                OnPressUp?.Invoke();

            // --- Hover detection (every frame, no button pressed) ---
            if (!mouse.leftButton.isPressed && !mouse.rightButton.isPressed && !mouse.middleButton.isPressed)
            {
                Vector2 pos = mouse.position.ReadValue();
                if (!IsPointerOverUI() && TryRaycastCell(pos, out Coord3 hoverCoord))
                {
                    if (!_lastHoveredCell.HasValue || !_lastHoveredCell.Value.Equals(hoverCoord))
                    {
                        if (_lastHoveredCell.HasValue) OnHoverExit?.Invoke();
                        _lastHoveredCell = hoverCoord;
                        OnHoverEnter?.Invoke(hoverCoord);
                    }
                }
                else if (_lastHoveredCell.HasValue)
                {
                    _lastHoveredCell = null;
                    OnHoverExit?.Invoke();
                }
            }

            // --- Click: reveal / flag / double-click ---
            if (left || right)
            {
                if (IsPointerOverUI()) return;

                Vector2 pos = mouse.position.ReadValue();
                if (TryRaycastCell(pos, out Coord3 coord))
                {
                    if (left)
                    {
                        if (_pendingTapCoord.HasValue && _pendingTapCoord.Value.Equals(coord))
                        {
                            // Second click on same cell — chord
                            _pendingTapCoord = null;
                            OnDoubleClick?.Invoke(coord);
                        }
                        else
                        {
                            // First click — delay to check for double
                            if (_pendingTapCoord.HasValue)
                            {
                                // Flush previous pending tap on different cell
                                OnReveal?.Invoke(_pendingTapCoord.Value);
                            }
                            _pendingTapCoord = coord;
                            _pendingTapTime = Time.time;
                            _pendingIsDesktop = true;
                        }
                    }
                    else
                    {
                        OnFlag?.Invoke(coord);
                    }
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
                    _isHighlighting = false;
                    break;

                case UnityEngine.InputSystem.TouchPhase.Moved:
                case UnityEngine.InputSystem.TouchPhase.Stationary:
                {
                    float dist = Vector2.Distance(touch.screenPosition, _touchStartPos);
                    if (dist > TapMoveTolerance)
                    {
                        _touchMoved = true;
                        if (_isHighlighting)
                        {
                            OnHighlightEnd?.Invoke();
                            _isHighlighting = false;
                        }
                        OnOrbit?.Invoke(touch.delta);
                    }
                    else if (!_touchMoved && !_isHighlighting)
                    {
                        float duration = Time.time - _touchStartTime;
                        if (duration >= HighlightPressDuration)
                        {
                            if (!IsPointerOverUI() && TryRaycastCell(_touchStartPos, out Coord3 coord))
                            {
                                _isHighlighting = true;
                                OnHighlightStart?.Invoke(coord);
                            }
                        }
                    }
                    break;
                }

                case UnityEngine.InputSystem.TouchPhase.Ended:
                {
                    float holdDuration = Time.time - _touchStartTime;

                    if (_isHighlighting)
                    {
                        OnHighlightEnd?.Invoke();
                        _isHighlighting = false;

                        // Long press >= 0.5s on a cell = flag
                        if (!_touchMoved && holdDuration >= LongPressFlagDuration)
                        {
                            if (!IsPointerOverUI() && TryRaycastCell(_touchStartPos, out Coord3 flagCoord))
                            {
                                Debug.Log("LONG PRESS at " + flagCoord);
                                OnFlag?.Invoke(flagCoord);
                            }
                        }
                        break;
                    }
                    if (!_touchMoved && !IsPointerOverUI())
                    {
                        if (holdDuration < HighlightPressDuration)
                        {
                            if (TryRaycastCell(touch.screenPosition, out Coord3 coord))
                            {
                                if (_pendingTapCoord.HasValue && _pendingTapCoord.Value.Equals(coord))
                                {
                                    // Second tap on same cell — chord
                                    _pendingTapCoord = null;
                                    OnDoubleTap?.Invoke(coord);
                                }
                                else
                                {
                                    // First tap — delay to check for double
                                    if (_pendingTapCoord.HasValue)
                                    {
                                        OnReveal?.Invoke(_pendingTapCoord.Value);
                                    }
                                    _pendingTapCoord = coord;
                                    _pendingTapTime = Time.time;
                                    _pendingIsDesktop = false;
                                }
                            }
                        }
                    }
                    break;
                }
            }
        }

        private void HandleTwoFingerTouch(Touch t0, Touch t1)
        {
            // Cancel highlight if switching to two-finger
            if (_isHighlighting)
            {
                OnHighlightEnd?.Invoke();
                _isHighlighting = false;
            }

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
