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
            PlayWinChime();
        }

        /// <summary>
        /// Harmonic chime: C5, E5, G5 at 80ms intervals with 0.8s decay,
        /// plus a low 130Hz swell underneath.
        /// </summary>
        public void PlayWinChime()
        {
            // Chime tones — longer decay for shimmer
            PlayToneDelayed(523f, 0.8f, 0.3f, 0.0f);   // C5
            PlayToneDelayed(659f, 0.8f, 0.3f, 0.08f);  // E5
            PlayToneDelayed(784f, 0.8f, 0.3f, 0.16f);  // G5

            // Low swell underneath
            var swell = GenerateLowSwell();
            _source.PlayOneShot(swell, 0.25f);
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
        /// Layered explosion: bass thump + mid crack + rumble tail + debris clicks.
        /// Each layer on its own spatial AudioSource for full volume and clarity.
        /// </summary>
        public void PlayExplosion(Vector3 worldPos)
        {
            var bassThump = GenerateBassThump();
            var midCrack = GenerateMidCrack();
            var rumbleTail = GenerateRumbleTail();
            var debrisClips = GenerateDebrisClicks();

            PlaySpatialOneShot(worldPos, bassThump, 1.0f);
            PlaySpatialOneShot(worldPos, midCrack, 0.8f);
            PlaySpatialOneShot(worldPos, rumbleTail, 0.6f);

            // Debris clicks staggered over 0.3-0.8s
            for (int i = 0; i < debrisClips.Length; i++)
                StartCoroutine(PlayDelayedSpatial(worldPos, debrisClips[i], 0.4f, debrisClips[i].name));
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

        private void PlaySpatialOneShot(Vector3 pos, AudioClip clip, float volume)
        {
            var obj = new GameObject("ExplosionAudio");
            obj.transform.position = pos;
            var src = obj.AddComponent<AudioSource>();
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 2f;
            src.maxDistance = 30f;
            src.playOnAwake = false;
            src.clip = clip;
            src.volume = volume;
            src.Play();
            Destroy(obj, clip.length + 0.1f);
        }

        private System.Collections.IEnumerator PlayDelayedSpatial(Vector3 pos, AudioClip clip, float volume, string tag)
        {
            // Delay is baked into the clip name as a float parsed at generation
            float delay = 0f;
            if (tag.StartsWith("debris_"))
            {
                var parts = tag.Split('_');
                if (parts.Length > 1) float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out delay);
            }
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            PlaySpatialOneShot(pos, clip, volume);
        }

        // --- Layer 1: Bass Thump (impact transient) ---
        private static AudioClip GenerateBassThump()
        {
            int sampleRate = 44100;
            float duration = 0.3f;
            int count = (int)(sampleRate * duration);
            var clip = AudioClip.Create("bassThump", count, 1, sampleRate, false);
            float[] samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float progress = (float)i / count;
                // Frequency sweep 80→60 Hz for weight
                float freq = Mathf.Lerp(80f, 60f, progress);
                // Instant attack, steep exponential decay
                float env = Mathf.Exp(-8f * progress);
                // Fundamental + sub-harmonic for chest punch
                float fundamental = Mathf.Sin(2f * Mathf.PI * freq * t);
                float sub = Mathf.Sin(2f * Mathf.PI * freq * 0.5f * t) * 0.6f;
                // Second harmonic for presence
                float harmonic = Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.2f;
                samples[i] = (fundamental + sub + harmonic) * env;
                // Soft clip to avoid digital distortion
                samples[i] = Mathf.Clamp(samples[i], -0.95f, 0.95f);
            }
            clip.SetData(samples, 0);
            return clip;
        }

        // --- Layer 2: Mid Crack (detonation snap) ---
        private static AudioClip GenerateMidCrack()
        {
            int sampleRate = 44100;
            float duration = 0.15f;
            int count = (int)(sampleRate * duration);
            var clip = AudioClip.Create("midCrack", count, 1, sampleRate, false);
            float[] samples = new float[count];
            var rng = new System.Random(42);
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float progress = (float)i / count;
                // Instant attack, very fast decay
                float env = Mathf.Exp(-20f * progress);
                // White noise base
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                // Bandpass approximation: sum of sine waves in 800-2000 Hz range
                float band = Mathf.Sin(2f * Mathf.PI * 800f * t) * 0.3f
                           + Mathf.Sin(2f * Mathf.PI * 1200f * t) * 0.25f
                           + Mathf.Sin(2f * Mathf.PI * 1600f * t) * 0.2f
                           + Mathf.Sin(2f * Mathf.PI * 2000f * t) * 0.15f;
                samples[i] = (noise * 0.4f + band) * env;
                samples[i] = Mathf.Clamp(samples[i], -0.95f, 0.95f);
            }
            clip.SetData(samples, 0);
            return clip;
        }

        // --- Layer 3: Rumble Tail ---
        private static AudioClip GenerateRumbleTail()
        {
            int sampleRate = 44100;
            float duration = 1.2f;
            int count = (int)(sampleRate * duration);
            var clip = AudioClip.Create("rumbleTail", count, 1, sampleRate, false);
            float[] samples = new float[count];
            var rng = new System.Random(77);
            float attackDuration = 0.1f;
            int attackSamples = (int)(sampleRate * attackDuration);
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float progress = (float)i / count;
                // 0.1s attack ramp, then slow exponential decay
                float env;
                if (i < attackSamples)
                    env = (float)i / attackSamples;
                else
                    env = Mathf.Exp(-2.5f * (progress - attackDuration / duration));
                // Low frequency sweep 100→40 Hz
                float freq = Mathf.Lerp(100f, 40f, progress);
                float sine = Mathf.Sin(2f * Mathf.PI * freq * t);
                // Noise texture in the low range
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                // Simple low-pass on noise: average with previous
                if (i > 0)
                    noise = noise * 0.3f + samples[i - 1] * 0.5f;
                samples[i] = (sine * 0.6f + noise * 0.4f) * env;
            }
            clip.SetData(samples, 0);
            return clip;
        }

        // --- Layer 4: Debris Clicks (6-8 random high-freq clicks) ---
        private static AudioClip[] GenerateDebrisClicks()
        {
            var rng = new System.Random(123);
            int clickCount = rng.Next(6, 9); // 6-8 clicks
            var clips = new AudioClip[clickCount];
            int sampleRate = 44100;
            float clickDuration = 0.05f;
            int clickSamples = (int)(sampleRate * clickDuration);

            for (int c = 0; c < clickCount; c++)
            {
                // Random delay between 0.3-0.8s encoded in clip name
                float delay = 0.3f + (float)rng.NextDouble() * 0.5f;
                // Random frequency 2000-4000 Hz
                float freq = 2000f + (float)rng.NextDouble() * 2000f;

                var clip = AudioClip.Create(
                    $"debris_{delay.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}",
                    clickSamples, 1, sampleRate, false);
                float[] samples = new float[clickSamples];
                for (int i = 0; i < clickSamples; i++)
                {
                    float t = (float)i / sampleRate;
                    float progress = (float)i / clickSamples;
                    // Sharp transient: instant attack, fast decay
                    float env = Mathf.Exp(-30f * progress);
                    float sine = Mathf.Sin(2f * Mathf.PI * freq * t);
                    // Add noise for metallic character
                    float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.3f;
                    samples[i] = (sine * 0.7f + noise) * env;
                }
                clip.SetData(samples, 0);
                clips[c] = clip;
            }
            return clips;
        }

        // ===== Haptics =====

        public void VibrateLight()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            VibrateAndroid(25, 80);
