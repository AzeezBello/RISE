using UnityEngine;

namespace Rise.UI
{
    // Procedural audio synthesis: engine hum pitched to vehicle speed, rain loop, generator drone during NEPA outages,
    // horn on crash, and two-tone cash chime on transactions. Generated in memory so no audio files are required.
    public class SoundSystem : MonoBehaviour
    {
        public static SoundSystem I { get; private set; }

        Session session;
        AudioSource engineSrc, rainSrc, genSrc, streetAmbienceSrc, owambeMusicSrc, sfxSrc;
        readonly AudioSource[] sfxPool = new AudioSource[4];
        int sfxPoolIdx;
        AudioClip chimeClip, hornClip, gunshotClip, whistleClip, kickClip, woodworkClip, crowdClip, streetAmbienceClip, owambeMusicClip;
        public bool Initialized { get; private set; }
        public string CurrentBiome { get; private set; } = "coastal";
        public string CurrentMusicStyle { get; private set; } = "Afrobeats & Owambe";
        public AudioClip StreetAmbienceClip => streetAmbienceClip;
        public AudioClip OwambeMusicClip => owambeMusicClip;
        public float StreetAmbienceVolume => streetAmbienceSrc != null ? streetAmbienceSrc.volume : 0f;
        public float OwambeMusicVolume => owambeMusicSrc != null ? owambeMusicSrc.volume : 0f;

        public void SetBiome(string biome)
        {
            CurrentBiome = string.IsNullOrEmpty(biome) ? "coastal" : biome;
            switch (CurrentBiome)
            {
                case "southwest":
                    CurrentMusicStyle = "Fuji & Apala";
                    if (owambeMusicSrc != null) owambeMusicSrc.pitch = 0.95f;
                    break;
                case "southeast":
                    CurrentMusicStyle = "Ogene & Highlife";
                    if (owambeMusicSrc != null) owambeMusicSrc.pitch = 1.06f;
                    break;
                case "niger_delta":
                    CurrentMusicStyle = "Port Harcourt Creek Highlife";
                    if (owambeMusicSrc != null) owambeMusicSrc.pitch = 1.02f;
                    break;
                case "sahel":
                    CurrentMusicStyle = "Hausa Goje & Kalangu";
                    if (owambeMusicSrc != null) owambeMusicSrc.pitch = 0.90f;
                    break;
                case "abuja":
                    CurrentMusicStyle = "Abuja Executive Lounge & Afrobeats";
                    if (owambeMusicSrc != null) owambeMusicSrc.pitch = 1.0f;
                    break;
                default:
                    CurrentMusicStyle = "Afrobeats & Owambe";
                    if (owambeMusicSrc != null) owambeMusicSrc.pitch = 1.0f;
                    break;
            }
        }

        public static string RegionalGreeting(string stateId)
        {
            switch (stateId)
            {
                case "ogun": return "E ku ishe o! Welcome to Ogun State";
                case "oyo": return "E kaabo si Ibadan! Oyo Alaafin";
                case "fct": return "Sannu da zuwa! Welcome to Abuja FCT";
                case "rivers": return "Bole & Fish! Welcome to Port Harcourt";
                case "anambra": return "Nnoo! Kedu? Welcome to Onitsha & East";
                case "kano": return "Sannu da zuwa! Ina kwana? Welcome to Kano";
                default: return "E kaabo! How far? Welcome to Lagos";
            }
        }

        const int SampleRate = 22050;

