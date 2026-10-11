using UnityEngine;

namespace Corehold
{
    // Every sound is synthesised at startup, no audio files: weapons, explosions, UI, jingles and two music loops.
    public static class Sounds
    {
        public const int Rate = 22050;

        public static AudioClip Mg, Cannon, Missile, Explosion, BigExplosion, Hit;
        public static AudioClip Build, Upgrade, Sell, Click, WaveHorn, Alarm, Victory, Defeat;
        public static AudioClip BattleMusic, MenuMusic;

        static System.Random random;

        public static void Create()
        {
            if (Mg != null) return;
            random = new System.Random(7);

            Mg = Clip("Mg", 0.14f, MachineGun);
            Cannon = Clip("Cannon", 0.9f, CannonShot);
            Missile = Clip("Missile", 0.7f, MissileLaunch);
            Explosion = Clip("Explosion", 1f, t => Blast(t, 1f));
            BigExplosion = Clip("BigExplosion", 1.6f, t => Blast(t, 0.6f));
            Hit = Clip("Hit", 0.06f, t => Noise() * Mathf.Exp(-t * 90f) * 0.5f + Sine(2400f, t) * Mathf.Exp(-t * 120f) * 0.2f);

            Build = Clip("Build", 0.45f, BuildClunk);
            Upgrade = Clip("Upgrade", 0.75f, t => Arpeggio(t, new[] { 523.3f, 659.3f, 784f, 1046.5f }, 0.09f, 7f) * 0.35f);
            Sell = Clip("Sell", 0.4f, t => Bell(t, 1318.5f, 0f) + Bell(t, 1760f, 0.08f));
            Click = Clip("Click", 0.05f, t => Sine(1100f, t) * Mathf.Exp(-t * 80f) * 0.45f);
            WaveHorn = Clip("WaveHorn", 1.3f, Horn);
            Alarm = Clip("Alarm", 0.6f, t => Square(((int)(t / 0.15f)) % 2 == 0 ? 660f : 880f, t) * Envelope(t, 0.6f, 0.01f, 0.08f) * 0.18f);
            Victory = Clip("Victory", 1.8f, t => Fanfare(t, new[] { 523.3f, 659.3f, 784f, 1046.5f }, new[] { 0f, 0.15f, 0.3f, 0.45f }, 1.7f));
            Defeat = Clip("Defeat", 1.6f, t => Fanfare(t, new[] { 392f, 311.1f, 261.6f }, new[] { 0f, 0.35f, 0.7f }, 1.5f) * 0.8f);

            BattleMusic = BuildBattleMusic();
            MenuMusic = BuildMenuMusic();
        }

        // ---------------- Effects ----------------

        static float MachineGun(float t)
        {
            float f = 140f * Mathf.Max(0.3f, 1f - t * 2f);
            return LowNoise(0.6f) * Mathf.Exp(-t * 45f) * 0.9f + Sine(f, t) * Mathf.Exp(-t * 35f) * 0.6f;
        }

        static float CannonShot(float t)
        {
            float body = Mathf.Sin(2f * Mathf.PI * (35f * t + 70f / 3f * (1f - Mathf.Exp(-3f * t))));
            return body * Mathf.Exp(-t * 5f) * 0.9f + LowNoise(0.08f) * Mathf.Exp(-t * 6f) * 1.8f + Noise() * Mathf.Exp(-t * 60f) * 0.35f;
        }

        static float MissileLaunch(float t)
        {
            float env = Mathf.Min(t / 0.05f, 1f) * Mathf.Exp(-t * 3f);
            return LowNoise(0.03f + 0.35f * t) * env * 1.5f + Sine(200f + 600f * t, t) * env * 0.12f;
        }

