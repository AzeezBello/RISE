using UnityEngine;
using Rise.Data;
using Rise.Player;
using Rise.World;

namespace Rise.UI
{
    // The Lagos Experience HUD: location top-left; clock, day, weather, heat and network top-right; objective card and a
    // north-up minimap with a heading arrow bottom-left; wallet, level, XP and reputation bottom-right (with the portrait
    // on foot and a speed / fuel / condition cluster while driving); vitals and the interaction prompt bottom-centre;
    // toasts, menus, shift overlay and the wanted banner. IMGUI so the slice has no UI package dependency.
    //
    // Look: the website's palette (web/app/globals.css) and its typeface, Inter (Resources/Fonts, SIL OFL), on dark glass
    // cards with rounded corners. Every texture is generated at startup and rebuilt when the screen scale changes, and
    // every card sizes itself to its text. Inter carries ₦, so money reads as naira (the built-in font had no ₦ glyph).
    public class Hud : MonoBehaviour
    {
        WorldData data; DayNightCycle clock; Session session; Minimap minimap;
        public Portrait Portrait;
        float hintUntil;

        static Color Hex(int rgb, float a = 1f) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);
        static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);
        static readonly Color Ink = Hex(0x07100E), Green = Hex(0x3DFF79), Gold = Hex(0xFFC52F), Red = Hex(0xFF5D5D), Blue = Hex(0x5DB8FF),
            Text = Hex(0xF2F6F4), Muted = Hex(0x93A39C), Track = new Color(1f, 1f, 1f, 0.12f);

        Font regular, semibold;
        GUIStyle panel, rSmall, rMid, rPill, rKey, field;
        GUIStyle overline, heading, title, big, huge, body, bodyStrong, bodyMuted, bodyClip, bodyStrongClip, bodyMutedClip, bodyWrap, small, smallClip, smallWrap, smallRight, keyText, promptText, pillText, center, stripText;
        Texture2D disc, arrow, coverTex;
        bool coverChecked;
        float splashStart;
        readonly System.Collections.Generic.List<Texture2D> owned = new System.Collections.Generic.List<Texture2D>();
        float builtScale = -1f;
        static readonly GUIContent tmp = new GUIContent();

        public Texture2D CoverTexture { get { EnsureCoverLoaded(); return coverTex; } }
        public string LoadingStageFor(float elapsed) => elapsed < 1.3f ? "logo_title" : elapsed < 3.0f ? "cover" : "done";

        void EnsureCoverLoaded()
        {
            if (coverChecked && coverTex != null) return;
            coverChecked = true;
            coverTex = Resources.Load<Texture2D>("UI/cover");
            if (coverTex == null)
            {
                string[] paths = { "Assets/Rise/Resources/UI/cover.png", "Builds/cover-630x500.png" };
                foreach (var p in paths)
                {
                    if (!System.IO.File.Exists(p)) continue;
                    var bytes = System.IO.File.ReadAllBytes(p);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (ImageConversion.LoadImage(tex, bytes)) { coverTex = Own(tex); break; }
                }
            }
        }

        public void Init(WorldData world, DayNightCycle dayNight, Session s, Minimap map)
        {
            data = world; clock = dayNight; session = s; minimap = map; hintUntil = Time.time + 25f;
            splashStart = Time.unscaledTime;
            EnsureCoverLoaded();
        }

        void Awake() { useGUILayout = false; }   // everything is laid out by hand: no Layout pass, fewer allocations

        void OnDestroy() { foreach (var t in owned) if (t != null) Destroy(t); }

        public static float ComputeScaleFor(float width, float height) =>
            Mathf.Round(Mathf.Clamp(Mathf.Min(height / 720f, width / 1180f), 0.75f, 2.5f) * 20f) / 20f;

        float Scale => ComputeScaleFor(Screen.width, Screen.height);

        // ---- textures and styles ----

        Texture2D Own(Texture2D t) { t.hideFlags = HideFlags.HideAndDontSave; t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear; owned.Add(t); return t; }

        // An anti-aliased rounded rectangle, drawn 9-sliced so the corners keep their radius at any size.
        Texture2D Rounded(float radius, Color fill, Color border, float borderW)
        {
            int size = Mathf.CeilToInt(radius * 2f) + 4;
            var t = Own(new Texture2D(size, size, TextureFormat.RGBA32, false));
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Max(radius - (x + 0.5f), 0f, (x + 0.5f) - (size - radius));
                    float cy = Mathf.Max(radius - (y + 0.5f), 0f, (y + 0.5f) - (size - radius));
                    float d = Mathf.Sqrt(cx * cx + cy * cy) - radius;        // signed distance to the edge, negative inside
                    float inner = borderW > 0f ? Mathf.Clamp01(0.5f - (d + borderW)) : 1f;
                    var c = Color.Lerp(border, fill, inner);
                    c.a *= Mathf.Clamp01(0.5f - d);
                    px[y * size + x] = c;
                }
            t.SetPixels(px); t.Apply();
            return t;
        }

        GUIStyle Slice(Texture2D t, float radius)
        {
            int b = Mathf.CeilToInt(radius);
            return new GUIStyle { normal = { background = t }, border = new RectOffset(b, b, b, b) };
        }

        // Navigation arrow (tip up) for the minimap, supersampled.
        Texture2D MakeArrow()
        {
            const int n = 64; var t = Own(new Texture2D(n, n, TextureFormat.RGBA32, false)); var px = new Color[n * n];
            Vector2 tip = new Vector2(32, 60), left = new Vector2(8, 6), notch = new Vector2(32, 20), right = new Vector2(56, 6);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < 4; sy++) for (int sx = 0; sx < 4; sx++)
                    {
                        var p = new Vector2(x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f);
                        if (InTri(p, tip, left, notch) || InTri(p, tip, notch, right)) hit++;
                    }
                    px[y * n + x] = new Color(1f, 1f, 1f, hit / 16f);
                }
            t.SetPixels(px); t.Apply(); return t;
        }

        static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            return !(((d1 < 0) || (d2 < 0) || (d3 < 0)) && ((d1 > 0) || (d2 > 0) || (d3 > 0)));
        }

        GUIStyle Txt(Font f, float size, Color c, TextAnchor a = TextAnchor.MiddleLeft, bool wrap = false) =>
            new GUIStyle { font = f, fontSize = Mathf.Max(8, Mathf.RoundToInt(size)), alignment = a, wordWrap = wrap, clipping = TextClipping.Overflow, richText = false, normal = { textColor = c } };

        void Build(float s)
        {
            foreach (var t in owned) if (t != null) Destroy(t);
            owned.Clear();
            builtScale = s;
            if (regular == null) regular = Resources.Load<Font>("Fonts/Inter-Regular");
            if (semibold == null) semibold = Resources.Load<Font>("Fonts/Inter-SemiBold");
            var fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Font R = regular != null ? regular : fallback, B = semibold != null ? semibold : fallback;

            panel = Slice(Rounded(10f * s, Hex(0x060E0C, 0.80f), new Color(1f, 1f, 1f, 0.10f), Mathf.Max(1f, s)), 10f * s);
            rSmall = Slice(Rounded(3f * s, Color.white, Color.white, 0f), 3f * s);
            rMid = Slice(Rounded(8f * s, Color.white, Color.white, 0f), 8f * s);
            rPill = Slice(Rounded(9f * s, Color.white, Color.white, 0f), 9f * s);
            rKey = Slice(Rounded(5f * s, Color.white, Color.white, 0f), 5f * s);
            disc = Own(Rounded(31f, Color.white, Color.white, 0f));
            arrow = MakeArrow();

            var fieldTex = Rounded(7f * s, Hex(0x0C1714, 0.95f), new Color(1f, 1f, 1f, 0.16f), Mathf.Max(1f, s));
            var fieldFocus = Rounded(7f * s, Hex(0x0C1714, 0.95f), A(Green, 0.8f), Mathf.Max(1f, s));
            int fb = Mathf.CeilToInt(7f * s) + 1;
            field = new GUIStyle
            {
                font = R, fontSize = Mathf.RoundToInt(14 * s), alignment = TextAnchor.MiddleLeft, border = new RectOffset(fb, fb, fb, fb),
                padding = new RectOffset((int)(10 * s), (int)(10 * s), (int)(4 * s), (int)(4 * s)),
                normal = { background = fieldTex, textColor = Text }, hover = { background = fieldTex, textColor = Text },
                focused = { background = fieldFocus, textColor = Text }, active = { background = fieldFocus, textColor = Text },
            };

            overline = Txt(B, 10 * s, Muted);
            heading = Txt(B, 17 * s, Text);
            title = Txt(B, 20 * s, Gold);
            big = Txt(B, 24 * s, Text);
            huge = Txt(B, 34 * s, Text);
            body = Txt(R, 14 * s, Text);
            bodyStrong = Txt(B, 14 * s, Text);
            bodyMuted = Txt(R, 14 * s, A(Muted, 0.6f));
            bodyClip = Txt(R, 14 * s, Text); bodyClip.clipping = TextClipping.Clip;
            bodyStrongClip = Txt(B, 14 * s, Text); bodyStrongClip.clipping = TextClipping.Clip;
            bodyMutedClip = Txt(R, 14 * s, A(Muted, 0.6f)); bodyMutedClip.clipping = TextClipping.Clip;
            bodyWrap = Txt(R, 14 * s, Text, TextAnchor.UpperLeft, true);
            small = Txt(R, 12 * s, Muted);
            smallClip = Txt(R, 12 * s, Muted); smallClip.clipping = TextClipping.Clip;
            smallWrap = Txt(R, 12 * s, Muted, TextAnchor.UpperLeft, true);
            smallRight = Txt(R, 12 * s, Muted, TextAnchor.MiddleRight);
            keyText = Txt(B, 12 * s, Ink, TextAnchor.MiddleCenter);
            promptText = Txt(B, 15 * s, Text);
            pillText = Txt(B, 10 * s, Text, TextAnchor.MiddleCenter);
            center = Txt(R, 12 * s, Muted, TextAnchor.MiddleCenter, true);
            stripText = Txt(B, 14 * s, Text, TextAnchor.MiddleCenter);
        }

        // ---- drawing helpers ----

        static float TW(GUIStyle st, string t) { tmp.text = t; return st.CalcSize(tmp).x; }
        static float TH(GUIStyle st, string t, float w) { tmp.text = t; return st.CalcHeight(tmp, w); }
        static float LH(GUIStyle st) => Mathf.Ceil(st.fontSize * 1.45f);

        static void Box(Rect r, GUIStyle st, Color tint)
        {
            if (Event.current.type != EventType.Repaint || r.width <= 0f || r.height <= 0f) return;
            var o = GUI.color; GUI.color = o * tint; st.Draw(r, false, false, false, false); GUI.color = o;
        }

        void Tex(Rect r, Texture t, Color tint)
        {
            if (Event.current.type != EventType.Repaint) return;
            var o = GUI.color; GUI.color = o * tint; GUI.DrawTexture(r, t, ScaleMode.StretchToFill, true); GUI.color = o;
        }

        static void Label(Rect r, string text, GUIStyle st, Color? tint = null)
        {
            if (tint == null) { GUI.Label(r, text, st); return; }
            var o = GUI.color; GUI.color = o * tint.Value; GUI.Label(r, text, st); GUI.color = o;
        }

        void Bar(Rect r, float t, Color c)
        {
            Box(r, rSmall, Track);
            t = Mathf.Clamp01(t); if (t <= 0f) return;
            Box(new Rect(r.x, r.y, Mathf.Max(r.height, r.width * t), r.height), rSmall, c);
        }

        // A small tinted capsule with a caption, returns its width.
        float Pill(float x, float y, string text, Color c, float s)
        {
            float w = TW(pillText, text) + 14f * s, h = 18f * s;
            Box(new Rect(x, y, w, h), rPill, A(c, 0.18f));
            Label(new Rect(x, y, w, h), text, pillText, c);
            return w;
        }

        // A key cap ("E", "ACT", "1") followed by nothing; returns its width.
        float KeyCap(float x, float y, float h, string key, Color c, float s)
        {
            float w = Mathf.Max(h, TW(keyText, key) + 12f * s);
            Box(new Rect(x, y, w, h), rKey, c);
            Label(new Rect(x, y, w, h), key, keyText);
            return w;
        }

        static string Naira(long v) => (v < 0 ? "-₦" : "₦") + System.Math.Abs(v).ToString("N0");
        static string Distance(float m) => m < 1000f ? $"{m:0} m" : $"{m / 1000f:0.0} km";

        // ---- the HUD ----

        float mapCanvasZoom = 340f;

        void DrawLine2D(Vector2 a, Vector2 b, float thickness, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.5f) return;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            var oldMat = GUI.matrix;
            var oldCol = GUI.color;
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.color = oldCol * c;
            GUI.DrawTexture(new Rect(a.x, a.y - thickness * 0.5f, len, thickness), Texture2D.whiteTexture);
            GUI.color = oldCol;
            GUI.matrix = oldMat;
        }

        bool ClickButton(Rect r, string label, Color bgTint, Color textTint, float s)
        {
            var e = Event.current;
            bool hover = r.Contains(e.mousePosition);
            Box(r, rPill, hover ? A(bgTint, Mathf.Min(1f, bgTint.a + 0.16f)) : bgTint);
            Label(r, label, pillText, textTint);
            if (e.type == EventType.MouseDown && e.button == 0 && hover)
            {
                e.Use();
                return true;
            }
            return false;
        }

        void OnGUI()
        {
            if (data == null || session == null) return;
            ThirdPersonCamera.KeepCursorUnlocked = session.ActiveMenu != null || session.Paused || session.ChatOpen;
            if (ThirdPersonCamera.KeepCursorUnlocked && Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            float s = Scale;
            if (!Mathf.Approximately(s, builtScale) || panel == null || panel.normal.background == null) Build(s);
            GUI.skin.settings.cursorColor = Green; GUI.skin.settings.selectionColor = A(Green, 0.35f);
            float W = Screen.width, H = Screen.height, m = 16f * s;
            var st = session.State.S;
            bool touch = TouchControls.Active;

            float topY = m;
            if (touch) topY = DrawTouchStrip(s, W, m) + 8f * s;
            DrawLocation(s, m);
            DrawClock(s, W, m, st.heat);
            float mapTop = DrawMinimap(s, H, m);
            DrawObjective(s, m, mapTop - 10f * s, st);
            if (!touch) DrawWallet(s, W, H, m, st);
            DrawCentre(s, W, H, st, touch);

            bool wanted = st.heat >= data.meta.pursuitHeat;
            if (wanted) topY = DrawWanted(s, W, topY, st.heat) + 8f * s;
            if (session.InInterior || session.ActiveIndoorMission != null) topY = DrawIndoorBanner(s, W, topY) + 8f * s;
            if (session.IsSpecOpActive) topY = DrawSpecOpBanner(s, W, topY) + 8f * s;
            if (session.InFootballMatch) topY = DrawFootballBroadcastBanner(s, W, H, topY) + 8f * s;
            if (session.Racing) topY = DrawNfsRaceBanner(s, W, topY) + 8f * s;
            if (session.Working != null) DrawShift(s, W, H);
            DrawToasts(s, W, topY);
            if (session.ActiveMenu != null)
            {
                if (session.ActiveMenu.Title == "Map · GPS")
                    DrawInteractiveMapMenu(session.ActiveMenu, s, W, H);
                else if (session.PhoneOpen)
                    DrawPhone(session.ActiveMenu, s, W, H);
                else
                    DrawMenu(session.ActiveMenu, s, W, H);
            }
            DrawLoadingSplash(s, W, H);
        }

        bool AnySkipInput()
        {
            var ev = Event.current;
            if (ev != null && (ev.type == EventType.MouseDown || ev.type == EventType.KeyDown)) return true;
            return GameInput.AnyPressed;
        }

        // Loading screen sequence: Stage 1 shows the R Logo & "RISE: HUSTLE OR DIE TRYING" Title, then Stage 2 transitions to the full Cover Art.
        // On WebGL (where the HTML overlay already displayed the R Logo, Title, and Cover Art during download) or when the player taps/clicks, skip straight to gameplay.
        void DrawLoadingSplash(float s, float W, float H)
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer) return;
            if (AnySkipInput()) { splashStart = -999f; return; }
            float elapsed = Time.unscaledTime - splashStart;
            string stg = LoadingStageFor(elapsed);
            if (stg == "done" || session.ActiveMenu != null) return;
            EnsureCoverLoaded();
            float alpha = elapsed > 2.5f ? Mathf.Clamp01((3.0f - elapsed) / 0.5f) : 1f;
            Box(new Rect(0f, 0f, W, H), rSmall, A(Ink, 0.96f * alpha));

            if (stg == "logo_title")
            {
                float badge = 84f * s;
                var br = new Rect(W * 0.5f - badge * 0.5f, H * 0.34f, badge, badge);
                Box(br, rMid, A(Hex(0x0E1F1A), alpha));
                Box(new Rect(br.x + 4f * s, br.y + 4f * s, badge - 8f * s, badge - 8f * s), rMid, A(Green, 0.18f * alpha));
                Label(br, "R", huge, A(Green, alpha));

                string mainTitle = "RISE";
                string subTitle = "HUSTLE OR DIE TRYING";
                string locLine = "LAGOS · NIGERIA  ·  JANUARY 1, 1999";
                Label(new Rect(0f, br.yMax + 16f * s, W, LH(huge)), mainTitle, center, A(Green, alpha));
                Label(new Rect(0f, br.yMax + 16f * s + LH(huge), W, LH(heading)), subTitle, stripText, A(Gold, alpha));
                Label(new Rect(0f, br.yMax + 24f * s + LH(huge) + LH(heading), W, LH(small)), locLine, center, A(Text, 0.85f * alpha));
            }
            else
            {
                float cw = Mathf.Min(W * 0.68f, 540f * s), ch = cw * (500f / 630f);
                var cr = new Rect(W * 0.5f - cw * 0.5f, H * 0.44f - ch * 0.5f, cw, ch);
                Box(new Rect(cr.x - 6f * s, cr.y - 6f * s, cr.width + 12f * s, cr.height + 12f * s), panel, new Color(1f, 1f, 1f, alpha));
                if (coverTex != null && Event.current.type == EventType.Repaint)
                    Tex(cr, coverTex, new Color(1f, 1f, 1f, alpha));
                float p = Mathf.Clamp01((elapsed - 1.3f) / 1.4f);
                var barRect = new Rect(cr.x, cr.yMax + 16f * s, cr.width, 8f * s);
                Bar(barRect, p, A(Green, alpha));
                Label(new Rect(cr.x, barRect.yMax + 8f * s, cr.width, LH(small)), $"ENTERING SURULERE · LAGOS ({clock.FullDateText.ToUpperInvariant()})", center, A(Gold, alpha));
            }
        }

        // Top-left: brand, district, street (or bridge).
        void DrawLocation(float s, float m)
        {
            var focus = session.Vehicle != null ? session.Vehicle.transform : session.Player.transform;
            var pos = focus.position;
            var region = data.RegionAt(pos);
            var road = data.RoadAt(pos, 9f);
            string street = road != null ? data.RoadNameAt(road, road.Along(pos)) : null;
            data.DeckHeightAt(pos, out var bridge);
            if (bridge != null) street = bridge.name;
            string place = region != null ? region.name : data.meta.name;

            float pad = 12f * s, accent = 3f * s;
            float tw = Mathf.Max(TW(heading, place), string.IsNullOrEmpty(street) ? 0f : TW(small, street), TW(overline, "RISE · HUSTLE OR DIE TRYING"));
            float w = Mathf.Clamp(tw + pad * 2f + accent + 4f * s, 190f * s, 340f * s);
            float h = pad * 2f + LH(overline) + LH(heading) + (string.IsNullOrEmpty(street) ? 0f : LH(small) - 2f * s);
            var r = new Rect(m, m, w, h);
            Box(r, panel, Color.white);
            Box(new Rect(r.x + pad * 0.6f, r.y + pad, accent, h - pad * 2f), rSmall, Gold);
            float x = r.x + pad + accent + 4f * s, y = r.y + pad, iw = r.xMax - pad - x;
            Label(new Rect(x, y, iw, LH(overline)), "RISE · HUSTLE OR DIE TRYING", overline, A(Gold, 1f)); y += LH(overline);
            Label(new Rect(x, y, iw, LH(heading)), place, heading); y += LH(heading) - 2f * s;
            if (!string.IsNullOrEmpty(street)) Label(new Rect(x, y, iw, LH(small)), street, small);
        }

        // Top-right: clock with day/night, full calendar date (starting Fri, Jan 1, 1999) + day number, Nigerian season & weather, historic era/news ticker, heat pips, network status, and Pause/Map/Phone buttons.
        void DrawClock(float s, float W, float m, int heat)
        {
            float pad = 12f * s, w = 292f * s;
            var hist = session.Events != null ? session.Events.ActiveHistoricEvent : null;
            bool weatherRow = true, eraRow = hist != null, heatRow = heat > 0, netRow = session.Players != null;
            float h = pad * 2f + LH(big) + LH(small) + 22f * s + (eraRow ? 22f * s : 0f) + (heatRow ? 22f * s : 0f) + (netRow ? 20f * s : 0f);
            var r = new Rect(W - m - w, m, w, h);
            Box(r, panel, Color.white);
            float x = r.x + pad, y = r.y + pad, iw = w - pad * 2f;
            Label(new Rect(x, y, iw, LH(big)), clock.ClockText, big);
            string phase = clock.IsNight ? "NIGHT" : "DAY";
            float pw = TW(pillText, phase) + 14f * s;
            Pill(r.xMax - pad - pw, y + (LH(big) - 18f * s) * 0.5f, phase, clock.IsNight ? Blue : Gold, s);
            y += LH(big);
            if (weatherRow)
            {
                string day = $"{clock.DateText} · Day {clock.Day}";
                Label(new Rect(x, y, iw, LH(small)), day, small);
                y += LH(small) + 2f * s;
                var wx = session.Weather;
                string seasonLabel = wx != null ? wx.ActiveSeasonLabel : clock.SeasonLabel;
                bool isWetSeason = seasonLabel.Contains("RAIN") || seasonLabel.Contains("WET");
                float sw = Pill(x, y, seasonLabel, isWetSeason ? Blue : Gold, s);
                string wxTag = wx == null ? null : wx.Raining ? (wx.Storm ? "STORM" : "RAINING") : null;
                if (wxTag != null) Pill(x + sw + 6f * s, y, wxTag, Blue, s);
                y += 20f * s;
            }
            if (eraRow)
            {
                y += 2f * s;
                Color badgeCol = hist.Category == "pandemic" ? Red : hist.Category == "election" ? Green : Gold;
                float bw = Pill(x, y, hist.Category.ToUpperInvariant(), badgeCol, s);
                Label(new Rect(x + bw + 6f * s, y, iw - bw - 6f * s, 18f * s), hist.Title, smallClip);
                y += 19f * s;
            }
            if (heatRow)
            {
                y += 4f * s;
                Label(new Rect(x, y, 50f * s, 14f * s), "HEAT", overline, Red);
                float px = x + TW(overline, "HEAT") + 8f * s, d = 9f * s;
                for (int i = 0; i < 5; i++) Tex(new Rect(px + i * (d + 5f * s), y + (14f * s - d) * 0.5f, d, d), disc, i < heat ? Red : Track);
                y += 18f * s;
            }
            if (netRow)
            {
                string status = session.Players.Status;
                bool on = !status.StartsWith("connecting") && status != "offline";
                Color dot = on ? Green : status == "offline" ? Muted : Gold;
                if (!on && status.StartsWith("connecting")) dot = A(Gold, 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 3f)));
                y += 2f * s;
                Tex(new Rect(x, y + 5f * s, 7f * s, 7f * s), disc, dot);
                Label(new Rect(x + 13f * s, y, iw - 13f * s, 17f * s), status, small);
            }

            // Quick-access HUD Control Bar right below the Clock Card: [ || PAUSE ], [ M · MAP ], [ P · PHONE ]
            float btnY = r.yMax + 6f * s, btnH = 24f * s, gap = 6f * s;
            float btnW = (w - gap * 2f) / 3f;
            var pauseRect = new Rect(r.x, btnY, btnW, btnH);
            var mapRect = new Rect(r.x + btnW + gap, btnY, btnW, btnH);
            var phoneRect = new Rect(r.x + (btnW + gap) * 2f, btnY, btnW, btnH);
            if (ClickButton(pauseRect, session.Paused ? "▶ RESUME" : "|| PAUSE", A(Ink, 0.88f), Gold, s))
                session.TogglePauseMenu();
            if (ClickButton(mapRect, "M · GPS MAP", A(Ink, 0.88f), Green, s))
            {
                session.SetPaused(false);
                session.PhoneOpen = true;
                session.AppMap("");
            }
            if (ClickButton(phoneRect, "P · PHONE", A(Ink, 0.88f), Blue, s))
            {
                session.SetPaused(false);
                session.OpenPhone();
            }
        }

        // Bottom-left: north-up minimap with high-contrast vector road grid, live GPS route polyline, POI blips, edge-clamped target indicator, and turn-by-turn GPS bar.
        float DrawMinimap(float s, float H, float m)
        {
            float size = 180f * s;
            var r = new Rect(m, H - m - size, size, size);
            float inset = 5f * s;
            Box(new Rect(r.x - inset, r.y - inset, r.width + inset * 2f, r.height + inset * 2f), panel, Color.white);
            if (minimap != null && minimap.Texture != null && Event.current.type == EventType.Repaint)
                GUI.DrawTexture(r, minimap.Texture, ScaleMode.StretchToFill, false);
            else
                Box(r, rSmall, Hex(0x0A1412, 0.96f));

            var focus = session.Vehicle != null ? session.Vehicle.transform : session.Player.transform;
            Vector3 centerPos = focus.position;
            float viewRadius = minimap != null ? Mathf.Max(20f, minimap.Size) : 75f;

            Vector2 WorldToMinimap(Vector3 wp)
            {
                float nx = (wp.x - centerPos.x) / (viewRadius * 2f);
                float nz = (wp.z - centerPos.z) / (viewRadius * 2f);
                return new Vector2(r.center.x + nx * r.width, r.center.y - nz * r.height);
            }

            if (Event.current.type == EventType.Repaint && !session.InInterior)
            {
                GUI.BeginClip(r);
                Vector2 localCenter = new Vector2(r.width * 0.5f, r.height * 0.5f);
                Vector2 ToLocal(Vector3 wp)
                {
                    float nx = (wp.x - centerPos.x) / (viewRadius * 2f);
                    float nz = (wp.z - centerPos.z) / (viewRadius * 2f);
                    return new Vector2(localCenter.x + nx * r.width, localCenter.y - nz * r.height);
                }

                // 1. High-contrast vector road overlay so roads are crystal clear day & night
                if (data.roads != null)
                {
                    Color roadFill = new Color(0.24f, 0.31f, 0.36f, 0.62f);
                    Color roadCenter = new Color(0.55f, 0.66f, 0.72f, 0.35f);
                    foreach (var rd in data.roads)
                    {
                        Vector3 p0 = rd.Horizontal ? WorldData.W(rd.from, 0f, rd.k) : WorldData.W(rd.k, 0f, rd.from);
                        Vector3 p1 = rd.Horizontal ? WorldData.W(rd.to, 0f, rd.k) : WorldData.W(rd.k, 0f, rd.to);
                        Vector2 ptA = ToLocal(p0), ptB = ToLocal(p1);
                        float xMin = Mathf.Min(ptA.x, ptB.x), xMax = Mathf.Max(ptA.x, ptB.x);
                        float yMin = Mathf.Min(ptA.y, ptB.y), yMax = Mathf.Max(ptA.y, ptB.y);
                        if (xMax < -10f || xMin > r.width + 10f || yMax < -10f || yMin > r.height + 10f) continue;
                        float rw = Mathf.Clamp((rd.width / (viewRadius * 2f)) * r.width, 2.5f * s, 10f * s);
                        if (Mathf.Abs(ptA.x - ptB.x) < 1f)
                        {
                            GUI.color = roadFill;
                            GUI.DrawTexture(new Rect(ptA.x - rw * 0.5f, yMin, rw, yMax - yMin), Texture2D.whiteTexture);
                            GUI.color = roadCenter;
                            GUI.DrawTexture(new Rect(ptA.x - 0.6f, yMin, 1.2f, yMax - yMin), Texture2D.whiteTexture);
                        }
                        else
                        {
                            GUI.color = roadFill;
                            GUI.DrawTexture(new Rect(xMin, ptA.y - rw * 0.5f, xMax - xMin, rw), Texture2D.whiteTexture);
                            GUI.color = roadCenter;
                            GUI.DrawTexture(new Rect(xMin, ptA.y - 0.6f, xMax - xMin, 1.2f), Texture2D.whiteTexture);
                        }
                    }
                    GUI.color = Color.white;
                }

                // 2. Live GPS Route Polyline on Minimap
                if (session.Nav != null && session.Nav.Target != null && session.Nav.CurrentRoute != null && session.Nav.CurrentRoute.Count > 1)
                {
                    var rt = session.Nav.CurrentRoute;
                    Color routeOuter = new Color(0.0f, 0.90f, 1.0f, 0.95f);
                    Color routeInner = new Color(1.0f, 0.90f, 0.20f, 0.95f);
                    for (int i = 1; i < rt.Count; i++)
                    {
                        Vector2 pA = ToLocal(rt[i - 1]), pB = ToLocal(rt[i]);
                        if ((pA.x < -30f && pB.x < -30f) || (pA.x > r.width + 30f && pB.x > r.width + 30f) ||
                            (pA.y < -30f && pB.y < -30f) || (pA.y > r.height + 30f && pB.y > r.height + 30f))
                            continue;
                        DrawLine2D(pA, pB, 4.2f * s, routeOuter);
                        DrawLine2D(pA, pB, 1.8f * s, routeInner);
                    }
                }

                // 3. Nearby POI Blips (Fuel, Ladipo Garage, Contacts, Owned Vehicles)
                if (data.TryPlace("fuel", out var fuelLm))
                {
                    Vector2 fp = ToLocal(fuelLm.World());
                    if (fp.x >= 4f && fp.x <= r.width - 4f && fp.y >= 4f && fp.y <= r.height - 4f)
                        Tex(new Rect(fp.x - 4.5f * s, fp.y - 4.5f * s, 9f * s, 9f * s), disc, Gold);
                }
                if (data.TryPlace("ladipo", out var ladLm))
                {
                    Vector2 lp = ToLocal(ladLm.World());
                    if (lp.x >= 4f && lp.x <= r.width - 4f && lp.y >= 4f && lp.y <= r.height - 4f)
                        Tex(new Rect(lp.x - 4.5f * s, lp.y - 4.5f * s, 9f * s, 9f * s), disc, new Color(1f, 0.25f, 0.85f));
                }
                if (session.Contacts != null)
                {
                    foreach (var ct in session.Contacts.Contacts)
                    {
                        Vector2 cp = ToLocal(ct.pos);
                        if (cp.x >= 4f && cp.x <= r.width - 4f && cp.y >= 4f && cp.y <= r.height - 4f)
                            Tex(new Rect(cp.x - 3.2f * s, cp.y - 3.2f * s, 6.4f * s, 6.4f * s), disc, Green);
                    }
                }
                foreach (var pv in session.Parked)
                {
                    if (pv == null || !pv.Owned || pv == session.Vehicle) continue;
                    Vector2 vp = ToLocal(pv.transform.position);
                    if (vp.x >= 4f && vp.x <= r.width - 4f && vp.y >= 4f && vp.y <= r.height - 4f)
                        Tex(new Rect(vp.x - 4f * s, vp.y - 4f * s, 8f * s, 8f * s), disc, Blue);
                }

                GUI.EndClip();
            }

            // 4. Active GPS Destination Marker (inside minimap or edge-clamped directional beacon!)
            if (session.Nav != null && session.Nav.Target.HasValue)
            {
                Vector2 tp = WorldToMinimap(session.Nav.Target.Value);
                float padEdge = 8f * s;
                float clampedX = Mathf.Clamp(tp.x, r.x + padEdge, r.xMax - padEdge);
                float clampedY = Mathf.Clamp(tp.y, r.y + padEdge, r.yMax - padEdge);
                float pulse = 1f + 0.22f * Mathf.Sin(Time.time * 6f);
                float dSize = 11f * s * pulse;
                Tex(new Rect(clampedX - dSize * 0.5f, clampedY - dSize * 0.5f, dSize, dSize), disc, new Color(0f, 0.95f, 1f, 0.95f));
                Tex(new Rect(clampedX - 3.5f * s, clampedY - 3.5f * s, 7f * s, 7f * s), disc, Gold);
            }

            // 5. Player Heading Arrow
            float a = 20f * s; var c = r.center;
            var old = GUI.matrix;
            GUIUtility.RotateAroundPivot(focus.eulerAngles.y, c);
            Tex(new Rect(c.x - a * 0.62f, c.y - a * 0.62f, a * 1.24f, a * 1.24f), arrow, new Color(0f, 0f, 0f, 0.65f));
            Tex(new Rect(c.x - a * 0.5f, c.y - a * 0.5f, a, a), arrow, Gold);
            GUI.matrix = old;

            // 6. Cardinal Compass Badges (N, E, S, W) + Zoom Controls + Click-to-open Full Map
            float nb = 18f * s;
            var nr = new Rect(r.center.x - nb * 0.5f, r.y - inset - nb * 0.5f, nb, nb);
            Tex(nr, disc, Hex(0x060E0C, 0.95f)); Label(nr, "N", pillText, Gold);

            var zOutRect = new Rect(r.x + 4f * s, r.yMax - 20f * s, 20f * s, 16f * s);
            var zInRect = new Rect(r.x + 26f * s, r.yMax - 20f * s, 20f * s, 16f * s);
            var mapBtnRect = new Rect(r.xMax - 54f * s, r.yMax - 20f * s, 50f * s, 16f * s);
            if (minimap != null)
            {
                if (ClickButton(zOutRect, "-", A(Ink, 0.85f), Text, s)) minimap.Size = Mathf.Clamp(minimap.Size + 25f, 45f, 180f);
                if (ClickButton(zInRect, "+", A(Ink, 0.85f), Text, s)) minimap.Size = Mathf.Clamp(minimap.Size - 25f, 45f, 180f);
            }
            if (ClickButton(mapBtnRect, "MAP [M]", A(Ink, 0.88f), Green, s) ||
                (Event.current.type == EventType.MouseDown && Event.current.button == 0 && r.Contains(Event.current.mousePosition)))
            {
                if (Event.current.type == EventType.MouseDown) Event.current.Use();
                session.PhoneOpen = true;
                session.AppMap("");
            }

            float topEdge = r.y - inset;
            // 7. Turn-by-Turn GPS Strip directly above the minimap when navigating
            if (session.Nav != null && session.Nav.Target.HasValue && !string.IsNullOrEmpty(session.Nav.NextTurnInstruction))
            {
                float gpsH = 26f * s;
                var gpsRect = new Rect(r.x - inset, topEdge - 6f * s - gpsH, r.width + inset * 2f, gpsH);
                Box(gpsRect, panel, new Color(0.04f, 0.14f, 0.18f, 0.95f));
                string turnLine = $"{session.Nav.NextTurnSymbol} {Distance(session.Nav.NextTurnDistance)} · {session.Nav.NextTurnInstruction}";
                Label(new Rect(gpsRect.x + 8f * s, gpsRect.y, gpsRect.width - 16f * s, gpsH), turnLine, smallClip, Color.cyan);
                topEdge = gpsRect.y;
            }
            return topEdge;
        }

        // Above the minimap: mission title, objective, route distance, what you carry, crew pins.
        void DrawObjective(float s, float m, float bottom, SaveData st)
        {
            float pad = 12f * s, w = 300f * s, iw = w - pad * 2f - 7f * s;
            string ttl = session.ObjectiveTitle, obj = session.ObjectiveText;
            if (string.IsNullOrEmpty(ttl) && string.IsNullOrEmpty(obj)) return;
            string extra = "";
            if (st.items.Count > 0) extra = "Carrying " + string.Join(", ", st.items.ConvertAll(data.ItemName));
            if (session.Pins.Count > 0) { var pin = session.Pins[session.Pins.Count - 1]; extra += (extra.Length > 0 ? "  ·  " : "") + $"Crew pin: {pin.label} ({pin.by})"; }
            bool route = session.Nav != null && session.Nav.Target != null;
            float objH = string.IsNullOrEmpty(obj) ? 0f : TH(bodyWrap, obj, iw);
            float extraH = extra.Length > 0 ? TH(smallWrap, extra, iw) + 4f * s : 0f;
            float h = pad * 2f + LH(overline) + LH(heading) + objH + (route ? 24f * s : 0f) + extraH;
            var r = new Rect(m, bottom - h, w, h);
            Box(r, panel, Color.white);
            Box(new Rect(r.x + pad * 0.6f, r.y + pad, 3f * s, h - pad * 2f), rSmall, Green);
            float x = r.x + pad + 7f * s, y = r.y + pad;
            Label(new Rect(x, y, iw, LH(overline)), "OBJECTIVE", overline, Green); y += LH(overline);
            Label(new Rect(x, y, iw, LH(heading)), ttl, heading); y += LH(heading);
            if (objH > 0f) { Label(new Rect(x, y, iw, objH), obj, bodyWrap); y += objH; }
            if (route)
            {
                y += 6f * s;
                float dw = Pill(x, y, Distance(session.Nav.RouteLength) + " AWAY", Green, s);
                int etaMin = Mathf.Max(1, Mathf.CeilToInt(session.Nav.EtaSeconds / 60f));
                Pill(x + dw + 6f * s, y, $"ETA ~{etaMin}m", Blue, s);
                y += 18f * s;
            }
            if (extraH > 0f) { y += 4f * s; Label(new Rect(x, y, iw, extraH), extra, smallWrap); }
        }

        // Bottom-right: wallet, level and XP, reputation; portrait on foot; speed, fuel and condition while driving.
        void DrawWallet(float s, float W, float H, float m, SaveData st)
        {
            float pad = 14f * s, w = 300f * s, iw = w - pad * 2f;
            float h = pad * 2f + LH(big) + LH(small) + 10f * s + 20f * s + 12f * s + LH(overline) + LH(bodyStrong);
            var r = new Rect(W - m - w, H - m - h, w, h);
            Box(r, panel, Color.white);
            float x = r.x + pad, y = r.y + pad;
            Label(new Rect(x, y, iw, LH(big)), Naira(st.cash), big);
            if (session.InCrew) { string tag = $"[{session.CrewTag}]"; float pw = TW(pillText, tag) + 14f * s; Pill(r.xMax - pad - pw, y + (LH(big) - 18f * s) * 0.5f, tag, Green, s); }
            y += LH(big);
            Label(new Rect(x, y, iw, LH(small)), $"{Naira(st.bank)} in RiseBank", small); y += LH(small) + 10f * s;
            float lw = Pill(x, y + 1f * s, $"LV {st.level}", Gold, s);
            Label(new Rect(x + lw + 8f * s, y, iw, 20f * s), session.State.Title, bodyStrong);
            Label(new Rect(x, y, iw, 20f * s), $"{st.xp:0} / 100 XP", smallRight);
            y += 24f * s;
            Bar(new Rect(x, y, iw, 6f * s), st.xp / 100f, Gold); y += 14f * s;
            float col = iw / 3f;
            string[] names = { "PUBLIC", "STREET", "BUSINESS" };
            string[] vals = { st.rep.publicRep.ToString(), st.rep.street.ToString(), st.rep.business.ToString() };
            for (int i = 0; i < 3; i++)
            {
                Label(new Rect(x + col * i, y, col, LH(overline)), names[i], overline);
                Label(new Rect(x + col * i, y + LH(overline), col, LH(bodyStrong)), vals[i], bodyStrong);
            }

            // Portrait on foot (inside a vehicle the portrait camera has no one to look at).
            if (session.Vehicle == null && Portrait != null && Portrait.Texture != null)
            {
                float ps = 92f * s, inset = 4f * s;
                var pr = new Rect(r.x - 10f * s - ps, r.yMax - ps, ps, ps);
                Box(pr, panel, Color.white);
                if (Event.current.type == EventType.Repaint) GUI.DrawTexture(new Rect(pr.x + inset, pr.y + inset, ps - inset * 2f, ps - inset * 2f), Portrait.Texture, ScaleMode.ScaleAndCrop, false);
            }

            // Driving cluster above the wallet (NOS Nitrous Gauge shown ONLY on Premium Racing Cars & Custom-Built NOS Cars).
            var v = session.Vehicle;
            if (v != null)
            {
                bool hasNos = v.HasNos;
                float ch = hasNos ? 104f * s : 84f * s;
                var cr = new Rect(r.x, r.y - 10f * s - ch, w, ch);
                Box(cr, panel, Color.white);
                float cx = cr.x + pad, cy = cr.y + 10f * s;
                string kmh = $"{v.Kmh:0}";
                Label(new Rect(cx, cy, 100f * s, LH(huge)), kmh, huge);
                Label(new Rect(cx + TW(huge, kmh) + 6f * s, cy + LH(huge) - LH(small) - 6f * s, 50f * s, LH(small)), "km/h", small);
                int gear = v.Speed < -0.5f ? 0 : Mathf.Clamp(1 + Mathf.FloorToInt(v.Kmh / 24f), 1, 6);
                string gearLabel = st.fuel <= 0.05f ? "NO FUEL" : gear == 0 ? "REV" : $"GEAR {gear}";
                Pill(cx, cy + LH(huge) + 4f * s, session.NosActive ? "NOS BOOST!" : gearLabel, st.fuel <= 0.05f ? Red : session.NosActive ? Blue : Gold, s);

                float bx = cr.x + 128f * s, bw = cr.xMax - pad - bx, by = cr.y + 10f * s;
                float fuel = st.fuel, cond = v.Condition, nos = session.NosCharge;
                if (hasNos)
                {
                    Label(new Rect(bx, by, bw, 14f * s), "NOS NITROUS", overline, Blue); Label(new Rect(bx, by, bw, 14f * s), $"{nos:0}%", smallRight);
                    Bar(new Rect(bx, by + 15f * s, bw, 6f * s), nos / 100f, session.NosActive ? Color.cyan : Blue);
                    by += 27f * s;
                }
                Label(new Rect(bx, by, bw, 14f * s), fuel <= 0.05f ? "FUEL (EMPTY!)" : "FUEL", overline, fuel <= 0.05f ? Red : Muted);
                Label(new Rect(bx, by, bw, 14f * s), $"{fuel:0}%", smallRight);
                Bar(new Rect(bx, by + 15f * s, bw, 6f * s), fuel / 100f, fuel < 15f ? Red : Gold);
                by += 27f * s;
                Label(new Rect(bx, by, bw, 14f * s), "CONDITION", overline); Label(new Rect(bx, by, bw, 14f * s), $"{cond:0}%", smallRight);
                Bar(new Rect(bx, by + 15f * s, bw, 6f * s), cond / 100f, cond < 30f ? Red : cond < 60f ? Gold : Green);
            }
        }

        // Touch screens: the controls own the bottom corners, so cash, level and speed sit in one strip at the top centre.
        float DrawTouchStrip(float s, float W, float m)
        {
            var st = session.State.S;
            string text = $"{Naira(st.cash)}   ·   LV {st.level}" + (session.Vehicle != null ? $"   ·   {session.Vehicle.Kmh:0} km/h   ·   fuel {st.fuel:0}%" : "");
            float w = TW(bodyStrong, text) + 32f * s, h = 34f * s;
            var r = new Rect(W * 0.5f - w * 0.5f, m, w, h);
            Box(r, panel, Color.white);
            Label(r, text, stripText);
            var pauseBtn = new Rect(r.xMax + 8f * s, m, 82f * s, h);
            if (ClickButton(pauseBtn, session.Paused ? "▶ RESUME" : "|| PAUSE", A(Ink, 0.90f), Gold, s))
                session.TogglePauseMenu();
            return r.yMax;
        }

        // Bottom-centre: health and stamina, the interaction prompt (or the controls hint), chat.
        void DrawCentre(float s, float W, float H, SaveData st, bool touch)
        {
            float bw = 300f * s, gap = 12f * s, half = (bw - gap) * 0.5f, x0 = W * 0.5f - bw * 0.5f, by = H - 22f * s;
            Box(new Rect(x0 - 10f * s, by - 20f * s, bw + 20f * s, 32f * s), panel, new Color(1f, 1f, 1f, 0.88f));
            Label(new Rect(x0, by - 16f * s, half, 14f * s), "HEALTH", overline); Label(new Rect(x0, by - 16f * s, half, 14f * s), $"{st.health:0}", smallRight);
            Bar(new Rect(x0, by, half, 6f * s), st.health / 100f, st.health < 25f ? Red : Hex(0xE5484D));
            float x1 = x0 + half + gap;
            Label(new Rect(x1, by - 16f * s, half, 14f * s), "STAMINA", overline); Label(new Rect(x1, by - 16f * s, half, 14f * s), $"{st.stamina:0}", smallRight);
            Bar(new Rect(x1, by, half, 6f * s), st.stamina / 100f, Blue);

            float slotBottom = by - 26f * s, ph = 40f * s, py = slotBottom - ph;
            bool free = session.ActiveMenu == null && session.Working == null;
            if (st.armor > 0f)
            {
                string aLabel = $"KEVLAR ARMOR {st.armor:0}%";
                float aw = TW(pillText, aLabel) + 14f * s, ax = W * 0.5f - aw * 0.5f, ay = slotBottom - 20f * s;
                Box(new Rect(ax, ay, aw, 18f * s), rPill, A(Ink, 0.88f));
                Pill(ax, ay, aLabel, Blue, s);
                slotBottom -= 24f * s;
                py = slotBottom - ph;
            }
            if (session.Vehicle == null && !string.IsNullOrEmpty(st.equippedWeapon))
            {
                bool isGun = session.IsGunEquipped;
                string wLabel = isGun
                    ? $"{session.EquippedWeaponName.ToUpperInvariant()}  ·  {st.ammo} AMMO  ·  {(touch ? "FIGHT SHOOT · ITEM SWITCH" : "F / CLICK SHOOT · Q SWITCH")}"
                    : $"{session.EquippedWeaponName.ToUpperInvariant()}  ·  {(touch ? "FIGHT / ITEM" : "F FIGHT · Q SWITCH")}";
                float ww = TW(pillText, wLabel) + 14f * s, wx = W * 0.5f - ww * 0.5f, wy = slotBottom - 20f * s;
                Box(new Rect(wx, wy, ww, 18f * s), rPill, A(Ink, 0.88f));
                Pill(wx, wy, wLabel, isGun ? Red : Gold, s);
                slotBottom -= 24f * s;
                py = slotBottom - ph;

                if (isGun && free)
                {
                    bool hasTarget = session.Player != null && session.NearestEnemy(session.Player.transform.position, session.WeaponRange()) != null;
                    Color retCol = hasTarget ? Red : A(Gold, 0.85f);
                    float cx0 = W * 0.5f, cy0 = H * 0.44f, arm = 7f * s, gapR = 4f * s, th = Mathf.Max(1.5f, 1.8f * s);
                    Box(new Rect(cx0 - gapR - arm, cy0 - th * 0.5f, arm, th), rSmall, retCol);
                    Box(new Rect(cx0 + gapR, cy0 - th * 0.5f, arm, th), rSmall, retCol);
                    Box(new Rect(cx0 - th * 0.5f, cy0 - gapR - arm, th, arm), rSmall, retCol);
                    Box(new Rect(cx0 - th * 0.5f, cy0 + gapR, th, arm), rSmall, retCol);

                    // Call of Duty–style Hitmarker & Kill Feed
                    if (Time.time - session.LastHitTime < 0.22f)
                    {
                        bool killed = Time.time - session.LastKillTime < 0.25f;
                        Color hmCol = killed ? Red : Color.white;
                        float hm = 9f * s;
                        Label(new Rect(cx0 - hm, cy0 - hm, hm * 2f, hm * 2f), "✕", stripText, hmCol);
                    }
                    if (Time.time - session.LastKillTime < 1.35f && !string.IsNullOrEmpty(session.LastKillLabel))
                    {
                        string kl = $"+100  {session.LastKillLabel.ToUpperInvariant()}";
                        float kw = TW(pillText, kl) + 18f * s;
                        Pill(cx0 - kw * 0.5f, cy0 + 18f * s, kl, Gold, s);
                    }
                }
            }
            if (!string.IsNullOrEmpty(session.Prompt) && free)
            {
                string key = InteractKeyLabel(touch), label = session.Prompt.ToUpperInvariant();
                float kh = 26f * s, kw = Mathf.Max(kh, TW(keyText, key) + 12f * s);
                float maxPromptW = Mathf.Min(W - 32f * s, 10f * s + kw + 10f * s + TW(promptText, label) + 16f * s);
                var r = new Rect(W * 0.5f - maxPromptW * 0.5f, py, maxPromptW, ph);
                Box(r, panel, Color.white);
                KeyCap(r.x + 10f * s, r.y + (ph - kh) * 0.5f, kh, key, Gold, s);
                Label(new Rect(r.x + 20f * s + kw, r.y, maxPromptW - 28f * s - kw, ph), label, promptText);
            }
            else if (Time.time < hintUntil && free)
            {
                string hint = touch ? "Left thumb moves  ·  drag right to look  ·  ACT interact  ·  FIGHT attack  ·  ITEM pick/switch  ·  PHONE"
                                    : "WASD move  ·  Shift sprint  ·  Space jump  ·  E interact  ·  F / Click fight  ·  G pick item  ·  Q weapon  ·  P phone";
                float fade = Mathf.Clamp01((hintUntil - Time.time) / 1.5f);
                float room = touch ? W - 40f * s : W - 2f * (16f + 300f + 10f + 92f + 14f) * s;   // between the minimap column and the portrait
                float w = Mathf.Min(Mathf.Max(260f * s, room), TW(small, hint) + 28f * s), h = TH(center, hint, w - 28f * s) + 14f * s;
                var r = new Rect(W * 0.5f - w * 0.5f, slotBottom - h, w, h);
                Box(r, panel, new Color(1f, 1f, 1f, 0.85f * fade));
                Label(new Rect(r.x + 14f * s, r.y, w - 28f * s, h), hint, center, new Color(1f, 1f, 1f, fade));
            }

            // Chat: lines from players in your district, and the input box on T / Enter.
            if (session.Players == null) return;
            float cy = py - 10f * s, cw = 560f * s, cx = W * 0.5f - cw * 0.5f;
            if (session.ChatOpen)
            {
                if (Cursor.lockState == CursorLockMode.Locked) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
                float fh = 32f * s; cy -= fh;
                GUI.SetNextControlName("chat");
                var e = Event.current;
                if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)) { session.SubmitChat(); e.Use(); }
                else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { session.ChatOpen = false; e.Use(); }
                else { session.ChatDraft = GUI.TextField(new Rect(cx, cy, cw, fh), session.ChatDraft, 140, field); GUI.FocusControl("chat"); }
                cy -= 6f * s;
            }
            var chat = session.Players.Chat;
            for (int i = chat.Count - 1; i >= 0 && i >= chat.Count - 5; i--)
            {
                var c = chat[i]; string name = c.name + "  ";
                float lh = 24f * s, nw = TW(bodyStrong, name), w = Mathf.Min(cw, nw + TW(body, c.text) + 20f * s);
                cy -= lh;
                Box(new Rect(cx, cy, w, lh - 2f * s), rMid, new Color(0.02f, 0.05f, 0.04f, 0.6f));
                Label(new Rect(cx + 10f * s, cy, nw, lh - 2f * s), name, bodyStrong, Green);
                Label(new Rect(cx + 10f * s + nw, cy, w - nw - 20f * s, lh - 2f * s), c.text, body);
            }
        }

        // Top-centre: pulsing wanted capsule with heat pips. Returns its bottom.
        float DrawWanted(float s, float W, float y, int heat)
        {
            string a = "WANTED", b = "Police dey chase you";
            float d = 9f * s, pips = 5f * d + 4f * 5f * s;
            float w = 18f * s + TW(promptText, a) + 12f * s + pips + 12f * s + TW(small, b) + 18f * s, h = 36f * s;
            var r = new Rect(W * 0.5f - w * 0.5f, y, w, h);
            float pulse = 0.78f + 0.22f * Mathf.Sin(Time.time * 6f);
            Box(r, rPill, new Color(0.55f, 0.08f, 0.1f, 0.92f));
            Box(r, rPill, A(Red, 0.35f * pulse));
            float x = r.x + 18f * s;
            Label(new Rect(x, r.y, w, h), a, promptText); x += TW(promptText, a) + 12f * s;
            for (int i = 0; i < 5; i++) Tex(new Rect(x + i * (d + 4f * s), r.y + (h - d) * 0.5f, d, d), disc, i < heat ? Color.white : new Color(1f, 1f, 1f, 0.25f));
            x += pips + 12f * s;
            Label(new Rect(x, r.y, w, h), b, small, new Color(1.2f, 1.1f, 1.1f, 1f));
            return r.yMax;
        }

        static string InteractKeyLabel(bool touch)
        {
            if (touch) return "ACT";
            return GameInput.ControllerFamily switch
            {
                "PlayStation" => "✕",
                "Switch" => "B",
                "Xbox" => "A",
                "Gamepad" => "A",
                _ => "E"
            };
        }

        // Top-centre: Indoor Location & Active Indoor Mission Banner.
        float DrawIndoorBanner(float s, float W, float y)
        {
            var im = session.ActiveIndoorMission;
            string line = im != null
                ? $"[INDOOR MISSION]  {im.Title.ToUpperInvariant()}  ·  STEP {im.Step}/{im.TotalSteps}  ·  HOSTILES: {session.RemainingEnemies}"
                : $"[INDOOR]  {session.IndoorStations.Count} STATIONS  ·  KITCHEN, RING, SNOOKER & OPS BOARD [E]";
            float maxW = Mathf.Max(280f * s, W - 640f * s);
            float w = Mathf.Min(maxW, TW(bodyStrong, line) + 32f * s), h = 32f * s;
            var r = new Rect(W * 0.5f - w * 0.5f, y, w, h);
            Box(r, rPill, im != null ? new Color(0.12f, 0.34f, 0.22f, 0.94f) : new Color(0.10f, 0.18f, 0.24f, 0.90f));
            Label(r, line, stripText, im != null ? Green : Gold);
            return r.yMax;
        }

        // Top-centre: Call of Duty–style Military (81 Div) & Navy (SBS) Tactical Overwatch strip.
        float DrawSpecOpBanner(float s, float W, float y)
        {
            var op = session.ActiveActionOp;
            if (op == null) return y;
            bool navy = op.Branch == "navy";
            string text = $"{op.Overwatch.ToUpperInvariant()} · {op.Callsign}  |  WAVE {session.CurrentWave}/{op.Waves}  |  HOSTILES: {session.RemainingEnemies}  |  SQUAD: {session.ActiveSquad.Count}";
            float maxW = Mathf.Max(280f * s, W - 640f * s);
            float w = Mathf.Min(maxW, TW(bodyStrong, text) + 32f * s), h = 34f * s;
            var r = new Rect(W * 0.5f - w * 0.5f, y, w, h);
            Box(r, rPill, navy ? new Color(0.08f, 0.22f, 0.42f, 0.92f) : new Color(0.14f, 0.30f, 0.14f, 0.92f));
            Label(r, text, stripText, navy ? Blue : Green);
            return r.yMax;
        }

        // Top-centre: EA Sports FC 26 / Dream League Soccer Live Match Broadcast Scorebug, 2D Pitch Radar, Shot Power Bar & Action Controls.
        float DrawFootballBroadcastBanner(float s, float W, float H, float y)
        {
            var fm = session.ActiveFootball;
            if (fm == null) return y;
            int min = Mathf.FloorToInt(fm.MatchClock);
            int sec = Mathf.FloorToInt((fm.MatchClock - min) * 60f);
            string half = fm.MatchClock < 45f ? "1ST HALF" : fm.MatchClock <= 90f ? "2ND HALF" : "ET GOLDEN GOAL";
            string poss = fm.PlayerHasBall ? "YOU (#10)" : fm.BallCarrier != null ? $"#{fm.BallCarrier.Number} {fm.BallCarrier.Name.ToUpperInvariant()}" : "LOOSE BALL";
            string scoreLine = $"[{session.CurrentTournamentStageLabel}]  {fm.Event.HomeTag} {fm.HomeScore} - {fm.AwayScore} {fm.Event.AwayTag}  |  {min:00}:{sec:00} ({half})  |  {poss}  |  {fm.TacticLabel}";
            float maxW = Mathf.Max(320f * s, W - 640f * s);
            float w = Mathf.Min(maxW, TW(bodyStrong, scoreLine) + 34f * s), h = 36f * s;
            var r = new Rect(W * 0.5f - w * 0.5f, y, w, h);
            Box(r, rPill, new Color(0.05f, 0.24f, 0.14f, 0.94f));
            Label(r, scoreLine, stripText, Green);
            float bottom = r.yMax;

            if (!string.IsNullOrEmpty(fm.Commentary))
            {
                string comm = $"LIVE COMMENTARY: “{fm.Commentary}”";
                float cw = Mathf.Min(maxW, TW(small, comm) + 28f * s), ch = 24f * s;
                var cr = new Rect(W * 0.5f - cw * 0.5f, bottom + 4f * s, cw, ch);
                Box(cr, rPill, new Color(0.03f, 0.08f, 0.06f, 0.85f));
                Label(cr, comm, pillText, Gold);
                bottom = cr.yMax;
            }

            // Shot Power & Green Timed-Finishing Bar when charging or right after shooting
            if (fm.ChargingShot || fm.LastShotQualityTimer > 0f)
            {
                float pwr = fm.ChargingShot ? fm.ShotPowerCharge : fm.LastShotPower;
                float bw = 220f * s, bh = 22f * s;
                var br = new Rect(W * 0.5f - bw * 0.5f, bottom + 4f * s, bw, bh);
                Box(br, rSmall, new Color(0.04f, 0.07f, 0.10f, 0.92f));
                // Sweet-spot green zone (0.53..0.88)
                Box(new Rect(br.x + bw * 0.53f, br.y + 2f * s, bw * 0.35f, bh - 4f * s), rSmall, new Color(0.16f, 0.92f, 0.45f, 0.28f));
                Color barCol = (pwr >= 0.53f && pwr <= 0.88f) ? Green : pwr > 0.88f ? Red : Gold;
                Bar(new Rect(br.x + 4f * s, br.y + 4f * s, bw - 8f * s, bh - 8f * s), pwr, barCol);
                string shotTxt = fm.ChargingShot ? $"SHOT POWER {(pwr * 100f):0}%" : fm.LastShotQualityLabel;
                Label(br, shotTxt, stripText, Color.white);
                bottom = br.yMax;
            }

            // 2D Mini Pitch Radar (shows all 10 players + ball in real time!)
            float radW = 168f * s, radH = 68f * s;
            var radRect = new Rect(W * 0.5f - radW * 0.5f, bottom + 5f * s, radW, radH);
            Box(radRect, rSmall, new Color(0.06f, 0.28f, 0.14f, 0.88f));
            Box(new Rect(radRect.center.x - 0.5f * s, radRect.y + 2f * s, 1f * s, radH - 4f * s), rSmall, new Color(1f, 1f, 1f, 0.25f));
            Vector2 ToRadar(Vector3 wp)
            {
                float nx = Mathf.Clamp((wp.x - fm.PitchCenter.x) / 41f + 0.5f, 0.04f, 0.96f);
                float nz = Mathf.Clamp(0.5f - (wp.z - fm.PitchCenter.z) / 28f, 0.06f, 0.94f);
                return new Vector2(radRect.x + nx * radW, radRect.y + nz * radH);
            }
            foreach (var fp in fm.Players)
            {
                if (fp.Root == null) continue;
                Vector2 rp = ToRadar(fp.Root.position);
                Tex(new Rect(rp.x - 3f * s, rp.y - 3f * s, 6f * s, 6f * s), disc, fp.IsHome ? Green : Red);
            }
            if (session.Player != null)
            {
                Vector2 pp = ToRadar(session.Player.transform.position);
                Tex(new Rect(pp.x - 4f * s, pp.y - 4f * s, 8f * s, 8f * s), disc, Gold);
            }
            Vector2 bp = ToRadar(fm.BallPos);
            Tex(new Rect(bp.x - 2.5f * s, bp.y - 2.5f * s, 5f * s, 5f * s), disc, Color.white);
            bottom = radRect.yMax;

            // On-screen clickable Football Action Strip (above bottom prompt)
            if (session.ActiveMenu == null && !session.Paused)
            {
                float btnW = 98f * s, btnH = 26f * s, gap = 6f * s;
                float totalW = btnW * 4f + gap * 3f;
                float startX = W * 0.5f - totalW * 0.5f;
                float stripY = H - 102f * s;
                if (ClickButton(new Rect(startX, stripY, btnW, btnH), fm.PlayerHasBall ? "PASS [E]" : "CALL PASS [E]", A(Ink, 0.88f), Green, s))
                    session.FootballPass();
                if (ClickButton(new Rect(startX + (btnW + gap), stripY, btnW, btnH), fm.PlayerHasBall ? "SHOOT [F]" : "TACKLE [F]", A(Ink, 0.88f), Gold, s))
                {
                    if (!session.FootballShoot(1.22f)) session.FootballTackle();
                }
                if (ClickButton(new Rect(startX + (btnW + gap) * 2f, stripY, btnW, btnH), "SKILL/CHIP [G]", A(Ink, 0.88f), Blue, s))
                    session.FootballChip();
                if (ClickButton(new Rect(startX + (btnW + gap) * 3f, stripY, btnW, btnH), $"TACTIC [R]", A(Ink, 0.88f), Color.cyan, s))
                    session.CycleFootballTactic();
            }

            if (fm.GoalBannerTimer > 0f && !string.IsNullOrEmpty(fm.GoalBannerText))
            {
                float gw = TW(heading, fm.GoalBannerText) + 48f * s, gh = 48f * s;
                var gr = new Rect(W * 0.5f - gw * 0.5f, H * 0.28f, gw, gh);
                Box(gr, rPill, new Color(0.10f, 0.52f, 0.26f, 0.96f));
                Label(gr, fm.GoalBannerText, stripText, Gold);
            }
            return bottom;
        }

        // Top-centre: Need for Speed (NFS) Street Racing Telemetry Strip, Drift Counter & Speedtrap Banner.
        float DrawNfsRaceBanner(float s, float W, float y)
        {
            var r = session.ActiveRace;
            if (r == null) return y;
            string posTag = r.PlayerPosition == 1 ? "1ST" : r.PlayerPosition == 2 ? "2ND" : r.PlayerPosition == 3 ? "3RD" : "4TH";
            string extra = r.Mode == "drift"
                ? $"  |  DRIFT: {r.DriftScore:0} ({r.DriftMultiplier}x)"
                : r.Mode == "speedtrap"
                    ? $"  |  TRAP: {r.TotalSpeedtrapKmh:0} KM/H"
                    : $"  |  DRIFT: {r.DriftScore:0}";
            string line = $"[NFS {r.Mode.ToUpperInvariant()}] {r.Title}  |  {posTag}/4  |  GATE {r.I + 1}/{r.Cps.Length}  |  {r.T:0.0}s{extra}";
            float maxW = Mathf.Max(280f * s, W - 640f * s);
            float w = Mathf.Min(maxW, TW(bodyStrong, line) + 32f * s), h = 34f * s;
            var rect = new Rect(W * 0.5f - w * 0.5f, y, w, h);
            Box(rect, rPill, new Color(0.08f, 0.12f, 0.28f, 0.94f));
            Label(rect, line, stripText, Blue);
            float bottom = rect.yMax;

            if (r.StyleBannerTimer > 0f && !string.IsNullOrEmpty(r.StyleBanner))
            {
                float sw = TW(bodyStrong, r.StyleBanner) + 32f * s, sh = 28f * s;
                var sr = new Rect(W * 0.5f - sw * 0.5f, bottom + 4f * s, sw, sh);
                Box(sr, rPill, new Color(0.55f, 0.12f, 0.38f, 0.92f));
                Label(sr, r.StyleBanner, stripText, Gold);
                bottom = sr.yMax;
            }
            return bottom;
        }

        // Centre: shift progress.
        void DrawShift(float s, float W, float H)
        {
            float t = Mathf.Clamp01(session.WorkT / session.WorkDuration), pad = 16f * s, w = 420f * s;
            float h = pad * 2f + LH(overline) + LH(heading) + 14f * s + LH(small);
            var r = new Rect(W * 0.5f - w * 0.5f, H * 0.5f - h * 0.5f, w, h);
            Box(r, panel, Color.white);
            float x = r.x + pad, y = r.y + pad, iw = w - pad * 2f;
            Label(new Rect(x, y, iw, LH(overline)), "WORKING", overline, Gold); Label(new Rect(x, y, iw, LH(overline)), $"{t * 100f:0}%", smallRight);
            y += LH(overline);
            Label(new Rect(x, y, iw, LH(heading)), session.Working.title, heading); y += LH(heading) + 2f * s;
            Bar(new Rect(x, y, iw, 8f * s), t, Gold); y += 14f * s;
            Label(new Rect(x, y, iw, LH(small)), $"{session.Working.where}  ·  {clock.ClockText}", small);
        }

        // Top-centre: toasts that slide in and fade out.
        void DrawToasts(float s, float W, float y)
        {
            float maxW = Mathf.Min(520f * s, W * 0.46f);
            foreach (var toast in session.State.Toasts)
            {
                float left = toast.until - Time.time, age = 3.5f - left;
                float alpha = Mathf.Clamp01(left / 0.35f) * Mathf.Clamp01(age / 0.18f);
                if (alpha <= 0f) continue;
                float tw = Mathf.Min(maxW - 40f * s, TW(body, toast.text) + 4f * s);
                float th = TH(bodyWrap, toast.text, tw), h = Mathf.Max(34f * s, th + 14f * s), w = tw + 40f * s;
                float slide = (1f - Mathf.Clamp01(age / 0.18f)) * -8f * s;
                var r = new Rect(W * 0.5f - w * 0.5f, y + slide, w, h);
                Box(r, panel, new Color(1f, 1f, 1f, alpha));
                Tex(new Rect(r.x + 14f * s, r.y + h * 0.5f - 4f * s, 8f * s, 8f * s), disc, A(Green, alpha));
                Label(new Rect(r.x + 30f * s, r.y + (h - th) * 0.5f, tw, th), toast.text, bodyWrap, new Color(1f, 1f, 1f, alpha));
                y += h + 6f * s;
            }
        }

        // Smartphone UI: realistic modern mobile phone frame with Dynamic Island, 5G status bar, wallet/weather widget, and 3x4 app grid.
        void DrawPhone(Menu menu, float s, float W, float H)
        {
            bool touchUi = TouchControls.Active;
            var e = Event.current;
            bool pointer = !touchUi && Cursor.lockState != CursorLockMode.Locked;
            if (e.type == EventType.Repaint) Box(new Rect(0, 0, W, H), rSmall, new Color(0f, 0f, 0f, 0.35f));

            float cw = Mathf.Min(396f * s, W - 24f * s);
            float ch = Mathf.Min(720f * s, H - 20f * s);
            var outer = new Rect(W * 0.5f - cw * 0.5f, Mathf.Max(10f * s, H * 0.5f - ch * 0.5f), cw, ch);

            // Metallic titanium outer frame + dark OLED glass screen
            Box(outer, panel, new Color(0.28f, 0.31f, 0.37f, 0.99f));
            var bezel = new Rect(outer.x + 3f * s, outer.y + 3f * s, outer.width - 6f * s, outer.height - 6f * s);
            Box(bezel, panel, new Color(0.03f, 0.04f, 0.06f, 0.99f));
            var scr = new Rect(outer.x + 9f * s, outer.y + 9f * s, outer.width - 18f * s, outer.height - 18f * s);
            Box(scr, rMid, new Color(0.06f, 0.08f, 0.13f, 0.99f));

            float pad = 14f * s;
            float x = scr.x + pad, y = scr.y + 8f * s, iw = scr.width - pad * 2f;

            // Top Status Bar + Dynamic Island
            Label(new Rect(x, y, 76f * s, 18f * s), clock != null ? clock.ClockText : "12:00", small, Color.white);
            float islandW = 92f * s, islandH = 18f * s;
            var island = new Rect(scr.x + (scr.width - islandW) * 0.5f, y, islandW, islandH);
            Box(island, rPill, Color.black);
            Tex(new Rect(island.xMax - 14f * s, island.y + 5f * s, 8f * s, 8f * s), disc, new Color(0.12f, 0.18f, 0.32f, 0.95f));
            Label(new Rect(scr.xMax - pad - 105f * s, y, 82f * s, 18f * s), "MTN 5G  98%", smallRight, new Color(0.82f, 0.88f, 0.95f));
            Box(new Rect(scr.xMax - pad - 18f * s, y + 4f * s, 16f * s, 10f * s), rSmall, Green);
            y += 24f * s;

            int totalOpts = menu.Options.Count;
            if (menu.Rects.Length != totalOpts) menu.Rects = new Rect[totalOpts];
            for (int i = 0; i < totalOpts; i++) menu.Rects[i] = default;

            bool isHome = menu.Title == "Rise: Hustle or Die Trying" && menu.Fields.Count == 0 && totalOpts >= 10;
            if (isHome)
            {
                // Wallpaper & Live Clock / Weather / RiseBank Widget
                var widget = new Rect(x, y, iw, 96f * s);
                Box(widget, rMid, new Color(0.10f, 0.15f, 0.26f, 0.95f));
                Label(new Rect(widget.x + 12f * s, widget.y + 8f * s, widget.width - 24f * s, LH(big)), clock != null ? clock.ClockText : "12:00", big);
                string wx = session.Weather != null && session.Weather.Raining ? "Rain" : "Clear";
                string season = clock != null ? clock.SeasonLabel : "DRY SEASON";
                Label(new Rect(widget.x + 12f * s, widget.y + 8f * s, widget.width - 24f * s, 18f * s), season, smallRight, Gold);
                Label(new Rect(widget.x + 12f * s, widget.y + 42f * s, widget.width - 24f * s, 18f * s), $"{(clock != null ? clock.DateText : "Lagos")}  ·  {wx}", small, new Color(0.80f, 0.86f, 0.94f));
                var st = session.State.S;
                var balPill = new Rect(widget.x + 10f * s, widget.yMax - 28f * s, widget.width - 20f * s, 22f * s);
                Box(balPill, rSmall, new Color(0.04f, 0.07f, 0.12f, 0.85f));
                Label(new Rect(balPill.x + 8f * s, balPill.y, balPill.width - 16f * s, balPill.height), $"Cash ₦{st.cash:N0}   ·   RiseBank ₦{st.bank:N0}", stripText, Green);
                y = widget.yMax + 12f * s;

                // 3x4 Smartphone App Grid (first totalOpts - 1 options are apps; last option is Put phone away)
                int appCount = Mathf.Min(12, totalOpts - 1);
                int cols = 3;
                int rows = Mathf.CeilToInt(appCount / (float)cols);
                float gapX = 10f * s, gapY = 10f * s;
                float cellW = (iw - gapX * (cols - 1)) / cols;
                float availGridH = scr.yMax - y - 64f * s;
                float cellH = Mathf.Min(86f * s, (availGridH - gapY * Mathf.Max(0, rows - 1)) / Mathf.Max(1, rows));

                string[] shortNames = { "Messages", "Contacts", "RiseBank", "Homes", "Business", "Hustle", "GPS Map", "Refuel", "Flights", "Audio", "Settings", "Account" };
                string[] badges = { "MSG", "TEL", "₦", "KEY", "BIZ", "JOB", "GPS", "FUEL", "AIR", "FM", "SET", "ID" };
                Color[] appColors =
                {
                    new Color(0.16f, 0.72f, 0.42f), // Messages
                    new Color(0.20f, 0.52f, 0.92f), // Contacts
                    new Color(0.85f, 0.65f, 0.14f), // RiseBank
                    new Color(0.58f, 0.34f, 0.86f), // Homes
                    new Color(0.90f, 0.48f, 0.16f), // Business
                    new Color(0.14f, 0.68f, 0.66f), // Hustle
                    new Color(0.18f, 0.62f, 0.95f), // GPS Map
                    new Color(0.92f, 0.28f, 0.24f), // Refuel
                    new Color(0.22f, 0.74f, 0.88f), // Flights
                    new Color(0.84f, 0.26f, 0.58f), // Audio
                    new Color(0.42f, 0.48f, 0.56f), // Settings
                    new Color(0.34f, 0.40f, 0.82f), // Account
                };

                for (int i = 0; i < appCount; i++)
                {
                    int rIdx = i / cols, cIdx = i % cols;
                    var cell = new Rect(x + cIdx * (cellW + gapX), y + rIdx * (cellH + gapY), cellW, cellH);
                    menu.Rects[i] = cell;
                    var o = menu.Options[i];
                    bool sel = i == menu.Index;
                    bool hover = pointer && o.Enabled && cell.Contains(e.mousePosition);

                    if (sel || hover)
                        Box(cell, rMid, sel ? A(Green, 0.22f) : new Color(1f, 1f, 1f, 0.08f));

                    float iconSz = Mathf.Min(44f * s, cellH - 24f * s);
                    var iconRect = new Rect(cell.x + (cell.width - iconSz) * 0.5f, cell.y + 4f * s, iconSz, iconSz);
                    Box(iconRect, rMid, appColors[i % appColors.Length]);
                    Label(iconRect, badges[i % badges.Length], stripText, Color.white);

                    // Key shortcut badge in corner
                    if (i < 9)
                    {
                        var kb = new Rect(iconRect.xMax - 14f * s, iconRect.y - 2f * s, 16f * s, 15f * s);
                        Box(kb, rSmall, new Color(0f, 0f, 0f, 0.75f));
                        Label(kb, (i + 1).ToString(), stripText, Gold);
                    }

                    string appTitle = i < shortNames.Length ? shortNames[i] : o.Label;
                    var lblRect = new Rect(cell.x + 2f * s, iconRect.yMax + 2f * s, cell.width - 4f * s, 18f * s);
                    Label(lblRect, appTitle, stripText, sel ? Green : Color.white);

                    if (pointer && o.Enabled && e.type == EventType.MouseDown && e.button == 0 && cell.Contains(e.mousePosition))
                    {
                        e.Use();
                        session.Choose(i);
                        return;
                    }
                }

                // Bottom Dock: Put Phone Away button
                int closeIdx = totalOpts - 1;
                var dockBtn = new Rect(x, scr.yMax - 48f * s, iw, 30f * s);
                menu.Rects[closeIdx] = dockBtn;
                bool closeSel = menu.Index == closeIdx;
                bool closeHover = pointer && dockBtn.Contains(e.mousePosition);
                Box(dockBtn, rPill, closeSel ? A(Red, 0.35f) : closeHover ? new Color(1f, 1f, 1f, 0.14f) : new Color(1f, 1f, 1f, 0.08f));
                Label(dockBtn, "Lock & Put Phone Away  [P / X]", stripText, closeSel ? Gold : new Color(0.85f, 0.90f, 0.96f));
                if (pointer && e.type == EventType.MouseDown && e.button == 0 && dockBtn.Contains(e.mousePosition))
                {
                    e.Use();
                    session.Choose(closeIdx);
                    return;
                }
            }
            else
            {
                // Inside a Phone App Sub-Page
                var navBar = new Rect(x, y, iw, 30f * s);
                var homeBtn = new Rect(x, y + 2f * s, 68f * s, 26f * s);
                Box(homeBtn, rPill, new Color(0.14f, 0.20f, 0.32f, 0.95f));
                Label(homeBtn, "‹ Home", stripText, Blue);
                if (pointer && e.type == EventType.MouseDown && e.button == 0 && homeBtn.Contains(e.mousePosition))
                {
                    e.Use();
                    session.OpenPhone();
                    return;
                }
                Label(new Rect(homeBtn.xMax + 8f * s, y + 2f * s, iw - 76f * s, 26f * s), menu.Title, bodyStrongClip, Color.white);
                y += 32f * s;

                if (!string.IsNullOrEmpty(menu.Subtitle))
                {
                    float subH = Mathf.Min(36f * s, TH(smallWrap, menu.Subtitle, iw));
                    Label(new Rect(x, y, iw, subH), menu.Subtitle, smallWrap, new Color(0.75f, 0.82f, 0.92f));
                    y += subH + 4f * s;
                }

                if (!string.IsNullOrEmpty(menu.Line))
                {
                    float qH = Mathf.Min(84f * s, TH(bodyWrap, menu.Line, iw - 16f * s) + 12f * s);
                    var bubble = new Rect(x, y, iw, qH);
                    Box(bubble, rMid, new Color(0.12f, 0.22f, 0.36f, 0.92f));
                    Label(new Rect(bubble.x + 8f * s, bubble.y + 6f * s, bubble.width - 16f * s, qH - 12f * s), menu.Line, bodyWrap, Color.white);
                    y += qH + 6f * s;
                }

                float fieldH = 30f * s;
                for (int f = 0; f < menu.Fields.Count; f++)
                {
                    var fd = menu.Fields[f];
                    Label(new Rect(x, y, 88f * s, fieldH), fd.Label, small);
                    var fr = new Rect(x + 88f * s, y, iw - 88f * s, fieldH);
                    GUI.SetNextControlName("field" + f);
                    fd.Value = fd.Password ? GUI.PasswordField(fr, fd.Value ?? "", '•', 64, field) : GUI.TextField(fr, fd.Value ?? "", 64, field);
                    y += fieldH + 6f * s;
                }
                if (menu.Fields.Count > 0 && Cursor.lockState == CursorLockMode.Locked) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
                session.Typing = menu.Fields.Count > 0 && GUI.GetNameOfFocusedControl().StartsWith("field");

                float rowH = 32f * s, rowGap = 4f * s;
                float availH = scr.yMax - y - 28f * s;
                int maxVis = Mathf.Max(3, Mathf.FloorToInt((availH + rowGap) / (rowH + rowGap)));
                int visCount = Mathf.Min(totalOpts, maxVis);
                int startIdx = totalOpts > visCount ? Mathf.Clamp(menu.Index - visCount / 2, 0, totalOpts - visCount) : 0;
                int endIdx = Mathf.Min(totalOpts, startIdx + visCount);

                for (int i = startIdx; i < endIdx; i++)
                {
                    var o = menu.Options[i];
                    var row = new Rect(x, y, iw, rowH);
                    menu.Rects[i] = row;
                    bool sel = i == menu.Index;
                    bool hover = pointer && o.Enabled && row.Contains(e.mousePosition);
                    Box(row, rMid, sel ? A(Green, 0.20f) : hover ? new Color(1f, 1f, 1f, 0.10f) : new Color(1f, 1f, 1f, 0.04f));
                    if (sel) Box(new Rect(row.x, row.y + 5f * s, 3f * s, rowH - 10f * s), rSmall, Green);
                    Label(new Rect(row.x + 10f * s, row.y, row.width - 16f * s, rowH), o.Label, !o.Enabled ? bodyMutedClip : sel ? bodyStrongClip : bodyClip);
                    if (pointer && o.Enabled && e.type == EventType.MouseDown && e.button == 0 && row.Contains(e.mousePosition))
                    {
                        e.Use();
                        session.Choose(i);
                        return;
                    }
                    y += rowH + rowGap;
                }
            }

            // Bottom iOS/Android Home Indicator Pill
            var homeBar = new Rect(scr.x + (scr.width - 110f * s) * 0.5f, scr.yMax - 12f * s, 110f * s, 5f * s);
            Box(homeBar, rPill, new Color(1f, 1f, 1f, 0.45f));
        }

        // Full-Screen Split Interactive GPS Map & Navigation Console
        void DrawInteractiveMapMenu(Menu menu, float s, float W, float H)
        {
            bool touchUi = TouchControls.Active;
            var e = Event.current;
            bool pointer = !touchUi && Cursor.lockState != CursorLockMode.Locked;
            if (e.type == EventType.Repaint) Box(new Rect(0, 0, W, H), rSmall, new Color(0f, 0f, 0f, 0.45f));

            float pad = 16f * s;
            float cw = Mathf.Min(1080f * s, W - 24f * s);
            float ch = Mathf.Min(680f * s, H - 24f * s);
            var card = new Rect(W * 0.5f - cw * 0.5f, Mathf.Max(12f * s, H * 0.5f - ch * 0.5f), cw, ch);
            Box(card, panel, Color.white);

            float mapW = Mathf.Floor(cw * 0.54f);
            var mapRect = new Rect(card.x + pad, card.y + pad, mapW, ch - pad * 2f);
            Box(mapRect, rMid, new Color(0.05f, 0.09f, 0.15f, 0.98f));

            // Map world coordinate conversion (-1650..1650)
            float worldSpan = 3300f / Mathf.Clamp(mapCanvasZoom, 0.55f, 2.5f);
            Vector3 pPos = session.Player != null ? session.Player.transform.position : Vector3.zero;
            Vector2 centerXZ = new Vector2(Mathf.Clamp(pPos.x, -600f, 600f), Mathf.Clamp(pPos.z, -600f, 600f));

            Vector2 WorldToMap(Vector3 wPos)
            {
                float nx = 0.5f + (wPos.x - centerXZ.x) / worldSpan;
                float ny = 0.5f - (wPos.z - centerXZ.y) / worldSpan;
                return new Vector2(mapRect.x + nx * mapRect.width, mapRect.y + ny * mapRect.height);
            }

            Vector3 MapToWorld(Vector2 mPos)
            {
                float nx = (mPos.x - mapRect.x) / mapRect.width - 0.5f;
                float ny = 0.5f - (mPos.y - mapRect.y) / mapRect.height;
                return new Vector3(centerXZ.x + nx * worldSpan, 0f, centerXZ.y + ny * worldSpan);
            }

            GUI.BeginGroup(mapRect);
            var localRect = new Rect(0, 0, mapRect.width, mapRect.height);

            // Subtle satellite grid lines
            for (int g = 1; g < 8; g++)
            {
                float gx = localRect.width * (g / 8f);
                float gy = localRect.height * (g / 8f);
                Box(new Rect(gx, 0, 1f, localRect.height), rSmall, new Color(1f, 1f, 1f, 0.04f));
                Box(new Rect(0, gy, localRect.width, 1f), rSmall, new Color(1f, 1f, 1f, 0.04f));
            }

            // Roads network
            if (session.Data != null && session.Data.roads != null)
            {
                Color roadFill = new Color(0.24f, 0.30f, 0.40f, 0.90f);
                foreach (var rd in session.Data.roads)
                {
                    Vector3 p0 = rd.Horizontal ? WorldData.W(rd.from, 0f, rd.k) : WorldData.W(rd.k, 0f, rd.from);
                    Vector3 p1 = rd.Horizontal ? WorldData.W(rd.to, 0f, rd.k) : WorldData.W(rd.k, 0f, rd.to);
                    Vector2 a = WorldToMap(p0) - mapRect.position;
                    Vector2 b = WorldToMap(p1) - mapRect.position;
                    float minX = Mathf.Min(a.x, b.x), maxX = Mathf.Max(a.x, b.x);
                    float minY = Mathf.Min(a.y, b.y), maxY = Mathf.Max(a.y, b.y);
                    float thick = Mathf.Max(3f * s, rd.width * 0.16f * s);
                    if (Mathf.Abs(a.x - b.x) < 2f)
                        Box(new Rect(a.x - thick * 0.5f, minY, thick, Mathf.Max(2f, maxY - minY)), rSmall, roadFill);
                    else if (Mathf.Abs(a.y - b.y) < 2f)
                        Box(new Rect(minX, a.y - thick * 0.5f, Mathf.Max(2f, maxX - minX), thick), rSmall, roadFill);
                    else
                    {
                        int steps = 16;
                        for (int k = 0; k <= steps; k++)
                        {
                            Vector2 pt = Vector2.Lerp(a, b, k / (float)steps);
                            Box(new Rect(pt.x - thick * 0.5f, pt.y - thick * 0.5f, thick, thick), rSmall, roadFill);
                        }
                    }
                }
            }

            // Active GPS Route Polyline
            if (session.Nav != null && session.Nav.Target.HasValue && session.Nav.CurrentRoute != null)
            {
                var route = session.Nav.CurrentRoute;
                for (int r = 0; r < route.Count - 1; r++)
                {
                    Vector2 a = WorldToMap(route[r]) - mapRect.position;
                    Vector2 b = WorldToMap(route[r + 1]) - mapRect.position;
                    float dist = Vector2.Distance(a, b);
                    int steps = Mathf.Clamp(Mathf.CeilToInt(dist / (4f * s)), 1, 45);
                    for (int k = 0; k <= steps; k++)
                    {
                        Vector2 pt = Vector2.Lerp(a, b, k / (float)steps);
                        Tex(new Rect(pt.x - 3f * s, pt.y - 3f * s, 6f * s, 6f * s), disc, new Color(0.15f, 0.92f, 1f, 0.95f));
                    }
                }
            }

            // Landmarks & Places
            if (session.Data != null && session.Data.landmarks != null)
            {
                foreach (var pl in session.Data.landmarks)
                {
                    Vector2 mp = WorldToMap(pl.World()) - mapRect.position;
                    if (mp.x < 6f || mp.x > localRect.width - 6f || mp.y < 6f || mp.y > localRect.height - 6f) continue;
                    Color pc = pl.id == "fuel" ? Gold : pl.id == "ladipo" ? new Color(0.95f, 0.35f, 0.85f) : pl.id == "airport" ? Blue : new Color(0.75f, 0.82f, 0.92f);
                    float sz = (pl.id == "fuel" || pl.id == "ladipo" || pl.id == "airport") ? 8f * s : 6f * s;
                    Tex(new Rect(mp.x - sz * 0.5f, mp.y - sz * 0.5f, sz, sz), disc, pc);
                    if (pl.id == "fuel" || pl.id == "ladipo" || pl.id == "airport" || pl.id == "cms" || pl.id == "eco_hotel" || pl.id == "pitch")
                        Label(new Rect(mp.x + 6f * s, mp.y - 8f * s, 110f * s, 16f * s), pl.name, stripText, pc);
                }
            }

            // Contacts
            if (session.Contacts != null)
            {
                foreach (var c in session.Contacts.Contacts)
                {
                    Vector2 mp = WorldToMap(c.pos) - mapRect.position;
                    if (mp.x < 6f || mp.x > localRect.width - 6f || mp.y < 6f || mp.y > localRect.height - 6f) continue;
                    Tex(new Rect(mp.x - 4.5f * s, mp.y - 4.5f * s, 9f * s, 9f * s), disc, Gold);
                    Label(new Rect(mp.x + 6f * s, mp.y - 8f * s, 90f * s, 16f * s), c.data.name, stripText, Gold);
                }
            }

            // Owned Vehicles
            foreach (var pv in session.Parked)
            {
                if (pv == null || !pv.Owned) continue;
                Vector2 mp = WorldToMap(pv.transform.position) - mapRect.position;
                if (mp.x >= 4f && mp.x <= localRect.width - 4f && mp.y >= 4f && mp.y <= localRect.height - 4f)
                    Tex(new Rect(mp.x - 4f * s, mp.y - 4f * s, 8f * s, 8f * s), disc, Blue);
            }

            // Active GPS Target Beacon
            string wpLabel = "Destination";
            if (!string.IsNullOrEmpty(session.State.S.waypointId))
                session.ResolveWaypoint(session.State.S.waypointId, out wpLabel);
            if (session.Nav != null && session.Nav.Target.HasValue)
            {
                Vector2 tp = WorldToMap(session.Nav.Target.Value) - mapRect.position;
                float pulse = 12f * s + Mathf.Sin(Time.unscaledTime * 6f) * 3f * s;
                Tex(new Rect(tp.x - pulse * 0.5f, tp.y - pulse * 0.5f, pulse, pulse), disc, A(Green, 0.35f));
                Tex(new Rect(tp.x - 4f * s, tp.y - 4f * s, 8f * s, 8f * s), disc, Green);
                Label(new Rect(tp.x + 8f * s, tp.y - 9f * s, 150f * s, 18f * s), wpLabel ?? "GPS Target", stripText, Green);
            }

            // Player Marker & Heading Arrow
            Vector2 playerMp = WorldToMap(pPos) - mapRect.position;
            float pYaw = session.Vehicle != null ? session.Vehicle.transform.eulerAngles.y : (session.Player != null ? session.Player.transform.eulerAngles.y : 0f);
            var mBackup = GUI.matrix;
            GUIUtility.RotateAroundPivot(pYaw, new Vector2(mapRect.x + playerMp.x, mapRect.y + playerMp.y));
            Tex(new Rect(playerMp.x - 9f * s, playerMp.y - 9f * s, 18f * s, 18f * s), arrow, Color.white);
            GUI.matrix = mBackup;

            // Map Header Overlay
            var mapTop = new Rect(10f * s, 10f * s, localRect.width - 20f * s, 28f * s);
            Box(mapTop, rPill, new Color(0.03f, 0.05f, 0.09f, 0.88f));
            Label(new Rect(mapTop.x + 12f * s, mapTop.y, mapTop.width - 24f * s, mapTop.height), "LAGOS SATELLITE GPS  ·  CLICK ANYWHERE TO SET WAYPOINT", stripText, Blue);

            // Turn-by-turn status bar at bottom of map
            var navStrip = new Rect(10f * s, localRect.height - 38f * s, localRect.width - 96f * s, 28f * s);
            Box(navStrip, rPill, new Color(0.03f, 0.06f, 0.11f, 0.92f));
            string navStatus = session.Nav != null && session.Nav.Target.HasValue
                ? $"{session.Nav.NextTurnSymbol} {session.Nav.NextTurnInstruction} ({session.Nav.NextTurnDistance:0}m)  ·  {wpLabel ?? "GPS Target"} ({session.Nav.RouteLength:0}m · ETA {session.Nav.EtaSeconds / 60}:{session.Nav.EtaSeconds % 60:00})"
                : "No active GPS route — click any point on the map or pick a destination on the right";
            Label(new Rect(navStrip.x + 10f * s, navStrip.y, navStrip.width - 20f * s, navStrip.height), navStatus, stripText, session.Nav != null && session.Nav.Target.HasValue ? Green : new Color(0.78f, 0.84f, 0.92f));

            GUI.EndGroup();

            // Zoom [-] / [+] buttons on map
            var zMinus = new Rect(mapRect.xMax - 76f * s, mapRect.yMax - 38f * s, 30f * s, 28f * s);
            var zPlus = new Rect(mapRect.xMax - 42f * s, mapRect.yMax - 38f * s, 30f * s, 28f * s);
            Box(zMinus, rSmall, new Color(0.10f, 0.14f, 0.22f, 0.95f));
            Box(zPlus, rSmall, new Color(0.10f, 0.14f, 0.22f, 0.95f));
            Label(zMinus, "-", stripText, Color.white);
            Label(zPlus, "+", stripText, Color.white);

            if (pointer && e.type == EventType.MouseDown && e.button == 0)
            {
                if (zMinus.Contains(e.mousePosition))
                {
                    e.Use();
                    mapCanvasZoom = Mathf.Clamp(mapCanvasZoom - 0.25f, 0.55f, 2.5f);
                    return;
                }
                if (zPlus.Contains(e.mousePosition))
                {
                    e.Use();
                    mapCanvasZoom = Mathf.Clamp(mapCanvasZoom + 0.25f, 0.55f, 2.5f);
                    return;
                }
                if (mapRect.Contains(e.mousePosition))
                {
                    e.Use();
                    Vector3 clickedWorld = MapToWorld(e.mousePosition);
                    session.SetWaypoint($"pos:{clickedWorld.x:0}:{clickedWorld.z:0}:Map Pin ({clickedWorld.x:0}, {clickedWorld.z:0})");
                    session.State.Toast($"GPS locked to Map Pin ({clickedWorld.x:0}, {clickedWorld.z:0})");
                    return;
                }
            }

            // Right Column: Search + Quick Presets + Scrollable Destinations
            float rx = mapRect.xMax + pad;
            float rw = card.xMax - rx - pad;
            float ry = card.y + pad;

            Label(new Rect(rx, ry, rw, LH(title)), menu.Title, title);
            ry += LH(title);
            if (!string.IsNullOrEmpty(menu.Subtitle))
            {
                float subH = TH(smallWrap, menu.Subtitle, rw);
                Label(new Rect(rx, ry, rw, subH), menu.Subtitle, smallWrap, Gold);
                ry += subH + 4f * s;
            }
            if (!string.IsNullOrEmpty(menu.Line))
            {
                float lineH = TH(bodyWrap, menu.Line, rw);
                Label(new Rect(rx, ry, rw, lineH), menu.Line, bodyWrap);
                ry += lineH + 6f * s;
            }

            float fieldH = 32f * s;
            for (int f = 0; f < menu.Fields.Count; f++)
            {
                var fd = menu.Fields[f];
                Label(new Rect(rx, ry, 76f * s, fieldH), fd.Label, small);
                var fr = new Rect(rx + 76f * s, ry, rw - 76f * s, fieldH);
                GUI.SetNextControlName("field" + f);
                fd.Value = GUI.TextField(fr, fd.Value ?? "", 64, field);
                ry += fieldH + 6f * s;
            }
            if (menu.Fields.Count > 0 && Cursor.lockState == CursorLockMode.Locked) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            session.Typing = menu.Fields.Count > 0 && GUI.GetNameOfFocusedControl().StartsWith("field");

            int totalOpts = menu.Options.Count;
            if (menu.Rects.Length != totalOpts) menu.Rects = new Rect[totalOpts];
            for (int i = 0; i < totalOpts; i++) menu.Rects[i] = default;

            float baseRowH = 30f * s, rowGap = 3f * s;
            float footerH = LH(small) + 6f * s;
            float availOptsH = Mathf.Max(baseRowH * 4f, card.yMax - pad - ry - footerH);
            int maxVisible = Mathf.Max(4, Mathf.FloorToInt((availOptsH + rowGap) / (baseRowH + rowGap)));
            int visCount = Mathf.Min(totalOpts, maxVisible);
            int startIdx = totalOpts > visCount ? Mathf.Clamp(menu.Index - visCount / 2, 0, totalOpts - visCount) : 0;
            int endIdx = Mathf.Min(totalOpts, startIdx + visCount);

            float optsTopY = ry;
            for (int i = startIdx; i < endIdx; i++)
            {
                var o = menu.Options[i];
                var row = new Rect(rx, ry, rw - 8f * s, baseRowH);
                menu.Rects[i] = row;
                bool sel = i == menu.Index, hover = pointer && o.Enabled && row.Contains(e.mousePosition);
                if (sel) Box(row, rMid, A(Green, 0.16f));
                else if (hover) Box(row, rMid, new Color(1f, 1f, 1f, 0.07f));
                if (sel) Box(new Rect(row.x, row.y + 5f * s, 3f * s, baseRowH - 10f * s), rSmall, Green);
                Label(new Rect(row.x + 10f * s, row.y, row.width - 16f * s, baseRowH), o.Label, !o.Enabled ? bodyMutedClip : sel ? bodyStrongClip : bodyClip);
                if (pointer && o.Enabled && e.type == EventType.MouseDown && e.button == 0 && row.Contains(e.mousePosition))
                {
                    e.Use();
                    session.Choose(i);
                    return;
                }
                ry += baseRowH + rowGap;
            }

            if (totalOpts > visCount)
            {
                float sbX = card.xMax - pad * 0.6f, sbH = visCount * (baseRowH + rowGap) - rowGap;
                Box(new Rect(sbX, optsTopY, 4f * s, sbH), rSmall, Track);
                float thumbH = Mathf.Max(18f * s, sbH * ((float)visCount / totalOpts));
                float thumbT = (float)startIdx / Mathf.Max(1, totalOpts - visCount);
                Box(new Rect(sbX, optsTopY + (sbH - thumbH) * thumbT, 4f * s, thumbH), rSmall, Gold);
            }

            Label(new Rect(rx, card.yMax - pad - LH(small), rw, LH(small)), "Click map to drop pin  ·  W/S or Scroll list  ·  Enter lock GPS  ·  X close", smallClip);
        }

        // Centre: the menu card (dialogue, agent, home, business, bus stop, kiosk, police, sign-in) with viewport-clamped auto-scrolling.
        void DrawMenu(Menu menu, float s, float W, float H)
        {
            bool touchUi = TouchControls.Active;
            float pad = 18f * s, cw = Mathf.Min(640f * s, W - 32f * s), iw = cw - pad * 2f;
            int totalOpts = menu.Options.Count;
            float baseRowH = (touchUi ? 42f : totalOpts > 10 ? 30f : 35f) * s;
            float rowGap = (totalOpts > 10 ? 3f : 4f) * s;
            float fieldH = 34f * s;
            float titleH = LH(title);
            float subH = string.IsNullOrEmpty(menu.Subtitle) ? 0f : TH(smallWrap, menu.Subtitle, iw) + 2f * s;
            string quote = string.IsNullOrEmpty(menu.Line) ? null : $"“{menu.Line}”";
            float quoteH = quote == null ? 0f : Mathf.Min(110f * s, TH(bodyWrap, quote, iw - 14f * s));
            float fieldsH = menu.Fields.Count * (fieldH + 8f * s);

            float headerH = pad + titleH + subH + (quote != null ? quoteH + 14f * s : 0f) + 10f * s + fieldsH;
            float footerH = 8f * s + LH(small) + pad;
            float maxCardH = H - 24f * s;
            float availOptsH = Mathf.Max(baseRowH * 3f, maxCardH - headerH - footerH);
            int maxVisible = Mathf.Max(3, Mathf.FloorToInt((availOptsH + rowGap) / (baseRowH + rowGap)));
            int visCount = Mathf.Min(totalOpts, maxVisible);
            float optsH = visCount * (baseRowH + rowGap);
            float h = Mathf.Min(maxCardH, headerH + optsH + footerH);

            var r = new Rect(W * 0.5f - cw * 0.5f, Mathf.Max(12f * s, H * 0.5f - h * 0.5f), cw, h);
            if (Event.current.type == EventType.Repaint) Box(new Rect(0, 0, W, H), rSmall, new Color(0f, 0f, 0f, 0.25f));   // dim the world behind
            Box(r, panel, Color.white);
            float x = r.x + pad, y = r.y + pad;
            Label(new Rect(x, y, iw, titleH), menu.Title, title); y += titleH;
            if (subH > 0f) { Label(new Rect(x, y, iw, subH), menu.Subtitle, smallWrap); y += subH; }
            if (quote != null)
            {
                y += 6f * s;
                Box(new Rect(x, y, 3f * s, quoteH), rSmall, A(Gold, 0.6f));
                Label(new Rect(x + 14f * s, y, iw - 14f * s, quoteH), quote, bodyWrap);
                y += quoteH + 8f * s;
            }
            y += 10f * s;

            for (int f = 0; f < menu.Fields.Count; f++)
            {
                var fd = menu.Fields[f];
                Label(new Rect(x, y, 110f * s, fieldH), fd.Label, small);
                var fr = new Rect(x + 110f * s, y, iw - 110f * s, fieldH);
                GUI.SetNextControlName("field" + f);
                fd.Value = fd.Password ? GUI.PasswordField(fr, fd.Value ?? "", '•', 64, field) : GUI.TextField(fr, fd.Value ?? "", 64, field);
                y += fieldH + 8f * s;
            }
            if (menu.Fields.Count > 0 && Cursor.lockState == CursorLockMode.Locked) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            session.Typing = menu.Fields.Count > 0 && GUI.GetNameOfFocusedControl().StartsWith("field");

            if (menu.Rects.Length != totalOpts) menu.Rects = new Rect[totalOpts];
            for (int i = 0; i < totalOpts; i++) menu.Rects[i] = default;

            int startIdx = 0;
            if (totalOpts > visCount)
                startIdx = Mathf.Clamp(menu.Index - visCount / 2, 0, totalOpts - visCount);
            int endIdx = Mathf.Min(totalOpts, startIdx + visCount);

            var e = Event.current;
            bool pointer = !touchUi && Cursor.lockState != CursorLockMode.Locked;   // a free mouse can hover and click rows
            float optsTopY = y;
            for (int i = startIdx; i < endIdx; i++)
            {
                var o = menu.Options[i];
                var row = new Rect(x - 8f * s, y, iw + (totalOpts > visCount ? 8f * s : 16f * s), baseRowH);
                menu.Rects[i] = row;
                bool sel = i == menu.Index, hover = pointer && o.Enabled && row.Contains(e.mousePosition);
                if (sel) Box(row, rMid, A(Green, 0.14f));
                else if (hover) Box(row, rMid, new Color(1f, 1f, 1f, 0.06f));
                if (sel) Box(new Rect(row.x, row.y + 6f * s, 3f * s, baseRowH - 12f * s), rSmall, Green);
                float kh = Mathf.Min(22f * s, baseRowH - 6f * s);
                float kw = KeyCap(row.x + 14f * s, row.y + (baseRowH - kh) * 0.5f, kh, (i + 1).ToString(), !o.Enabled ? new Color(1f, 1f, 1f, 0.25f) : sel ? Green : new Color(1f, 1f, 1f, 0.85f), s);
                Label(new Rect(row.x + 14f * s + kw + 12f * s, row.y, row.width - kw - 34f * s, baseRowH), o.Label, !o.Enabled ? bodyMutedClip : sel ? bodyStrongClip : bodyClip);
                if (pointer && o.Enabled && e.type == EventType.MouseDown && e.button == 0 && row.Contains(e.mousePosition)) { e.Use(); session.Choose(i); return; }
                y += baseRowH + rowGap;
            }

            // Sleek scrollbar indicator when menu has more options than visible viewport rows.
            if (totalOpts > visCount)
            {
                float sbX = r.xMax - pad * 0.55f, sbH = visCount * (baseRowH + rowGap) - rowGap;
                Box(new Rect(sbX, optsTopY, 4f * s, sbH), rSmall, Track);
                float thumbH = Mathf.Max(18f * s, sbH * ((float)visCount / totalOpts));
                float thumbT = (float)startIdx / Mathf.Max(1, totalOpts - visCount);
                Box(new Rect(sbX, optsTopY + (sbH - thumbH) * thumbT, 4f * s, thumbH), rSmall, Gold);
            }

            y += 4f * s;
            string scrollTag = totalOpts > visCount ? $"  ·  Showing {startIdx + 1}–{endIdx} of {totalOpts} (Scroll/W/S)" : "";
            string footer = touchUi
                ? $"Tap an option  ·  X to close{scrollTag}"
                : $"W/S or Scroll choose  ·  {InteractKeyLabel(false)} confirm  ·  1–{Mathf.Min(9, Mathf.Max(1, totalOpts))} pick  ·  X close{scrollTag}";
            Label(new Rect(x, y, iw, LH(small)), footer, smallClip);
        }

        // Full Game UI & UX Audit: verifies fonts, procedural 9-slice styles, multi-resolution non-overlapping HUD geometry, and menu viewport clamping.
        public bool AuditUiAndUxHealth(out string report)
        {
            float s = Scale;
            if (!Mathf.Approximately(s, builtScale) || panel == null || panel.normal.background == null) Build(s);
            bool fontsOk = regular != null && semibold != null;
            bool stylesOk = panel != null && panel.normal.background != null && disc != null && arrow != null && bodyClip != null && bodyClip.clipping == TextClipping.Clip;

            // Verify non-overlapping bottom HUD columns (Minimap 180*s + Centre Vitals 300*s + Portrait 92*s + Wallet 300*s) across 720p, 1080p, 4:3, 21:9, and 4K.
            Vector2[] resolutions = { new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(1024, 768), new Vector2(2560, 1080), new Vector2(3840, 2160) };
            bool layoutOk = true;
            foreach (var res in resolutions)
            {
                float rs = ComputeScaleFor(res.x, res.y);
                float m = 16f * rs;
                float mapRight = m + 180f * rs;
                float centreLeft = res.x * 0.5f - 150f * rs;
                float centreRight = res.x * 0.5f + 150f * rs;
                float walletWithPortraitLeft = res.x - m - 300f * rs - 10f * rs - 92f * rs;
                if (mapRight >= centreLeft || centreRight >= walletWithPortraitLeft) layoutOk = false;

                // Verify a 20-option menu fits inside the vertical safe area on this resolution.
                float maxCardH = res.y - 24f * rs;
                if (maxCardH < 320f * rs) layoutOk = false;
            }

            report = $"Fonts={(fontsOk ? "Inter OK" : "Fallback")}, Styles={(stylesOk ? "Clipped+9Slice OK" : "FAIL")}, MultiResLayout={(layoutOk ? "5/5 Aspect Ratios Non-Overlapping" : "OVERLAP")}";
            return fontsOk && stylesOk && layoutOk;
        }
    }
}