        public void Init(Session s)
        {
            I = this;
            session = s;
            if ( gameObject.GetComponent<AudioListener>() == null && FindAnyObjectByType<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();

            engineSrc = MakeLoopSource("Engine Audio", MakeEngineClip());
            rainSrc = MakeLoopSource("Rain Audio", MakeRainClip());
            genSrc = MakeLoopSource("Generator Audio", MakeGeneratorClip());
            streetAmbienceClip = MakeStreetAmbienceClip();
            owambeMusicClip = MakeOwambeMusicClip();
            streetAmbienceSrc = MakeLoopSource("Lagos Street Ambience", streetAmbienceClip);
            owambeMusicSrc = MakeLoopSource("Owambe & Afrobeats Music", owambeMusicClip);
            for (int i = 0; i < sfxPool.Length; i++)
            {
                var src = i == 0 ? gameObject.AddComponent<AudioSource>() : new GameObject("SFX_Voice_" + i).AddComponent<AudioSource>();
                if (i > 0) src.transform.SetParent(transform, false);
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                sfxPool[i] = src;
            }
            sfxSrc = sfxPool[0];

            chimeClip = MakeChimeClip();
            hornClip = MakeHornClip();
            gunshotClip = MakeGunshotClip();
            whistleClip = MakeWhistleClip();
            kickClip = MakeKickClip();
            woodworkClip = MakeWoodworkClip();
            crowdClip = MakeCrowdClip();
            Initialized = true;
        }

        void PlayPolySfx(AudioClip clip, float volume, float pitchJitter = 0.05f)
        {
            if (!Initialized || session?.State?.S == null || !session.State.S.sound || clip == null) return;
            var src = sfxPool[sfxPoolIdx];
            sfxPoolIdx = (sfxPoolIdx + 1) % sfxPool.Length;
            if (src == null) src = sfxSrc;
            if (src == null) return;
            src.pitch = pitchJitter > 0.001f ? 1f + Random.Range(-pitchJitter, pitchJitter) : 1f;
            src.PlayOneShot(clip, volume);
        }

        AudioSource MakeLoopSource(string label, AudioClip clip)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.volume = 0f;
            src.spatialBlend = 0f;
            src.playOnAwake = false;
            src.Play();
            return src;
        }