        // decay < 1 makes a longer, deeper blast.
        static float Blast(float t, float decay)
        {
            float rumble = Mathf.Sin(2f * Mathf.PI * (30f * t + 25f / 2f * (1f - Mathf.Exp(-2f * t))));
            return LowNoise(0.06f * decay + 0.02f) * Mathf.Exp(-t * 3.5f * decay) * 2f
                 + rumble * Mathf.Exp(-t * 4f * decay) * 0.8f
                 + Noise() * Mathf.Exp(-t * 40f) * 0.3f;
        }

        static float BuildClunk(float t)
        {
            float s = 0f;
            foreach (var start in new[] { 0f, 0.15f })
            {
                float tt = t - start;
                if (tt < 0f) continue;
                s += (Sine(180f, tt) + 0.5f * Sine(370f, tt)) * Mathf.Exp(-tt * 25f) * 0.5f + Noise() * Mathf.Exp(-tt * 40f) * 0.3f;
            }
            return s;
        }

        static float Bell(float t, float f, float start)
        {
            float tt = t - start;
            if (tt < 0f) return 0f;
            return (Sine(f, tt) + 0.4f * Sine(f * 2.76f, tt)) * Mathf.Exp(-tt * 12f) * 0.3f;
        }

        static float Arpeggio(float t, float[] notes, float step, float decay)
        {
            float s = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                float tt = t - i * step;
                if (tt < 0f) continue;
                s += (Sine(notes[i], tt) + 0.3f * Sine(notes[i] * 2f, tt)) * Mathf.Exp(-tt * decay) * Mathf.Min(tt / 0.005f, 1f);
            }
            return s;
        }

        // Military horn call: two brassy notes.
        static float Horn(float t)
        {
            float f = t < 0.5f ? 220f : 293.7f;
            float tt = t < 0.5f ? t : t - 0.5f;
            float len = t < 0.5f ? 0.45f : 0.8f;
            float vibrato = 1f + 0.006f * Mathf.Sin(2f * Mathf.PI * 5.5f * t);
            return Brass(f * vibrato, t) * Envelope(tt, len, 0.04f, 0.12f) * 0.3f;
        }

