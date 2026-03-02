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
            // Short harsh buzz (legacy, kept for non-spatial callers)
            PlayNoise(0.08f, 0.25f);
        }

        /// <summary>
        /// Layered explosion: low boom + debris scatter + rumble tail.
        /// Plays at a world position via a temporary spatial AudioSource.
        /// </summary>
        public void PlayExplosion(Vector3 worldPos)
        {
            // Layer 1: Low-frequency boom (60 Hz sine with fast decay, 0.3s)
            var boom = GenerateExplosionBoom(0.35f);
            // Layer 2: Debris scatter (filtered noise burst, 0.25s)
            var debris = GenerateDebrisScatter(0.25f);
            // Layer 3: Rumble tail (very low sine sweep 40→20 Hz, 0.6s)
            var rumble = GenerateRumbleTail(0.6f);
            // Layer 4: High glass/crystal shatter
            var shatter = GenerateGlassShatter(0.2f);

            PlaySpatialOneShot(worldPos, boom, 0.5f);
            PlaySpatialOneShot(worldPos, debris, 0.3f);
            PlaySpatialOneShot(worldPos, rumble, 0.35f);
            PlaySpatialOneShot(worldPos, shatter, 0.25f);
        }

        /// <summary>Low bass rumble for charge-up phase. Plays on the main source.</summary>
        public void PlayChargeRumble()
        {
            var clip = GenerateChargeRumble(0.25f);
            _source.PlayOneShot(clip, 0.3f);
        }

        private static AudioClip GenerateChargeRumble(float duration)
        {
            int sampleRate = 44100;
            int count = (int)(sampleRate * duration);
            var clip = AudioClip.Create("chargeRumble", count, 1, sampleRate, false);
            float[] samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float progress = (float)i / count;
                // Rising frequency 30→60 Hz with increasing amplitude
                float freq = Mathf.Lerp(30f, 60f, progress);
                float env = progress * progress; // quadratic rise
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.8f;
            }
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip GenerateGlassShatter(float duration)
        {
            int sampleRate = 44100;
            int count = (int)(sampleRate * duration);
            var clip = AudioClip.Create("glassShatter", count, 1, sampleRate, false);
            float[] samples = new float[count];
            var rng = new System.Random(55);
            for (int i = 0; i < count; i++)
            {
                float progress = (float)i / count;
                float t = (float)i / sampleRate;
                // Sharp attack, fast decay
                float env = progress < 0.02f
                    ? progress / 0.02f
                    : Mathf.Exp(-10f * (progress - 0.02f));
                // High-frequency noise (glass-like)
                float raw = (float)(rng.NextDouble() * 2.0 - 1.0);
                // High-pass bias: subtract low-frequency component
                float hp = raw;
                if (i > 0) hp = raw - samples[i - 1] * 0.3f;
                // Add resonant high-frequency sine clusters
                float ring = Mathf.Sin(2f * Mathf.PI * 4200f * t) * 0.3f
                           + Mathf.Sin(2f * Mathf.PI * 6800f * t) * 0.2f
                           + Mathf.Sin(2f * Mathf.PI * 9500f * t) * 0.1f;
                samples[i] = (hp * 0.5f + ring) * env;
            }
            clip.SetData(samples, 0);
            return clip;
        }

        private void PlaySpatialOneShot(Vector3 pos, AudioClip clip, float volume)
        {
            var obj = new GameObject("ExplosionAudio");
            obj.transform.position = pos;
            var src = obj.AddComponent<AudioSource>();
            src.spatialBlend = 1f; // full 3D
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 2f;
            src.maxDistance = 30f;
            src.playOnAwake = false;
            src.clip = clip;
            src.volume = volume;
            src.Play();
            Destroy(obj, clip.length + 0.1f);
        }

        private static AudioClip GenerateExplosionBoom(float duration)
        {
            int sampleRate = 44100;
            int count = (int)(sampleRate * duration);
            var clip = AudioClip.Create("boom", count, 1, sampleRate, false);
            float[] samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float progress = (float)i / count;
                // Frequency drops from 80 Hz to 40 Hz
                float freq = Mathf.Lerp(80f, 40f, progress);
                // Sharp exponential decay
                float env = Mathf.Exp(-6f * progress);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env;
                // Add sub-harmonic punch
                samples[i] += Mathf.Sin(2f * Mathf.PI * freq * 0.5f * t) * env * 0.5f;
            }
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip GenerateDebrisScatter(float duration)
        {
            int sampleRate = 44100;
            int count = (int)(sampleRate * duration);
            var clip = AudioClip.Create("debris", count, 1, sampleRate, false);
            float[] samples = new float[count];
            // Use deterministic seed for consistency
            var rng = new System.Random(99);
            for (int i = 0; i < count; i++)
            {
                float progress = (float)i / count;
                // Quick attack, medium decay
                float env = progress < 0.05f
                    ? progress / 0.05f
                    : Mathf.Exp(-4f * (progress - 0.05f));
                // Filtered noise: bias toward mid frequencies by averaging neighbors
                float raw = (float)(rng.NextDouble() * 2.0 - 1.0);
                samples[i] = raw * env * 0.6f;
                // Simple low-pass: average with previous
                if (i > 0)
                    samples[i] = samples[i] * 0.4f + samples[i - 1] * 0.6f;
            }
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip GenerateRumbleTail(float duration)
        {
            int sampleRate = 44100;
            int count = (int)(sampleRate * duration);
            var clip = AudioClip.Create("rumble", count, 1, sampleRate, false);
            float[] samples = new float[count];
            var rng = new System.Random(77);
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float progress = (float)i / count;
                // Sweep from 40 Hz down to 20 Hz
                float freq = Mathf.Lerp(40f, 20f, progress);
                float env = Mathf.Exp(-3f * progress);
                float sine = Mathf.Sin(2f * Mathf.PI * freq * t) * env;
                // Add subtle noise texture
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * env * 0.15f;
                samples[i] = sine + noise;
            }
            clip.SetData(samples, 0);
            return clip;
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
