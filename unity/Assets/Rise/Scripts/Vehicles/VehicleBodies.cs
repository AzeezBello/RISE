using System.Collections.Generic;
using UnityEngine;
using Rise.Data;

namespace Rise.Vehicles
{
    // Procedural vehicle bodies (danfo, keke, korope, sedan, okada, generic) sized from the exported vehicle classes.
    // +z is forward. Each body gets one BoxCollider on the root so the camera and traffic can see it.
    public static class VehicleBodies
    {
        static Shader standard;
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static readonly Dictionary<string, Material> tintedColormapMats = new Dictionary<string, Material>();
        static Material checkerMat;
        public static readonly string[] CarColours = { "#f29b0a", "#1c2430", "#9e1b1b", "#1d4d8c", "#d0d6dc", "#1f7a44", "#e06d06", "#374151", "#6d28d9", "#eab308", "#0f766e" };
        public static readonly string[] LuxuryColours = { "#0d1117", "#d4af37", "#991b1b", "#153668", "#f1f5f9", "#22c55e", "#f59e0b" };

        public static Material Mat(string hex, float gloss = 0.72f, float metallic = 0.24f)
        {
            string key = $"{hex}:{gloss:F2}:{metallic:F2}";
            if (materials.TryGetValue(key, out var m) && m != null) return m;
            standard ??= ShaderLib.Lit;
            m = new Material(standard);
            ColorUtility.TryParseHtmlString(hex, out var c);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", gloss);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            return materials[key] = m;
        }

        static Material CheckerMat()
        {
            if (checkerMat != null) return checkerMat;
            standard ??= ShaderLib.Lit;
            checkerMat = new Material(standard) { name = "TaxiChecker", color = Color.white };
            if (checkerMat.HasProperty("_BaseColor")) checkerMat.SetColor("_BaseColor", Color.white);
            checkerMat.mainTexture = ShaderLib.CheckerTex;
            if (checkerMat.HasProperty("_BaseMap")) { checkerMat.SetTexture("_BaseMap", ShaderLib.CheckerTex); checkerMat.SetTextureScale("_BaseMap", new Vector2(4f, 1f)); }
            checkerMat.SetFloat("_Smoothness", 0.84f);
            if (checkerMat.HasProperty("_Metallic")) checkerMat.SetFloat("_Metallic", 0.28f);
            return checkerMat;
        }

        static Transform Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material m, Quaternion? rot = null)
        {
            var p = GameObject.CreatePrimitive(type);
            p.transform.SetParent(parent, false); p.transform.localPosition = pos; p.transform.localScale = scale;
            if (rot.HasValue) p.transform.localRotation = rot.Value;
            p.GetComponent<MeshRenderer>().sharedMaterial = m;
            var col = p.GetComponent<Collider>();
            if (Application.isPlaying) Object.Destroy(col); else Object.DestroyImmediate(col);
            return p.transform;
        }

        static Transform Wheel(Transform parent, Vector3 pos, float radius) =>
            Part(parent, PrimitiveType.Cylinder, pos, new Vector3(radius * 2f, 0.12f, radius * 2f), Mat("#141414", 0.2f), Quaternion.Euler(0, 0, 90));

        // Models per class (Resources/Models/Vehicles): realistic 3D GLB vehicles across the Lagos fleet
        // (Nissan 200SX S14, Dodge Charger SRT Hellcat, Mercedes-Benz S65 AMG Coupe, Audi e-tron GT, Ferrari 296 Speciale,
        // Ferrari F40, Dodge Ram 1500 Rebel, UAZ 469, Isuzu & Tata Signa cargo trucks, VW Crafter, Danfo Vanagon,
        // Bajaj RE Keke, Suzuki Carry Korope, Suzuki GSX-R750, Kawasaki Ninja ZX-10R, Suzuki Hayabusa).
        static readonly Dictionary<string, string[]> Models = new Dictionary<string, string[]>
        {
            ["car"] = new[] { "1997_nissan_200sx_s14", "2015_dodge_charger_srt_hellcat", "2015_mercedes-benz_s65_amg_coupe" },
            ["luxury"] = new[] { "2015_mercedes-benz_s65_amg_coupe", "2018_audi_e-tron_gt_concept", "2026_ferrari_296_speciale_a", "ferrari_f40_modified", "2015_dodge_charger_srt_hellcat" },
            ["sports"] = new[] { "2026_ferrari_296_speciale_a", "ferrari_f40_modified", "2018_audi_e-tron_gt_concept", "1997_nissan_200sx_s14" },
            ["police"] = new[] { "uaz_469_un", "2015_dodge_charger_srt_hellcat" },
            ["lawma"] = new[] { "isuzu_cargo_base_truck", "tata_signa_cargo_truck" },
            ["fire"] = new[] { "tata_signa_cargo_truck", "2003-gmc-topkick-c6500" },
            ["ambulance"] = new[] { "volkswagen_crafter" },
            ["delivery"] = new[] { "isuzu_cargo_base_truck", "volkswagen_crafter" },
            ["truck"] = new[] { "tata_signa_cargo_truck", "isuzu_cargo_base_truck", "heavy_commercial_vehicle_hcv" },
            ["danfo"] = new[] { "danfo_vanagon" },
            ["keke"] = new[] { "keke_bajaj_re" },
            ["korope"] = new[] { "suzuki_carry_minivan" },
            ["okada"] = new[] { "suzuki_gsx-r750" },
            ["brt"] = new[] { "volkswagen_crafter" },
            ["army"] = new[] { "uaz_469_un", "2003-gmc-topkick-c6500" },
            ["tanker"] = new[] { "fuel_truck", "heavy_commercial_vehicle_hcv" },
            ["pickup"] = new[] { "dodge_ram_1500_rebel", "lightbody_90_md_pickup_-_low_poly_model" },
            ["van"] = new[] { "volkswagen_crafter" },
            ["motorcycle"] = new[] { "2008_kawasaki_ninja_zx-10r-em", "suzuki_hayabusa_gsx-1300r-k8" },
        };
        // Extra yaw (degrees) to bring a model's native front onto +z. glTFast keeps glTF +Z as Unity +z, so most models need
        // none; the keke's front is along glTF +X, which is -x in Unity.
        static readonly Dictionary<string, float> ModelYaw = new Dictionary<string, float> { ["keke_bajaj_re"] = 90f, ["suzuki_carry_minivan"] = -45f };   // the korope is modelled 45° off its axis
        // Up-axis corrections (Euler degrees, applied before ModelYaw) for models exported with Z up or lying on a side,
        // which otherwise import standing on end or on their side. Found with RISE ▸ Render Vehicle Sheet (Editor/VehicleSheet.cs).
        public static readonly Dictionary<string, Vector3> ModelFix = new Dictionary<string, Vector3>
        {
            ["suzuki_carry_minivan"] = new Vector3(-90f, 0f, 0f),          // korope: lay on its wheels
            ["keke_bajaj_re"] = new Vector3(-90f, 0f, 0f),                 // keke: was on its side
            ["suzuki_gsx-r750"] = new Vector3(-90f, 0f, 0f),               // okada: stood on its tail
            ["2008_kawasaki_ninja_zx-10r-em"] = new Vector3(-90f, 0f, 0f), // motorcycle: was on its side
            ["suzuki_hayabusa_gsx-1300r-k8"] = new Vector3(-90f, 0f, 0f),  // motorcycle: stood on its tail
            ["2003-gmc-topkick-c6500"] = new Vector3(-90f, 0f, 0f),        // army: stood on its tail
            ["heavy_commercial_vehicle_hcv"] = new Vector3(-90f, 180f, 0f), // tanker: on its side and facing backwards
            ["lightbody_90_md_pickup_-_low_poly_model"] = new Vector3(-90f, 0f, 0f),   // pickup: was on its side
        };
        // Tools can try other corrections without editing the table (VehicleSheet.RenderCandidates).
        public static System.Func<string, Vector3?> FixOverride;
        public static bool UseModels = true;

