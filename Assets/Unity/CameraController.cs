using UnityEngine;
using UnityEngine.InputSystem;

namespace Minesweeper3D.Unity
{
    /// <summary>
    /// Orbit/zoom camera around the grid center.
    /// Middle-mouse drag to orbit, Ctrl+scroll to zoom.
    /// Starts at an isometric-style angle looking down at the 3D grid.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Orbit")]
        [SerializeField] private float orbitSpeed = 0.25f;

        [Header("Zoom")]
        [SerializeField] private float zoomSpeed = 2f;
        [SerializeField] private float minDistance = 5f;
        [SerializeField] private float maxDistance = 40f;

        private Vector3 _target;
        private float _distance;
        private float _azimuth;
        private float _elevation;

        public void Init(Vector3 target, float initialDistance)
        {
            _target = target;
            _distance = initialDistance;
            _azimuth = 45f;    // diagonal view
            _elevation = 30f;  // looking down at the cube
            ApplyPosition();
        }

        private void LateUpdate()
        {
            HandleOrbit();
            HandleZoom();
            ApplyPosition();
        }

        private void HandleOrbit()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.middleButton.isPressed) return;

            Vector2 delta = mouse.delta.ReadValue();
            _azimuth += delta.x * orbitSpeed;
            _elevation -= delta.y * orbitSpeed;
            _elevation = Mathf.Clamp(_elevation, 5f, 85f);
        }

        private void HandleZoom()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (!kb.leftCtrlKey.isPressed && !kb.rightCtrlKey.isPressed) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            float scrollY = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) < 0.01f) return;

            _distance -= Mathf.Sign(scrollY) * zoomSpeed;
            _distance = Mathf.Clamp(_distance, minDistance, maxDistance);
        }

        private void ApplyPosition()
        {
            float azRad = _azimuth * Mathf.Deg2Rad;
            float elRad = _elevation * Mathf.Deg2Rad;

            float x =  _distance * Mathf.Cos(elRad) * Mathf.Sin(azRad);
            float y =  _distance * Mathf.Sin(elRad);
            float z = -_distance * Mathf.Cos(elRad) * Mathf.Cos(azRad);

            transform.position = _target + new Vector3(x, y, z);
            transform.LookAt(_target);
        }
    }
}
