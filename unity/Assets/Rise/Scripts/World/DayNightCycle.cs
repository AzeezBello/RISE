using UnityEngine;

namespace Rise.World
{
    // Game clock (one game hour per 45 real seconds by default, a new game starts at 08:30) driving the sun, ambient
    // light, fog and the street-lamp glow. Natural Lagos light: warm low sun, neutral noon, moonlit night.
    public class DayNightCycle : MonoBehaviour
    {
        public float Clock = 8.5f;          // hours, 0–24
        public int Day = 1;
        public float HoursPerSecond = 1f / 45f;

        Light sun;
        Material lampHead, skyDomeMat, sunDiscMat;
        Transform skyDome, sunDisc;
        [System.NonSerialized] public Weather Weather;
        [System.NonSerialized] public Events Events;
        static readonly Color NightAmbient = new Color(0.12f, 0.15f, 0.24f), DayAmbient = new Color(0.68f, 0.66f, 0.64f);
        static readonly Color NightEquator = new Color(0.14f, 0.14f, 0.22f), GoldenEquator = new Color(0.96f, 0.68f, 0.44f), DayEquator = new Color(0.78f, 0.76f, 0.72f);
        static readonly Color NightGround = new Color(0.06f, 0.07f, 0.10f), DayGround = new Color(0.36f, 0.33f, 0.29f);
        static readonly Color NightFog = new Color(0.07f, 0.09f, 0.15f), GoldenFog = new Color(0.95f, 0.72f, 0.50f), DayFog = new Color(0.84f, 0.85f, 0.86f);
        static readonly Color LowSun = new Color(1f, 0.68f, 0.38f), NoonSun = new Color(1f, 0.96f, 0.90f), Moon = new Color(0.55f, 0.68f, 0.92f);

        public float SunElevation01 { get; private set; }   // -1 midnight … 1 noon
        public bool IsNight => SunElevation01 < 0.05f;
        public string ClockText => $"{Mathf.FloorToInt(Clock):00}:{Mathf.FloorToInt((Clock % 1f) * 60f):00}";

        // Calendar starting Friday, January 1, 1999 (Day 1 = Fri, Jan 1, 1999).
        public static readonly System.DateTime EpochDate = new System.DateTime(1999, 1, 1);
        static readonly string[] DayNamesShort = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
        static readonly string[] DayNamesFull = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
        static readonly string[] MonthNamesShort = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        static readonly string[] MonthNamesFull = { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };

        public System.DateTime Date => EpochDate.AddDays(Mathf.Max(0, Day - 1));
        public string DayOfWeekShort => DayNamesShort[(int)Date.DayOfWeek];
        public string DayOfWeekFull => DayNamesFull[(int)Date.DayOfWeek];
        public string MonthShort => MonthNamesShort[Date.Month - 1];
        public string MonthFull => MonthNamesFull[Date.Month - 1];
        public int DayOfMonth => Date.Day;
        public int Year => Date.Year;
        public string DateText => $"{DayOfWeekShort}, {MonthShort} {DayOfMonth}, {Year}";
        public string FullDateText => $"{DayOfWeekFull}, {MonthFull} {DayOfMonth}, {Year}";

        // Nigerian Seasonal Calendar: Dry & Harmattan Season (Nov–Feb), Early Wet (Mar), Rainy Season (Apr–Jul),
        // August Break (Aug), and Late Rains (Sep–Oct).
        public string SeasonId => Date.Month switch
        {
            12 or 1 => "dry_harmattan",
            11 or 2 => "dry",
            3 => "early_wet",
            4 or 5 or 6 or 7 => "rainy",
            8 => "august_break",
            _ => "late_rains"
        };

        public string SeasonLabel => Date.Month switch
        {
            12 or 1 => "DRY · HARMATTAN",
            11 or 2 => "DRY SEASON",
            3 => "EARLY WET",
            4 or 5 or 6 or 7 => "RAINY SEASON",
            8 => "AUGUST BREAK",
            _ => "LATE RAINS"
        };

        public bool IsDrySeason => Date.Month == 11 || Date.Month == 12 || Date.Month == 1 || Date.Month == 2;
        public bool IsRainySeason => Date.Month >= 4 && Date.Month <= 10 && Date.Month != 8;

        public static int DayForDate(int year, int month, int day)
        {
            int y = Mathf.Clamp(year, 1999, 2100);
            int m = Mathf.Clamp(month, 1, 12);
            int d = Mathf.Clamp(day, 1, System.DateTime.DaysInMonth(y, m));
            return Mathf.Max(1, (int)(new System.DateTime(y, m, d) - EpochDate).TotalDays + 1);
        }

        public void SetDate(int year, int month, int day, float? clockHour = null) => SetTime(clockHour ?? Clock, DayForDate(year, month, day));

        public void Init(Light sunLight, float startClock, float hoursPerSecond, Material lamp)
        {
            sun = sunLight; Clock = startClock; HoursPerSecond = hoursPerSecond; lampHead = lamp;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = ShaderLib.EnvCubemap;
            RenderSettings.reflectionIntensity = 1.0f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0014f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.82f;
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp) urp.shadowDistance = 180f;
            BuildSkyDome();
            Apply();
        }