        static AudioClip MakeEngineClip()
        {
            // One second of lowpass-smoothed 55 Hz sawtooth (110 cycles in 22050 samples for seamless loop).
            int n = SampleRate;
            var data = new float[n];
            float prev = 0f;
            const float freq = 55f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float raw = 2f * ((t * freq) - Mathf.Floor(0.5f + t * freq));
                prev = prev + 0.12f * (raw - prev);
                data[i] = prev * 0.6f;
            }
            var clip = AudioClip.Create("EngineHum", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeRainClip()
        {
            int n = SampleRate * 2;
            var data = new float[n];
            var rng = new System.Random(101);
            float prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                float hp = white - prev;
                prev = white * 0.85f;
                data[i] = Mathf.Clamp(hp * 0.35f, -1f, 1f);
            }
            var clip = AudioClip.Create("RainLoop", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeGeneratorClip()
        {
            int n = SampleRate;
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float wave = Mathf.Sin(t * 120f * Mathf.PI * 2f) * 0.6f + Mathf.Sin(t * 240f * Mathf.PI * 2f) * 0.3f + Mathf.Sin(t * 60f * Mathf.PI * 2f) * 0.25f;
                data[i] = wave * 0.5f;
            }
            var clip = AudioClip.Create("GeneratorHum", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // 4-second looping Lagos street ambience: warm market murmur, traffic rumble, and distant danfo horn calls.
        static AudioClip MakeStreetAmbienceClip()
        {
            int n = SampleRate * 4;
            var data = new float[n];
            var rng = new System.Random(1999);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.045f * (noise - lp);
                float rumble = Mathf.Sin(t * 48f * Mathf.PI * 2f) * 0.18f + Mathf.Sin(t * 96f * Mathf.PI * 2f) * 0.08f;
                // Distant danfo two-tap horn around t=1.1s and t=1.3s, plus keke beep at t=2.8s
                float horn = 0f;
                if ((t > 1.05f && t < 1.20f) || (t > 1.24f && t < 1.42f))
                    horn = (Mathf.Sin(t * 392f * Mathf.PI * 2f) + 0.6f * Mathf.Sin(t * 493.88f * Mathf.PI * 2f)) * 0.14f;
                else if (t > 2.75f && t < 2.92f)
                    horn = Mathf.Sin(t * 587.33f * Mathf.PI * 2f) * 0.11f;
                data[i] = Mathf.Clamp((lp * 0.48f + rumble + horn) * 0.55f, -1f, 1f);
            }
            var clip = AudioClip.Create("LagosStreetAmbience", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // 4-second looping Owambe Jùjú / Afrobeats groove (120 BPM): talking drum pitch bends, shaker clave, and highlife chord groove.
        static AudioClip MakeOwambeMusicClip()
        {
            int n = SampleRate * 4;
            var data = new float[n];
            var rng = new System.Random(2026);
            float[] notes = { 261.63f, 329.63f, 392.00f, 440.00f, 392.00f, 329.63f, 293.66f, 392.00f };
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float beat = (t * 2f) % 1f;          // 120 BPM quarter-note phase
                float sixteenth = (t * 8f) % 1f;     // 16th-note shaker/clave phase
                int step = Mathf.FloorToInt(t * 2f) % notes.Length;

                // Talking drum (Gangan) pitch-bent bass hit
                float drumFreq = Mathf.Lerp(145f, 82f, beat) + 22f * Mathf.Sin(t * 6f * Mathf.PI);
                float drumEnv = Mathf.Exp(-beat * 6.5f);
                float talkingDrum = Mathf.Sin(t * drumFreq * Mathf.PI * 2f) * drumEnv * 0.42f;

                // Shekere / Afrobeats shaker
                float shakerNoise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float shakerEnv = (sixteenth < 0.25f) ? Mathf.Exp(-sixteenth * 18f) * 0.18f : 0.02f;
                float shaker = shakerNoise * shakerEnv;

                // Highlife guitar / keyboard pluck
                float f = notes[step];
                float pluckEnv = Mathf.Exp(-((t * 4f) % 1f) * 4.5f);
                float highlife = (Mathf.Sin(t * f * Mathf.PI * 2f) + 0.5f * Mathf.Sin(t * f * 2f * Mathf.PI * 2f)) * pluckEnv * 0.26f;

                data[i] = Mathf.Clamp((talkingDrum + shaker + highlife) * 0.65f, -1f, 1f);
            }
            var clip = AudioClip.Create("OwambeAfrobeatsLoop", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeChimeClip()
        {
            int n = Mathf.RoundToInt(SampleRate * 0.4f);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float freq = t < 0.08f ? 880f : 1320f;
                float env = Mathf.Exp(-t * 12f);
                data[i] = Mathf.Sin(t * freq * Mathf.PI * 2f) * env * 0.3f;
            }
            var clip = AudioClip.Create("CashChime", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeHornClip()
        {
            int n = Mathf.RoundToInt(SampleRate * 0.5f);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 8f);
                float sq = Mathf.Sin(t * 180f * Mathf.PI * 2f) >= 0f ? 1f : -1f;
                data[i] = sq * env * 0.22f;
            }
            var clip = AudioClip.Create("CrashHorn", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeGunshotClip()
        {
            int n = Mathf.RoundToInt(SampleRate * 0.28f);
            var data = new float[n];
            var rng = new System.Random(707);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 32f);
                float thump = Mathf.Sin(t * 115f * Mathf.PI * 2f) * Mathf.Exp(-t * 20f);
                data[i] = Mathf.Clamp((noise * 0.65f + thump * 0.55f) * 0.55f, -1f, 1f);
            }
            var clip = AudioClip.Create("GunshotCrack", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void PlayCash() => PlayPolySfx(chimeClip, 0.5f, 0.02f);

        public void PlayHorn() => PlayPolySfx(hornClip, 0.52f, 0.06f);

        public void PlayGunshot(float volume = 0.6f) => PlayPolySfx(gunshotClip, volume, 0.06f);

        static AudioClip MakeWhistleClip()
        {
            int n = Mathf.RoundToInt(SampleRate * 0.45f);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float trill = 2850f + Mathf.Sin(t * 48f * Mathf.PI * 2f) * 140f;
                float env = Mathf.Clamp01(t / 0.03f) * Mathf.Exp(-Mathf.Max(0f, t - 0.25f) * 10f);
                data[i] = Mathf.Sin(t * trill * Mathf.PI * 2f) * env * 0.25f;
            }
            var clip = AudioClip.Create("RefWhistle", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeKickClip()
        {
            int n = Mathf.RoundToInt(SampleRate * 0.16f);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float freq = Mathf.Lerp(155f, 52f, t / 0.16f);
                float env = Mathf.Exp(-t * 28f);
                data[i] = Mathf.Sin(t * freq * Mathf.PI * 2f) * env * 0.55f;
            }
            var clip = AudioClip.Create("BallKick", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeCrowdClip()
        {
            int n = Mathf.RoundToInt(SampleRate * 1.1f);
            var data = new float[n];
            var rng = new System.Random(303);
            float prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                prev = prev + 0.18f * (white - prev);
                float horn = Mathf.Sin(t * 440f * Mathf.PI * 2f) * 0.25f + Mathf.Sin(t * 554.37f * Mathf.PI * 2f) * 0.2f;
                float env = Mathf.Clamp01(t / 0.08f) * Mathf.Exp(-t * 2.2f);
                data[i] = Mathf.Clamp((prev * 0.7f + horn) * env * 0.45f, -1f, 1f);
            }
            var clip = AudioClip.Create("CrowdCheer", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeWoodworkClip()
        {
            int n = Mathf.RoundToInt(SampleRate * 0.32f);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float ring = Mathf.Sin(t * 620f * Mathf.PI * 2f) * 0.55f + Mathf.Sin(t * 1180f * Mathf.PI * 2f) * 0.35f + Mathf.Sin(t * 195f * Mathf.PI * 2f) * 0.4f;
                float env = Mathf.Exp(-t * 14f);
                data[i] = Mathf.Clamp(ring * env * 0.6f, -1f, 1f);
            }
            var clip = AudioClip.Create("WoodworkClang", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void PlayWhistle() => PlayPolySfx(whistleClip, 0.45f, 0.02f);

        public void PlayKick(float volume = 0.55f) => PlayPolySfx(kickClip, volume, 0.07f);

        public void PlayWoodwork() => PlayPolySfx(woodworkClip, 0.72f, 0.04f);

        public void PlayCrowdCheer() => PlayPolySfx(crowdClip, 0.55f, 0.04f);

        void Update()
        {
            if (!Initialized || session == null || session.State?.S == null) return;
            bool on = session.State.S.sound;
            float master = on ? 0.5f : 0f;
            float dt = Time.deltaTime;

            bool driving = on && session.Vehicle != null;
            float kmh = driving ? session.Vehicle.Kmh : 0f;
            float targetPitch = driving ? 1f + (kmh * 1.6f) / 55f : 1f;
            float targetEngineVol = driving ? (0.08f + Mathf.Min(0.12f, kmh / 600f)) * master : 0f;
            engineSrc.pitch = Mathf.Lerp(engineSrc.pitch, targetPitch, 1f - Mathf.Exp(-10f * dt));
            engineSrc.volume = Mathf.Lerp(engineSrc.volume, targetEngineVol, 1f - Mathf.Exp(-7f * dt));

            bool raining = on && session.Weather != null && session.Weather.Raining;
            rainSrc.volume = Mathf.Lerp(rainSrc.volume, raining ? 0.035f * master : 0f, 1f - Mathf.Exp(-2f * dt));

            bool outage = on && session.Events != null && session.Events.Outage > 0f;
            genSrc.volume = Mathf.Lerp(genSrc.volume, outage ? 0.07f * master : 0f, 1f - Mathf.Exp(-2f * dt));

            // Lagos street ambience & Owambe / Afrobeats music behind the Sound toggle
            bool nearParty = false;
            if (on && session.Data != null && session.Focus != null)
            {
                var pos = session.Focus.position;
                bool insideParty = session.InInterior && session.IndoorStations.Exists(st => st.Id == "club_dance");
                var nearOwambe = session.Data.NearestLandmark("owambe", pos, 32f);
                var nearVenue = session.Data.NearestLandmark("venue", pos, 32f);
                nearParty = insideParty || nearOwambe != null || nearVenue != null;
            }
            float targetStreetVol = on && !session.InInterior ? 0.09f * master : 0f;
            float targetOwambeVol = on && nearParty ? 0.16f * master : (on && session.OwambeOpen ? 0.05f * master : 0f);
            streetAmbienceSrc.volume = Mathf.Lerp(streetAmbienceSrc.volume, targetStreetVol, 1f - Mathf.Exp(-4f * dt));
            owambeMusicSrc.volume = Mathf.Lerp(owambeMusicSrc.volume, targetOwambeVol, 1f - Mathf.Exp(-4f * dt));
        }

        public void SyncAmbientNow()
        {
            if (!Initialized || session == null || session.State?.S == null) return;
            bool on = session.State.S.sound;
            float master = on ? 0.5f : 0f;
            bool nearParty = false;
            if (on && session.Data != null && session.Focus != null)
            {
                var pos = session.Focus.position;
                bool insideParty = session.InInterior && session.IndoorStations.Exists(st => st.Id == "club_dance");
                nearParty = insideParty || session.Data.NearestLandmark("owambe", pos, 32f) != null || session.Data.NearestLandmark("venue", pos, 32f) != null;
            }
            streetAmbienceSrc.volume = on && !session.InInterior ? 0.09f * master : 0f;
            owambeMusicSrc.volume = on && (nearParty || session.OwambeOpen) ? (nearParty ? 0.16f * master : 0.05f * master) : 0f;
        }
    }
}