        // Spin a wheel by its axle: imported wheels (named "wheel-…") turn about their local x, procedural ones about y.
        public static void Spin(Transform w, float degrees) => w.Rotate(w.name.StartsWith("wheel") ? Vector3.right : Vector3.up, degrees, Space.Self);

        public static string LastModelName;   // the model the last TryModel call picked (diagnostics, smoke test)

        static GameObject TryModel(VehicleClassData cls, Transform parent, System.Random rng, out Transform[] wheels)
        {
            wheels = null; LastModelName = null;
            if (!UseModels || !Models.TryGetValue(cls.id, out var names)) return null;
            return BuildModel(cls, names[rng.Next(names.Length)], parent, out wheels, rng);
        }

        public static string[] ModelsFor(string classId) => Models.TryGetValue(classId, out var n) ? n : new string[0];

        // Fit one named model to a class: up-axis fix, yaw, class length, wheels on the ground. Null if it does not load.
        public static GameObject BuildModel(VehicleClassData cls, string name, Transform parent, out Transform[] wheels, System.Random rng = null)
        {
            wheels = null; LastModelName = null;
            var prefab = Resources.Load<GameObject>("Models/Vehicles/" + name);
            if (prefab == null) return null;
            var go = new GameObject(cls.name); go.transform.SetParent(parent, false);
            var model = Object.Instantiate(prefab, go.transform, false);
            model.name = "Model";
            // The Suzuki bikes come from a game rig: motion-blur copies of each wheel layered over the real ones, and light
            // "glow" quads (a headlight beam among them) that need an additive shader and otherwise show as a black blade.
            foreach (var rend in model.GetComponentsInChildren<Renderer>(true))
            {
                var n = rend.gameObject.name;
                if ((n.StartsWith("Wheel_") && n.Contains("_Motion")) || (n.StartsWith("lights_") && n.Contains("_glows_"))) rend.gameObject.SetActive(false);   // inactive: also out of the fitting bounds
            }
            if (LitRemap.Contains(name)) RemapToLit(model);
            var fix = FixOverride?.Invoke(name) ?? (ModelFix.TryGetValue(name, out var f) ? f : Vector3.zero);
            model.transform.localRotation = Quaternion.Euler(0f, ModelYaw.TryGetValue(name, out var yaw) ? yaw : 0f, 0f) * Quaternion.Euler(fix);
            // A model whose long axis lies along x is turned a quarter so its length runs along z (the front/back ambiguity
            // cannot be read from bounds; ModelYaw carries the known cases).
            bool tight = TightFit.Contains(name);
            var b = Measure(model, tight);
            if (b.size.x > b.size.z * 1.15f) { model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * model.transform.localRotation; b = Measure(model, tight); }
            // Scale to the class length, cap stylized roof height to human scale, feet on the ground, centred on the axle line.
            float kz = b.size.z > 0.01f ? cls.len / b.size.z : 1f;
            float kx = kz, ky = kz;
            if (cls.id == "car" || cls.id == "luxury" || cls.id == "police")
            {
                bool suv = name.Contains("suv");
                float maxH = suv ? 1.68f : 1.48f;
                if (b.size.y * ky > maxH) ky = maxH / b.size.y;
                if (b.size.x * kx > cls.wid * 1.04f) kx = (cls.wid * 1.04f) / b.size.x;
            }
            else if (cls.id == "van" || cls.id == "ambulance" || cls.id == "delivery")
            {
                float maxH = 2.05f;
                if (b.size.y * ky > maxH) ky = maxH / b.size.y;
                if (b.size.x * kx > cls.wid * 1.06f) kx = (cls.wid * 1.06f) / b.size.x;
            }
            model.transform.localScale = new Vector3(kx, ky, kz);
            b = Measure(model, tight);
            model.transform.localPosition = new Vector3(-(b.center.x - go.transform.position.x), -(b.min.y - go.transform.position.y), -(b.center.z - go.transform.position.z));
            b = Measure(model, tight);
            var ws = new List<Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>()) if (t.name.StartsWith("wheel-") && ws.Count < 4) ws.Add(t);   // Kenney names its wheels; the Lagos models bake them into the body
            wheels = ws.ToArray();
            foreach (var c in model.GetComponentsInChildren<Collider>()) { if (Application.isPlaying) Object.Destroy(c); else Object.DestroyImmediate(c); }
            var info = go.AddComponent<VehicleModelInfo>(); info.Model = name; info.Height = b.size.y; info.Length = b.size.z; info.Width = b.size.x;
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, b.size.y * 0.5f, 0f); col.size = new Vector3(cls.wid, b.size.y, cls.len);   // class width: the data's lanes assume it
            var finishRng = rng ?? new System.Random(cls.id.GetHashCode());
            ApplyVehicleFinish(go.transform, model, cls, name, b.size, finishRng);
            AttachOccupants(go.transform, cls, b.size, finishRng);
            LastModelName = name;
            return go;
        }

        static Material GetRecoloredColormapMat(Texture srcTex, string targetHex, float smoothness, float metallic)
        {
            string key = (srcTex != null ? srcTex.name : "null") + ":" + targetHex + ":" + smoothness.ToString("F2");
            if (tintedColormapMats.TryGetValue(key, out var cached) && cached != null) return cached;

            standard ??= ShaderLib.Lit;
            var mat = new Material(standard) { name = "Colormap_" + targetHex };
            mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);

            if (srcTex != null && ColorUtility.TryParseHtmlString(targetHex, out var targetCol))
            {
                if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    mat.color = targetCol;
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", targetCol);
                    return tintedColormapMats[key] = mat;
                }
                try
                {
                    Color.RGBToHSV(targetCol, out float tH, out float tS, out float tV);
                    int w = Mathf.Min(256, srcTex.width), h = Mathf.Min(256, srcTex.height);
                    var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
                    var prev = RenderTexture.active;
                    Graphics.Blit(srcTex, rt);
                    RenderTexture.active = rt;
                    var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "ColormapTex_" + targetHex, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(rt);

                    var pixels = tex.GetPixels();
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        Color.RGBToHSV(pixels[i], out float pH, out float pS, out float pV);
                        // Kenney colormap body paint swatches sit in the green/teal/blue band (H 0.22..0.85, S > 0.28).
                        // Red taillights (H < 0.06 || H > 0.94), amber lights (H 0.07..0.20), and tires/windows/chrome (S <= 0.28) stay untouched.
                        if (pH >= 0.22f && pH <= 0.85f && pS > 0.28f)
                        {
                            float vOut = Mathf.Clamp01(tV * (0.45f + 0.75f * pV));
                            pixels[i] = Color.HSVToRGB(tH, Mathf.Clamp01(tS * (0.75f + 0.25f * pS)), vOut);
                        }
                    }
                    tex.SetPixels(pixels);
                    tex.Apply(false, false);
                    mat.SetTexture("_BaseMap", tex);
                    mat.mainTexture = tex;
                    mat.color = Color.white;
                }
                catch
                {
                    mat.color = targetCol;
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", targetCol);
                }
            }
            return tintedColormapMats[key] = mat;
        }

        static void SetRendererBodyColor(GameObject model, string matToken, string hex, float smoothness, float metallic = 0.25f)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out var col)) return;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || !m.name.Contains(matToken)) continue;
                    string key = "BodyOverride:" + m.name + ":" + hex;
                    if (!materials.TryGetValue(key, out var inst) || inst == null)
                    {
                        inst = new Material(ShaderLib.Lit) { name = m.name + "_" + hex, color = col };
                        if (inst.HasProperty("_BaseColor")) inst.SetColor("_BaseColor", col);
                        inst.SetFloat("_Smoothness", smoothness);
                        if (inst.HasProperty("_Metallic")) inst.SetFloat("_Metallic", metallic);
                        materials[key] = inst;
                    }
                    mats[i] = inst;
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        static void ReplaceColormapMaterial(GameObject model, string targetHex, float smoothness, float metallic = 0.28f)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || !m.name.Contains("colormap")) continue;
                    Texture tex = m.HasProperty("baseColorTexture") ? m.GetTexture("baseColorTexture") : m.mainTexture;
                    mats[i] = GetRecoloredColormapMat(tex, targetHex, smoothness, metallic);
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        static readonly string[] BodyMaterialTokens =
        {
            "CarPaint", "Car_Paint", "CarPaint_flake", "Paint_body", "Paint", "Coloured",
            "d2phong3SG1", "S_Boya", "UTLTRUCK90_Bodymat", "pasted__pasted__truck_cabin",
            "gmcphong16SG1", "mat_2", "final1", "Material.001"
        };

        public static void PaintBodyMaterials(GameObject root, Color col, float smoothness = 0.85f, float metallic = 0.35f)
        {
            string hex = "#" + ColorUtility.ToHtmlStringRGB(col);
            PaintBodyMaterials(root, hex, col, smoothness, metallic);
        }

        public static void PaintBodyMaterials(GameObject root, string hex, float smoothness = 0.85f, float metallic = 0.35f)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out var col)) return;
            PaintBodyMaterials(root, hex, col, smoothness, metallic);
        }

        static void PaintBodyMaterials(GameObject root, string hex, Color col, float smoothness, float metallic)
        {
            var occ = root.transform.Find("Occupants");
            var nfsMods = root.transform.Find("NfsMods");
            bool matchedSpecific = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.StartsWith("wheel") || r.name == "DropShadow" || r.name == "Lightbar" || r.GetComponent<TextMesh>() != null) continue;
                if (occ != null && r.transform.IsChildOf(occ)) continue;
                if (nfsMods != null && r.transform.IsChildOf(nfsMods)) continue;
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    string mn = m.name;
                    if (mn.Contains("colormap"))
                    {
                        Texture tex = m.HasProperty("baseColorTexture") ? m.GetTexture("baseColorTexture") : m.mainTexture;
                        mats[i] = GetRecoloredColormapMat(tex, hex, smoothness, metallic);
                        changed = true;
                        matchedSpecific = true;
                        continue;
                    }
                    bool isBodyToken = false;
                    for (int t = 0; t < BodyMaterialTokens.Length; t++)
                    {
                        if (mn.Contains(BodyMaterialTokens[t])) { isBodyToken = true; break; }
                    }
                    if (!isBodyToken) continue;
                    string key = $"BodyPaint:{mn}:{hex}:{smoothness:F2}:{metallic:F2}";
                    if (!materials.TryGetValue(key, out var inst) || inst == null)
                    {
                        inst = new Material(ShaderLib.Lit) { name = mn + "_" + hex, color = col };
                        if (inst.HasProperty("_BaseColor")) inst.SetColor("_BaseColor", col);
                        inst.SetFloat("_Smoothness", smoothness);
                        if (inst.HasProperty("_Metallic")) inst.SetFloat("_Metallic", metallic);
                        materials[key] = inst;
                    }
                    mats[i] = inst;
                    changed = true;
                    matchedSpecific = true;
                }
                if (changed) r.sharedMaterials = mats;
            }

            // Fallback for procedural primitive vehicles (no GLB body tokens)
            if (!matchedSpecific && root.transform.Find("Model") == null)
            {
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.name.StartsWith("wheel") || r.name == "DropShadow" || r.name == "Lightbar" || r.GetComponent<TextMesh>() != null) continue;
                    if (occ != null && r.transform.IsChildOf(occ)) continue;
                    if (nfsMods != null && r.transform.IsChildOf(nfsMods)) continue;
                    var m = r.sharedMaterial;
                    if (m != null && (m.name.Contains("1a2530") || m.name.Contains("141414"))) continue;
                    r.sharedMaterial = Mat(hex, smoothness, metallic);
                }
            }
        }

        // Applies authentic Lagos vehicle colours, metallic PBR clear-coats, stripes, lightbars and signage to GLB models.
        static void ApplyVehicleFinish(Transform root, GameObject model, VehicleClassData cls, string modelName, Vector3 size, System.Random rng)
        {
            float wid = size.x, h = size.y, len = size.z;
            switch (cls.id)
            {
                case "lawma":
                {
                    // Vivid LAWMA Orange (#ff6a00) compactor/cargo truck + amber beacon
                    PaintBodyMaterials(model, "#ff6a00", 0.62f, 0.18f);
                    Part(root, PrimitiveType.Cube, new Vector3(0f, h * 0.96f, len * 0.26f), new Vector3(wid * 0.42f, 0.12f, 0.22f), Mat("#ff9900", 0.9f));
                    break;
                }
                case "brt":
                {
                    // LAMATA BRT Blue (#1559b8)
                    SetRendererBodyColor(model, "Car_Paint", "#1559b8", 0.78f, 0.25f);
                    break;
                }
                case "ambulance":
                {
                    // Lagos State Ambulance Service (LASAMBUS) White (#f8fafc) + emergency red/blue lightbar
                    SetRendererBodyColor(model, "Car_Paint", "#f8fafc", 0.82f, 0.20f);
                    var lb = Part(root, PrimitiveType.Cube, new Vector3(0f, h + 0.04f, len * 0.22f), new Vector3(wid * 0.48f, 0.12f, 0.24f), Mat("#ef4444", 0.92f));
                    lb.name = "Lightbar";
                    break;
                }
                case "korope":
                {
                    // Lagos Yellow (#f5c518) Suzuki Carry Korope minibus
                    SetRendererBodyColor(model, "S_Boya", "#f5c518", 0.72f, 0.18f);
                    break;
                }
                case "danfo":
                {
                    // Iconic Lagos Danfo yellow
                    break;
                }
                case "police":
                {
                    // Nigeria Police Force (NPF) deep navy blue (#142442) + roof Lightbar
                    PaintBodyMaterials(model, "#142442", 0.78f, 0.30f);
                    var lb = Part(root, PrimitiveType.Cube, new Vector3(0f, h + 0.05f, -0.1f), new Vector3(wid * 0.48f, 0.12f, 0.24f), Mat("#2563eb", 0.92f));
                    lb.name = "Lightbar";
                    break;
                }
                case "army":
                {
                    // Nigerian Army Olive Drab (#3b5323)
                    PaintBodyMaterials(model, "#3b5323", 0.42f, 0.15f);
                    SetRendererBodyColor(model, "gmcphong8SG1", "#2e421b", 0.38f, 0.15f);
                    break;
                }
                case "fire":
                {
                    PaintBodyMaterials(model, "#c62828", 0.75f, 0.22f);
                    var lb = Part(root, PrimitiveType.Cube, new Vector3(0f, h + 0.05f, len * 0.28f), new Vector3(wid * 0.48f, 0.12f, 0.24f), Mat("#ef4444", 0.92f));
                    lb.name = "Lightbar";
                    break;
                }
                case "pickup":
                {
                    string pickCol = CarColours[rng.Next(CarColours.Length)];
                    PaintBodyMaterials(model, pickCol, 0.74f, 0.32f);
                    break;
                }
                case "luxury":
                case "sports":
                {
                    string luxCol = LuxuryColours[rng.Next(LuxuryColours.Length)];
                    PaintBodyMaterials(model, luxCol, 0.92f, 0.52f);
                    break;
                }
                case "car":
                case "van":
                case "truck":
                case "delivery":
                {
                    bool isTaxi = cls.id == "car" && rng.NextDouble() < 0.22;
                    string carCol = isTaxi ? "#f29b0a" : CarColours[rng.Next(CarColours.Length)];
                    PaintBodyMaterials(model, carCol, 0.88f, 0.36f);
                    break;
                }
            }
            // Ground contact drop shadow under every vehicle so wheels sit firmly planted on the road.
            if (cls.id != "okada" && cls.id != "motorcycle")
            {
                var shadow = Part(root, PrimitiveType.Cube, new Vector3(0f, 0.02f, 0f), new Vector3(wid * 0.92f, 0.015f, len * 0.92f), Mat("#090c0e", 0.05f, 0f));
                shadow.name = "DropShadow";
            }
        }

        // Adds rear trunk garnish bar, multi-element ruby LED taillights, recessed license plate, side wing mirrors,
        // checkered taxi livery + wedge roof sign (for taxis), and an animated hinged front door ("DoorPivot").
        static void AttachAutomotiveTrim(Transform root, float wid, float h, float len, string bodyHex, bool isTaxi)
        {
            var trimDark = Mat("#12151a", 0.78f, 0.35f);
            var rubyLight = Mat("#d91b24", 0.92f, 0.25f);
            var reverseLens = Mat("#e8eef5", 0.92f, 0.30f);
            var plateGold = Mat("#f59e0b", 0.65f, 0.15f);
            var bodyMat = Mat(bodyHex, 0.88f, 0.36f);
            var glassMat = Mat("#111924", 0.95f, 0.40f);

            // Rear black trunk garnish bar & multi-element taillights
            float rearZ = -len * 0.47f;
            Part(root, PrimitiveType.Cube, new Vector3(0f, h * 0.48f, rearZ), new Vector3(wid * 0.78f, h * 0.11f, 0.06f), trimDark);
            foreach (float sx in new[] { -1f, 1f })
            {
                Part(root, PrimitiveType.Cube, new Vector3(sx * wid * 0.36f, h * 0.48f, rearZ - 0.01f), new Vector3(wid * 0.14f, h * 0.12f, 0.07f), rubyLight);
                Part(root, PrimitiveType.Cube, new Vector3(sx * wid * 0.32f, h * 0.48f, rearZ - 0.015f), new Vector3(wid * 0.05f, h * 0.06f, 0.07f), reverseLens);
                // Side wing mirrors
                Part(root, PrimitiveType.Cube, new Vector3(sx * (wid * 0.48f), h * 0.63f, len * 0.14f), new Vector3(0.14f, 0.08f, 0.10f), bodyMat);
            }

            // Recessed rear license plate well & yellow/black plate
            Part(root, PrimitiveType.Cube, new Vector3(0f, h * 0.33f, rearZ - 0.01f), new Vector3(0.36f, 0.14f, 0.04f), trimDark);
            Part(root, PrimitiveType.Cube, new Vector3(0f, h * 0.33f, rearZ - 0.02f), new Vector3(0.30f, 0.10f, 0.04f), plateGold);

            // Hinged front-right door ("DoorPivot") that swings open on vehicle enter/exit
            var doorPivot = new GameObject("DoorPivot").transform;
            doorPivot.SetParent(root, false);
            doorPivot.localPosition = new Vector3(wid * 0.44f, h * 0.42f, len * 0.16f);
            Part(doorPivot, PrimitiveType.Cube, new Vector3(0.02f, -h * 0.04f, -len * 0.11f), new Vector3(0.055f, h * 0.36f, len * 0.22f), bodyMat);
            Part(doorPivot, PrimitiveType.Cube, new Vector3(0.015f, h * 0.20f, -len * 0.11f), new Vector3(0.04f, h * 0.20f, len * 0.20f), glassMat);

            if (isTaxi)
            {
                // Black-and-amber checkered rear & quarter-panel strips
                var chk = CheckerMat();
                Part(root, PrimitiveType.Cube, new Vector3(-wid * 0.25f, h * 0.34f, rearZ - 0.008f), new Vector3(wid * 0.22f, h * 0.12f, 0.05f), chk);
                Part(root, PrimitiveType.Cube, new Vector3(wid * 0.25f, h * 0.34f, rearZ - 0.008f), new Vector3(wid * 0.22f, h * 0.12f, 0.05f), chk);
                Part(root, PrimitiveType.Cube, new Vector3(-wid * 0.46f, h * 0.44f, -len * 0.04f), new Vector3(0.04f, h * 0.11f, len * 0.56f), chk);
                Part(root, PrimitiveType.Cube, new Vector3(wid * 0.46f, h * 0.44f, -len * 0.04f), new Vector3(0.04f, h * 0.11f, len * 0.56f), chk);

                // Sculpted 3D wedge illuminated TAXI roof sign
                var signRoot = new GameObject("TaxiSign").transform;
                signRoot.SetParent(root, false);
                signRoot.localPosition = new Vector3(0f, h + 0.02f, -len * 0.02f);
                Part(signRoot, PrimitiveType.Cube, new Vector3(0f, 0.02f, 0f), new Vector3(0.32f, 0.04f, 0.62f), trimDark);
                var signMat = Mat("#f59e0b", 0.88f, 0.15f);
                Part(signRoot, PrimitiveType.Cube, new Vector3(0f, 0.12f, 0f), new Vector3(0.22f, 0.18f, 0.56f), signMat);
                Part(signRoot, PrimitiveType.Cube, new Vector3(0f, 0.21f, 0f), new Vector3(0.14f, 0.06f, 0.52f), signMat);
            }
        }

        // Models whose parts carry rotated local boxes, so renderer bounds overstate them (the korope's read 3.8 m square):
        // these are fitted on their vertices instead.
        static readonly HashSet<string> TightFit = new HashSet<string> { "suzuki_carry_minivan" };
        static readonly Dictionary<Mesh, Vector3[]> vertexCache = new Dictionary<Mesh, Vector3[]>();
        static bool warnedUnreadable;

        // Models whose glTF materials render black in player builds (keke and pickup use KHR_materials_specular, which works in
        // the editor only): their opaque materials are rebuilt on URP Lit from the base colour texture and factor.
        static readonly HashSet<string> LitRemap = new HashSet<string>
        {
            "keke_bajaj_re", "lightbody_90_md_pickup_-_low_poly_model",
            "1997_nissan_200sx_s14", "2015_dodge_charger_srt_hellcat", "2015_mercedes-benz_s65_amg_coupe",
            "2018_audi_e-tron_gt_concept", "2026_ferrari_296_speciale_a", "ferrari_f40_modified",
            "dodge_ram_1500_rebel", "fuel_truck", "isuzu_cargo_base_truck", "tata_signa_cargo_truck", "uaz_469_un"
        };
        static readonly Dictionary<Material, Material> litCache = new Dictionary<Material, Material>();

        static void RemapToLit(GameObject model)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i]; if (src == null) continue;
                    if (src.renderQueue >= 2800 || src.name.Contains("Transp") || (src.HasProperty("baseColorFactor") && src.GetColor("baseColorFactor").a < 0.98f)) continue;
                    if (!litCache.TryGetValue(src, out var lit))
                    {
                        lit = new Material(ShaderLib.Lit) { name = src.name + " (Lit)" };
                        if (src.HasProperty("baseColorTexture")) lit.SetTexture("_BaseMap", src.GetTexture("baseColorTexture"));
                        if (src.HasProperty("baseColorFactor")) lit.SetColor("_BaseColor", src.GetColor("baseColorFactor"));
                        if (lit.HasProperty("_Cull")) lit.SetFloat("_Cull", 0f);
                        lit.SetFloat("_Smoothness", 0.35f);
                        litCache[src] = lit;
                    }
                    mats[i] = lit;
                }
                r.sharedMaterials = mats;
            }
        }

        static Bounds Measure(GameObject model, bool tight)
        {
            if (!tight) return Entities.CharacterRig.Bounds(model);
            bool any = false; var b = new Bounds();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = mf.sharedMesh; if (mesh == null) continue;
                if (!mesh.isReadable) { if (!warnedUnreadable) { warnedUnreadable = true; Debug.LogWarning($"RISE: mesh '{mesh.name}' is not readable; fitting {model.transform.parent?.name} on renderer bounds"); } return Entities.CharacterRig.Bounds(model); }
                if (!vertexCache.TryGetValue(mesh, out var verts)) vertexCache[mesh] = verts = mesh.vertices;
                var m = mf.transform.localToWorldMatrix;
                foreach (var v in verts) { var p = m.MultiplyPoint3x4(v); if (any) b.Encapsulate(p); else { b = new Bounds(p, Vector3.zero); any = true; } }
            }
            return any ? b : Entities.CharacterRig.Bounds(model);
        }

        public static GameObject Build(VehicleClassData cls, Transform parent, System.Random rng, out Transform[] wheels)
        {
            var modelled = TryModel(cls, parent, rng, out wheels);
            if (modelled != null) return modelled;
            var go = new GameObject(cls.name);
            go.transform.SetParent(parent, false);
            var t = go.transform;
            float len = cls.len, wid = cls.wid;
            var black = Mat("#141414", 0.3f); var glass = Mat("#1a2530", 0.9f); var yellow = Mat("#f5c518", 0.5f);
            var w = new List<Transform>();
            float height;
            switch (cls.id)
            {
                case "danfo": case "korope": case "brt":
                    height = cls.id == "brt" ? 3f : 2.1f;
                    Part(t, PrimitiveType.Cube, new Vector3(0, height * 0.5f + 0.2f, 0), new Vector3(wid, height, len), cls.id == "brt" ? Mat("#1c4fa0", 0.5f) : yellow);
                    Part(t, PrimitiveType.Cube, new Vector3(0, 1.0f, 0), new Vector3(wid + 0.04f, 0.28f, len + 0.04f), black);          // the black stripe
                    Part(t, PrimitiveType.Cube, new Vector3(0, height * 0.5f + 0.65f, 0.3f), new Vector3(wid + 0.02f, 0.5f, len * 0.7f), glass);
                    foreach (var x in new[] { -wid * 0.42f, wid * 0.42f }) foreach (var z in new[] { len * 0.32f, -len * 0.32f }) w.Add(Wheel(t, new Vector3(x, 0.36f, z), 0.36f));
                    break;
                case "keke":
                    height = 1.0f;
                    Part(t, PrimitiveType.Cube, new Vector3(0, 0.75f, 0), new Vector3(wid, height, len), yellow);
                    Part(t, PrimitiveType.Cube, new Vector3(0, 1.5f, 0), new Vector3(wid + 0.1f, 0.1f, len + 0.1f), black);             // canopy
                    w.Add(Wheel(t, new Vector3(0, 0.3f, len * 0.42f), 0.3f));
                    foreach (var x in new[] { -wid * 0.4f, wid * 0.4f }) w.Add(Wheel(t, new Vector3(x, 0.3f, -len * 0.35f), 0.3f));
                    height = 1.6f;
                    break;
                case "okada": case "motorcycle":
                    Part(t, PrimitiveType.Cube, new Vector3(0, 0.7f, 0), new Vector3(0.4f, 0.4f, len * 0.7f), Mat(CarColours[rng.Next(CarColours.Length)], 0.7f));
                    Part(t, PrimitiveType.Cube, new Vector3(0, 0.95f, -0.3f), new Vector3(0.5f, 0.15f, 0.8f), black);                   // seat
                    w.Add(Wheel(t, new Vector3(0, 0.33f, len * 0.38f), 0.33f)); w.Add(Wheel(t, new Vector3(0, 0.33f, -len * 0.38f), 0.33f));
                    height = 1.2f;
                    break;
                case "police":
                    height = 1.5f;
                    Part(t, PrimitiveType.Cube, new Vector3(0, 0.55f, 0), new Vector3(wid * 0.72f, 0.6f, len), Mat("#1f2b45", 0.8f));
                    Part(t, PrimitiveType.Cube, new Vector3(0, 1.1f, -0.2f), new Vector3(wid * 0.64f, 0.55f, len * 0.5f), glass);
                    Part(t, PrimitiveType.Cube, new Vector3(0, 1.45f, -0.2f), new Vector3(wid * 0.5f, 0.15f, 0.3f), Mat("#3d7cff", 0.9f)).name = "Lightbar";
                    foreach (var x in new[] { -wid * 0.34f, wid * 0.34f }) foreach (var z in new[] { len * 0.3f, -len * 0.3f }) w.Add(Wheel(t, new Vector3(x, 0.32f, z), 0.32f));
                    break;
                case "car": case "luxury": case "pickup": case "van": case "delivery": case "ambulance":
                    height = 1.5f;
                    var paint = Mat(cls.id == "luxury" ? LuxuryColours[rng.Next(LuxuryColours.Length)] : CarColours[rng.Next(CarColours.Length)], 0.92f);
                    Part(t, PrimitiveType.Cube, new Vector3(0, 0.55f, 0), new Vector3(wid * 0.72f, 0.6f, len), paint);
                    Part(t, PrimitiveType.Cube, new Vector3(0, 1.1f, -0.2f), new Vector3(wid * 0.64f, 0.55f, len * 0.5f), glass);
                    foreach (var x in new[] { -wid * 0.34f, wid * 0.34f }) foreach (var z in new[] { len * 0.3f, -len * 0.3f }) w.Add(Wheel(t, new Vector3(x, 0.32f, z), 0.32f));
                    break;
                default:   // trucks, tanker, fire, army, lawma
                    height = 2.8f;
                    string body = cls.id == "fire" ? "#c62828" : cls.id == "army" ? "#3b5323" : cls.id == "lawma" ? "#ff6a00" : cls.id == "tanker" ? "#c9c9c9" : "#6c7a89";
                    Part(t, PrimitiveType.Cube, new Vector3(0, 1.3f, len * 0.3f), new Vector3(wid, 2.2f, len * 0.3f), Mat(body, 0.5f));            // cab
                    Part(t, PrimitiveType.Cube, new Vector3(0, 1.7f, len * 0.3f), new Vector3(wid + 0.02f, 0.6f, len * 0.3f * 0.6f), glass);
                    if (cls.id == "tanker") Part(t, PrimitiveType.Cylinder, new Vector3(0, 1.6f, -len * 0.15f), new Vector3(wid * 0.9f, len * 0.3f, wid * 0.9f), Mat(body, 0.7f), Quaternion.Euler(90, 0, 0));
                    else Part(t, PrimitiveType.Cube, new Vector3(0, 1.5f, -len * 0.18f), new Vector3(wid, 2.4f, len * 0.6f), Mat(body, 0.4f));
                    foreach (var x in new[] { -wid * 0.42f, wid * 0.42f }) foreach (var z in new[] { len * 0.35f, -len * 0.1f, -len * 0.35f }) w.Add(Wheel(t, new Vector3(x, 0.45f, z), 0.45f));
                    break;
            }
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0, height * 0.5f + 0.1f, 0);
            col.size = new Vector3(wid, height, len);
            AttachOccupants(go.transform, cls, new Vector3(wid, height, len), rng ?? new System.Random(cls.id.GetHashCode()));
            wheels = w.ToArray();
            return go;
        }

        static readonly string[] SkinHex = { "#3b2416", "#4a2e1b", "#5c3a21", "#6e472b", "#2d1b10" };
        static readonly string[] TopHex = { "#c98a2c", "#2a5c8a", "#8a2a3b", "#2e6e4e", "#d8d2c5", "#6b3fa0", "#b85d19", "#1e4d2b", "#c43c2b", "#e0a82e" };
        static readonly string[] HelmetHex = { "#d93829", "#e5b822", "#1f4e8c", "#222428" };

        // Seated driver + passengers under "Occupants" ("Driver" and "Passengers"). Traffic vehicles keep both active;
        // VehicleController hides them while parked and shows "Driver" when driven.
        public static void AttachOccupants(Transform root, VehicleClassData cls, Vector3 bodySize, System.Random rng)
        {
            var existing = root.Find("Occupants");
            if (existing != null) { if (Application.isPlaying) Object.Destroy(existing.gameObject); else Object.DestroyImmediate(existing.gameObject); }

            var occ = new GameObject("Occupants").transform;
            occ.SetParent(root, false);
            var driverRoot = new GameObject("Driver").transform;
            driverRoot.SetParent(occ, false);
            var paxRoot = new GameObject("Passengers").transform;
            paxRoot.SetParent(occ, false);

            float wid = Mathf.Max(0.8f, bodySize.x), h = Mathf.Max(1.1f, bodySize.y), len = Mathf.Max(1.8f, bodySize.z);
            bool police = cls.id == "police";

            switch (cls.id)
            {
                case "okada":
                case "motorcycle":
                {
                    float riderY = Mathf.Max(0.78f, h * 0.68f);
                    float pillionY = Mathf.Max(0.84f, h * 0.73f);
                    BuildSeatedPerson(driverRoot, "Rider", new Vector3(0f, riderY, -0.06f), rng, isDriver: true, bikePose: true, helmet: true, scale: 1.08f);
                    if (cls.id == "okada" && rng.NextDouble() < 0.85)
                        BuildSeatedPerson(paxRoot, "Pillion", new Vector3(0f, pillionY, -0.46f), rng, isDriver: false, bikePose: true, helmet: false, scale: 1.04f);
                    break;
                }
                case "keke":
                {
                    // Keep the rider and rear passengers tucked inside the Bajaj RE cabin canopy so they do not clip the roof/rear shell.
                    BuildSeatedPerson(driverRoot, "Rider", new Vector3(0f, h * 0.22f, len * 0.10f), rng, isDriver: true, windowSide: 0, scale: 0.66f);
                    BuildSeatedPerson(paxRoot, "PaxL", new Vector3(-wid * 0.12f, h * 0.22f, -len * 0.10f), rng, windowSide: 0, scale: 0.64f);
                    BuildSeatedPerson(paxRoot, "PaxR", new Vector3(wid * 0.12f, h * 0.22f, -len * 0.10f), rng, windowSide: 0, scale: 0.64f);
                    break;
                }
                case "danfo":
                {
                    BuildSeatedPerson(driverRoot, "DriverSeat", new Vector3(-wid * 0.21f, h * 0.38f, len * 0.22f), rng, isDriver: true, windowSide: -1, scale: 0.86f);
                    BuildSeatedPerson(paxRoot, "Conductor", new Vector3(wid * 0.24f, h * 0.36f, len * 0.05f), rng, hangingSide: true, windowSide: 1, scale: 0.86f);
                    float[] zs = { -len * 0.05f, -len * 0.18f, -len * 0.30f };
                    for (int i = 0; i < zs.Length; i++)
                    {
                        BuildSeatedPerson(paxRoot, "PaxL" + i, new Vector3(-wid * 0.21f, h * 0.38f, zs[i]), rng, windowSide: -1, scale: 0.84f);
                        if (rng.NextDouble() < 0.85)
                            BuildSeatedPerson(paxRoot, "PaxR" + i, new Vector3(wid * 0.21f, h * 0.38f, zs[i]), rng, windowSide: 1, scale: 0.84f);
                    }
                    break;
                }
                case "korope":
                {
                    BuildSeatedPerson(driverRoot, "DriverSeat", new Vector3(-wid * 0.20f, h * 0.38f, len * 0.18f), rng, isDriver: true, windowSide: -1, scale: 0.84f);
                    BuildSeatedPerson(paxRoot, "FrontPax", new Vector3(wid * 0.20f, h * 0.38f, len * 0.18f), rng, windowSide: 1, scale: 0.84f);
                    BuildSeatedPerson(paxRoot, "MidPaxL", new Vector3(-wid * 0.20f, h * 0.38f, -len * 0.06f), rng, windowSide: -1, scale: 0.82f);
                    BuildSeatedPerson(paxRoot, "MidPaxR", new Vector3(wid * 0.20f, h * 0.38f, -len * 0.06f), rng, windowSide: 1, scale: 0.82f);
                    BuildSeatedPerson(paxRoot, "RearPaxL", new Vector3(-wid * 0.20f, h * 0.38f, -len * 0.22f), rng, windowSide: -1, scale: 0.82f);
                    BuildSeatedPerson(paxRoot, "RearPaxR", new Vector3(wid * 0.20f, h * 0.38f, -len * 0.22f), rng, windowSide: 1, scale: 0.82f);
                    break;
                }
                case "brt":
                {
                    BuildSeatedPerson(driverRoot, "DriverSeat", new Vector3(-wid * 0.22f, h * 0.38f, len * 0.32f), rng, isDriver: true, windowSide: -1, scale: 0.88f);
                    float[] zs = { len * 0.16f, 0f, -len * 0.16f, -len * 0.30f };
                    for (int i = 0; i < zs.Length; i++)
                    {
                        BuildSeatedPerson(paxRoot, "PaxL" + i, new Vector3(-wid * 0.22f, h * 0.40f, zs[i]), rng, windowSide: -1, scale: 0.86f);
                        BuildSeatedPerson(paxRoot, "PaxR" + i, new Vector3(wid * 0.22f, h * 0.40f, zs[i]), rng, windowSide: 1, scale: 0.86f);
                    }
                    break;
                }
                case "pickup":
                {
                    BuildSeatedPerson(driverRoot, "DriverSeat", new Vector3(-wid * 0.18f, h * 0.36f, len * 0.05f), rng, isDriver: true, windowSide: -1, scale: 0.82f);
                    if (rng.NextDouble() < 0.75)
                        BuildSeatedPerson(paxRoot, "FrontPax", new Vector3(wid * 0.18f, h * 0.36f, len * 0.05f), rng, windowSide: 1, scale: 0.82f);
                    if (rng.NextDouble() < 0.70)
                        BuildSeatedPerson(paxRoot, "BedPax", new Vector3(wid * 0.14f, h * 0.40f, -len * 0.22f), rng, scale: 0.84f);
                    break;
                }
                case "car":
                case "luxury":
                case "sports":
                case "police":
                {
                    string uHex = police ? "#162442" : null;
                    BuildSeatedPerson(driverRoot, "DriverSeat", new Vector3(-wid * 0.18f, h * 0.35f, 0f), rng, isDriver: true, uniformPolice: police, uniformHex: uHex, windowSide: -1, scale: 0.82f);
                    if (police || rng.NextDouble() < 0.65)
                        BuildSeatedPerson(paxRoot, "FrontPax", new Vector3(wid * 0.18f, h * 0.35f, 0f), rng, uniformPolice: police, uniformHex: uHex, windowSide: 1, scale: 0.82f);
                    if (!police && cls.id != "sports" && rng.NextDouble() < 0.45)
                        BuildSeatedPerson(paxRoot, "RearPax", new Vector3(wid * 0.18f, h * 0.35f, -len * 0.14f), rng, windowSide: 1, scale: 0.80f);
                    break;
                }
                case "van":
                case "delivery":
                case "ambulance":
                {
                    BuildSeatedPerson(driverRoot, "DriverSeat", new Vector3(-wid * 0.19f, h * 0.37f, len * 0.16f), rng, isDriver: true, windowSide: -1, scale: 0.84f);
                    if (rng.NextDouble() < 0.65)
                        BuildSeatedPerson(paxRoot, "FrontPax", new Vector3(wid * 0.19f, h * 0.37f, len * 0.16f), rng, windowSide: 1, scale: 0.84f);
                    break;
                }
                default: // truck, tanker, lawma, fire, army
                {
                    string uHex = cls.id == "lawma" ? "#ff6a00" : cls.id == "fire" ? "#c62828" : cls.id == "army" ? "#3b5323" : null;
                    BuildSeatedPerson(driverRoot, "DriverSeat", new Vector3(-wid * 0.20f, h * 0.40f, len * 0.24f), rng, isDriver: true, uniformHex: uHex, windowSide: -1, scale: 0.86f);
                    if (rng.NextDouble() < 0.75)
                        BuildSeatedPerson(paxRoot, "CoDriver", new Vector3(wid * 0.20f, h * 0.40f, len * 0.24f), rng, uniformHex: uHex, windowSide: 1, scale: 0.86f);
                    break;
                }
            }
        }

        static Transform BuildSeatedPerson(Transform parent, string name, Vector3 seatPos, System.Random rng, bool isDriver = false, bool bikePose = false, bool helmet = false, bool hangingSide = false, bool uniformPolice = false, string uniformHex = null, int windowSide = 0, float scale = 0.86f)
        {
            var p = new GameObject(name).transform;
            p.SetParent(parent, false);
            p.localPosition = seatPos;
            p.localScale = Vector3.one * scale;
            if (bikePose) p.localRotation = Quaternion.Euler(isDriver ? 18f : 8f, 0f, 0f);
            else if (hangingSide) p.localRotation = Quaternion.Euler(0f, 8f, -4f);

            var skin = Mat(SkinHex[rng.Next(SkinHex.Length)], 0.35f);
            var top = !string.IsNullOrEmpty(uniformHex) ? Mat(uniformHex, 0.45f) : uniformPolice ? Mat("#1b2845", 0.4f) : Mat(TopHex[rng.Next(TopHex.Length)], 0.4f);
            var pants = Mat("#1e2430", 0.3f);
            var hair = Mat("#141210", 0.25f);

            // Seated torso & shoulders (taller & broader on bikes so riders are unmistakably visible above the tank/fairing)
            float torsoY = bikePose ? 0.24f : 0.20f;
            Vector3 torsoSize = bikePose ? new Vector3(0.34f, 0.44f, 0.24f) : new Vector3(0.30f, 0.38f, 0.20f);
            var torso = Part(p, PrimitiveType.Cube, new Vector3(0f, torsoY, 0f), torsoSize, top);
            torso.name = "Torso";
            if (bikePose && isDriver)
            {
                // High-vis reflector vest worn by Lagos okada riders
                var vest = Part(p, PrimitiveType.Cube, new Vector3(0f, torsoY + 0.01f, 0f), new Vector3(0.36f, 0.36f, 0.26f), Mat("#c8e020", 0.5f));
                vest.name = "Vest";
            }

            // Seated thighs & lower legs (spread wider on bikes so legs straddle outside the fuel tank)
            float legSpread = bikePose ? 0.21f : 0.08f;
            Part(p, PrimitiveType.Cube, new Vector3(-legSpread, 0.04f, 0.13f), new Vector3(0.13f, 0.13f, 0.26f), pants);
            Part(p, PrimitiveType.Cube, new Vector3(legSpread, 0.04f, 0.13f), new Vector3(0.13f, 0.13f, 0.26f), pants);
            if (bikePose)
            {
                Part(p, PrimitiveType.Cube, new Vector3(-legSpread, -0.15f, 0.20f), new Vector3(0.11f, 0.28f, 0.12f), pants);
                Part(p, PrimitiveType.Cube, new Vector3(legSpread, -0.15f, 0.20f), new Vector3(0.11f, 0.28f, 0.12f), pants);
            }

            // Arms
            if (isDriver)
            {
                float armX = bikePose ? 0.19f : 0.15f;
                float armY = bikePose ? 0.27f : 0.22f;
                float armZ = bikePose ? 0.18f : 0.12f;
                Part(p, PrimitiveType.Cube, new Vector3(-armX, armY, armZ), new Vector3(0.10f, 0.10f, 0.28f), top, Quaternion.Euler(14f, 8f, 0f));
                Part(p, PrimitiveType.Cube, new Vector3(armX, armY, armZ), new Vector3(0.10f, 0.10f, 0.28f), top, Quaternion.Euler(14f, -8f, 0f));
                Part(p, PrimitiveType.Sphere, new Vector3(-armX * 0.85f, armY - 0.03f, armZ + 0.13f), new Vector3(0.09f, 0.09f, 0.09f), skin);
                Part(p, PrimitiveType.Sphere, new Vector3(armX * 0.85f, armY - 0.03f, armZ + 0.13f), new Vector3(0.09f, 0.09f, 0.09f), skin);
            }
            else if (hangingSide)
            {
                Part(p, PrimitiveType.Cube, new Vector3(-0.14f, 0.34f, 0.04f), new Vector3(0.09f, 0.26f, 0.09f), top, Quaternion.Euler(-15f, 0f, 10f));
                Part(p, PrimitiveType.Cube, new Vector3(0.14f, 0.24f, 0.06f), new Vector3(0.09f, 0.22f, 0.09f), skin, Quaternion.Euler(0f, 0f, -18f));
            }
            else
            {
                Part(p, PrimitiveType.Cube, new Vector3(-0.15f, 0.18f, 0.04f), new Vector3(0.09f, 0.22f, 0.09f), top);
                Part(p, PrimitiveType.Cube, new Vector3(0.15f, 0.18f, 0.04f), new Vector3(0.09f, 0.22f, 0.09f), top);
            }

            if (windowSide != 0)
            {
                float sx = windowSide * 0.06f;
                var sillArm = Part(p, PrimitiveType.Cube, new Vector3(sx, 0.21f, 0.04f), new Vector3(0.10f, 0.10f, 0.20f), skin);
                sillArm.name = "WindowArm";
                var sillShoulder = Part(p, PrimitiveType.Cube, new Vector3(sx, 0.28f, 0.01f), new Vector3(0.12f, 0.16f, 0.16f), top);
                sillShoulder.name = "TorsoWindow";
            }

            // Neck & Head
            float headY = bikePose ? 0.56f : 0.51f;
            Part(p, PrimitiveType.Cylinder, new Vector3(windowSide * 0.02f, headY - 0.09f, 0.02f), new Vector3(0.09f, 0.06f, 0.09f), skin);
            var head = Part(p, PrimitiveType.Sphere, new Vector3(windowSide * 0.03f, headY, 0.03f), new Vector3(0.22f, 0.24f, 0.22f), skin);
            head.name = "Head";

            if (helmet)
            {
                var hMat = Mat(HelmetHex[rng.Next(HelmetHex.Length)], 0.75f);
                Part(p, PrimitiveType.Sphere, new Vector3(0f, headY + 0.03f, 0.02f), new Vector3(0.27f, 0.26f, 0.28f), hMat);
                Part(p, PrimitiveType.Cube, new Vector3(0f, headY + 0.01f, 0.14f), new Vector3(0.22f, 0.09f, 0.07f), Mat("#141820", 0.9f)); // visor
            }
            else
            {
                Part(p, PrimitiveType.Sphere, new Vector3(windowSide * 0.03f, headY + 0.06f, 0.02f), new Vector3(0.23f, 0.14f, 0.23f), uniformPolice ? Mat("#141d33", 0.5f) : hair);
            }

            return p;
        }
    }
}