        void BuildSkyDome()
        {
            if (skyDome != null) return;
            var domeGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            domeGo.name = "SkyDome";
            var col = domeGo.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
            // Invert triangle winding & normals on an instanced sphere mesh so it renders cleanly from inside.
            var mf = domeGo.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                var mesh = Instantiate(mf.sharedMesh);
                mesh.name = "InvertedSkySphere";
                var tris = mesh.triangles;
                for (int i = 0; i < tris.Length; i += 3) { int tmp = tris[i]; tris[i] = tris[i + 2]; tris[i + 2] = tmp; }
                mesh.triangles = tris;
                mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 4000f);
                mf.sharedMesh = mesh;
            }
            skyDomeMat = new Material(ShaderLib.Unlit) { name = "SkyDomeMat", color = Color.white };
            skyDomeMat.mainTexture = ShaderLib.SkyDomeTex;
            if (skyDomeMat.HasProperty("_BaseMap")) skyDomeMat.SetTexture("_BaseMap", ShaderLib.SkyDomeTex);
            var mr = domeGo.GetComponent<MeshRenderer>();
            mr.sharedMaterial = skyDomeMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            skyDome = domeGo.transform;
            skyDome.SetParent(transform, false);
            skyDome.localScale = Vector3.one * 3100f;

            var discGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            discGo.name = "SunDisc";
            var dCol = discGo.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(dCol); else DestroyImmediate(dCol);
            sunDiscMat = new Material(ShaderLib.Unlit) { name = "SunDiscMat", color = new Color(1.0f, 0.90f, 0.68f) };
            var dMr = discGo.GetComponent<MeshRenderer>();
            dMr.sharedMaterial = sunDiscMat;
            dMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dMr.receiveShadows = false;
            sunDisc = discGo.transform;
            sunDisc.SetParent(skyDome, false);
            sunDisc.localScale = Vector3.one * 0.045f;
        }

        void Update() => Advance(Time.deltaTime * HoursPerSecond);

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null && skyDome != null) skyDome.position = cam.transform.position;
        }

        public void SetTime(float clock, int day) { Clock = Mathf.Repeat(clock, 24f); Day = day; Apply(); }

        // Move the clock forward by game hours (a shift, sleep, a danfo ride), rolling the day over.
        public void Advance(float hours)
        {
            Clock += hours;
            while (Clock >= 24f) { Clock -= 24f; Day++; }
            Apply();
        }

        void Apply()
        {
            if (sun == null) return;
            float e = Mathf.Sin((Clock - 6f) / 24f * Mathf.PI * 2f);   // 0 at 06:00, 1 at noon, 0 at 18:00, -1 at midnight
            SunElevation01 = e;
            float sunAngle = (Clock / 24f) * 360f - 90f;
            sun.transform.rotation = Quaternion.Euler(sunAngle, 38f, 0f);
            float dayAmount = Mathf.Clamp01(e * 1.6f);
            float goldenHour = Mathf.Clamp01(1f - Mathf.Abs(e - 0.22f) / 0.38f);
            if (e > 0f)
            {
                sun.color = Color.Lerp(LowSun, NoonSun, Mathf.Clamp01(e * 1.85f));
                sun.intensity = Mathf.Lerp(0.45f, 1.48f, dayAmount);
            }
            else
            {
                // Moonlight from the opposite side of the sky so the night still has shape.
                sun.transform.rotation = Quaternion.Euler(sunAngle + 180f, 38f, 0f);
                sun.color = Moon;
                sun.intensity = 0.22f;
            }
            float t = Mathf.Clamp01(e * 1.4f + 0.18f);
            var ambient = Color.Lerp(NightAmbient, DayAmbient, t);
            var equator = Color.Lerp(Color.Lerp(NightEquator, DayEquator, t), GoldenEquator, goldenHour * 0.72f);
            var ground = Color.Lerp(NightGround, DayGround, t);
            var fog = Color.Lerp(Color.Lerp(NightFog, DayFog, t), GoldenFog, goldenHour * 0.65f);
            float density = 0.0014f, intensity = sun.intensity;
            if (Weather != null) Weather.Grade(ref ambient, ref fog, ref density, ref intensity);
            RenderSettings.ambientSkyColor = ambient;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            RenderSettings.ambientLight = ambient;
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = density;
            RenderSettings.reflectionIntensity = Mathf.Lerp(0.38f, 1.0f, t);
            sun.intensity = intensity;
            if (skyDomeMat != null)
            {
                var skyTint = Color.Lerp(new Color(0.12f, 0.15f, 0.26f), Color.Lerp(Color.white, new Color(1.04f, 0.88f, 0.72f), goldenHour * 0.55f), t);
                skyDomeMat.color = skyTint;
                if (skyDomeMat.HasProperty("_BaseColor")) skyDomeMat.SetColor("_BaseColor", skyTint);
            }
            if (sunDisc != null)
            {
                Vector3 sunDir = -sun.transform.forward;
                sunDisc.localPosition = new Vector3(sunDir.x, Mathf.Max(0.06f, sunDir.y), sunDir.z).normalized * 0.46f;
                var discCol = e > 0f ? new Color(1.4f, 1.12f, 0.75f) : new Color(0.75f, 0.85f, 1.1f);
                if (sunDiscMat != null)
                {
                    sunDiscMat.color = discCol;
                    if (sunDiscMat.HasProperty("_BaseColor")) sunDiscMat.SetColor("_BaseColor", discCol);
                }
            }
            if (lampHead != null) lampHead.SetColor("_EmissionColor", Events != null && Events.Outage > 0f ? Color.black : Color.Lerp(new Color(1f, 0.72f, 0.3f) * 2.4f, Color.black, Mathf.Clamp01(e * 3f + 0.3f)));
        }
    }
}
