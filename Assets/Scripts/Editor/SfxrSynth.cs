using System;
using System.IO;
using UnityEngine;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// DrPetter's sfxr, as synthesis rather than as a website.
    ///
    /// The browser tool at sfxr.me exports one file per click, which is fine for a
    /// person auditioning and useless for filling eleven sounds with four variants
    /// each. Implementing the algorithm instead means the parameters live in a
    /// file that can be re-run, and a sound nobody likes is a changed seed rather
    /// than a re-download.
    ///
    /// This is a faithful port of the original synth loop, including its
    /// eight-times supersampling and its slightly odd ordering - the envelope is
    /// advanced before it is evaluated, the high-pass runs off the low-pass's
    /// delta. Those details are what makes it sound like sfxr rather than like a
    /// square wave with an envelope on it.
    /// </summary>
    public struct SfxrParams
    {
        /// <summary>0 square, 1 saw, 2 sine, 3 noise.</summary>
        public int WaveType;

        public float BaseFreq, FreqLimit, FreqRamp, FreqDramp;
        public float Duty, DutyRamp;
        public float VibStrength, VibSpeed;
        public float EnvAttack, EnvSustain, EnvPunch, EnvDecay;
        public float ArpMod, ArpSpeed;
        public float PhaOffset, PhaRamp;
        public float LpfFreq, LpfRamp, LpfResonance;
        public float HpfFreq, HpfRamp;
        public float RepeatSpeed;

        /// <summary>The defaults sfxr starts every new sound from.</summary>
        public static SfxrParams Default()
        {
            return new SfxrParams
            {
                WaveType = 0,
                BaseFreq = 0.3f,
                FreqLimit = 0f,
                EnvAttack = 0f,
                EnvSustain = 0.3f,
                EnvDecay = 0.4f,
                EnvPunch = 0f,
                Duty = 0f,
                LpfFreq = 1f,
                LpfResonance = 0f
            };
        }
    }

    /// <summary>Which of sfxr's classic generators a sound is drawn from.</summary>
    public enum SfxrKind
    {
        Laser,
        Explosion,
        HitHurt,
        Powerup,
        Blip
    }

    public static class SfxrSynth
    {
        public const int SampleRate = 44100;

        /// <summary>
        /// Nothing this game plays is long. A cap stops a pathological parameter
        /// set - a slow decay with a frequency limit it never reaches - from
        /// writing a multi-megabyte file nobody asked for.
        /// </summary>
        private const int MaxSamples = SampleRate * 4;

        public static float[] Render(SfxrParams p)
        {
            float fperiod = 100.0f / (p.BaseFreq * p.BaseFreq + 0.001f);
            int period = (int)fperiod;
            float fmaxperiod = 100.0f / (p.FreqLimit * p.FreqLimit + 0.001f);

            float fslide = 1.0f - Cube(p.FreqRamp) * 0.01f;
            float fdslide = -Cube(p.FreqDramp) * 0.000001f;

            float squareDuty = 0.5f - p.Duty * 0.5f;
            float squareSlide = -p.DutyRamp * 0.00005f;

            float arpMod = p.ArpMod >= 0f
                ? 1.0f - p.ArpMod * p.ArpMod * 0.9f
                : 1.0f + p.ArpMod * p.ArpMod * 10.0f;
            int arpTime = 0;
            int arpLimit = (int)(p.ArpSpeed * p.ArpSpeed * 20000f + 32f);
            if (p.ArpSpeed >= 1f)
            {
                arpLimit = 0;
            }

            // Low-pass. Resonance feeds the damping term, which is why a high
            // resonance and a low cutoff together is what makes a laser sing.
            float fltp = 0f, fltdp = 0f, fltphp = 0f;
            float fltw = Cube(p.LpfFreq) * 0.1f;
            float fltwD = 1.0f + p.LpfRamp * 0.0001f;
            float fltdmp = 5.0f / (1.0f + p.LpfResonance * p.LpfResonance * 20f) * (0.01f + fltw);
            if (fltdmp > 0.8f)
            {
                fltdmp = 0.8f;
            }

            float flthp = p.HpfFreq * p.HpfFreq * 0.1f;
            float flthpD = 1.0f + p.HpfRamp * p.HpfRamp * (p.HpfRamp < 0f ? -0.0003f : 0.0003f);

            float vibPhase = 0f;
            float vibSpeed = p.VibSpeed * p.VibSpeed * 0.01f;
            float vibAmp = p.VibStrength * 0.5f;

            int envStage = 0, envTime = 0;
            int[] envLength =
            {
                Mathf.Max(1, (int)(p.EnvAttack * p.EnvAttack * 100000f)),
                Mathf.Max(1, (int)(p.EnvSustain * p.EnvSustain * 100000f)),
                Mathf.Max(1, (int)(p.EnvDecay * p.EnvDecay * 100000f))
            };

            float fphase = p.PhaOffset * p.PhaOffset * 1020f;
            if (p.PhaOffset < 0f)
            {
                fphase = -fphase;
            }

            float fdphase = p.PhaRamp * p.PhaRamp;
            if (p.PhaRamp < 0f)
            {
                fdphase = -fdphase;
            }

            int iphase = Mathf.Abs((int)fphase);
            int ipp = 0;
            float[] phaserBuffer = new float[1024];

            System.Random noiseRng = new System.Random(1);
            float[] noiseBuffer = new float[32];
            for (int i = 0; i < 32; i++)
            {
                noiseBuffer[i] = (float)(noiseRng.NextDouble() * 2.0 - 1.0);
            }

            int repTime = 0;
            int repLimit = (int)(p.RepeatSpeed * p.RepeatSpeed * 20000f + 32f);
            if (p.RepeatSpeed <= 0f)
            {
                repLimit = 0;
            }

            // Reset targets, restored when repeat fires. Only the pitch sweep
            // restarts - the envelope keeps running, which is what makes repeat
            // sound like one sound stuttering rather than several sounds.
            float baseFperiod = fperiod;
            float baseFslide = fslide;
            float baseSquareDuty = squareDuty;

            float[] output = new float[MaxSamples];
            int written = 0;
            int phase = 0;

            for (int n = 0; n < MaxSamples; n++)
            {
                repTime++;
                if (repLimit != 0 && repTime >= repLimit)
                {
                    repTime = 0;
                    fperiod = baseFperiod;
                    fslide = baseFslide;
                    squareDuty = baseSquareDuty;
                }

                arpTime++;
                if (arpLimit != 0 && arpTime >= arpLimit)
                {
                    arpLimit = 0;
                    fperiod *= arpMod;
                }

                fslide += fdslide;
                fperiod *= fslide;
                if (fperiod > fmaxperiod)
                {
                    fperiod = fmaxperiod;
                    if (p.FreqLimit > 0f)
                    {
                        break;
                    }
                }

                float rfperiod = fperiod;
                if (vibAmp > 0f)
                {
                    vibPhase += vibSpeed;
                    rfperiod = fperiod * (1.0f + Mathf.Sin(vibPhase) * vibAmp);
                }

                period = (int)rfperiod;
                if (period < 8)
                {
                    period = 8;
                }

                squareDuty += squareSlide;
                squareDuty = Mathf.Clamp(squareDuty, 0f, 0.5f);

                envTime++;
                if (envTime > envLength[envStage])
                {
                    envTime = 0;
                    envStage++;
                    if (envStage == 3)
                    {
                        break;
                    }
                }

                float envVol;
                switch (envStage)
                {
                    case 0: envVol = (float)envTime / envLength[0]; break;
                    case 1: envVol = 1.0f + (1.0f - (float)envTime / envLength[1]) * 2.0f * p.EnvPunch; break;
                    default: envVol = 1.0f - (float)envTime / envLength[2]; break;
                }

                fphase += fdphase;
                iphase = Mathf.Min(1023, Mathf.Abs((int)fphase));

                if (flthpD != 0f)
                {
                    flthp = Mathf.Clamp(flthp * flthpD, 0.00001f, 0.1f);
                }

                // Eight samples per output sample. This is where sfxr's grit comes
                // from: the aliasing of the raw oscillator is averaged rather than
                // filtered, so it stays bright without screaming.
                float ssample = 0f;
                for (int si = 0; si < 8; si++)
                {
                    phase++;
                    if (phase >= period)
                    {
                        phase %= period;
                        if (p.WaveType == 3)
                        {
                            for (int i = 0; i < 32; i++)
                            {
                                noiseBuffer[i] = (float)(noiseRng.NextDouble() * 2.0 - 1.0);
                            }
                        }
                    }

                    float fp = (float)phase / period;
                    float sample;
                    switch (p.WaveType)
                    {
                        case 0: sample = fp < squareDuty ? 0.5f : -0.5f; break;
                        case 1: sample = 1.0f - fp * 2f; break;
                        case 2: sample = Mathf.Sin(fp * 2f * Mathf.PI); break;
                        default: sample = noiseBuffer[Mathf.Min(31, phase * 32 / period)]; break;
                    }

                    float pp = fltp;
                    fltw = Mathf.Clamp(fltw * fltwD, 0f, 0.1f);
                    if (p.LpfFreq != 1.0f)
                    {
                        fltdp += (sample - fltp) * fltw;
                        fltdp -= fltdp * fltdmp;
                    }
                    else
                    {
                        fltp = sample;
                        fltdp = 0f;
                    }

                    fltp += fltdp;

                    fltphp += fltp - pp;
                    fltphp -= fltphp * flthp;
                    sample = fltphp;

                    phaserBuffer[ipp & 1023] = sample;
                    sample += phaserBuffer[(ipp - iphase + 1024) & 1023];
                    ipp = (ipp + 1) & 1023;

                    ssample += sample * envVol;
                }

                output[written++] = ssample / 8f;
            }

            float[] trimmed = new float[written];
            Array.Copy(output, trimmed, written);
            return trimmed;
        }

        private static float Cube(float v)
        {
            return v * v * v;
        }

        /// <summary>
        /// Scales to a fixed peak.
        ///
        /// sfxr's own output level swings wildly with the parameters, and per-sound
        /// balance already lives on the SoundDefinition's volume field. Normalising
        /// here means that field means what it says instead of compensating for
        /// whatever the synth happened to produce.
        /// </summary>
        public static void Normalise(float[] samples, float peak = 0.89f)
        {
            float loudest = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float a = Mathf.Abs(samples[i]);
                if (a > loudest)
                {
                    loudest = a;
                }
            }

            if (loudest < 0.0001f)
            {
                return;
            }

            float scale = peak / loudest;
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] *= scale;
            }
        }

        /// <summary>
        /// Fades the last few milliseconds to zero.
        ///
        /// A sound cut off mid-cycle ends on a step, and a step is a click. sfxr's
        /// envelope reaches zero on its own, but the frequency-limit exit does not.
        /// </summary>
        public static void FadeOut(float[] samples, int milliseconds = 8)
        {
            int fade = Mathf.Min(samples.Length, SampleRate * milliseconds / 1000);
            for (int i = 0; i < fade; i++)
            {
                samples[samples.Length - 1 - i] *= (float)i / fade;
            }
        }

        /// <summary>
        /// Writes 16-bit PCM. Channels and rate are parameters because this also
        /// writes level-corrected copies of imported clips, which are not all mono
        /// and not all 44.1k - a copy that silently changed either would be a
        /// different sound, not a quieter one.
        /// </summary>
        public static void WriteWav(string path, float[] samples, int channels = 1, int sampleRate = SampleRate)
        {
            channels = Mathf.Max(1, channels);
            sampleRate = Mathf.Max(1, sampleRate);

            using (FileStream file = new FileStream(path, FileMode.Create))
            using (BinaryWriter w = new BinaryWriter(file))
            {
                int dataBytes = samples.Length * 2;
                int blockAlign = channels * 2;

                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + dataBytes);
                w.Write(new[] { 'W', 'A', 'V', 'E' });
                w.Write(new[] { 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);                       // PCM
                w.Write((short)channels);
                w.Write(sampleRate);
                w.Write(sampleRate * blockAlign);        // byte rate
                w.Write((short)blockAlign);
                w.Write((short)16);                      // bits
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(dataBytes);

                for (int i = 0; i < samples.Length; i++)
                {
                    w.Write((short)(Mathf.Clamp(samples[i], -1f, 1f) * 32767f));
                }
            }
        }
    }

    /// <summary>
    /// sfxr's classic generators, kept beside the synth rather than in the editor
    /// tool so they can be exercised without Unity - which matters here, because
    /// whoever writes these parameters cannot hear the result and needs some other
    /// way to know a preset produces a sound at all.
    /// </summary>
    public static class SfxrPresets
    {
        /// <summary>
        /// Shifts a sound's pitch, moving its sweep target with it.
        ///
        /// Scaling the base frequency alone silences the laser preset outright.
        /// That preset derives its frequency limit from the base it picked, so a
        /// lower base against an unchanged limit means the downward sweep has
        /// already passed its target on the very first sample - and the synth
        /// stops the moment it does, writing an empty clip. The boss's shot is
        /// pitched down 35%, which is exactly far enough to hit it.
        /// </summary>
        public static void Pitch(ref SfxrParams p, float scale)
        {
            p.BaseFreq = Mathf.Clamp(p.BaseFreq * scale, 0.02f, 1f);

            if (p.FreqLimit > 0f)
            {
                p.FreqLimit = Mathf.Clamp(p.FreqLimit * scale, 0f, 1f);
            }
        }

        // ---------- presets ----------
        //
        // sfxr's classic generators. The ranges are the original's; what varies
        // between runs is which value inside them each variant draws.

        public static SfxrParams Build(SfxrKind kind, System.Random rng)
        {
            switch (kind)
            {
                case SfxrKind.Laser: return Laser(rng);
                case SfxrKind.Explosion: return Explosion(rng);
                case SfxrKind.HitHurt: return HitHurt(rng);
                case SfxrKind.Powerup: return Powerup(rng);
                default: return Blip(rng);
            }
        }

        private static SfxrParams Laser(System.Random rng)
        {
            SfxrParams p = SfxrParams.Default();
            p.WaveType = Rnd(rng, 3);
            if (p.WaveType == 2 && Rnd(rng, 2) == 0)
            {
                p.WaveType = Rnd(rng, 2);
            }

            p.BaseFreq = 0.5f + Frnd(rng, 0.5f);
            p.FreqLimit = p.BaseFreq - 0.2f - Frnd(rng, 0.6f);
            if (p.FreqLimit < 0.2f)
            {
                p.FreqLimit = 0.2f;
            }

            p.FreqRamp = -0.15f - Frnd(rng, 0.2f);

            if (Rnd(rng, 3) == 0)
            {
                p.BaseFreq = 0.3f + Frnd(rng, 0.6f);
                p.FreqLimit = Frnd(rng, 0.1f);
                p.FreqRamp = -0.35f - Frnd(rng, 0.3f);
            }

            if (Rnd(rng, 1) == 0)
            {
                p.Duty = Frnd(rng, 0.5f);
                p.DutyRamp = Frnd(rng, 0.2f);
            }
            else
            {
                p.Duty = 0.4f + Frnd(rng, 0.5f);
                p.DutyRamp = -Frnd(rng, 0.7f);
            }

            p.EnvAttack = 0f;
            p.EnvSustain = 0.1f + Frnd(rng, 0.2f);
            p.EnvDecay = Frnd(rng, 0.4f);
            if (Rnd(rng, 1) == 0)
            {
                p.EnvPunch = Frnd(rng, 0.3f);
            }

            if (Rnd(rng, 2) == 0)
            {
                p.PhaOffset = Frnd(rng, 0.2f);
                p.PhaRamp = -Frnd(rng, 0.2f);
            }

            if (Rnd(rng, 1) == 0)
            {
                p.HpfFreq = Frnd(rng, 0.3f);
            }

            return p;
        }

        private static SfxrParams Explosion(System.Random rng)
        {
            SfxrParams p = SfxrParams.Default();
            p.WaveType = 3;

            if (Rnd(rng, 1) == 0)
            {
                p.BaseFreq = 0.1f + Frnd(rng, 0.4f);
                p.FreqRamp = -0.1f + Frnd(rng, 0.4f);
            }
            else
            {
                p.BaseFreq = 0.2f + Frnd(rng, 0.7f);
                p.FreqRamp = -0.2f - Frnd(rng, 0.2f);
            }

            p.BaseFreq *= p.BaseFreq;

            if (Rnd(rng, 4) == 0)
            {
                p.FreqRamp = 0f;
            }

            if (Rnd(rng, 2) == 0)
            {
                p.RepeatSpeed = 0.3f + Frnd(rng, 0.5f);
            }

            p.EnvAttack = 0f;
            p.EnvSustain = 0.1f + Frnd(rng, 0.3f);
            p.EnvDecay = Frnd(rng, 0.5f);

            if (Rnd(rng, 1) == 0)
            {
                p.PhaOffset = -0.3f + Frnd(rng, 0.9f);
                p.PhaRamp = -Frnd(rng, 0.3f);
            }

            p.EnvPunch = 0.2f + Frnd(rng, 0.6f);

            if (Rnd(rng, 1) == 0)
            {
                p.VibStrength = Frnd(rng, 0.7f);
                p.VibSpeed = Frnd(rng, 0.6f);
            }

            if (Rnd(rng, 2) == 0)
            {
                p.ArpSpeed = 0.6f + Frnd(rng, 0.3f);
                p.ArpMod = 0.8f - Frnd(rng, 1.6f);
            }

            return p;
        }

        private static SfxrParams HitHurt(System.Random rng)
        {
            SfxrParams p = SfxrParams.Default();
            p.WaveType = Rnd(rng, 2);
            if (p.WaveType == 2)
            {
                p.WaveType = 3;
            }

            if (p.WaveType == 0)
            {
                p.Duty = Frnd(rng, 0.6f);
            }

            p.BaseFreq = 0.2f + Frnd(rng, 0.6f);
            p.FreqRamp = -0.3f - Frnd(rng, 0.4f);
            p.EnvAttack = 0f;
            p.EnvSustain = Frnd(rng, 0.1f);
            p.EnvDecay = 0.1f + Frnd(rng, 0.2f);

            if (Rnd(rng, 1) == 0)
            {
                p.HpfFreq = Frnd(rng, 0.3f);
            }

            return p;
        }

        private static SfxrParams Powerup(System.Random rng)
        {
            SfxrParams p = SfxrParams.Default();

            if (Rnd(rng, 1) == 0)
            {
                p.WaveType = 1;
            }
            else
            {
                p.Duty = Frnd(rng, 0.6f);
            }

            if (Rnd(rng, 1) == 0)
            {
                p.BaseFreq = 0.2f + Frnd(rng, 0.3f);
                p.FreqRamp = 0.1f + Frnd(rng, 0.4f);
                p.RepeatSpeed = 0.4f + Frnd(rng, 0.4f);
            }
            else
            {
                p.BaseFreq = 0.2f + Frnd(rng, 0.3f);
                p.FreqRamp = 0.05f + Frnd(rng, 0.2f);

                if (Rnd(rng, 1) == 0)
                {
                    p.VibStrength = Frnd(rng, 0.7f);
                    p.VibSpeed = Frnd(rng, 0.6f);
                }
            }

            p.EnvAttack = 0f;
            p.EnvSustain = Frnd(rng, 0.4f);
            p.EnvDecay = 0.1f + Frnd(rng, 0.4f);

            return p;
        }

        private static SfxrParams Blip(System.Random rng)
        {
            SfxrParams p = SfxrParams.Default();
            p.WaveType = Rnd(rng, 1);

            if (p.WaveType == 0)
            {
                p.Duty = Frnd(rng, 0.6f);
            }

            p.BaseFreq = 0.2f + Frnd(rng, 0.4f);
            p.EnvAttack = 0f;
            p.EnvSustain = 0.1f + Frnd(rng, 0.1f);
            p.EnvDecay = Frnd(rng, 0.2f);
            p.HpfFreq = 0.1f;

            return p;
        }

        private static float Frnd(System.Random rng, float range)
        {
            return (float)rng.NextDouble() * range;
        }

        private static int Rnd(System.Random rng, int max)
        {
            return rng.Next(0, max + 1);
        }
    }
}
