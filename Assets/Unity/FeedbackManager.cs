using UnityEngine;

namespace Minesweeper3D.Unity
{
    /// <summary>
    /// Procedural sound effects and haptic feedback. No audio files needed.
    /// </summary>
    public class FeedbackManager : MonoBehaviour
    {
        private AudioSource _source;
        private static FeedbackManager _instance;
        public static FeedbackManager Instance => _instance;

        public void Init()
        {
            _instance = this;
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.volume = 0.3f;
        }

        // ===== Sound Effects =====

        public void PlayTap()
        {
            PlayTone(800f, 0.04f, 0.15f);
        }

        public void PlayRevealCascade(int cellCount)
        {
            // Rising sweep — higher pitch for larger cascades
            float freq = Mathf.Lerp(400f, 900f, Mathf.Clamp01(cellCount / 30f));
            PlayTone(freq, 0.08f, 0.12f, sweep: 1.3f);
        }

        public void PlayFlag()
        {
            PlayTone(600f, 0.06f, 0.2f);
            // Second tone slightly delayed for "click-clack" feel
            PlayToneDelayed(900f, 0.04f, 0.15f, 0.06f);
        }

        public void PlayUnflag()
        {
            PlayTone(900f, 0.04f, 0.15f);
            PlayToneDelayed(600f, 0.06f, 0.12f, 0.04f);
        }

        public void PlayWin()
        {
            // Major chord arpeggio: C5 E5 G5 C6
            PlayToneDelayed(523f, 0.15f, 0.25f, 0.0f);
            PlayToneDelayed(659f, 0.15f, 0.25f, 0.12f);
            PlayToneDelayed(784f, 0.15f, 0.25f, 0.24f);
            PlayToneDelayed(1047f, 0.25f, 0.3f, 0.36f);
        }

        public void PlayLose()
        {
            // Descending minor: low rumble
            PlayToneDelayed(300f, 0.2f, 0.3f, 0.0f);
            PlayToneDelayed(250f, 0.2f, 0.25f, 0.15f);
            PlayToneDelayed(200f, 0.3f, 0.2f, 0.30f);
        }

        public void PlayMineReveal()
        {
            // Short harsh buzz
            PlayNoise(0.08f, 0.25f);
        }

        // ===== Haptics =====

        public void VibrateLight()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            VibrateAndroid(20);
#endif
        }

        public void VibrateMedium()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            VibrateAndroid(40);
#endif
        }

        public void VibrateHeavy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            VibrateAndroid(100);
#endif
        }

        public void VibratePattern()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            VibratePatternAndroid(new long[] { 0, 30, 50, 30, 50, 80 });
#endif
        }

        // ===== Audio Generation =====

        private void PlayTone(float frequency, float duration, float volume, float sweep = 1f)
        {
            var clip = GenerateTone(frequency, duration, volume, sweep);
            _source.PlayOneShot(clip, volume);
        }

        private void PlayToneDelayed(float frequency, float duration, float volume, float delay)
        {
            StartCoroutine(PlayDelayedCoroutine(frequency, duration, volume, delay));
        }

        private System.Collections.IEnumerator PlayDelayedCoroutine(float freq, float dur, float vol, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            PlayTone(freq, dur, vol);
        }

        private void PlayNoise(float duration, float volume)
        {
            var clip = GenerateNoise(duration, volume);
            _source.PlayOneShot(clip, volume);
        }

        private static AudioClip GenerateTone(float frequency, float duration, float volume, float sweep)
        {
            int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * duration);
            var clip = AudioClip.Create("tone", sampleCount, 1, sampleRate, false);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float progress = (float)i / sampleCount;
                float freq = Mathf.Lerp(frequency, frequency * sweep, progress);
                float envelope = 1f - progress; // linear decay
                envelope *= envelope; // quadratic decay for snappier feel
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope;
            }

            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip GenerateNoise(float duration, float volume)
        {
            int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * duration);
            var clip = AudioClip.Create("noise", sampleCount, 1, sampleRate, false);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float progress = (float)i / sampleCount;
                float envelope = 1f - progress;
                samples[i] = Random.Range(-1f, 1f) * envelope * 0.5f;
            }

            clip.SetData(samples, 0);
            return clip;
        }

        // ===== Android Vibration =====

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void VibrateAndroid(long milliseconds)
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    if (vibrator != null)
                        vibrator.Call("vibrate", milliseconds);
                }
            }
            catch (System.Exception) { }
        }

        private static void VibratePatternAndroid(long[] pattern)
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    if (vibrator != null)
                        vibrator.Call("vibrate", pattern, -1);
                }
            }
            catch (System.Exception) { }
        }
#endif
    }
}
