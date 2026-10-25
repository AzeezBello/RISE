using System.Collections.Generic;
using UnityEngine;
using Rise.Data;

namespace Rise.World
{
    // Builds the Lagos map from the exported district data: ground, water, roads with pavements, medians and street
    // lights, bridge decks with barriers and pillars, landmarks with signs, bus stops, properties, compound cells,
    // procedural housing, toll plazas and footbridges. Everything is static geometry and is batched at the end.
    public class WorldBuilder : MonoBehaviour
    {
        public int MaxHouses = 7800;
        public float HouseGrid = 20f;

        public WorldData Data { get; private set; }
        public Transform Root { get; private set; }
        public Material LampHead { get; private set; }
        public int ObjectCount { get; private set; }

        readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        readonly List<Vector2> placedPlots = new List<Vector2>();
        Transform blueLineTrain, redLineTrain;
        Font font;
        Shader standard;

        // Palette
        const string Earth = "#6f6a5c", Asphalt = "#3a3a3d", AsphaltLight = "#45454a", Pavement = "#9a958a", Concrete = "#b9b5aa",
            Water = "#2b6e8f", Sand = "#d9c8a0", Pitch = "#4f8a3a", Zinc = "#5b5f63", Glass = "#7fa6c4", Yellow = "#f5c518",
            Black = "#141414", Steel = "#7d8287", LampWarm = "#ffd27a", Cream = "#e2d6bf", BrtBlue = "#1559b8", LawmaOrange = "#ff6a00";
        static readonly string[] HouseColours = { "#e2d6bf", "#d8b98c", "#c9c2b3", "#b8c4cc", "#d9a66e", "#c7b7a2", "#e8e0d0", "#a9b7a1", "#e6c786", "#c48b71", "#9bb0a8", "#d4c4a8" };

        public void Build(WorldData data)
        {
            Data = data;
            placedPlots.Clear();
            if (GameBootstrap.Mobile)
            {
                MaxHouses = Mathf.Min(MaxHouses, Application.isEditor ? 2200 : 580);
                HouseGrid = Mathf.Max(HouseGrid, Application.isEditor ? 28f : 42f);
            }
            else if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                MaxHouses = Mathf.Min(MaxHouses, 1600);
                HouseGrid = Mathf.Max(HouseGrid, 28f);
            }
            standard = ShaderLib.Lit;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Root = new GameObject("Lagos").transform;
            Root.SetParent(transform, false);
            LampHead = Mat("lamp", LampWarm, 0.3f, true);

            BuildGround();
            BuildWater();
            BuildRoads();
            BuildBridges();
            BuildRailway();
            BuildLandmarks();
            BuildBusStops();
            BuildProperties();
            BuildCells();
            BuildHousing();
            BuildTolls();
            BuildFootbridges();
            BuildBillboards();
            BuildStreetFurniture();
            BuildBusinesses();
            BuildVegetation();

            if (!GameBootstrap.Mobile) StaticBatchingUtility.Combine(Root.gameObject);
            BuildMovingTrains();
        }

        // ---------- materials and primitives ----------

