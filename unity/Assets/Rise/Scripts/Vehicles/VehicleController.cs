using UnityEngine;
using Rise.Data;

namespace Rise.Vehicles
{
    // Arcade driving model: approach-based speed with class max / boost / accel,
    // braking, slow reverse, handbrake, steering that firms up with speed and loosens as condition drops.
    // The Rigidbody carries the vehicle so bridge ramps, kerbs and buildings behave; rotation is driven by code.
    public class VehicleController : MonoBehaviour
    {
        [System.NonSerialized] public VehicleClassData Class;
        public float Speed;                 // m/s, negative when reversing
        public float Condition = 100f;      // 0–100, crashes wear it down
        public bool Driven;
        [System.NonSerialized] public float Wet = 1f;   // 0.7 in the rain: less grip, weaker brakes
        [System.NonSerialized] public bool Insured;     // half crash damage
        TextMesh slogan;
        public bool Owned;
        [System.NonSerialized] public string OwnedId;
        public Transform[] Wheels;
        public float LastImpact;            // m/s lost in the last crash, read and cleared by the session
        [System.NonSerialized] public string LastHit;   // what the last crash was against (diagnostics)

        // NFS Garage tuning stages & visual customization state
        public int EngineStage;
        public int TurboStage;
        public int SuspensionStage;
        public int NosStage;
        public int Spoiler;
        public int SkirtKit;
        public int RoofScoop;
        public int Rims;
        public int NeonColor = -1;
        public int PaintFinish;

        public bool IsPremiumRacing => Class != null && (Class.id == "luxury" || Class.id == "sports" || Class.id == "supercar" || Class.id == "muscle");
        public bool IsCustomBuilt => EngineStage > 0 || TurboStage > 0 || SuspensionStage > 0 || NosStage > 0 || Spoiler > 0 || SkirtKit > 0 || RoofScoop > 0 || Rims > 0 || NeonColor >= 0;
        // Only premium racing cars and custom-built / NOS-upgraded cars have Nitrous (NOS)!
        public bool HasNos => IsPremiumRacing || NosStage > 0;
        public int EffectiveNosStage => Mathf.Max(IsPremiumRacing ? 1 : 0, NosStage);

        Rigidbody rb;
        float yaw, doorAnimT;
        Transform occRoot, driverOcc, passengersOcc, doorPivot;

        public bool HasDriver => driverOcc != null && driverOcc.gameObject.activeSelf && driverOcc.childCount > 0;
        public int PassengerCount => passengersOcc != null && passengersOcc.gameObject.activeSelf ? passengersOcc.childCount : 0;

        public void Setup(VehicleClassData cls, Transform[] wheels)
        {
            Class = cls; Wheels = wheels;
            yaw = transform.eulerAngles.y;
            rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();   // Unity's fake-null objects defeat ??
            rb.mass = Mathf.Max(300f, cls.len * cls.wid * 120f);
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.linearDamping = 0.2f;
            rb.isKinematic = true;          // parked until someone drives it
            occRoot = transform.Find("Occupants");
            doorPivot = transform.Find("DoorPivot");
            if (occRoot != null)
            {
                driverOcc = occRoot.Find("Driver");
                passengersOcc = occRoot.Find("Passengers");
            }
            SetOccupants(false, false);     // parked vehicles have no driver inside until entered
        }

        public void TriggerDoorSwing() => doorAnimT = 0.85f;

        public void SetOccupants(bool showDriver, bool showPassengers)
        {
            if (driverOcc != null) driverOcc.gameObject.SetActive(showDriver);
            if (passengersOcc != null) passengersOcc.gameObject.SetActive(showPassengers);
        }

        public void StyleDriver(Color skin, Color top)
        {
            if (driverOcc == null) return;
            foreach (var r in driverOcc.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.name == "Head" || r.name == "WindowArm") r.material.color = skin;
                else if (r.name == "Torso" || r.name == "TorsoWindow") r.material.color = top;
            }
        }