        static float Fanfare(float t, float[] notes, float[] starts, float end)
        {
            float s = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                float tt = t - starts[i];
                float len = (i + 1 < notes.Length ? starts[i + 1] + 0.1f : end) - starts[i];
                if (tt < 0f || tt > len) continue;
                s += Brass(notes[i], t) * Envelope(tt, len, 0.02f, 0.15f);
            }
            return s * 0.22f;
        }

        // ---------------- Music ----------------

        // 110 bpm battle loop, four bars in A minor: kick, snare, hats, a driving bass and a quiet pad.
        static AudioClip BuildBattleMusic()
        {
            const float bpm = 110f;
            float beat = 60f / bpm;
            var roots = new[] { 110f, 87.31f, 130.81f, 98f };
            var thirds = new[] { 1.189f, 1.26f, 1.26f, 1.26f };     // minor third on A, major on F C G
            int n = Mathf.RoundToInt(beat * 16f * Rate);
            var data = new float[n];
            var bassFilter = new LowPass(0.1f);
            var hatFilter = new LowPass(0.5f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                int beatIndex = (int)(t / beat);
                float tb = t - beatIndex * beat;
                int bar = (beatIndex / 4) % 4;
                float barTime = t - (beatIndex / 4) * 4f * beat;

                float kick = Mathf.Sin(2f * Mathf.PI * (50f * tb + 3f * (1f - Mathf.Exp(-30f * tb)))) * Mathf.Exp(-tb * 9f) * 0.7f;
                float snare = beatIndex % 2 == 1 ? (Noise() * Mathf.Exp(-tb * 18f) * 0.28f + Sine(180f, tb) * Mathf.Exp(-tb * 20f) * 0.18f) : 0f;
                float eighth = beat * 0.5f;
                float te = t % eighth;
                float noise = Noise();
                float hat = (noise - hatFilter.Next(noise)) * Mathf.Exp(-te * 60f) * 0.12f;

                int step = (int)(t / eighth) % 8;
                float f = roots[bar] * (step == 3 || step == 7 ? 2f : 1f);
                float saw = 2f * ((f * t) % 1f) - 1f;
                float bass = bassFilter.Next(saw) * Mathf.Exp(-te * 5f) * 0.55f;

                float pad = (Sine(roots[bar] * 2f, t) + Sine(roots[bar] * 2f * thirds[bar], t) + Sine(roots[bar] * 3f, t)) * 0.035f
                          * Mathf.Clamp01(Mathf.Min(barTime / 0.08f, (4f * beat - barTime) / 0.08f));
                data[i] = SoftClip(kick + snare + hat + bass + pad);
            }
            return ToClip("BattleMusic", data);
        }

        // Calm 80 bpm loop: slow pads and soft plucked arpeggios.
        static AudioClip BuildMenuMusic()
        {
            const float bpm = 80f;
            float beat = 60f / bpm, bar = beat * 4f;
            var roots = new[] { 220f, 174.6f, 261.6f, 196f };
            var thirds = new[] { 1.189f, 1.26f, 1.26f, 1.26f };
            int n = Mathf.RoundToInt(bar * 4f * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                int b = (int)(t / bar) % 4;
                float tb = t - (int)(t / bar) * bar;
                float swell = Mathf.Clamp01(Mathf.Min(tb / 0.8f, (bar - tb) / 0.6f));
                float r = roots[b];
                float pad = (Sine(r, t) + Sine(r * thirds[b], t) * 0.8f + Sine(r * 1.5f, t) * 0.7f + Sine(r * 0.5f, t) * 0.6f) * 0.06f * swell;

                float eighth = beat * 0.5f;
                int step = (int)(tb / eighth);
                float te = tb - step * eighth;
                float[] chord = { r * 2f, r * 2f * thirds[b], r * 3f, r * 4f };
                float pluck = Sine(chord[step % 4], te) * Mathf.Exp(-te * 7f) * 0.09f;
                float kick = tb < beat ? Mathf.Sin(2f * Mathf.PI * (45f * tb + 2f * (1f - Mathf.Exp(-25f * tb)))) * Mathf.Exp(-tb * 7f) * 0.3f : 0f;
                data[i] = SoftClip(pad + pluck + kick);
            }
            return ToClip("MenuMusic", data);
        }

        // ---------------- Building blocks ----------------

        sealed class LowPass
        {
            float y;
            public float A;
            public LowPass(float a) { A = a; }
            public float Next(float x) { y += A * (x - y); return y; }
        }

        static readonly LowPass noiseFilter = new LowPass(0.5f);

        static float Noise() => (float)random.NextDouble() * 2f - 1f;

        // Filtered noise; smaller a = darker. Filter state carries between samples of one clip.
        static float LowNoise(float a)
        {
            noiseFilter.A = Mathf.Clamp01(a);
            return noiseFilter.Next(Noise()) * (1f + 0.6f / Mathf.Max(a, 0.05f) * 0.1f);
        }

        static float Sine(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);

        static float Square(float f, float t) => Sine(f, t) >= 0f ? 1f : -1f;

        // Bright harmonic tone, roughly a brass instrument.
        static float Brass(float f, float t)
        {
            float s = 0f;
            for (int k = 1; k <= 6; k++) s += Sine(f * k, t) / k;
            return s;
        }

        static float Envelope(float t, float length, float attack, float release) =>
            Mathf.Clamp01(Mathf.Min(t / attack, (length - t) / release));

        static float SoftClip(float x) => x / (1f + Mathf.Abs(x));

        static AudioClip Clip(string name, float seconds, System.Func<float, float> sample)
        {
            noiseFilter.Next(0f);
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                // Short fade at both ends so no clip starts or stops with a click.
                float edge = Mathf.Clamp01(Mathf.Min(t / 0.002f, (seconds - t) / 0.01f));
                data[i] = SoftClip(sample(t) * 1.2f) * edge;
            }
            return ToClip(name, data);
        }

        static AudioClip ToClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
