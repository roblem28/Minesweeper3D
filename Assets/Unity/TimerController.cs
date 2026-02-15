using UnityEngine;

namespace Minesweeper3D.Unity
{
    public class TimerController : MonoBehaviour
    {
        private float _elapsed;
        private bool _running;

        public float ElapsedSeconds => _elapsed;

        public string FormattedTime
        {
            get
            {
                int total = Mathf.FloorToInt(_elapsed);
                int minutes = total / 60;
                int seconds = total % 60;
                return $"{minutes:D2}:{seconds:D2}";
            }
        }

        public void StartTimer() => _running = true;
        public void StopTimer() => _running = false;

        public void ResetTimer()
        {
            _elapsed = 0f;
            _running = false;
        }

        private void Update()
        {
            if (_running)
                _elapsed += Time.deltaTime;
        }
    }
}