        // Move a vehicle: the Rigidbody (interpolated) keeps its own pose, so the transform alone is not enough.
        public void Place(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            if (rb != null) { rb.position = position; rb.rotation = rotation; if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; } }   // kinematic (parked) bodies have no velocity to clear
            yaw = rotation.eulerAngles.y;
            Physics.SyncTransforms();
        }

        public void SetDriven(bool driven)
        {
            Driven = driven;
            rb.isKinematic = !driven;
            if (!driven) Speed = 0f;
            yaw = transform.eulerAngles.y;
            SetOccupants(driven, false);
            TriggerDoorSwing();
        }

        void Update()
        {
            if (doorPivot == null) return;
            if (doorAnimT > 0f)
            {
                doorAnimT = Mathf.Max(0f, doorAnimT - Time.deltaTime);
                float phase = 1f - (doorAnimT / 0.85f);
                float open = Mathf.Sin(phase * Mathf.PI);
                doorPivot.localRotation = Quaternion.Euler(0f, -open * 54f, 0f);
            }
            else if (doorPivot.localRotation != Quaternion.identity)
            {
                doorPivot.localRotation = Quaternion.identity;
            }
        }

        // gas / brake 0–1, steer -1 (left) … 1 (right)
        public void Drive(float gas, float brake, bool boost, bool handbrake, float steer, float fuel01, float dt)
        {
            bool hasFuel = fuel01 > 0.001f;
            float engineMult = 1f + EngineStage * 0.12f;
            float turboBoostMult = 1f + TurboStage * 0.10f;
            float accelMult = (1f + EngineStage * 0.12f) * (1f + TurboStage * 0.15f);
            float brakeMult = 1f + SuspensionStage * 0.15f;
            float gripMult = 1f + SuspensionStage * 0.12f;

            float baseMax = (boost && HasNos) ? Class.boost * turboBoostMult : Class.max * (1f + TurboStage * 0.06f);
            float maxF = hasFuel ? baseMax * engineMult * (Condition < 30f ? 0.5f : 1f) * Wet : 0f;

            if (handbrake) Speed = Mathf.MoveTowards(Speed, 0f, 45f * brakeMult * dt);
            else if (!hasFuel)
            {
                // Out of fuel: engine stalls — cannot accelerate forward or reverse; coast or brake to a stop.
                float decel = brake > 0.05f ? 28f * Wet * brakeMult * brake : 8.5f;
                Speed = Mathf.MoveTowards(Speed, 0f, decel * dt);
            }
            else if (gas > 0.05f) Speed = Mathf.Min(maxF, Speed + (Speed < 0f ? 30f : (boost && HasNos ? Class.accel * 1.35f : Class.accel) * accelMult) * gas * dt);
            else if (brake > 0.05f) Speed = Speed > 0.5f ? Mathf.Max(0f, Speed - 28f * Wet * brakeMult * brake * dt) : Mathf.Max(-7f, Speed - 10f * brake * dt);
            else Speed = Mathf.MoveTowards(Speed, 0f, 6f * dt);

            if (Mathf.Abs(Speed) > 0.3f)
                yaw += steer * 2.3f * gripMult * Wet * Mathf.Rad2Deg * (0.6f + 0.4f * Condition / 100f) * Mathf.Min(1f, Mathf.Abs(Speed) / 9f) * dt * Mathf.Sign(Speed);

            if (Wheels != null) foreach (var w in Wheels) VehicleBodies.Spin(w, Speed * dt * 120f);
        }

        void FixedUpdate()
        {
            if (!Driven || rb == null || rb.isKinematic) return;
            var v = transform.forward * Speed;
            v.y = rb.linearVelocity.y;
            rb.linearVelocity = v;
            rb.MoveRotation(Quaternion.Euler(0f, yaw, 0f));
        }

        void OnCollisionEnter(Collision c)
        {
            if (!Driven || c.contactCount == 0) return;
            // Ground contacts (road, bridge deck, kerb top) are not crashes: ignore road/bridge surfaces and low wheel-level
            // contacts, and only count horizontal impacts above bumper height.
            string hitName = c.gameObject.name;
            if (hitName == "Deck" || hitName == "Carriageway" || hitName == "Ground" || hitName == "Pavement") return;
            var contact = c.GetContact(0);
            if (contact.point.y < transform.position.y + 0.45f) return;
            var n = contact.normal;
            if (Mathf.Abs(n.y) > 0.5f) return;
            float square = Mathf.Abs(Vector3.Dot(n, transform.forward));   // 1 head-on, 0 side scrape
            float closing = Mathf.Abs(Vector3.Dot(c.relativeVelocity, n));
            if (closing < 3.5f) return;
            float lost = Mathf.Abs(Speed) * Mathf.Clamp(0.15f + 0.75f * square, 0f, 0.9f);
            Speed -= Mathf.Sign(Speed) * lost;
            Condition = Mathf.Max(0f, Condition - lost * 1.2f * (Insured ? 0.5f : 1f));
            LastImpact = lost;
            LastHit = $"{hitName} n={n} closing={closing:0.0} square={square:0.00} at {contact.point}";
        }

        public float Kmh => Mathf.Abs(Speed) * 3.6f;

        static (float gloss, float metallic) FinishParams(int finish) => finish switch
        {
            1 => (0.92f, 0.72f), // Metallic Flake
            2 => (0.96f, 0.85f), // Pearlescent / Candy
            3 => (0.22f, 0.08f), // Stealth Matte
            4 => (0.98f, 0.96f), // 24K Gold / Chrome Mirror
            _ => (0.85f, 0.35f), // Factory Gloss
        };

        // Repaint: only exterior body paint materials, keeping glass, tires, wheels, chrome and interior intact.
        public void ApplyLivery(Color c)
        {
            var (gloss, metallic) = FinishParams(PaintFinish);
            VehicleBodies.PaintBodyMaterials(gameObject, c, gloss, metallic);
        }

        public void ApplyLivery(string hex, int finish = -1)
        {
            if (finish >= 0) PaintFinish = finish;
            var (gloss, metallic) = FinishParams(PaintFinish);
            VehicleBodies.PaintBodyMaterials(gameObject, hex, gloss, metallic);
        }

        public static readonly string[] NeonHexPalette =
        {
            "#00f5ff", // Cyber Cyan
            "#ff1493", // Tokyo Magenta
            "#39ff14", // Nitrous Lime
            "#ff8c00", // Lagos Gold Amber
            "#8b5cf6", // Ultra Violet
            "#ff2400", // Diablo Red
        };

        public static readonly string[] RimHexPalette =
        {
            "#d4d8dd", // Factory Alloy
            "#ffd700", // 24K Gold BBS
            "#111317", // Forged Carbon Black
            "#e5e7eb", // Chrome Deep-Dish
            "#ff3b1f", // Track Anodized Red
            "#00d8ff", // Nismo Cyber Blue
        };

        static readonly Material[] SharedNeonMats = new Material[6];

        public void ApplyOwnedCustomizations(OwnedVehicle o, WorldData data = null)
        {
            if (o == null) return;
            Insured = o.insured;
            Condition = o.cond;
            EngineStage = o.engineStage;
            TurboStage = o.turboStage;
            SuspensionStage = o.suspensionStage;
            NosStage = o.nosStage;
            Spoiler = o.spoiler;
            SkirtKit = o.skirtKit;
            RoofScoop = o.roofScoop;
            Rims = o.rims;
            NeonColor = o.neonColor;
            PaintFinish = o.paintFinish;

            if (!string.IsNullOrEmpty(o.customHex))
                ApplyLivery(o.customHex, o.paintFinish);
            else if (data != null && o.livery >= 0 && o.livery < data.liveries.Length)
                ApplyLivery(data.liveries[o.livery], o.paintFinish);

            if (data != null && o.slogan >= 0 && o.slogan < data.slogans.Length)
                ApplySlogan(data.slogans[o.slogan]);

            RebuildNfsVisualMods();
        }

        public void RebuildNfsVisualMods()
        {
            var existing = transform.Find("NfsMods");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }
            if (Wheels != null)
            {
                for (int i = 0; i < Wheels.Length; i++)
                {
                    if (Wheels[i] == null) continue;
                    var existingCap = Wheels[i].Find("CustomRimCap");
                    if (existingCap != null)
                    {
                        if (Application.isPlaying) Destroy(existingCap.gameObject);
                        else DestroyImmediate(existingCap.gameObject);
                    }
                }
            }

            if (Spoiler <= 0 && SkirtKit <= 0 && RoofScoop <= 0 && Rims <= 0 && NeonColor < 0 && NosStage <= 0)
                return;

            var root = new GameObject("NfsMods").transform;
            root.SetParent(transform, false);

            var info = GetComponent<VehicleModelInfo>();
            Vector3 sz = info != null && info.Length > 0.1f ? new Vector3(info.Width, info.Height, info.Length) : new Vector3(Class != null ? Class.wid : 1.9f, 1.45f, Class != null ? Class.len : 4.4f);
            float wid = Mathf.Max(0.85f, sz.x);
            float h = Mathf.Max(1.05f, sz.y);
            float len = Mathf.Max(2.0f, sz.z);

            var carbonMat = VehicleBodies.Mat("#14171c", 0.78f, 0.45f);
            var chromeMat = VehicleBodies.Mat("#d8dee9", 0.94f, 0.90f);
            var accentMat = VehicleBodies.Mat(NeonColor >= 0 && NeonColor < NeonHexPalette.Length ? NeonHexPalette[NeonColor] : "#f29b0a", 0.88f, 0.55f);

            // 1. Rear Spoiler / Carbon GT Wing / Time-Attack Aero Wing
            if (Spoiler > 0)
            {
                float trunkY = Mathf.Clamp(h * 0.68f, 0.78f, 1.38f);
                float trunkZ = -len * 0.42f;
                float wingH = Spoiler == 1 ? 0.16f : Spoiler == 2 ? 0.28f : 0.36f;
                float wingW = wid * (Spoiler == 1 ? 0.86f : Spoiler == 2 ? 0.98f : 1.08f);
                var spRoot = new GameObject("Spoiler").transform;
                spRoot.SetParent(root, false);
                spRoot.localPosition = new Vector3(0f, trunkY, trunkZ);

                CreateModPart(spRoot, "StrutL", PrimitiveType.Cube, new Vector3(-wingW * 0.30f, wingH * 0.5f, 0f), new Vector3(0.05f, wingH, 0.18f), carbonMat);
                CreateModPart(spRoot, "StrutR", PrimitiveType.Cube, new Vector3(wingW * 0.30f, wingH * 0.5f, 0f), new Vector3(0.05f, wingH, 0.18f), carbonMat);
                CreateModPart(spRoot, "MainBlade", PrimitiveType.Cube, new Vector3(0f, wingH, -0.04f), new Vector3(wingW, 0.04f, Spoiler >= 2 ? 0.28f : 0.20f), carbonMat, Quaternion.Euler(8f, 0f, 0f));
                CreateModPart(spRoot, "EndplateL", PrimitiveType.Cube, new Vector3(-wingW * 0.5f, wingH + 0.02f, -0.04f), new Vector3(0.03f, 0.16f, 0.30f), accentMat);
                CreateModPart(spRoot, "EndplateR", PrimitiveType.Cube, new Vector3(wingW * 0.5f, wingH + 0.02f, -0.04f), new Vector3(0.03f, 0.16f, 0.30f), accentMat);
                if (Spoiler >= 3)
                    CreateModPart(spRoot, "UpperDeck", PrimitiveType.Cube, new Vector3(0f, wingH + 0.11f, -0.08f), new Vector3(wingW * 0.94f, 0.03f, 0.20f), accentMat, Quaternion.Euler(14f, 0f, 0f));
            }

            // 2. Widebody Front Carbon Splitter + Side Skirts + Rear Diffuser + Twin Titanium Exhausts
            if (SkirtKit > 0 || NosStage > 0)
            {
                var kitRoot = new GameObject("BodyKit").transform;
                kitRoot.SetParent(root, false);
                if (SkirtKit > 0)
                {
                    float flare = SkirtKit >= 2 ? 0.07f : 0.03f;
                    CreateModPart(kitRoot, "FrontSplitter", PrimitiveType.Cube, new Vector3(0f, 0.17f, len * 0.48f), new Vector3(wid + flare * 2f, 0.05f, 0.26f), carbonMat);
                    CreateModPart(kitRoot, "SkirtL", PrimitiveType.Cube, new Vector3(-(wid * 0.5f + flare * 0.5f), 0.18f, 0f), new Vector3(0.08f + flare, 0.08f, len * 0.62f), carbonMat);
                    CreateModPart(kitRoot, "SkirtR", PrimitiveType.Cube, new Vector3(wid * 0.5f + flare * 0.5f, 0.18f, 0f), new Vector3(0.08f + flare, 0.08f, len * 0.62f), carbonMat);
                    CreateModPart(kitRoot, "RearDiffuser", PrimitiveType.Cube, new Vector3(0f, 0.18f, -len * 0.47f), new Vector3(wid * 0.82f, 0.07f, 0.20f), carbonMat);
                }
                // Dual Titanium Exhaust Tips
                CreateModPart(kitRoot, "ExhaustL", PrimitiveType.Cylinder, new Vector3(-wid * 0.26f, 0.22f, -len * 0.49f), new Vector3(0.11f, 0.14f, 0.11f), chromeMat, Quaternion.Euler(90f, 0f, 0f));
                CreateModPart(kitRoot, "ExhaustR", PrimitiveType.Cylinder, new Vector3(wid * 0.26f, 0.22f, -len * 0.49f), new Vector3(0.11f, 0.14f, 0.11f), chromeMat, Quaternion.Euler(90f, 0f, 0f));
            }

            // 3. Roof Air Scoop
            if (RoofScoop > 0)
            {
                float roofY = Mathf.Clamp(h * 0.94f, 1.10f, 1.85f);
                var scoop = CreateModPart(root, "RoofScoop", PrimitiveType.Cube, new Vector3(0f, roofY, len * 0.04f), new Vector3(wid * (RoofScoop >= 2 ? 0.36f : 0.24f), 0.10f, 0.46f), carbonMat);
                CreateModPart(scoop, "IntakeLip", PrimitiveType.Cube, new Vector3(0f, 0.15f, 0.45f), new Vector3(0.92f, 0.55f, 0.15f), accentMat);
            }

            // 4. Custom Rims
            if (Rims > 0 && Wheels != null)
            {
                string rimHex = RimHexPalette[Mathf.Clamp(Rims, 0, RimHexPalette.Length - 1)];
                var rimMat = VehicleBodies.Mat(rimHex, 0.92f, 0.85f);
                for (int i = 0; i < Wheels.Length; i++)
                {
                    var w = Wheels[i];
                    if (w == null) continue;
                    var existingCap = w.Find("CustomRimCap");
                    if (existingCap != null)
                    {
                        if (Application.isPlaying) Destroy(existingCap.gameObject);
                        else DestroyImmediate(existingCap.gameObject);
                    }
                    float side = w.localPosition.x < 0f ? -0.07f : 0.07f;
                    var cap = CreateModPart(w, "CustomRimCap", PrimitiveType.Cylinder, new Vector3(side, 0f, 0f), new Vector3(0.38f, 0.03f, 0.38f), rimMat, Quaternion.Euler(0f, 0f, 90f));
                    CreateModPart(cap, "SpokeA", PrimitiveType.Cube, Vector3.zero, new Vector3(0.92f, 1.25f, 0.16f), rimMat);
                    CreateModPart(cap, "SpokeB", PrimitiveType.Cube, Vector3.zero, new Vector3(0.16f, 1.25f, 0.92f), rimMat);
                }
            }

            // 5. 3D Neon Underglow Kit + Point Light
            if (NeonColor >= 0 && NeonColor < NeonHexPalette.Length)
            {
                string nHex = NeonHexPalette[NeonColor];
                ColorUtility.TryParseHtmlString(nHex, out var nCol);
                var neonMat = SharedNeonMats[NeonColor];
                if (neonMat == null)
                {
                    neonMat = new Material(ShaderLib.Unlit) { name = "NFS_Neon_" + NeonColor, color = nCol, enableInstancing = true };
                    if (neonMat.HasProperty("_BaseColor")) neonMat.SetColor("_BaseColor", nCol);
                    SharedNeonMats[NeonColor] = neonMat;
                }

                var neonRoot = new GameObject("NeonUnderglow").transform;
                neonRoot.SetParent(root, false);
                CreateModPart(neonRoot, "NeonL", PrimitiveType.Cube, new Vector3(-wid * 0.44f, 0.11f, 0f), new Vector3(0.06f, 0.03f, len * 0.68f), neonMat);
                CreateModPart(neonRoot, "NeonR", PrimitiveType.Cube, new Vector3(wid * 0.44f, 0.11f, 0f), new Vector3(0.06f, 0.03f, len * 0.68f), neonMat);
                CreateModPart(neonRoot, "NeonF", PrimitiveType.Cube, new Vector3(0f, 0.11f, len * 0.38f), new Vector3(wid * 0.76f, 0.03f, 0.06f), neonMat);
                CreateModPart(neonRoot, "NeonB", PrimitiveType.Cube, new Vector3(0f, 0.11f, -len * 0.38f), new Vector3(wid * 0.76f, 0.03f, 0.06f), neonMat);

                var glowObj = new GameObject("NeonLight");
                glowObj.transform.SetParent(neonRoot, false);
                glowObj.transform.localPosition = new Vector3(0f, 0.24f, 0f);
                var lt = glowObj.AddComponent<Light>();
                lt.type = LightType.Point;
                lt.color = nCol;
                lt.range = 5.8f;
                lt.intensity = 2.8f;
                lt.shadows = LightShadows.None;
            }
        }

        static Transform CreateModPart(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, Quaternion? rot = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (rot.HasValue) go.transform.localRotation = rot.Value;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        // A danfo slogan painted above the windscreen.
        public void ApplySlogan(string text)
        {
            if (slogan == null)
            {
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var go = new GameObject("Slogan"); go.transform.SetParent(transform, false);
                float roof = GetComponent<VehicleModelInfo>() != null ? GetComponent<VehicleModelInfo>().Height : 2.1f; go.transform.localPosition = new Vector3(0f, roof + 0.45f, 0f); go.transform.localRotation = Quaternion.identity;
                slogan = go.AddComponent<TextMesh>(); slogan.font = font; slogan.fontSize = 56; slogan.characterSize = 0.05f; slogan.fontStyle = FontStyle.Bold; slogan.anchor = TextAnchor.MiddleCenter; slogan.alignment = TextAlignment.Center; slogan.color = new Color(0.03f, 0.06f, 0.05f);
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                var board = GameObject.CreatePrimitive(PrimitiveType.Cube); board.name = "Slogan board"; Destroy(board.GetComponent<Collider>()); board.transform.SetParent(transform, false); board.transform.localPosition = new Vector3(0f, roof + 0.45f, 0.05f); board.transform.localScale = new Vector3(2.1f, 0.4f, 0.05f);
                board.GetComponent<MeshRenderer>().sharedMaterial = VehicleBodies.Mat("#f5c518", 0.5f);
            }
            slogan.text = text;
        }
    }
}
