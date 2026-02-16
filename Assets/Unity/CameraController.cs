using UnityEngine;

namespace Minesweeper3D.Unity
{
    /// <summary>
    /// Orbit/zoom camera around the grid center.
    /// Input comes from InputManager events (ApplyOrbit, ApplyZoom).
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

        public void Init(Vector3 target, float gridWorldSize)
        {
            _target = target;

            // Auto-frame: fit full grid visible on phone landscape (use vertical FOV)
            var cam = GetComponent<Camera>();
            if (cam != null && cam.fieldOfView > 0f)
            {
                float halfFov = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
                float aspect = cam.aspect > 0f ? cam.aspect : 16f / 9f;
                // Use the tighter axis (vertical on landscape phones)
                float halfFovH = Mathf.Atan(Mathf.Tan(halfFov) * aspect);
                float fov = Mathf.Min(halfFov, halfFovH);
                _distance = (gridWorldSize * 0.5f) / Mathf.Tan(fov) * 1.15f;
            }
            else
            {
                _distance = gridWorldSize;
            }
            _distance = Mathf.Clamp(_distance, minDistance, maxDistance);

            _azimuth = 45f;    // diagonal view
            _elevation = 30f;  // looking down at the cube
            ApplyPosition();
        }

        private void LateUpdate()
        {
            ApplyPosition();
        }

        public void ApplyOrbit(Vector2 delta)
        {
            _azimuth += delta.x * orbitSpeed;
            _elevation -= delta.y * orbitSpeed;
            _elevation = Mathf.Clamp(_elevation, 5f, 85f);
        }

        public void ApplyZoom(float delta)
        {
            _distance -= delta * zoomSpeed;
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
