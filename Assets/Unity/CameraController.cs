using UnityEngine;

namespace Minesweeper3D.Unity
{
    /// <summary>
    /// Orbit/zoom camera around the grid center.
    /// Middle-mouse drag to orbit, Ctrl+scroll to zoom.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Orbit")]
        [SerializeField] private float orbitSpeed = 4f;

        [Header("Zoom")]
        [SerializeField] private float zoomSpeed = 3f;
        [SerializeField] private float minDistance = 3f;
        [SerializeField] private float maxDistance = 30f;

        private Vector3 _target;
        private float _distance;
        private float _azimuth;   // horizontal angle in degrees
        private float _elevation; // vertical angle in degrees

        public void Init(Vector3 target, float initialDistance)
        {
            _target = target;
            _distance = initialDistance;
            _azimuth = 0f;
            _elevation = 35f;
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
            if (!Input.GetMouseButton(2)) return; // middle mouse button

            float dx = Input.GetAxis("Mouse X");
            float dy = Input.GetAxis("Mouse Y");

            _azimuth += dx * orbitSpeed;
            _elevation -= dy * orbitSpeed;
            _elevation = Mathf.Clamp(_elevation, 5f, 85f);
        }

        private void HandleZoom()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl)
                     || Input.GetKey(KeyCode.RightControl);
            if (!ctrl) return;

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll == 0f) return;

            _distance -= scroll * zoomSpeed;
            _distance = Mathf.Clamp(_distance, minDistance, maxDistance);
        }

        private void ApplyPosition()
        {
            float azRad = _azimuth * Mathf.Deg2Rad;
            float elRad = _elevation * Mathf.Deg2Rad;

            float x = _distance * Mathf.Cos(elRad) * Mathf.Sin(azRad);
            float y = _distance * Mathf.Sin(elRad);
            float z = -_distance * Mathf.Cos(elRad) * Mathf.Cos(azRad);

            transform.position = _target + new Vector3(x, y, z);
            transform.LookAt(_target);
        }
    }
}