#endif
        }

        public void VibrateMedium()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            VibrateAndroid(50, 180);
#endif
        }

        public void VibrateHeavy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            VibrateExplosion();
#endif
        }

        public void VibratePattern()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            VibratePatternAndroid(new long[] { 0, 30, 50, 30, 50, 80 });
#endif
        }

        public void VibrateWin()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            VibrateWinAndroid();
#endif
        }

        // ===== Win Sound Generation =====

        private static AudioClip GenerateLowSwell()
        {
            int sampleRate = 44100;
            float duration = 1.5f;
            int count = (int)(sampleRate * duration);
            var clip = AudioClip.Create("lowSwell", count, 1, sampleRate, false);
            float[] samples = new float[count];
            float attackDuration = 0.3f;
            int attackSamples = (int)(sampleRate * attackDuration);
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float progress = (float)i / count;
                // 0.3s attack ramp, then smooth decay
                float env;
                if (i < attackSamples)
                    env = (float)i / attackSamples;
                else
                    env = Mathf.Exp(-2f * (progress - attackDuration / duration));
                // 130Hz sine
                float sine = Mathf.Sin(2f * Mathf.PI * 130f * t);
                // Subtle second harmonic for warmth
                float harmonic = Mathf.Sin(2f * Mathf.PI * 260f * t) * 0.15f;
                samples[i] = (sine + harmonic) * env * 0.5f;
            }
            clip.SetData(samples, 0);
            return clip;
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
        private static int _apiLevel = -1;

        private static int GetApiLevel()
        {
            if (_apiLevel < 0)
            {
                try
                {
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                        _apiLevel = version.GetStatic<int>("SDK_INT");
                }
                catch (System.Exception) { _apiLevel = 25; }
            }
            return _apiLevel;
        }

        private static void VibrateAndroid(long milliseconds, int amplitude)
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    if (vibrator == null) return;

                    if (GetApiLevel() >= 26)
                    {
                        using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                        using (var effect = effectClass.CallStatic<AndroidJavaObject>(
                            "createOneShot", milliseconds, amplitude))
                        {
                            vibrator.Call("vibrate", effect);
                        }
                    }
                    else
                    {
                        vibrator.Call("vibrate", milliseconds);
                    }
                }
            }
            catch (System.Exception) { }
        }

        private static void VibrateExplosion()
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    if (vibrator == null) return;

                    if (GetApiLevel() >= 26)
                    {
                        long[] timings = { 0, 80, 30, 120, 20, 60 };
                        int[] amplitudes = { 0, 255, 0, 200, 0, 120 };
                        using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                        using (var effect = effectClass.CallStatic<AndroidJavaObject>(
                            "createWaveform", timings, amplitudes, -1))
                        {
                            vibrator.Call("vibrate", effect);
                        }
                    }
                    else
                    {
                        vibrator.Call("vibrate", (long)100);
                    }
                }
            }
            catch (System.Exception) { }
        }

        private static void VibrateWinAndroid()
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    if (vibrator == null) return;

                    if (GetApiLevel() >= 26)
                    {
                        long[] timings = { 0, 30, 20, 30, 20, 30 };
                        int[] amplitudes = { 0, 120, 0, 120, 0, 120 };
                        using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                        using (var effect = effectClass.CallStatic<AndroidJavaObject>(
                            "createWaveform", timings, amplitudes, -1))
                        {
                            vibrator.Call("vibrate", effect);
                        }
                    }
                    else
                    {
                        vibrator.Call("vibrate", new long[] { 0, 30, 20, 30, 20, 30 }, -1);
                    }
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