        Material Mat(string key, string hex, float smoothness = 0.15f, bool emissive = false)
        {
            if (materials.TryGetValue(key, out var m)) return m;
            m = new Material(standard) { name = key };
            var col = Hex(hex);
            m.color = col;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (key == "asphalt" || key == "asphalt-hi")
            {
                smoothness = Mathf.Max(smoothness, 0.34f);
                m.mainTexture = ShaderLib.AsphaltTex;
                if (m.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", ShaderLib.AsphaltTex); m.SetTextureScale("_BaseMap", new Vector2(8f, 8f)); }
            }
            else if (key == "pavement" || key == "concrete")
            {
                smoothness = Mathf.Max(smoothness, 0.24f);
                m.mainTexture = ShaderLib.PavementTex;
                if (m.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", ShaderLib.PavementTex); m.SetTextureScale("_BaseMap", new Vector2(6f, 6f)); }
            }
            else if (key == "glass")
            {
                smoothness = Mathf.Max(smoothness, 0.92f);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.35f);
            }
            else if (key.StartsWith("#") && smoothness <= 0.2f)
            {
                m.mainTexture = ShaderLib.BrickTex;
                if (m.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", ShaderLib.BrickTex); m.SetTextureScale("_BaseMap", new Vector2(3f, 3f)); }
            }
            m.SetFloat("_Smoothness", smoothness);
            if (emissive) { m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; m.SetColor("_EmissionColor", Color.black); }
            materials[key] = m;
            return m;
        }
        Material Mat(string hex, float smoothness = 0.15f) => Mat(hex, hex, smoothness);

        public static Color Hex(string hex, string fallback = "#8a7d6a")
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c)) return c;
            ColorUtility.TryParseHtmlString(fallback, out c);
            return c;
        }

        GameObject Box(string name, Vector3 center, Vector3 size, Material m, Transform parent = null, bool collider = true, Quaternion? rotation = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent ?? Root, false);
            go.transform.position = center;
            go.transform.localScale = size;
            if (rotation.HasValue) go.transform.rotation = rotation.Value;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            if (!collider) Remove(go.GetComponent<Collider>());
            go.isStatic = true;
            ObjectCount++;
            return go;
        }

        GameObject LocalBox(string name, Vector3 localCenter, Vector3 localSize, Material m, Transform parent, bool collider = true, Quaternion? localRotation = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            go.transform.localScale = localSize;
            if (localRotation.HasValue) go.transform.localRotation = localRotation.Value;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            if (!collider) Remove(go.GetComponent<Collider>());
            go.isStatic = true;
            ObjectCount++;
            return go;
        }

        static void Remove(Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        GameObject Cylinder(string name, Vector3 baseCenter, float radius, float height, Material m, Transform parent = null, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);   // unit cylinder: height 2, radius 0.5
            go.name = name;
            go.transform.SetParent(parent ?? Root, false);
            go.transform.position = baseCenter + Vector3.up * height * 0.5f;
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            if (!collider) Remove(go.GetComponent<Collider>());
            go.isStatic = true;
            ObjectCount++;
            return go;
        }

        GameObject PlaceGlb(string resourcePath, string name, Vector3 pos, Vector3 targetSize, Transform parent = null, float yaw = 0f, bool collider = true)
        {
            var go = Entities.CharacterRig.InstantiateGlb(resourcePath, name, pos, targetSize, parent ?? Root, yaw, collider);
            if (go != null) ObjectCount++;
            return go;
        }

        void Sign(string text, Vector3 position, string colour, float size = 1f, Transform parent = null, float offsetZ = 0.18f)
        {
            var go = new GameObject("Sign " + text);
            go.transform.SetParent(parent ?? Root, false);
            go.transform.position = position + new Vector3(0f, 0f, offsetZ);
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var tm = go.AddComponent<TextMesh>();
            tm.text = text; tm.font = font; tm.fontSize = 64; tm.characterSize = 0.07f * size; tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = Hex(colour, "#ffc52f");
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            var fl = go.AddComponent<Entities.FacingLabel>();
            fl.BoardSign = true; fl.BasePos = position; fl.OffsetZ = offsetZ;
            ObjectCount++;
        }

        // ---------- ground and water ----------

        void BuildGround()
        {
            var m = Data.meta;
            float w = m.maxX - m.minX + 400f, d = m.maxZ - m.minZ + 400f;
            Box("Ground", WorldData.W((m.minX + m.maxX) * 0.5f, -0.5f, (m.minZ + m.maxZ) * 0.5f), new Vector3(w, 1f, d), Mat(Earth));
        }

        void BuildWater()
        {
            var water = Mat("water", Water, 0.92f);
            foreach (var w in Data.waters)
                Box(w.name, WorldData.W((w.minX + w.maxX) * 0.5f, -0.175f, (w.minZ + w.maxZ) * 0.5f),
                    new Vector3(w.maxX - w.minX, 0.40f, w.maxZ - w.minZ), water, null, false);   // no collider: you swim, vehicles sink
        }

        // ---------- roads ----------

        // Intervals along `r` where a crossing road passes (plus a margin), so pavements and medians leave gaps.
        List<Vector2> CrossingGaps(RoadData r, float margin)
        {
            var gaps = new List<Vector2>();
            foreach (var o in Data.roads)
            {
                if (o.axis == r.axis) continue;
                if (r.k < o.from - r.width * 0.5f - 2f || r.k > o.to + r.width * 0.5f + 2f) continue; // the crossing road must reach us
                if (o.k < r.from - o.width * 0.5f - 2f || o.k > r.to + o.width * 0.5f + 2f) continue; // and we must reach it
                if (Data.OnBridge(r.axis, r.k, o.k) || Data.OnBridge(o.axis, o.k, r.k)) continue; // grade-separated overpass
                gaps.Add(new Vector2(o.k - o.width * 0.5f - margin, o.k + o.width * 0.5f + margin));
            }
            return gaps;
        }

        List<Vector2> ParallelCarriagewayGaps(RoadData r, float across)
        {
            var gaps = new List<Vector2>();
            foreach (var o in Data.roads)
            {
                if (o.axis != r.axis || Mathf.Approximately(o.k, r.k)) continue;
                if (Mathf.Abs(across - o.k) < o.width * 0.5f + 2.2f)
                    gaps.Add(new Vector2(o.from - 2f, o.to + 2f));
            }
            return gaps;
        }

        static List<Vector2> Split(float from, float to, List<Vector2> cuts)
        {
            var spans = new List<Vector2> { new Vector2(from, to) };
            foreach (var c in cuts)
            {
                var next = new List<Vector2>();
                foreach (var s in spans)
                {
                    if (c.y <= s.x || c.x >= s.y) { next.Add(s); continue; }
                    if (c.x > s.x) next.Add(new Vector2(s.x, c.x));
                    if (c.y < s.y) next.Add(new Vector2(c.y, s.y));
                }
                spans = next;
            }
            spans.RemoveAll(s => s.y - s.x < 0.5f);
            return spans;
        }

        void BuildRoads()
        {
            var roadsRoot = new GameObject("Roads").transform; roadsRoot.SetParent(Root, false);
            var pavement = Mat("pavement", Pavement);
            var concrete = Mat("concrete", Concrete);
            var pole = Mat("pole", Steel, 0.4f);
            var line = Mat("line", "#e8e2c8");
            foreach (var r in Data.roads)
            {
                bool h = r.Horizontal;
                var asphalt = Mat(r.cls == "expressway" || r.cls == "highway" ? "asphalt-hi" : "asphalt", r.cls == "expressway" || r.cls == "highway" ? AsphaltLight : Asphalt);
                var bridgeCuts = new List<Vector2>();
                foreach (var b in Data.bridges) if (b.axis == r.axis && Mathf.Approximately(b.k, r.k)) bridgeCuts.Add(new Vector2(b.From, b.To));
                float y = h ? 0.05f : 0.055f;   // tiny offset so crossing carriageways do not z-fight
                var group = new GameObject(r.name).transform; group.SetParent(roadsRoot, false);

                // Extend carriageway flush to the outer edge of any perpendicular road at r.from / r.to so L-corners & T-junctions have zero missing notches.
                float cFrom = r.from, cTo = r.to;
                foreach (var o in Data.roads)
                {
                    if (o.axis == r.axis) continue;
                    if (r.k < o.from - r.width * 0.5f - 2f || r.k > o.to + r.width * 0.5f + 2f) continue;
                    if (Data.OnBridge(r.axis, r.k, o.k) || Data.OnBridge(o.axis, o.k, r.k)) continue;
                    if (Mathf.Abs(r.from - o.k) <= o.width * 0.5f + 2f && !Data.OnBridge(r.axis, r.k, r.from))
                        cFrom = Mathf.Min(cFrom, o.k - o.width * 0.5f);
                    if (Mathf.Abs(r.to - o.k) <= o.width * 0.5f + 2f && !Data.OnBridge(r.axis, r.k, r.to))
                        cTo = Mathf.Max(cTo, o.k + o.width * 0.5f);
                }

                foreach (var span in Split(cFrom, cTo, bridgeCuts))
                {
                    float len = span.y - span.x, mid = (span.x + span.y) * 0.5f;
                    Box("Carriageway", r.Point(mid, r.k, y), h ? new Vector3(len, 0.1f, r.width) : new Vector3(r.width, 0.1f, len), asphalt, group);
                }

                var gaps = CrossingGaps(r, 3.1f);
                var baseCuts = Merge(gaps, bridgeCuts);
                for (int s = -1; s <= 1; s += 2)
                {
                    float across = r.k + s * (r.width * 0.5f + 1.6f);
                    var paveCuts = Merge(baseCuts, ParallelCarriagewayGaps(r, across));
                    foreach (var span in Split(r.from, r.to, paveCuts))
                    {
                        float len = span.y - span.x, mid = (span.x + span.y) * 0.5f;
                        Box("Pavement", r.Point(mid, across, 0.15f), h ? new Vector3(len, 0.3f, 3.2f) : new Vector3(3.2f, 0.3f, len), pavement, group);
                    }
                }
                var medianCuts = Merge(CrossingGaps(r, 7.5f), bridgeCuts);
                foreach (var span in Split(r.from, r.to, medianCuts))
                {
                    float len = span.y - span.x, mid = (span.x + span.y) * 0.5f;
                    // Centre line (or median) between the carriageways.
                    if (r.median)
                        Box("Median", r.Point(mid, r.k, 0.3f), h ? new Vector3(len, 0.5f, 1.4f) : new Vector3(1.4f, 0.5f, len), concrete, group);
                    else
                        Box("Centre line", r.Point(mid, r.k, 0.106f), h ? new Vector3(len, 0.01f, 0.25f) : new Vector3(0.25f, 0.01f, len), line, group, false);
                }

                if (!r.lights) continue;
                var lightGaps = Merge(CrossingGaps(r, 6f), bridgeCuts);
                for (float along = r.from + 12f; along < r.to - 6f; along += 36f)
                {
                    if (Inside(along, lightGaps)) continue;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float across = r.k + s * (r.width * 0.5f + 2.4f);
                        var basePos = r.Point(along, across, 0.3f);
                        if (Data.InWater(basePos)) continue;
                        Cylinder("Lamp post", basePos, 0.12f, 7f, pole, group, false);
                        // The arm leans over the carriageway (towards the centreline).
                        var toCentre = (r.Point(along, r.k, 0.3f) - basePos).normalized;
                        var head = basePos + Vector3.up * 7.1f + toCentre * 1.1f;
                        Box("Lamp", head, h ? new Vector3(0.5f, 0.22f, 2.2f) : new Vector3(2.2f, 0.22f, 0.5f), LampHead, group, false);
                    }
                }
            }
        }

        static List<Vector2> Merge(List<Vector2> a, List<Vector2> b) { var l = new List<Vector2>(a); l.AddRange(b); return l; }
        static bool Inside(float v, List<Vector2> gaps) { foreach (var g in gaps) if (v > g.x && v < g.y) return true; return false; }

        // ---------- bridges ----------

        void BuildBridges()
        {
            var concrete = Mat("deck", "#a7a39a", 0.1f);
            var barrier = Mat("barrier", Concrete);
            var pillar = Mat("pillar", "#8f8b82");
            foreach (var b in Data.bridges)
            {
                bool h = b.axis == "h";
                var road = Data.FindRoad(b.axis, b.k);
                float deckW = Mathf.Max(b.halfW * 2f, road?.width ?? 0f);
                var group = new GameObject(b.name).transform; group.SetParent(Root, false);
                var p = b.profile;
                for (int i = 1; i < p.Length; i++)
                {
                    float dA = p[i].along - p[i - 1].along, dH = p[i].height - p[i - 1].height;
                    float len = Mathf.Sqrt(dA * dA + dH * dH) + 0.6f;
                    float midA = (p[i].along + p[i - 1].along) * 0.5f, midH = (p[i].height + p[i - 1].height) * 0.5f;
                    float tilt = Mathf.Atan2(dH, dA) * Mathf.Rad2Deg;
                    // World +z is data -z, so a deck rising with data z falls with world z: positive x-rotation.
                    var rot = h ? Quaternion.Euler(0, 0, tilt) : Quaternion.Euler(tilt, 0, 0);
                    var center = h ? WorldData.W(midA, midH - 0.5f, b.k) : WorldData.W(b.k, midH - 0.5f, midA);
                    Box("Deck", center, h ? new Vector3(len, 1f, deckW) : new Vector3(deckW, 1f, len), concrete, group, true, rot);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float across = b.k + s * (deckW * 0.5f - 0.3f);
                        var c = h ? WorldData.W(midA, midH + 0.5f, across) : WorldData.W(across, midH + 0.5f, midA);
                        Box("Barrier", c, h ? new Vector3(len, 1.0f, 0.5f) : new Vector3(0.5f, 1.0f, len), barrier, group, true, rot);
                    }
                }
                for (float along = b.From + 11f; along < b.To - 6f; along += 22f)
                {
                    float height = h ? Data.DeckHeightAtData(along, b.k) : Data.DeckHeightAtData(b.k, along);
                    if (height < 2f) continue;
                    bool hitsCrossRoad = false;
                    foreach (var cr in Data.roads)
                    {
                        if (cr.axis == b.axis) continue;
                        if (b.k < cr.from - 2f || b.k > cr.to + 2f) continue;
                        if (Mathf.Abs(along - cr.k) < cr.width * 0.5f + 2.5f) { hitsCrossRoad = true; break; }
                    }
                    if (hitsCrossRoad) continue;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float across = b.k + s * deckW * 0.28f;
                        var basePos = h ? WorldData.W(along, -3f, across) : WorldData.W(across, -3f, along);
                        Cylinder("Pillar", basePos, 1.1f, height + 2.2f, pillar, group);
                    }
                }
            }
        }

        // ---------- places ----------

        static Vector2 Footprint(LandmarkData l)
        {
            if (l.kind == "artisan") return new Vector2(16, 13);
            if (l.big) return new Vector2(26, 20);
            if (l.kind == "hotel" || l.kind == "bank") return new Vector2(20, 16);
            return new Vector2(18, 14);
        }

        void BuildLandmarks()
        {
            var group = new GameObject("Landmarks").transform; group.SetParent(Root, false);
            var grey = Mat("stadium", "#8a8f93", 0.3f);
            var booth = Mat("booth", "#e4d14b");
            foreach (var l in Data.landmarks)
            {
                var pos = l.World();
                var body = Mat("lm-" + l.color, l.color, l.kind == "bank" || l.kind == "hotel" ? 0.45f : 0.15f);
                if (l.stadium)
                {
                    if (!BuildStadiumModel(l, pos, group))
                    {
                        Cylinder(l.name, pos, 34f, 14f, grey, group);
                        Cylinder(l.name + " bowl", pos + Vector3.up * 14f, 26f, 1.5f, Mat("pitch", Pitch), group);
                    }
                    Sign(l.shortName, pos + new Vector3(0, 18f, 36f), l.sign, 2.2f, group);
                    continue;
                }
                switch (l.kind)
                {
                    case "toll":
                        // BuildTolls() builds the full overhead gantry and booths for toll gates.
                        continue;
                    case "checkpoint": case "post":
                        Box(l.name, pos + Vector3.up * 1.6f, new Vector3(5f, 3.2f, 3.5f), Mat("lm-" + l.color, l.color), group);
                        Sign(l.shortName, pos + new Vector3(0, 4.2f, 0), l.sign, 0.8f, group);
                        continue;
                    case "pitch":
                    {
                        Box(l.name, pos + Vector3.up * 0.12f, new Vector3(44f, 0.24f, 30f), Mat("pitch", Pitch), group);
                        var white = Mat("pitchline", "#f5f5f0");
                        var postMat = Mat("goalpost", "#eeeeee", 0.5f);
                        Box("Center line", pos + Vector3.up * 0.25f, new Vector3(0.25f, 0.02f, 28f), white, group, false);
                        for (int side = -1; side <= 1; side += 2)
                        {
                            float gx = pos.x + side * 20.5f;
                            Cylinder("Goal post L", new Vector3(gx, pos.y + 0.2f, pos.z - 2.8f), 0.08f, 2.4f, postMat, group, false);
                            Cylinder("Goal post R", new Vector3(gx, pos.y + 0.2f, pos.z + 2.8f), 0.08f, 2.4f, postMat, group, false);
                            Box("Crossbar", new Vector3(gx, pos.y + 2.6f, pos.z), new Vector3(0.14f, 0.14f, 5.76f), postMat, group, false);
                        }
                        for (int sx = -1; sx <= 1; sx += 2)
                            for (int sz = -1; sz <= 1; sz += 2)
                            {
                                var fPos = pos + new Vector3(sx * 23f, 0.2f, sz * 16f);
                                Cylinder("Floodlight pole", fPos, 0.15f, 10f, Mat("pole", Steel, 0.4f), group);
                                Box("Floodlight head", fPos + Vector3.up * 10.2f, new Vector3(1.2f, 0.4f, 0.6f), LampHead, group, false);
                            }
                        Sign(l.shortName, pos + new Vector3(0, 3.5f, 16f), l.sign, 1.2f, group);
                        continue;
                    }
                    case "beach":
                        Box(l.name, pos + Vector3.up * 0.1f, new Vector3(60f, 0.2f, 40f), Mat("sand", Sand), group);
                        Sign(l.shortName, pos + new Vector3(0, 3.5f, 22f), l.sign, 1.2f, group);
                        continue;
                    case "airport":
                    {
                        var apron = Mat("apron", AsphaltLight, 0.25f);
                        var conc = Mat("concrete", Concrete, 0.25f);
                        var glassMat = Mat("glass-air", Glass, 0.9f);
                        var whiteMat = Mat("plane-white", "#f2f5f8", 0.78f);
                        var greenMat = Mat("plane-green", "#0c7b46", 0.65f);
                        var navyMat = Mat("plane-navy", "#153b78", 0.65f);
                        var darkMat = Mat("plane-dark", "#1c2026", 0.35f);

                        // Compact concrete apron & taxiway sized to fit city blocks without overlapping adjacent streets
                        Box(l.name + " apron", pos + Vector3.up * 0.12f, new Vector3(62f, 0.24f, 26f), apron, group);
                        // Main International Terminal (MMIA) + Domestic Wing (MMA2)
                        Box(l.name + " terminal", pos + new Vector3(0f, 5.5f, 6.5f), new Vector3(38f, 11f, 10f), body, group);
                        Box(l.name + " upper concourse", pos + new Vector3(0f, 12.2f, 6.5f), new Vector3(28f, 2.8f, 8f), conc, group);
                        Box(l.name + " glass", pos + new Vector3(0f, 5.2f, 1.35f), new Vector3(34f, 7.6f, 0.3f), glassMat, group, false);
                        Box(l.name + " landside canopy", pos + new Vector3(0f, 4.2f, 12.2f), new Vector3(30f, 0.5f, 3.2f), conc, group, false);
                        // 2 telescoping passenger jet bridges (aerobridges)
                        foreach (float jx in new[] { -11f, 11f })
                        {
                            Box("Jetbridge", pos + new Vector3(jx, 4.2f, -1.8f), new Vector3(2.2f, 2.4f, 6.5f), conc, group);
                            Box("Jetbridge glass", pos + new Vector3(jx, 4.4f, -1.8f), new Vector3(2.35f, 1.0f, 5.5f), glassMat, group, false);
                        }
                        // Control Tower with observation cab & radar dome
                        Cylinder(l.name + " tower", pos + new Vector3(23f, 0.2f, 6.5f), 2.2f, 24f, conc, group);
                        Box(l.name + " cab", pos + new Vector3(23f, 25.4f, 6.5f), new Vector3(6.4f, 3.2f, 6.4f), glassMat, group, false);
                        Box(l.name + " tower roof", pos + new Vector3(23f, 27.3f, 6.5f), new Vector3(7.0f, 0.6f, 7.0f), conc, group, false);
                        Cylinder(l.name + " radar", pos + new Vector3(23f, 27.6f, 6.5f), 0.9f, 1.8f, whiteMat, group, false);
                        // Airport Hangar & Fuel Farm tanks on the west apron
                        Box(l.name + " hangar", pos + new Vector3(-24f, 4.2f, 6.0f), new Vector3(11f, 8.4f, 10f), Mat("hangar", "#525e6a", 0.4f), group);
                        Cylinder("Fuel tank 1", pos + new Vector3(-26f, 0.2f, -3.5f), 2.4f, 5.0f, whiteMat, group);
                        Cylinder("Fuel tank 2", pos + new Vector3(-20f, 0.2f, -3.5f), 2.4f, 5.0f, whiteMat, group);

                        // Runway strip with threshold piano keys & centreline dashes
                        var rwPos = pos + new Vector3(0f, 0.17f, -8.5f);
                        Box(l.name + " runway", rwPos, new Vector3(60f, 0.12f, 8.5f), Mat("asphalt", Asphalt), group);
                        for (float rx = -24f; rx <= 24f; rx += 8f)
                            Box("Runway line", rwPos + new Vector3(rx, 0.08f, 0f), new Vector3(4.0f, 0.02f, 0.3f), whiteMat, group, false);

                        // 2 realistic parked aircraft at gates
                        BuildAircraft(pos + new Vector3(-8f, 1.8f, -5.2f), 90f, 0.72f, greenMat, whiteMat, darkMat, glassMat, group);
                        BuildAircraft(pos + new Vector3(12f, 1.7f, -5.2f), 90f, 0.65f, navyMat, whiteMat, darkMat, glassMat, group);

                        Sign("MURTALA MUHAMMED INT'L AIRPORT", pos + new Vector3(0, 14.5f, 11.8f), l.sign, 1.2f, group);
                        continue;
                    }
                    case "banana":
                    {
                        // Banana Island waterfront ultra-luxury estate: security archway, tiered white & glass villas, infinity pool
                        var whiteVilla = Mat("villa-white", "#f5f3ee", 0.6f);
                        var glassVilla = Mat("villa-glass", "#5ca0c8", 0.92f);
                        var teak = Mat("villa-teak", "#8a5a36", 0.4f);
                        var poolMat = Mat("villa-pool", "#1ec8e6", 0.95f);
                        var goldMat = Mat("villa-gold", "#e6b830", 0.75f);

                        Compound(pos, 18f, 16f, group);
                        // Gated security archway
                        Box("Banana Arch L", pos + new Vector3(-3.2f, 2.4f, 8.0f), new Vector3(0.9f, 4.8f, 0.9f), whiteVilla, group);
                        Box("Banana Arch R", pos + new Vector3(3.2f, 2.4f, 8.0f), new Vector3(0.9f, 4.8f, 0.9f), whiteVilla, group);
                        Box("Banana Arch Top", pos + new Vector3(0f, 5.0f, 8.0f), new Vector3(7.8f, 0.9f, 1.2f), goldMat, group);

                        // Main contemporary mansion + cantilevered glass upper deck
                        Box(l.name + " ground", pos + new Vector3(-1.8f, 2.4f, -0.8f), new Vector3(11.5f, 4.8f, 9.2f), whiteVilla, group);
                        Box(l.name + " second", pos + new Vector3(-1.2f, 6.8f, -0.8f), new Vector3(10.0f, 4.2f, 8.0f), whiteVilla, group);
                        Box(l.name + " penthouse", pos + new Vector3(-0.6f, 10.6f, -0.8f), new Vector3(7.8f, 3.4f, 6.4f), glassVilla, group);
                        Box(l.name + " terrace", pos + new Vector3(-1.2f, 4.9f, 3.6f), new Vector3(10.4f, 0.35f, 2.2f), teak, group, false);
                        Box(l.name + " glass front", pos + new Vector3(-1.2f, 6.8f, 3.3f), new Vector3(9.0f, 3.4f, 0.25f), glassVilla, group, false);

                        // Turquoise infinity swimming pool & deck
                        Box("Infinity pool surround", pos + new Vector3(5.8f, 0.22f, 1.2f), new Vector3(4.6f, 0.44f, 8.2f), teak, group);
                        Box("Infinity pool water", pos + new Vector3(5.8f, 0.46f, 1.2f), new Vector3(3.6f, 0.08f, 7.0f), poolMat, group, false);

                        Sign("BANANA ISLAND · IKOYI", pos + new Vector3(0f, 6.3f, 8.2f), "#ffd700", 1.0f, group);
                        continue;
                    }
                    case "rail":
                    {
                        BuildRailStation(l, pos, group);
                        continue;
                    }
                    case "army":
                    case "navy":
                    {
                        BuildBarracks(l, pos, group, l.kind == "navy");
                        continue;
                    }
                    case "cbn":
                    {
                        BuildCbnTower(l, pos, group);
                        continue;
                    }
                    case "efcc":
                    {
                        BuildEfccCommand(l, pos, group);
                        continue;
                    }
                    case "settlement": case "waterfront":
                    {
                        Box(l.name, pos + Vector3.up * 0.2f, new Vector3(50f, 0.4f, 40f), body, group);
                        var wood = Mat("wood-stilt", "#5c4028");
                        var zinc = Mat("zinc", Zinc, 0.5f);
                        for (int i = -2; i <= 2; i++)
                        {
                            var hutPos = pos + new Vector3(i * 9f, 1.6f, -8f + (i % 2) * 5f);
                            Box("Stilt hut", hutPos + Vector3.up * 1.2f, new Vector3(5.5f, 2.6f, 4.5f), wood, group);
                            Box("Hut roof", hutPos + Vector3.up * 2.65f, new Vector3(6.1f, 0.22f, 5.1f), zinc, group, false);
                            var canoePos = pos + new Vector3(i * 8f + 3f, 0.25f, -16f);
                            BuildFishingBoat(canoePos, 0f, wood, zinc, group);
                        }
                        Sign(l.shortName, pos + new Vector3(0, 4f, 22f), l.sign, 1.4f, group);
                        continue;
                    }
                    case "waypoint":
                        if (l.id.Contains("jetty") || l.id.Contains("canoe"))
                        {
                            BuildFishingJetty(l, pos, group);
                            continue;
                        }
                        Box(l.name, pos + Vector3.up * 0.2f, new Vector3(50f, 0.4f, 40f), body, group);
                        Sign(l.shortName, pos + new Vector3(0, 4f, 22f), l.sign, 1.4f, group);
                        continue;
                    case "street":
                    {
                        // A street-name sign on the pavement: a pole and a green board readable from both sides.
                        Box(l.name + " pole", pos + Vector3.up * 1.6f, new Vector3(0.12f, 3.2f, 0.12f), Mat("signpole", "#5a5f63"), group, false);
                        float bw = 1.2f + l.shortName.Length * 0.32f;
                        Box(l.name + " board", pos + Vector3.up * 3.35f, new Vector3(bw, 0.55f, 0.06f), body, group, false);
                        Sign(l.shortName, pos + new Vector3(0, 3.35f, 0f), l.sign, 0.42f, group, 0.06f);
                        continue;
                    }
                }
                var fp = Footprint(l);
                float h = Mathf.Max(3f, l.h);
                Box(l.name, pos + Vector3.up * h * 0.5f, new Vector3(fp.x, h, fp.y), body, group);
                Box(l.name + " parapet", pos + Vector3.up * (h + 0.3f), new Vector3(fp.x + 0.6f, 0.6f, fp.y + 0.6f), Mat("parapet", "#6b655a"), group, false);
                // Sign board on the street-facing side (north: data -z, world +z).
                var signPos = pos + new Vector3(0, h + 1.3f, fp.y * 0.5f + 0.2f);
                Box(l.name + " board", signPos, new Vector3(Mathf.Min(fp.x * 0.9f, 2.2f + l.shortName.Length * 1.05f), 1.8f, 0.25f), Mat("board", "#1b1b1f"), group, false);
                Sign(l.shortName, signPos, l.sign, 1f, group, 0.2f);
                if (l.kind == "market") BuildMarketStalls(pos, fp, group);
                if (l.id.Contains("fire"))
                {
                    var redDoor = Mat("fire-door", "#e53935", 0.55f);
                    var towerMat = Mat("fire-tower", "#8d2020", 0.3f);
                    for (int bay = -1; bay <= 1; bay++)
                        Box("Fire bay door", pos + new Vector3(bay * 5.2f, 1.8f, fp.y * 0.5f + 0.08f), new Vector3(3.8f, 3.4f, 0.14f), redDoor, group, false);
                    Box("Drill & hose tower", pos + new Vector3(-fp.x * 0.38f, h + 3.2f, -fp.y * 0.28f), new Vector3(3.2f, 6.4f, 3.2f), towerMat, group, false);
                }
            }
        }

        // Wet asphalt reflects the lamps and the sky: raise the roads' smoothness while it rains.
        public void SetWet(bool wet) { foreach (var key in new[] { "asphalt", "asphalt-hi" }) if (materials.TryGetValue(key, out var m)) m.SetFloat("_Smoothness", wet ? 0.82f : 0.15f); }

        public int StadiumModels { get; private set; }
        public int Trees { get; private set; }

        // The real stadium models: fitted to the stadium footprint, feet on the ground.
        bool BuildStadiumModel(LandmarkData l, Vector3 pos, Transform group)
        {
            if (GameBootstrap.Mobile) return false;   // 182k-triangle model: too heavy for a phone, the procedural stand is used
            bool teslim = (l.id + l.name).ToLower().Contains("teslim");
            var prefab = Resources.Load<GameObject>("Models/Landmarks/" + (teslim ? "teslim_balogun_stadium" : "national_stadium"));
            if (prefab == null) return false;
            Vector3 target = teslim ? new Vector3(50f, 23f, 46f) : new Vector3(54f, 25f, 50f);
            var go = Instantiate(prefab, group); go.name = l.name + " model";
            go.transform.position = pos; go.transform.rotation = prefab.transform.localRotation; go.transform.localScale = Vector3.one;
            var b = Entities.CharacterRig.Bounds(go);
            if (b.size.x <= 0f || b.size.y <= 0f || b.size.z <= 0f) { Remove(go); return false; }
            if (b.size.y > b.size.z || b.size.y > b.size.x)
            {
                go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f) * go.transform.rotation;
                b = Entities.CharacterRig.Bounds(go);
            }
            float fit = Mathf.Min(target.x / b.size.x, target.z / b.size.z, target.y / b.size.y);
            go.transform.localScale = Vector3.one * fit;
            b = Entities.CharacterRig.Bounds(go);
            go.transform.position += new Vector3(pos.x - b.center.x, pos.y - b.min.y, pos.z - b.center.z);
            b = Entities.CharacterRig.Bounds(go);
            foreach (var c in go.GetComponentsInChildren<Collider>()) Remove(c);
            var col = go.AddComponent<BoxCollider>(); col.center = go.transform.InverseTransformPoint(b.center); col.size = new Vector3(b.size.x / fit, b.size.y / fit, b.size.z / fit);
            StadiumModels++; ObjectCount++;
            return true;
        }

        // Trees: tropical and pine models in the Surulere plot interiors
        // and along the Eko Atlantic shore, height-fitted with a little jitter, kept off the road shoulders.
        void BuildVegetation()
        {
            var group = new GameObject("Vegetation").transform; group.SetParent(Root, false);
            var plan = GameBootstrap.Mobile
                ? new (string model, int count, float height)[] { ("jabami_anime_tree-grass_v1", 10, 6f), ("tree_spruce_pine", 4, 7f) }
                : new (string model, int count, float height)[] { ("jabami_anime_tree-grass_v1", 22, 6f), ("tree_spruce_pine", 9, 7f) };
            var zones = new (float x0, float x1, float z0, float z1)[] { (-120, -22, -120, -22), (22, 120, -120, -22), (-120, -22, 22, 120), (22, 120, 22, 120), (346, 614, 315, 330) };
            var rng = new System.Random(42);
            foreach (var (model, count, height) in plan)
            {
                var prefab = Resources.Load<GameObject>("Models/Vegetation/" + model);
                if (prefab == null) continue;
                for (int i = 0; i < count; i++)
                {
                    var z = zones[rng.Next(zones.Length)];
                    bool placed = false;
                    for (int attempt = 0; attempt < 48 && !placed; attempt++)
                    {
                        float dx = Mathf.Lerp(z.x0 + 2f, z.x1 - 2f, (float)rng.NextDouble()), dz = Mathf.Lerp(z.z0 + 2f, z.z1 - 2f, (float)rng.NextDouble());
                        if (Data.InWaterData(dx, dz) || NearRoadData(dx, dz, 7f) || ReservedData(dx, dz)) continue;
                        var tree = Instantiate(prefab, group); tree.name = model;
                        var pos = WorldData.W(dx, 0f, dz); pos.y = Data.DeckHeightAtData(dx, dz);
                        tree.transform.position = pos; tree.transform.rotation = prefab.transform.localRotation; tree.transform.localScale = Vector3.one;
                        var b = Entities.CharacterRig.Bounds(tree);
                        if (b.size.z > b.size.y || b.size.x > b.size.y)
                        {
                            tree.transform.rotation = Quaternion.Euler(-90f, 0f, 0f) * tree.transform.rotation;
                            b = Entities.CharacterRig.Bounds(tree);
                        }
                        tree.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f) * tree.transform.rotation;
                        b = Entities.CharacterRig.Bounds(tree);
                        float jitter = 0.85f + (float)rng.NextDouble() * 0.3f, k = b.size.y > 0.01f ? height * jitter / b.size.y : 1f;
                        tree.transform.localScale = Vector3.one * k;
                        b = Entities.CharacterRig.Bounds(tree);
                        tree.transform.position += new Vector3(pos.x - b.center.x, pos.y - b.min.y, pos.z - b.center.z);
                        foreach (var c in tree.GetComponentsInChildren<Collider>()) Remove(c);
                        var trunk = tree.AddComponent<BoxCollider>(); trunk.center = tree.transform.InverseTransformPoint(new Vector3(pos.x, pos.y + height * 0.5f, pos.z)); trunk.size = new Vector3(0.6f / k, height / k, 0.6f / k);
                        Trees++; ObjectCount++; placed = true;
                    }
                }
            }
        }

        void BuildAircraft(Vector3 pos, float yawDeg, float scale, Material accentMat, Material whiteMat, Material darkMat, Material glassMat, Transform group)
        {
            var plane = new GameObject("Aircraft").transform;
            plane.SetParent(group, false);
            plane.position = pos;
            plane.rotation = Quaternion.Euler(0f, yawDeg, 0f);
            plane.localScale = Vector3.one * scale;

            // Cylindrical fuselage + nose cone + cockpit glass + windows + swept wings + twin under-wing turbofans + vertical tail fin + horizontal tailplane
            LocalBox("Fuselage", Vector3.zero, new Vector3(22f, 2.5f, 2.6f), whiteMat, plane);
            LocalBox("Livery stripe", new Vector3(0f, 0.25f, 0f), new Vector3(22.1f, 0.42f, 2.68f), accentMat, plane, false);
            LocalBox("Nose", new Vector3(11.8f, -0.15f, 0f), new Vector3(2.4f, 1.8f, 1.9f), whiteMat, plane, false);
            LocalBox("Cockpit glass", new Vector3(10.6f, 0.65f, 0f), new Vector3(1.8f, 0.75f, 2.1f), glassMat, plane, false);
            LocalBox("Wings", new Vector3(1.2f, -0.35f, 0f), new Vector3(3.8f, 0.24f, 21.5f), whiteMat, plane, false);
            LocalBox("Winglet L", new Vector3(0.4f, 0.45f, -10.7f), new Vector3(1.2f, 1.5f, 0.18f), accentMat, plane, false);
            LocalBox("Winglet R", new Vector3(0.4f, 0.45f, 10.7f), new Vector3(1.2f, 1.5f, 0.18f), accentMat, plane, false);
            LocalBox("Engine L", new Vector3(2.4f, -0.95f, -4.6f), new Vector3(2.8f, 1.15f, 1.15f), accentMat, plane, false);
            LocalBox("Engine R", new Vector3(2.4f, -0.95f, 4.6f), new Vector3(2.8f, 1.15f, 1.15f), accentMat, plane, false);
            LocalBox("Tail fin", new Vector3(-9.6f, 2.5f, 0f), new Vector3(3.2f, 3.8f, 0.28f), accentMat, plane, false);
            LocalBox("Tailplane", new Vector3(-9.8f, 0.6f, 0f), new Vector3(2.2f, 0.18f, 7.6f), whiteMat, plane, false);
            LocalBox("Landing gear N", new Vector3(9.2f, -1.6f, 0f), new Vector3(0.5f, 0.9f, 0.6f), darkMat, plane, false);
            LocalBox("Landing gear M", new Vector3(0.4f, -1.6f, 0f), new Vector3(0.7f, 0.9f, 3.6f), darkMat, plane, false);
        }

        void BuildYacht(Vector3 pos, Material hullMat, Material glassMat, Material deckMat, Transform group)
        {
            Box("Yacht hull", pos + Vector3.up * 0.45f, new Vector3(3.4f, 1.1f, 9.5f), hullMat, group, false);
            Box("Yacht deck", pos + Vector3.up * 1.05f, new Vector3(3.1f, 0.18f, 8.8f), deckMat, group, false);
            Box("Yacht cabin", pos + new Vector3(0f, 1.65f, -0.5f), new Vector3(2.6f, 1.1f, 4.8f), hullMat, group, false);
            Box("Yacht bridge glass", pos + new Vector3(0f, 1.75f, 1.95f), new Vector3(2.4f, 0.75f, 0.3f), glassMat, group, false);
            Box("Yacht flybridge", pos + new Vector3(0f, 2.4f, -0.8f), new Vector3(2.2f, 0.3f, 3.2f), hullMat, group, false);
        }

        void BuildRailStation(LandmarkData l, Vector3 pos, Transform group)
        {
            var brtMat = Mat("rail-blue", BrtBlue, 0.55f);
            var whiteMat = Mat("rail-white", "#f2f4f8", 0.45f);
            var conc = Mat("concrete", Concrete, 0.2f);
            var glassMat = Mat("rail-glass", Glass, 0.88f);
            var yellowLine = Mat("yellow", Yellow, 0.4f);

            bool elevated = l.id == "marina-rail" || l.id == "theatre-rail";
            float platY = elevated ? 7.6f : 0.8f;
            if (elevated)
            {
                for (float px = -12f; px <= 12f; px += 8f)
                    Cylinder("Station pier", pos + new Vector3(px, -1f, 0f), 0.95f, platY + 1f, conc, group);
            }
            Box(l.name + " concourse", pos + new Vector3(0f, 2.4f, 0f), new Vector3(24f, 4.8f, 12f), whiteMat, group);
            Box(l.name + " glass", pos + new Vector3(0f, 2.5f, 6.08f), new Vector3(20f, 3.2f, 0.2f), glassMat, group, false);
            Box(l.name + " platform", pos + new Vector3(0f, platY, 0f), new Vector3(28f, 0.6f, 11f), conc, group);
            Box(l.name + " safety line", pos + new Vector3(0f, platY + 0.32f, 4.6f), new Vector3(27f, 0.04f, 0.3f), yellowLine, group, false);
            Box(l.name + " canopy", pos + new Vector3(0f, platY + 3.8f, 0f), new Vector3(29f, 0.45f, 11.8f), brtMat, group, false);
            Box(l.name + " fascia", pos + new Vector3(0f, platY + 4.15f, 0f), new Vector3(29.2f, 0.35f, 12.0f), whiteMat, group, false);
            Sign(l.shortName, pos + new Vector3(0f, platY + 5.1f, 5.8f), "#ffffff", 1.1f, group);
        }

        void BuildBarracks(LandmarkData l, Vector3 pos, Transform group, bool isNavy)
        {
            var mainMat = Mat(isNavy ? "navy-base" : "army-base", isNavy ? "#13294b" : "#3b5323", 0.32f);
            var trimMat = Mat(isNavy ? "navy-trim" : "army-trim", isNavy ? "#e8eef5" : "#8a9a68", 0.3f);
            var roofMat = Mat("barracks-roof", isNavy ? "#1e3a5f" : "#2b3d1a", 0.45f);
            var wallMat = Mat("barracks-wall", isNavy ? "#d8e0eb" : "#556b38", 0.25f);
            var gateRed = Mat("barracks-gate-red", "#c62828", 0.5f);
            var greenFlag = Mat("ng-green", "#008751", 0.4f);

            // Parade ground apron
            Box(l.name + " parade ground", pos + new Vector3(0f, 0.08f, 0f), new Vector3(26f, 0.16f, 20f), Mat("parade-asphalt", "#2b2f34", 0.2f), group);

            // Main HQ & Quarterguard building + twin barracks blocks
            float h = Mathf.Max(5.5f, l.h);
            Box(l.name + " HQ", pos + new Vector3(0f, h * 0.5f, -3.5f), new Vector3(16f, h, 8.5f), mainMat, group);
            Box(l.name + " HQ roof", pos + new Vector3(0f, h + 0.35f, -3.5f), new Vector3(16.8f, 0.7f, 9.2f), roofMat, group, false);
            Box(l.name + " HQ veranda", pos + new Vector3(0f, h * 0.48f, 1.0f), new Vector3(15.6f, 0.3f, 1.6f), trimMat, group, false);

            Box(l.name + " East Block", pos + new Vector3(9.5f, 2.4f, 2.5f), new Vector3(5.2f, 4.8f, 11f), trimMat, group);
            Box(l.name + " East Roof", pos + new Vector3(9.5f, 5.0f, 2.5f), new Vector3(5.6f, 0.4f, 11.4f), roofMat, group, false);

            // Perimeter walls + Quarterguard Archway & Boom Gate on street side (+z)
            Box(l.name + " wall L", pos + new Vector3(-8.2f, 1.25f, 9.6f), new Vector3(9.2f, 2.5f, 0.55f), wallMat, group);
            Box(l.name + " wall R", pos + new Vector3(8.2f, 1.25f, 9.6f), new Vector3(9.2f, 2.5f, 0.55f), wallMat, group);
            Box(l.name + " gate pillar L", pos + new Vector3(-3.4f, 2.6f, 9.6f), new Vector3(1.1f, 5.2f, 1.1f), mainMat, group);
            Box(l.name + " gate pillar R", pos + new Vector3(3.4f, 2.6f, 9.6f), new Vector3(1.1f, 5.2f, 1.1f), mainMat, group);
            Box(l.name + " gate beam", pos + new Vector3(0f, 4.9f, 9.6f), new Vector3(8.4f, 1.0f, 1.2f), mainMat, group, false);
            Box(l.name + " boom barrier", pos + new Vector3(0f, 1.15f, 9.6f), new Vector3(6.0f, 0.14f, 0.14f), gateRed, group, false);

            // Corner Watchtower
            var wtPos = pos + new Vector3(-11.2f, 0f, 8.2f);
            Box("Watchtower legs", wtPos + Vector3.up * 3.2f, new Vector3(2.2f, 6.4f, 2.2f), mainMat, group);
            Box("Watchtower cab", wtPos + Vector3.up * 7.1f, new Vector3(2.8f, 1.6f, 2.8f), trimMat, group, false);
            Box("Watchtower roof", wtPos + Vector3.up * 8.1f, new Vector3(3.3f, 0.35f, 3.3f), roofMat, group, false);

            // Flagpole with Nigerian Green-White-Green / Naval flag
            var flagPos = pos + new Vector3(-4.8f, 0f, 4.2f);
            Cylinder("Flagpole", flagPos, 0.08f, 8.5f, trimMat, group);
            Box("Flag G1", flagPos + new Vector3(0.45f, 7.9f, 0f), new Vector3(0.32f, 0.65f, 0.05f), greenFlag, group, false);
            Box("Flag W", flagPos + new Vector3(0.77f, 7.9f, 0f), new Vector3(0.32f, 0.65f, 0.05f), trimMat, group, false);
            Box("Flag G2", flagPos + new Vector3(1.09f, 7.9f, 0f), new Vector3(0.32f, 0.65f, 0.05f), greenFlag, group, false);

            // Naval gunboat / cutter at Naval Bases
            if (isNavy)
            {
                var cutterPos = pos + new Vector3(0f, 0.4f, -13.5f);
                var hullGrey = Mat("navy-cutter-hull", "#5c6873", 0.45f);
                Box("Naval Cutter Hull", cutterPos, new Vector3(13.5f, 1.6f, 3.6f), hullGrey, group, false);
                Box("Naval Cutter Bridge", cutterPos + new Vector3(1.2f, 1.5f, 0f), new Vector3(4.5f, 1.6f, 2.8f), trimMat, group, false);
                Box("Naval Cutter Mast", cutterPos + new Vector3(0.8f, 3.1f, 0f), new Vector3(0.35f, 1.8f, 0.35f), hullGrey, group, false);
                Box("Naval Deck Gun", cutterPos + new Vector3(4.8f, 1.15f, 0f), new Vector3(1.6f, 0.7f, 1.1f), mainMat, group, false);
            }

            Sign(l.shortName, pos + new Vector3(0f, 5.8f, 10.25f), l.sign, 0.92f, group, 0.18f);
        }

        void BuildCbnTower(LandmarkData l, Vector3 pos, Transform group)
        {
            var cbnGreen = Mat("cbn-green", "#0f5132", 0.55f);
            var granite = Mat("cbn-granite", "#e2dfd7", 0.45f);
            var glassMat = Mat("cbn-glass", "#5ca0c8", 0.90f);
            var goldMat = Mat("cbn-gold", "#ffd700", 0.78f);
            var steelMat = Mat("cbn-steel", "#374151", 0.7f);

            // High-security perimeter compound & granite podium
            Compound(pos, 26f, 20f, group);
            Box(l.name + " podium", pos + new Vector3(0f, 2.2f, -1f), new Vector3(22f, 4.4f, 15f), granite, group);
            Box(l.name + " tower", pos + new Vector3(0f, 12f, -1.5f), new Vector3(14f, 24f, 10f), cbnGreen, group);
            Box(l.name + " glass curtain", pos + new Vector3(0f, 12.5f, 3.6f), new Vector3(11.6f, 18f, 0.25f), glassMat, group, false);
            Box(l.name + " crown", pos + new Vector3(0f, 24.4f, -1.5f), new Vector3(15.2f, 0.9f, 11.2f), goldMat, group, false);

            // Reinforced Bullion Vault Bay & Armored CBN Bullion Van in courtyard
            Box("CBN Vault Bay", pos + new Vector3(-7.5f, 2.0f, 5.2f), new Vector3(5.6f, 4.0f, 4.8f), steelMat, group);
            var vanPos = pos + new Vector3(5.5f, 0.95f, 6.2f);
            Box("CBN Bullion Van Body", vanPos, new Vector3(2.4f, 1.9f, 5.2f), steelMat, group);
            Box("CBN Bullion Stripe", vanPos + new Vector3(0f, 0.15f, 0f), new Vector3(2.46f, 0.32f, 5.24f), goldMat, group, false);

            Sign("CENTRAL BANK OF NIGERIA · CBN", pos + new Vector3(0f, 25.8f, 4.2f), "#ffd700", 0.95f, group, 0.2f);
            Sign(l.shortName, pos + new Vector3(0f, 5.2f, 9.8f), "#ffd700", 0.95f, group, 0.2f);
        }

        void BuildEfccCommand(LandmarkData l, Vector3 pos, Transform group)
        {
            var efccRed = Mat("efcc-red", "#7f1d1d", 0.45f);
            var stone = Mat("efcc-stone", "#e5e7eb", 0.35f);
            var darkGlass = Mat("efcc-glass", "#1e293b", 0.88f);
            var gateRed = Mat("barracks-gate-red", "#dc2626", 0.55f);

            Compound(pos, 26f, 20f, group);
            float h = Mathf.Max(10f, l.h);
            Box(l.name + " HQ", pos + new Vector3(0f, h * 0.5f, -1.5f), new Vector3(18f, h, 11f), stone, group);
            Box(l.name + " eagle fascia", pos + new Vector3(0f, h - 0.8f, -1.5f), new Vector3(18.6f, 1.6f, 11.6f), efccRed, group, false);
            Box(l.name + " glass", pos + new Vector3(0f, h * 0.5f, 4.1f), new Vector3(14f, h * 0.65f, 0.22f), darkGlass, group, false);
            Box(l.name + " gate arch", pos + new Vector3(0f, 4.4f, 9.8f), new Vector3(8.5f, 0.9f, 1.1f), efccRed, group, false);
            Box(l.name + " boom gate", pos + new Vector3(0f, 1.1f, 9.8f), new Vector3(6.2f, 0.15f, 0.15f), gateRed, group, false);

            // EFCC Tactical Raid Van in forecourt
            var raidVan = pos + new Vector3(5.8f, 0.9f, 5.8f);
            Box("EFCC Raid Van", raidVan, new Vector3(2.3f, 1.8f, 4.8f), darkGlass, group);
            Box("EFCC Red Stripe", raidVan + new Vector3(0f, 0.1f, 0f), new Vector3(2.36f, 0.35f, 4.84f), efccRed, group, false);

            Sign("EFCC · ANTI-GRAFT COMMAND", pos + new Vector3(0f, h + 1.4f, 4.4f), "#ffffff", 0.92f, group, 0.2f);
            Sign(l.shortName, pos + new Vector3(0f, 5.3f, 10.2f), "#ffffff", 0.92f, group, 0.2f);
        }

        void BuildFishingJetty(LandmarkData l, Vector3 pos, Transform group)
        {
            var wood = Mat("wood-stilt", "#5c4028");
            var darkWood = Mat("wood-dark", "#3e2a19");
            var brick = Mat("kiln-brick", "#8a4228");
            var zinc = Mat("zinc", Zinc, 0.5f);
            var iceMat = Mat("fish-cooler", "#2980b9", 0.6f);

            // Timber boardwalk & pier extending toward water
            Box(l.name + " apron", pos + new Vector3(0f, 0.25f, 0f), new Vector3(20f, 0.35f, 14f), wood, group);
            Box(l.name + " pier", pos + new Vector3(0f, 0.35f, -12f), new Vector3(4.2f, 0.3f, 14f), wood, group);
            for (int i = -2; i <= 2; i++)
            {
                Cylinder("Jetty piling L", pos + new Vector3(-1.9f, -0.8f, -6f + i * 3f), 0.16f, 2.6f, darkWood, group);
                Cylinder("Jetty piling R", pos + new Vector3(1.9f, -0.8f, -6f + i * 3f), 0.16f, 2.6f, darkWood, group);
            }

            // Fish-smoking shed & traditional brick kilns + ice coolers
            Box("Fish smoking shed", pos + new Vector3(-6.2f, 1.45f, 2.2f), new Vector3(5.8f, 2.5f, 4.4f), darkWood, group);
            Box("Shed roof", pos + new Vector3(-6.2f, 2.85f, 2.2f), new Vector3(6.4f, 0.2f, 5.0f), zinc, group, false);
            Box("Smoking kiln 1", pos + new Vector3(5.5f, 0.75f, 2.5f), new Vector3(2.2f, 1.1f, 2.2f), brick, group);
            Box("Smoking kiln 2", pos + new Vector3(8.2f, 0.75f, 2.5f), new Vector3(2.2f, 1.1f, 2.2f), brick, group);
            Box("Fish ice cooler", pos + new Vector3(2.4f, 0.65f, 3.2f), new Vector3(1.6f, 0.8f, 1.0f), iceMat, group, false);

            // Moored motorized wooden fishing boats alongside the jetty
            BuildFishingBoat(pos + new Vector3(-4.6f, 0.22f, -13f), 12f, wood, iceMat, group);
            BuildFishingBoat(pos + new Vector3(4.8f, 0.22f, -14f), -8f, wood, brick, group);
            Sign(l.shortName, pos + new Vector3(0f, 3.8f, 6.8f), l.sign, 1.05f, group);
        }

        void BuildFishingBoat(Vector3 pos, float yaw, Material woodMat, Material trimMat, Transform group)
        {
            var boat = new GameObject("Fishing Boat").transform;
            boat.SetParent(group, false);
            boat.position = pos;
            boat.rotation = Quaternion.Euler(0f, yaw, 0f);
            LocalBox("Boat hull", Vector3.zero, new Vector3(1.55f, 0.48f, 6.2f), woodMat, boat, false);
            LocalBox("Boat gunwale", new Vector3(0f, 0.26f, 0f), new Vector3(1.68f, 0.14f, 6.4f), trimMat, boat, false);
            LocalBox("Boat thwarts", new Vector3(0f, 0.22f, 0.4f), new Vector3(1.45f, 0.08f, 2.6f), woodMat, boat, false);
            LocalBox("Fish basket", new Vector3(0f, 0.38f, 0.9f), new Vector3(0.85f, 0.35f, 1.1f), trimMat, boat, false);
            LocalBox("Outboard motor", new Vector3(0f, 0.36f, -3.25f), new Vector3(0.36f, 0.65f, 0.45f), Mat("outboard", "#1e293b", 0.6f), boat, false);
        }

        // Lagos Rail Mass Transit (LRMT):
        // 1. Blue Line elevated concrete viaduct across the Lagos Lagoon (data z = -20, x = 135..368) from National Theatre / Costain to Marina.
        // 2. Red Line mainland rail corridor (data x = 184, z = -470..-20) through Oyingbo, Yaba, and Ebute Metta (Mobolaji Johnson Station).
        void BuildRailway()
        {
            var group = new GameObject("Lagos Railway").transform; group.SetParent(Root, false);
            var viaductMat = Mat("rail-viaduct", "#9e9b93", 0.2f);
            var ballastMat = Mat("rail-ballast", "#59554f", 0.1f);
            var steelRail = Mat("rail-steel", "#aeb6bf", 0.75f);
            var sleeperMat = Mat("rail-sleeper", "#4a3e35", 0.15f);
            var brtStripe = Mat("rail-blue", BrtBlue, 0.55f);

            // --- LRMT Blue Line Elevated Viaduct (East-West along data z = -20, x = 136..364) ---
            float blueZ = -20f, blueY = 7.4f, blueLen = 364f - 136f, blueMidX = (136f + 364f) * 0.5f;
            var blueCenter = WorldData.W(blueMidX, blueY, blueZ);
            Box("Blue Line Viaduct Deck", blueCenter, new Vector3(blueLen, 0.9f, 6.6f), viaductMat, group);
            Box("Blue Line Parapet N", blueCenter + new Vector3(0f, 0.75f, 3.1f), new Vector3(blueLen, 0.75f, 0.3f), brtStripe, group, false);
            Box("Blue Line Parapet S", blueCenter + new Vector3(0f, 0.75f, -3.1f), new Vector3(blueLen, 0.75f, 0.3f), brtStripe, group, false);
            Box("Blue Rail L1", blueCenter + new Vector3(0f, 0.52f, -1.3f), new Vector3(blueLen, 0.12f, 0.14f), steelRail, group, false);
            Box("Blue Rail L2", blueCenter + new Vector3(0f, 0.52f, -0.3f), new Vector3(blueLen, 0.12f, 0.14f), steelRail, group, false);
            Box("Blue Rail R1", blueCenter + new Vector3(0f, 0.52f, 0.3f), new Vector3(blueLen, 0.12f, 0.14f), steelRail, group, false);
            Box("Blue Rail R2", blueCenter + new Vector3(0f, 0.52f, 1.3f), new Vector3(blueLen, 0.12f, 0.14f), steelRail, group, false);
            for (float x = 144f; x <= 358f; x += 24f)
            {
                Cylinder("Blue Line Pier", WorldData.W(x, -3f, blueZ), 0.95f, blueY + 2.8f, viaductMat, group);
                Cylinder("Catenary Mast", WorldData.W(x, blueY + 0.4f, blueZ), 0.1f, 4.2f, steelRail, group, false);
            }

            // --- LRMT Red Line Mainland Corridor (North-South along data x = 184, z = -475..-22) ---
            float redX = 184f, redFrom = -475f, redTo = -22f;
            var roadCuts = new List<Vector2>();
            foreach (var r in Data.roads)
            {
                if (!r.Horizontal) continue;
                if (redX < r.from - 2f || redX > r.to + 2f) continue;
                roadCuts.Add(new Vector2(r.k - r.width * 0.5f - 1.5f, r.k + r.width * 0.5f + 1.5f));
            }
            foreach (var span in Split(redFrom, redTo, roadCuts))
            {
                float len = span.y - span.x, midZ = (span.x + span.y) * 0.5f;
                var segPos = WorldData.W(redX, 0.12f, midZ);
                Box("Red Line Ballast", segPos, new Vector3(5.6f, 0.22f, len), ballastMat, group, false);
                Box("Red Rail W1", segPos + new Vector3(-1.2f, 0.15f, 0f), new Vector3(0.14f, 0.10f, len), steelRail, group, false);
                Box("Red Rail W2", segPos + new Vector3(-0.3f, 0.15f, 0f), new Vector3(0.14f, 0.10f, len), steelRail, group, false);
                Box("Red Rail E1", segPos + new Vector3(0.3f, 0.15f, 0f), new Vector3(0.14f, 0.10f, len), steelRail, group, false);
                Box("Red Rail E2", segPos + new Vector3(1.2f, 0.15f, 0f), new Vector3(0.14f, 0.10f, len), steelRail, group, false);
                for (float z = span.x + 3f; z < span.y - 2f; z += 5f)
                    Box("Sleeper", WorldData.W(redX, 0.22f, z), new Vector3(4.4f, 0.06f, 0.35f), sleeperMat, group, false);
            }
        }

        void BuildMovingTrains()
        {
            var trainsRoot = new GameObject("Active Trains").transform;
            trainsRoot.SetParent(transform, false);
            blueLineTrain = BuildMetroTrain("LRMT Blue Line Train", BrtBlue, 4, trainsRoot);
            redLineTrain = BuildMetroTrain("LRMT Red Line Train", "#c62828", 4, trainsRoot);
            UpdateTrains(0f);
        }

        Transform BuildMetroTrain(string name, string accentHex, int cars, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            var white = Mat("train-white", "#f2f5f9", 0.72f);
            var accent = Mat("train-" + accentHex, accentHex, 0.68f);
            var glass = Mat("train-glass", "#182432", 0.92f);
            var dark = Mat("train-under", "#1f2328", 0.25f);
            var headLight = LampHead;

            float carLen = 11.5f, gap = 0.7f;
            float total = cars * carLen + (cars - 1) * gap;
            for (int i = 0; i < cars; i++)
            {
                float cz = -total * 0.5f + carLen * 0.5f + i * (carLen + gap);
                var car = new GameObject("Coach " + (i + 1)).transform;
                car.SetParent(t, false);
                car.localPosition = new Vector3(0f, 0f, cz);

                CreateDynamicBox("Body", car, new Vector3(0f, 1.65f, 0f), new Vector3(2.85f, 2.5f, carLen), white);
                CreateDynamicBox("LiveryBand", car, new Vector3(0f, 1.25f, 0f), new Vector3(2.91f, 0.55f, carLen + 0.02f), accent);
                CreateDynamicBox("Roof", car, new Vector3(0f, 2.98f, 0f), new Vector3(2.4f, 0.24f, carLen - 0.4f), dark);
                CreateDynamicBox("WindowStrip", car, new Vector3(0f, 1.95f, 0f), new Vector3(2.92f, 0.72f, carLen * 0.88f), glass);
                CreateDynamicBox("BogieF", car, new Vector3(0f, 0.28f, carLen * 0.32f), new Vector3(2.2f, 0.48f, 2.1f), dark);
                CreateDynamicBox("BogieR", car, new Vector3(0f, 0.28f, -carLen * 0.32f), new Vector3(2.2f, 0.48f, 2.1f), dark);
                if (i == 0 || i == cars - 1)
                {
                    float fz = (i == cars - 1 ? 1f : -1f) * (carLen * 0.5f + 0.04f);
                    CreateDynamicBox("CabWindshield", car, new Vector3(0f, 2.0f, fz), new Vector3(2.4f, 0.85f, 0.12f), glass);
                    CreateDynamicBox("HeadlampL", car, new Vector3(-0.85f, 1.05f, fz), new Vector3(0.35f, 0.22f, 0.12f), headLight);
                    CreateDynamicBox("HeadlampR", car, new Vector3(0.85f, 1.05f, fz), new Vector3(0.35f, 0.22f, 0.12f), headLight);
                }
            }
            return t;
        }

        static void CreateDynamicBox(string name, Transform parent, Vector3 localPos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            Remove(go.GetComponent<Collider>());
        }

        void Update()
        {
            UpdateTrains(Time.time);
        }

        void UpdateTrains(float t)
        {
            if (blueLineTrain != null)
            {
                float u = Mathf.PingPong(t * 0.045f, 1f);
                float x = Mathf.Lerp(158f, 342f, Mathf.SmoothStep(0f, 1f, u));
                blueLineTrain.position = WorldData.W(x, 7.95f, -20f);
                blueLineTrain.rotation = Quaternion.Euler(0f, 90f, 0f);
            }
            if (redLineTrain != null)
            {
                float u = Mathf.PingPong(t * 0.032f + 0.35f, 1f);
                float z = Mathf.Lerp(-445f, -48f, Mathf.SmoothStep(0f, 1f, u));
                redLineTrain.position = WorldData.W(184f, 0.28f, z);
                redLineTrain.rotation = Quaternion.identity;
            }
        }

        void BuildMarketStalls(Vector3 pos, Vector2 fp, Transform group)
        {
            var rng = new System.Random((int)(pos.x * 7 + pos.z * 13));
            string[] tarps = { "#d9534f", "#f0ad4e", "#5bc0de", "#5cb85c", "#f5c518" };
            for (int i = 0; i < 6; i++)
            {
                float x = pos.x + (float)(rng.NextDouble() - 0.5) * (fp.x + 10f), z = pos.z - (fp.y * 0.5f + 3f + (float)rng.NextDouble() * 4f);
                if (NearRoadData(x, -z, 5.5f) || Data.InWaterData(x, -z)) continue;
                var basePos = new Vector3(x, 0.15f, z);
                Box("Stall", basePos + Vector3.up * 0.5f, new Vector3(2.4f, 1f, 1.4f), Mat("stall", "#7a5a3a"), group);
                Box("Tarp", basePos + Vector3.up * 2.3f, new Vector3(3f, 0.1f, 2.2f), Mat("tarp" + i % tarps.Length, tarps[i % tarps.Length]), group, false);
            }
            var binPos = pos + new Vector3(fp.x * 0.5f + 2.2f, 0.15f, -fp.y * 0.5f);
            if (!NearRoadData(binPos.x, -binPos.z, 4.5f)) BuildLawmaBin(binPos, group, true);
        }

        static bool IsBrtStop(string id) =>
            id == "cms" || id == "costain" || id == "ojstop" || id == "jibowu" || id == "ebutebus" ||
            id == "lekkibus" || id == "vibus" || id == "oyingbo-bus" || id == "maryland-bus" ||
            id == "ojota-bus" || id == "ketu-bus" || id == "ikeja-bus" || id == "mile-12-bus";

        void BuildBusStops()
        {
            var group = new GameObject("Bus stops").transform; group.SetParent(Root, false);
            var yellow = Mat("yellow", Yellow, 0.3f);
            var brtBlue = Mat("brt-blue", BrtBlue, 0.55f);
            var brtRed = Mat("brt-red", "#d32f2f", 0.5f);
            var glassMat = Mat("brt-glass", Glass, 0.85f);
            var pole = Mat("pole", Steel, 0.4f);
            foreach (var b in Data.busStops)
            {
                var pos = b.World(0.3f);
                bool brt = IsBrtStop(b.id);
                Cylinder("Post", pos + new Vector3(-2.4f, 0, 0), 0.09f, 3.0f, pole, group);
                Cylinder("Post", pos + new Vector3(2.4f, 0, 0), 0.09f, 3.0f, pole, group);
                if (brt)
                {
                    Box("BRT Roof", pos + Vector3.up * 3.05f, new Vector3(6.2f, 0.24f, 2.7f), brtBlue, group, false);
                    Box("BRT Trim", pos + Vector3.up * 3.22f, new Vector3(6.25f, 0.10f, 2.74f), brtRed, group, false);
                    Box("BRT Glass Back", pos + new Vector3(0f, 1.55f, -1.05f), new Vector3(4.8f, 2.2f, 0.10f), glassMat, group, false);
                    Box("Bench", pos + Vector3.up * 0.5f, new Vector3(4.4f, 0.14f, 0.55f), brtBlue, group);
                    Sign("BRT · " + b.shortName, pos + new Vector3(0, 3.85f, 0), "#ffffff", 0.72f, group);
                }
                else
                {
                    Box("Roof", pos + Vector3.up * 2.9f, new Vector3(5.4f, 0.18f, 2.4f), yellow, group, false);
                    Box("Bench", pos + Vector3.up * 0.5f, new Vector3(4.2f, 0.12f, 0.5f), Mat("bench", "#4a3a2a"), group);
                    Sign(b.shortName, pos + new Vector3(0, 3.7f, 0), "#141414", 0.7f, group);
                }
                BuildLawmaBin(pos + new Vector3(3.4f, 0f, 0.2f), group, false);
            }
        }

        // Iconic vivid-orange LAWMA street wheelie bins and roll-on dumpsters ("KEEP LAGOS CLEAN").
        void BuildLawmaBin(Vector3 basePos, Transform group, bool skip)
        {
            var lawmaMat = Mat("lawma-orange", LawmaOrange, 0.45f);
            var lidMat = Mat("lawma-lid", "#222428", 0.35f);
            if (skip)
            {
                Box("LAWMA Skip", basePos + Vector3.up * 0.75f, new Vector3(2.4f, 1.4f, 1.5f), lawmaMat, group);
                Box("LAWMA Skip Lid", basePos + Vector3.up * 1.5f, new Vector3(2.5f, 0.14f, 1.6f), lidMat, group, false);
            }
            else
            {
                Box("LAWMA Bin", basePos + Vector3.up * 0.55f, new Vector3(0.65f, 1.05f, 0.65f), lawmaMat, group, false);
                Box("LAWMA Bin Lid", basePos + Vector3.up * 1.12f, new Vector3(0.72f, 0.10f, 0.72f), lidMat, group, false);
            }
        }

        void BuildProperties()
        {
            var group = new GameObject("Properties").transform; group.SetParent(Root, false);
            foreach (var p in Data.properties)
            {
                var pos = p.World();
                float h = Mathf.Max(3.2f, p.h);
                Compound(pos, 20f, 18f, group);
                Box(p.name, pos + Vector3.up * h * 0.5f, new Vector3(13f, h, 10f), Mat("pr-" + p.color, p.color), group);
                if (p.style == "bungalow") Box("Roof", pos + Vector3.up * (h + 0.2f), new Vector3(14f, 0.4f, 11f), Mat("zinc", Zinc, 0.5f), group, false);
                else Box("Parapet", pos + Vector3.up * (h + 0.25f), new Vector3(13.6f, 0.5f, 10.6f), Mat("parapet", "#6b655a"), group, false);
                if (!GameBootstrap.Mobile)
                {
                    Box("AC unit", pos + new Vector3(-4.2f, 2.4f, 5.18f), new Vector3(0.85f, 0.55f, 0.35f), Mat("ac-unit", "#d8dce0"), group, false);
                    Box("Front door", pos + new Vector3(0f, 1.15f, 5.08f), new Vector3(1.5f, 2.3f, 0.16f), Mat("door-wood", "#4a3222"), group, false);
                }
                if (!string.IsNullOrEmpty(p.sign)) Sign(p.sign, pos + new Vector3(0, 2.6f, 10.6f), "#ffffff", 0.6f, group);
            }
        }

        void Compound(Vector3 pos, float w, float d, Transform group)
        {
            var fence = Mat("fence", "#9b9484");
            Box("Fence", pos + new Vector3(0, 0.9f, -d * 0.5f), new Vector3(w, 1.8f, 0.3f), fence, group);
            Box("Fence", pos + new Vector3(0, 0.9f, d * 0.5f), new Vector3(w, 1.8f, 0.3f), fence, group);
            Box("Fence", pos + new Vector3(-w * 0.5f, 0.9f, 0), new Vector3(0.3f, 1.8f, d), fence, group);
            Box("Fence", pos + new Vector3(w * 0.5f, 0.9f, 0), new Vector3(0.3f, 1.8f, d), fence, group);
        }

        void BuildCells()
        {
            var group = new GameObject("Compound cells").transform; group.SetParent(Root, false);
            foreach (var c in Data.cells)
            {
                if (Data.InWaterData(c.x, c.z) || NearRoadData(c.x, c.z, 14f) || OverlapsPlot(c.x, c.z, 16f)) continue;
                placedPlots.Add(new Vector2(c.x, c.z));
                var rng = new System.Random((int)(c.x * 31 + c.z * 17));
                var pos = c.World();
                switch (c.style)
                {
                    case "island": Tower(pos, 16f, 16f, 24f + (float)rng.NextDouble() * 26f, group, rng); break;
                    case "vi": Tower(pos, 14f, 14f, 18f + (float)rng.NextDouble() * 24f, group, rng); break;
                    case "yaba": House(pos, 12f, 10f, 7f + (float)rng.Next(0, 2) * 3.5f, HouseColours[rng.Next(HouseColours.Length)], false, group); break;
                    case "lekki": Compound(pos, 22f, 20f, group); House(pos, 13f, 11f, 8f, Cream, false, group); break;
                    case "festac":
                        // Festac ’77 medium-rise residential apartment block with wrap-around balconies
                        Compound(pos, 24f, 20f, group);
                        House(pos, 16f, 12f, 12.5f + (float)rng.Next(0, 2) * 3.5f, "#d8cfb8", false, group);
                        break;
                    case "magodo":
                        // Magodo GRA gated luxury twin-duplex compound with security gatehouse
                        Compound(pos, 24f, 22f, group);
                        House(pos + new Vector3(-3.6f, 0f, 0f), 8.2f, 11f, 9.2f, "#f5f3ee", false, group);
                        House(pos + new Vector3(3.6f, 0f, 0f), 8.2f, 11f, 9.2f, "#e8e0d0", false, group);
                        Box("Gatehouse", pos + new Vector3(-8.5f, 1.4f, 9.2f), new Vector3(3.2f, 2.8f, 2.6f), Mat("gatehouse", "#c9c2b3"), group);
                        break;
                    case "ladipo":
                        // Ladipo / Alimosho dense auto-parts warehouse & double-decker trade stalls
                        Box("Trade Warehouse", pos + Vector3.up * 3.8f, new Vector3(16f, 7.6f, 12f), Mat("ladipo-wh", "#6c7a89"), group);
                        Box("Canopy", pos + new Vector3(0f, 3.2f, 7.2f), new Vector3(16.4f, 0.25f, 3.2f), Mat("zinc", Zinc, 0.5f), group, false);
                        break;
                    case "epe":
                        // Epe waterfront fishing & timber veranda compound
                        Compound(pos, 20f, 18f, group);
                        House(pos, 12f, 9.5f, 5.2f, "#d9c8a0", true, group);
                        Box("Fish Veranda", pos + new Vector3(0f, 1.5f, -6.2f), new Vector3(10f, 2.6f, 3.0f), Mat("wood-stilt", "#5c4028"), group);
                        break;
                    default: Compound(pos, 22f, 20f, group); House(pos, 11f, 9f, rng.NextDouble() < 0.5 ? 3.6f : 7f, HouseColours[rng.Next(HouseColours.Length)], true, group); break;
                }
            }
        }

        void Tower(Vector3 pos, float w, float d, float h, Transform group, System.Random rng)
        {
            var glass = rng.NextDouble() < 0.55 ? Mat("glass", Glass, 0.85f) : Mat("tower", "#cfd3d6", 0.5f);
            Box("Tower", pos + Vector3.up * h * 0.5f, new Vector3(w, h, d), glass, group);
            Box("Crown", pos + Vector3.up * (h + 0.6f), new Vector3(w * 0.6f, 1.2f, d * 0.6f), Mat("parapet", "#6b655a"), group, false);
        }

        void House(Vector3 pos, float w, float d, float h, string colour, bool zincRoof, Transform group)
        {
            Box("House", pos + Vector3.up * h * 0.5f, new Vector3(w, h, d), Mat("house-" + colour, colour), group);
            if (zincRoof && h < 5f) Box("Roof", pos + Vector3.up * (h + 0.15f), new Vector3(w + 1f, 0.3f, d + 1f), Mat("zinc", Zinc, 0.5f), group, false);
            else Box("Parapet", pos + Vector3.up * (h + 0.25f), new Vector3(w + 0.4f, 0.5f, d + 0.4f), Mat("parapet", "#6b655a"), group, false);
            if (GameBootstrap.Mobile && !Application.isEditor) return;
            // Water tank, AC unit, balcony, and generator: Lagos compound details.
            Cylinder("Tank", pos + new Vector3(w * 0.3f, h + (zincRoof && h < 5f ? 0.3f : 0.5f), d * 0.25f), 0.7f, 1.2f, Mat("tank", Black), group, false);
            Box("AC unit", pos + new Vector3(-w * 0.28f, Mathf.Min(h - 0.8f, 2.6f), d * 0.5f + 0.18f), new Vector3(0.85f, 0.55f, 0.35f), Mat("ac-unit", "#d8dce0"), group, false);
            Box("Generator", pos + new Vector3(w * 0.5f + 0.7f, 0.45f, -d * 0.25f), new Vector3(1.1f, 0.9f, 0.75f), Mat("gen-box", Yellow), group, false);
            if (h >= 6.5f) Box("Balcony", pos + new Vector3(0f, 3.6f, d * 0.5f + 0.6f), new Vector3(w * 0.55f, 0.25f, 1.2f), Mat("concrete", Concrete), group, false);
        }

        bool OverlapsPlot(float x, float z, float minSep = 15.5f)
        {
            float r2 = minSep * minSep;
            for (int i = 0; i < placedPlots.Count; i++)
            {
                float dx = placedPlots[i].x - x, dz = placedPlots[i].y - z;
                if (dx * dx + dz * dz < r2) return true;
            }
            return false;
        }

        void PlaceRegionalBuilding(float x, float z, Transform group, bool roadside)
        {
            var region = Data.RegionAtData(x, z);
            string id = region != null ? region.id : "mainland";
            string biome = region != null && !string.IsNullOrEmpty(region.biome) ? region.biome : "coastal";
            var rng = new System.Random(Mathf.RoundToInt(x * 73f + z * 151f));
            float jx = roadside ? 0f : ((float)rng.NextDouble() - 0.5f) * 3f;
            float jz = roadside ? 0f : ((float)rng.NextDouble() - 0.5f) * 3f;
            var pos = WorldData.W(x + jx, 0f, z + jz);
            bool banana = id.Contains("banana");
            bool dense = id.Contains("island") || id == "obalende" || id == "vi" || id.Contains("marina") || id == "abuja-cbd" || id == "three-arms" || id == "ibadan-dugbe";
            bool estate = id.Contains("lekki") || id.Contains("ajah") || id.Contains("ikoyi") || id.Contains("oniru") || id.Contains("eleko") || id.Contains("magodo") || id == "wuse-maitama" || id == "ph-gra" || id == "enugu-coal-city";
            bool industrial = id.Contains("apapa") || id.Contains("costain") || id == "ota-agbara" || id == "trans-amadi" || id == "bonny-onne" || id == "nnewi-industrial" || (z > 105f && z < 190f && x < 165f);

            if (banana)
            {
                Compound(pos, 19f, 17f, group);
                House(pos, 13f, 11f, 10.5f + (float)rng.NextDouble() * 4f, "#f5f3ee", false, group);
            }
            else if (biome == "sahel")
            {
                // Northern Sahelian Tubali ochre mud-brick architecture with parapet walls (Kano, Kaduna, Borno)
                string[] sahelCols = { "#c88a4b", "#d49b63", "#b57c4a", "#d9a66e" };
                Compound(pos, 18f, 16f, group);
                House(pos, 12f, 10f, 6.5f + rng.Next(0, 2) * 3.2f, sahelCols[rng.Next(sahelCols.Length)], false, group);
            }
            else if (biome == "southeast" && !estate && !industrial)
            {
                // South-East multi-storey commercial concrete plazas & red-earth compounds (Onitsha, Enugu, Aba)
                string[] seCols = { "#d7ccc8", "#bcaaa4", "#e0d6cc", "#c5b8a5" };
                House(pos, 13f, 11f, 10.5f + rng.Next(0, 3) * 3.5f, seCols[rng.Next(seCols.Length)], false, group);
            }
            else if (dense)
            {
                Tower(pos, 14f, 12f, 14f + (float)rng.NextDouble() * 28f, group, rng);
            }
            else if (estate)
            {
                Compound(pos, 19f, 17f, group);
                House(pos, 12f, 10f, 8f + (rng.NextDouble() < 0.4 ? 3.5f : 0f), Cream, false, group);
            }
            else if (industrial)
            {
                double roll = rng.NextDouble();
                if (roll < 0.55)
                {
                    // Apapa / Costain / Oworonshoki warehouse & logistics depot
                    float wh = 6.5f + (float)rng.NextDouble() * 5.5f;
                    Box("Warehouse", pos + Vector3.up * wh * 0.5f, new Vector3(15f, wh, 12f), Mat("wh-" + (roll < 0.28 ? "#6c7a89" : "#b8c4cc"), roll < 0.28 ? "#6c7a89" : "#b8c4cc"), group);
                    Box("Warehouse roof", pos + Vector3.up * (wh + 0.2f), new Vector3(15.6f, 0.35f, 12.6f), Mat("zinc", Zinc, 0.5f), group, false);
                }
                else
                {
                    Compound(pos, 19f, 17f, group);
                    House(pos, 12f, 10f, 7f + rng.Next(0, 3) * 3.5f, HouseColours[rng.Next(HouseColours.Length)], false, group);
                }
            }
            else
            {
                double roll = rng.NextDouble();
                if (!roadside) Compound(pos, 19f, 17f, group);
                if (roll < 0.28) House(pos, 11f, 9f, 4.2f, HouseColours[rng.Next(HouseColours.Length)], true, group);
                else if (roll < 0.72) House(pos, 12f, 10f, 7.5f + rng.Next(0, 3) * 3.5f, HouseColours[rng.Next(HouseColours.Length)], false, group);
                else if (roll < 0.90)
                {
                    // Multi-family Lagos residential apartment court with twin residential wings
                    string colA = HouseColours[rng.Next(HouseColours.Length)];
                    House(pos + new Vector3(-2.6f, 0f, 0f), 7.5f, 10.5f, 10.5f, colA, false, group);
                    House(pos + new Vector3(3.2f, 0f, 0f), 6.8f, 9.5f, 7.2f, HouseColours[rng.Next(HouseColours.Length)], false, group);
                }
                else House(pos, 13f, 11f, 16f + (float)rng.NextDouble() * 10f, "#c9c2b3", false, group);
            }
            placedPlots.Add(new Vector2(x, z));
        }

        // Fill every street frontage and city block across Lagos:
        // 1. First line both sides of every non-bridge road so streets never look bare.
        // 2. Then fill the blocks between roads from the central mainland/island outward.
        void BuildHousing()
        {
            var group = new GameObject("Housing").transform; group.SetParent(Root, false);
            int built = 0;
            float roadStep = GameBootstrap.Mobile ? 26f : 20f;

            foreach (var r in Data.roads)
            {
                bool h = r.Horizontal;
                for (float along = r.from + 16f; along < r.to - 16f && built < MaxHouses; along += roadStep)
                {
                    if (Data.OnBridge(r.axis, r.k, along)) continue;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float across = r.k + s * (r.width * 0.5f + 14.5f);
                        float dx = h ? along : across, dz = h ? across : along;
                        if (Data.InWaterData(dx, dz) || NearRoadData(dx, dz, 13.5f) || ReservedData(dx, dz) || OverlapsPlot(dx, dz, 15.5f)) continue;
                        PlaceRegionalBuilding(dx, dz, group, true);
                        built++;
                    }
                }
            }

            var m = Data.meta;
            float fillMinX = Mathf.Max(m.minX, -1050f), fillMaxX = Mathf.Min(m.maxX, 1750f);
            float fillMinZ = Mathf.Max(m.minZ, -1550f), fillMaxZ = Mathf.Min(m.maxZ, 410f);
            var candidates = new List<Vector2>();
            for (float z = fillMinZ + HouseGrid * 0.5f; z < fillMaxZ; z += HouseGrid)
                for (float x = fillMinX + HouseGrid * 0.5f; x < fillMaxX; x += HouseGrid)
                    candidates.Add(new Vector2(x, z));

            // Prioritize central Lagos (Surulere, Costain, Apapa, Island, Ikoyi, Banana Island, VI, Yaba, Ebute Metta, Lekki) first.
            candidates.Sort((a, b) =>
            {
                float da = (a.x - 160f) * (a.x - 160f) + a.y * a.y;
                float db = (b.x - 160f) * (b.x - 160f) + b.y * b.y;
                return da.CompareTo(db);
            });

            foreach (var pt in candidates)
            {
                if (built >= MaxHouses) break;
                float x = pt.x, z = pt.y;
                if (Data.InWaterData(x, z) || NearRoadData(x, z, 14.5f) || ReservedData(x, z) || OverlapsPlot(x, z, 16f)) continue;
                var rng = new System.Random(Mathf.RoundToInt(x * 73f + z * 151f));
                if (rng.NextDouble() < 0.08) continue;
                PlaceRegionalBuilding(x, z, group, false);
                built++;
            }
        }

        bool NearRoadData(float x, float z, float margin)
        {
            foreach (var r in Data.roads)
            {
                float along = r.Horizontal ? x : z, across = r.Horizontal ? z : x;
                if (along < r.from - margin || along > r.to + margin) continue;
                if (Mathf.Abs(across - r.k) < r.width * 0.5f + margin) return true;
            }
            return false;
        }

        bool ReservedData(float x, float z)
        {
            // Keep clear of the Red Line railway corridor (x = 184, z = -475..-20) and Blue Line viaduct (z = -20, x = 136..364)
            if (x > 174f && x < 194f && z > -480f && z < -15f) return true;
            if (z > -30f && z < -10f && x > 132f && x < 368f) return true;
            foreach (var l in Data.landmarks)
            {
                float r = l.stadium ? 40f : l.kind == "airport" ? 58f : l.kind == "pitch" ? 30f : l.waterfront || l.kind == "settlement" || l.kind == "beach" ? 52f : l.big ? 24f : l.kind == "street" ? 12f : 19f;
                if ((l.x - x) * (l.x - x) + (l.z - z) * (l.z - z) < r * r) return true;
            }
            foreach (var b in Data.busStops) if ((b.x - x) * (b.x - x) + (b.z - z) * (b.z - z) < 14f * 14f) return true;
            foreach (var p in Data.properties) if ((p.x - x) * (p.x - x) + (p.z - z) * (p.z - z) < 19f * 19f) return true;
            foreach (var c in Data.cells) if ((c.x - x) * (c.x - x) + (c.z - z) * (c.z - z) < 16f * 16f) return true;
            return false;
        }

        // ---------- road furniture ----------

        void BuildTolls()
        {
            var group = new GameObject("Tolls").transform; group.SetParent(Root, false);
            foreach (var t in Data.tolls)
            {
                var r = Data.FindRoad(t.axis, t.k); if (r == null) continue;
                bool h = r.Horizontal;
                float width = r.width;
                Box("Canopy", r.Point(t.at, r.k, 6f), h ? new Vector3(8f, 0.5f, width + 4f) : new Vector3(width + 4f, 0.5f, 8f), Mat("canopy", "#2f5d8a"), group, false);
                int booths = Mathf.Max(2, Mathf.FloorToInt(width / 9f));
                for (int i = 0; i < booths; i++)
                {
                    float across = r.k - width * 0.5f + width * (i + 0.5f) / booths;
                    Box("Booth", r.Point(t.at, across, 1.5f), h ? new Vector3(2.2f, 3f, 1.6f) : new Vector3(1.6f, 3f, 2.2f), Mat("booth", "#e4d14b"), group);
                    Box("Pillar", r.Point(t.at, across, 4.4f), new Vector3(0.6f, 2.9f, 0.6f), Mat("pillar", "#8f8b82"), group, false);
                }
                Sign(t.name.ToUpper(), r.Point(t.at, r.k, 7.4f), "#ffffff", 1.2f, group);
            }
        }

        void BuildFootbridges()
        {
            var group = new GameObject("Footbridges").transform; group.SetParent(Root, false);
            var steel = Mat("steel", Steel, 0.4f);
            foreach (var f in Data.footbridges)
            {
                var r = Data.FindRoad(f.axis, f.k); if (r == null) continue;
                bool h = r.Horizontal;
                float span = r.width + 10f;
                float at = f.at;
                var crossGaps = CrossingGaps(r, 14f);
                foreach (var g in crossGaps)
                {
                    if (at > g.x && at < g.y)
                    {
                        float mid = (g.x + g.y) * 0.5f;
                        at = at >= mid ? g.y + 4f : g.x - 4f;
                    }
                }
                Box("Span", r.Point(at, r.k, 6.2f), h ? new Vector3(3f, 0.4f, span) : new Vector3(span, 0.4f, 3f), steel, group);
                for (int s = -1; s <= 1; s += 2)
                {
                    float across = r.k + s * (span * 0.5f - 1f);
                    Box("Tower", r.Point(at, across, 3.1f), new Vector3(2.6f, 6.2f, 2.6f), steel, group);
                    Box("Rail", r.Point(at + s * 1.4f, r.k, 7.0f), h ? new Vector3(0.1f, 1.2f, span) : new Vector3(span, 1.2f, 0.1f), steel, group, false);
                }
            }
        }

        // The four businesses for sale: small shops with a painted sign.
        void BuildBusinesses()
        {
            var group = new GameObject("Businesses").transform; group.SetParent(Root, false);
            foreach (var b in Data.businesses)
            {
                var pos = b.World();
                string colour = b.id == "kiosk" ? "#2e7d32" : b.id == "phone" ? "#1565c0" : b.id == "buka" ? "#c0392b" : "#f39c12";
                Box(b.name, pos + Vector3.up * 2f, new Vector3(8f, 4f, 6f), Mat("biz-" + colour, colour), group);
                Box(b.name + " awning", pos + new Vector3(0, 3.3f, 3.6f), new Vector3(8.4f, 0.15f, 1.6f), Mat("awning", "#f1ecdf"), group, false);
                Sign(b.name.ToUpper(), pos + new Vector3(0, 4.9f, 3.2f), "#ffffff", 0.8f, group);
            }
        }

        void BuildStreetFurniture()
        {
            var group = new GameObject("Kiosks and vendors").transform; group.SetParent(Root, false);
            foreach (var k in Data.kiosks)
            {
                var pos = k.World(0.3f);
                Box("Kiosk", pos + Vector3.up * 1.3f, new Vector3(2.6f, 2.6f, 2.2f), Mat("kiosk", "#2e7d32"), group);
                Sign("POS", pos + new Vector3(0, 3.1f, 0), "#ffffff", 0.6f, group);
                BuildLawmaBin(pos + new Vector3(1.9f, 0f, 0f), group, false);
            }
            foreach (var v in Data.vendors)
            {
                var pos = v.World(0.3f);
                Box("Table", pos + Vector3.up * 0.5f, new Vector3(2f, 0.9f, 1.2f), Mat("stall", "#7a5a3a"), group);
                Box("Umbrella", pos + Vector3.up * 2.4f, new Vector3(2.6f, 0.12f, 2.6f), Mat("umbrella", "#d9534f"), group, false);
            }
            if (Data.TryPlace("lawma", out var lawmaDepot))
            {
                var dp = lawmaDepot.World(0.15f);
                BuildLawmaBin(dp + new Vector3(-7f, 0f, 9f), group, true);
                BuildLawmaBin(dp + new Vector3(7f, 0f, 9f), group, true);
            }
        }

        static readonly (string brand, string sub, string bgHex, string fgHex)[] LagosAds =
        {
            ("MTN · EVERYWHERE YOU GO",       "5G HUSTLE BUNDLE · DIAL *312#",    "#ffcc00", "#0b1f3a"),
            ("GLO · GRANDMASTERS OF DATA",    "RULE YOUR WORLD · UNLIMITED DATA", "#009a49", "#ffffff"),
            ("AIRTEL · SMARTPHONE NETWORK",   "BINGE STREAMING · NO BUFFER",      "#e40000", "#ffffff"),
            ("INDOMIE · TASTES GREAT!",       "NO BE SMALL THING · BELLE FULL",   "#d62828", "#ffdd00"),
            ("BET9JA · CATCH THE BOOM!",      "PLAY RESPONSIBLY · 18+ ONLY",      "#0d5c2f", "#f2bd18"),
            ("OPAY & MONIEPOINT",             "INSTANT TRANSFER · ZERO DOWNTIME", "#00a86b", "#ffffff"),
            ("CHICKEN REPUBLIC",              "SOULFULLY SPICED · REFUEL MAX",    "#c62828", "#ffd700"),
            ("PEAK MILK · IT'S IN YOU",       "NOURISHING LAGOS EVERY MORNING",   "#1559b8", "#ffffff"),
            ("TROPHY & GOLDBERG LAGER",       "HONOURABLE! · ENJOY COLD",         "#9a6b12", "#fff8dc"),
            ("RISEBANK · DIGITAL BANKING",    "SAVE, BORROW & GROW YOUR HUSTLE",  "#1e1b4b", "#7dd3fc"),
            ("EKO HOTEL & SUITES · VI",       "LUXURY BY THE ATLANTIC · WELCOME", "#3b1d5e", "#ffd700"),
            ("LASG · GREATER LAGOS RISING",   "RIDE THE BLUE & RED RAIL LINES",   "#0284c7", "#ffffff"),
            ("NEW AFRIKA SHRINE · IKEJA",     "AFROBEAT SUNDAY JUMP · LIVE",      "#b45309", "#fde047"),
            ("DANGOTE REFINERY & CEMENT",     "BUILDING A SELF-RELIANT NIGERIA",  "#1d3557", "#fb923c"),
        };

        void BuildBillboards()
        {
            var group = new GameObject("Billboards").transform;
            group.SetParent(Root, false);
            var steel = Mat("steel", Steel, 0.45f);
            var darkFrame = Mat("bb-frame", "#181a1f", 0.35f);
            var lasaaBlue = Mat("lasaa", "#0284c7", 0.5f);

            // 1. Giant LASAA Unipole Roadside Billboards across Lagos districts
            var unipoles = new (float x, float z, float yaw)[]
            {
                (14f,    18f,   0f),   // Adeniran Ogunsanya · Surulere spawn view
                (-14f,  -52f,   0f),   // Itire Road · Surulere
                (58f,   -54f,  15f),   // Ojuelegba Roundabout
                (136f,  -14f,   0f),   // Costain Interchange / Eko Bridge approach
                (352f,  -14f,   0f),   // CMS / Nnamdi Azikiwe Street
                (374f, -242f,  90f),   // Third Mainland Bridge / Yaba approach
                (426f,   88f,  90f),   // Falomo Bridge north approach (Ikoyi)
                (484f,  112f,   0f),   // Banana Island Causeway entrance
                (426f,  226f,   0f),   // Adeola Odeku · Victoria Island
                (486f,  226f,   0f),   // Lekki Toll Gate approach
                (574f,  226f,   0f),   // Admiralty Way · Lekki Phase 1
                (814f,  226f,   0f),   // Ajah Roundabout
                (1350f, 224f,   0f),   // Ibeju-Lekki · Refinery corridor
                (1650f, 224f,   0f),   // Epe Gateway
                (374f, -316f,   0f),   // Jibowu · Yaba
                (120f, -308f,   0f),   // Ikorodu Road · Ebute Metta
                (-160f,-308f,   0f),   // Ikorodu Garage
                (16f,  -516f,   0f),   // Maryland / Mobolaji Bank Anthony Way
                (80f,  -744f,   0f),   // Allen Avenue · Ikeja
                (42f,  -865f,   0f),   // Murtala Muhammed Airport Road
                (104f, -1104f,  0f),   // Alausa Secretariat
                (18f,  -1304f,  0f),   // Ojodu Berger Interstate Gateway
                (-120f, 124f,   0f),   // Apapa–Oworonshoki Expressway (Orile)
                (-336f, 124f,   0f),   // Apapa Port Road
                (-236f, 262f,   0f),   // Mile 2 · Badagry Expressway
                (-624f, 262f,   0f),   // LASU Gate · Ojo
            };

            for (int i = 0; i < unipoles.Length; i++)
            {
                var u = unipoles[i];
                if (Data.InWaterData(u.x, u.z)) continue;
                var ad = LagosAds[i % LagosAds.Length];
                var pos = WorldData.W(u.x, Data.DeckHeightAtData(u.x, u.z), u.z);
                var rot = Quaternion.Euler(0f, u.yaw, 0f);
                var adMat = Mat("bb-ad-" + ad.bgHex, ad.bgHex, 0.42f);

                Cylinder("Unipole Plinth", pos, 0.72f, 1.1f, Mat("concrete", Concrete, 0.2f), group);
                Cylinder("Unipole Column", pos + Vector3.up * 1.0f, 0.36f, 8.2f, steel, group);
                Box("Billboard Frame", pos + Vector3.up * 10.6f, new Vector3(10.8f, 4.2f, 0.46f), darkFrame, group, false, rot);
                Box("Billboard Face", pos + Vector3.up * 10.6f, new Vector3(10.2f, 3.7f, 0.52f), adMat, group, false, rot);
                Box("Billboard Catwalk", pos + Vector3.up * 8.35f, new Vector3(11.0f, 0.14f, 1.15f), steel, group, false, rot);
                Box("Billboard Floodlight", pos + Vector3.up * 12.85f, new Vector3(9.4f, 0.16f, 0.65f), LampHead, group, false, rot);
                Box("LASAA Plate", pos + Vector3.up * 8.15f, new Vector3(2.1f, 0.38f, 0.50f), lasaaBlue, group, false, rot);

                Sign(ad.brand, pos + Vector3.up * 11.15f, ad.fgHex, 0.86f, group, 0.32f);
                Sign(ad.sub, pos + Vector3.up * 9.85f, "#ffffff", 0.56f, group, 0.32f);
            }

            // 2. Overhead Expressway Gantry Billboards spanning major highways
            var gantries = new (string axis, float k, float at, int adIdx)[]
            {
                ("v", 72f,   -95f,  0),  // Funsho Williams Avenue (Surulere)
                ("h", 142f,  -40f,  3),  // Apapa–Oworonshoki Expressway
                ("h", -330f,  80f,  1),  // Ikorodu Road
                ("h", 240f,  680f, 10),  // Lekki–Epe Expressway (Ajah corridor)
                ("h", 240f, 1200f, 13),  // Lekki–Epe Expressway (Ibeju-Lekki Refinery corridor)
                ("h", -500f,  90f,  2),  // Mobolaji Bank Anthony Way (Maryland–Ikeja)
                ("v", 0f,  -1220f, 11),  // Lagos–Ibadan Expressway (Alausa–Berger)
                ("h", 280f, -480f,  5),  // Lagos–Badagry Expressway
            };

            foreach (var g in gantries)
            {
                var r = Data.FindRoad(g.axis, g.k);
                if (r == null) continue;
                bool h = r.Horizontal;
                float span = r.width + 6f;
                var center = r.Point(g.at, r.k, 0f);
                var ad = LagosAds[g.adIdx % LagosAds.Length];
                var adMat = Mat("bb-ad-" + ad.bgHex, ad.bgHex, 0.42f);

                for (int s = -1; s <= 1; s += 2)
                {
                    float across = r.k + s * (span * 0.5f);
                    Cylinder("Gantry Leg", r.Point(g.at, across, 0f), 0.34f, 8.6f, steel, group);
                }
                Box("Gantry Truss", r.Point(g.at, r.k, 8.2f), h ? new Vector3(0.7f, 0.7f, span + 1f) : new Vector3(span + 1f, 0.7f, 0.7f), steel, group, false);
                Box("Gantry Ad Board", r.Point(g.at, r.k, 9.4f), h ? new Vector3(0.55f, 2.9f, Mathf.Min(span - 4f, 16f)) : new Vector3(Mathf.Min(span - 4f, 16f), 2.9f, 0.55f), adMat, group, false);
                Box("Gantry Floodlight", r.Point(g.at, r.k, 11.0f), h ? new Vector3(0.75f, 0.16f, 12f) : new Vector3(12f, 0.16f, 0.75f), LampHead, group, false);
                Sign(ad.brand, center + Vector3.up * 9.8f, ad.fgHex, 0.85f, group, 0.35f);
                Sign(ad.sub, center + Vector3.up * 8.75f, "#ffffff", 0.54f, group, 0.35f);
            }
        }
    }
}
