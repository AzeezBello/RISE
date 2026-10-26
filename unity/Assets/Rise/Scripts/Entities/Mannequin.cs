using UnityEngine;

namespace Rise.Entities
{
    // Detailed anatomical streetwear human used for characters, guards, and fallback rigs:
    // sculpted V-taper torso, shoulders, upper/forearms with tattoo & wristwatch, belt, tapered trousers,
    // two-tone streetwear sneakers, sculpted head with beard/hair fade, sunglasses, and Cuban link chain.
    public static class Mannequin
    {
        static Shader standard;
        static readonly System.Collections.Generic.Dictionary<string, Material> cache = new System.Collections.Generic.Dictionary<string, Material>();

        public static Material Mat(Color c, float smoothness = 0.28f, float metallic = 0.04f)
        {
            string key = $"{c.r:F2}_{c.g:F2}_{c.b:F2}_{smoothness:F2}_{metallic:F2}";
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            standard ??= ShaderLib.Lit;
            m = new Material(standard) { color = c };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            return cache[key] = m;
        }

        public static Color Hex(string hex, string fallback = "#b07a55")
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c)) return c;
            ColorUtility.TryParseHtmlString(fallback, out c);
            return c;
        }

        public static GameObject Build(Transform parent, Color top, Color pants, Color skin, Color? fila, int layer = 0, float scale = 1f)
        {
            var root = new GameObject("Body") { layer = layer };
            root.transform.SetParent(parent, false);
            root.transform.localScale = Vector3.one * scale;

            var skinMat = Mat(skin, 0.36f, 0.04f);
            var topMat = Mat(top, 0.28f, 0.05f);
            var pantsMat = Mat(pants, 0.26f, 0.04f);
            var hairMat = Mat(new Color(0.08f, 0.06f, 0.05f), 0.25f, 0.02f);
            var soleMat = Mat(new Color(0.94f, 0.95f, 0.96f), 0.45f, 0.05f);
            var shoeUpperMat = Mat(Color.Lerp(pants, new Color(0.14f, 0.15f, 0.18f), 0.6f), 0.42f, 0.08f);
            var beltMat = Mat(new Color(0.12f, 0.11f, 0.10f), 0.55f, 0.2f);
            var chainMat = Mat(new Color(0.92f, 0.85f, 0.65f), 0.88f, 0.92f);
            var shadowMat = Mat(new Color(0.03f, 0.04f, 0.04f), 0.1f, 0f);

            // Ground contact shadow
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.01f, 0f), new Vector3(0.58f, 0.006f, 0.58f), shadowMat, layer);

            // Two-tone streetwear sneakers (white vulcanized midsole + upper + toe cap) & crew socks under hip leg pivots
            foreach (float side in new[] { -1f, 1f })
            {
                float sx = side * 0.115f;
                var legPivot = new GameObject(side < 0f ? "LeftLegPivot" : "RightLegPivot") { layer = layer };
                legPivot.transform.SetParent(root.transform, false);
                legPivot.transform.localPosition = new Vector3(sx, 0.82f, 0f);

                Part(legPivot, PrimitiveType.Cube, new Vector3(0f, -0.79f, 0.03f), new Vector3(0.115f, 0.055f, 0.25f), soleMat, layer);
                Part(legPivot, PrimitiveType.Cube, new Vector3(0f, -0.745f, 0.02f), new Vector3(0.11f, 0.065f, 0.22f), shoeUpperMat, layer);
                Part(legPivot, PrimitiveType.Cylinder, new Vector3(0f, -0.66f, 0f), new Vector3(0.105f, 0.07f, 0.105f), soleMat, layer);
                // Tapered trouser legs (thigh + calf)
                Part(legPivot, PrimitiveType.Capsule, new Vector3(0f, -0.46f, 0f), new Vector3(0.145f, 0.24f, 0.145f), pantsMat, layer);
                Part(legPivot, PrimitiveType.Capsule, new Vector3(0f, -0.14f, 0f), new Vector3(0.165f, 0.24f, 0.165f), pantsMat, layer);
            }

            // Pelvis & leather belt with metallic buckle
            Part(root, PrimitiveType.Cube, new Vector3(0f, 0.88f, 0f), new Vector3(0.36f, 0.12f, 0.23f), pantsMat, layer);
            Part(root, PrimitiveType.Cube, new Vector3(0f, 0.94f, 0f), new Vector3(0.37f, 0.045f, 0.24f), beltMat, layer);
            Part(root, PrimitiveType.Cube, new Vector3(0f, 0.94f, 0.122f), new Vector3(0.065f, 0.048f, 0.015f), chainMat, layer);

            // Sculpted V-taper waist + athletic upper chest + collar rib
            Part(root, PrimitiveType.Cube, new Vector3(0f, 1.08f, 0f), new Vector3(0.37f, 0.24f, 0.23f), topMat, layer);
            Part(root, PrimitiveType.Cube, new Vector3(0f, 1.30f, 0f), new Vector3(0.44f, 0.28f, 0.26f), topMat, layer);
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, 1.45f, 0f), new Vector3(0.17f, 0.025f, 0.17f), topMat, layer);

            // Cuban link neck chain resting on collarbone
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, 1.42f, 0.04f), new Vector3(0.21f, 0.012f, 0.20f), chainMat, layer, Quaternion.Euler(18f, 0f, 0f));

            // Articulated shoulder pivots with deltoid shoulders, upper arms, forearms, hands & wristwatch
            foreach (float side in new[] { -1f, 1f })
            {
                var armPivot = new GameObject(side < 0f ? "LeftArmPivot" : "RightArmPivot") { layer = layer };
                armPivot.transform.SetParent(root.transform, false);
                armPivot.transform.localPosition = new Vector3(side * 0.25f, 1.38f, 0f);

                Part(armPivot, PrimitiveType.Sphere, new Vector3(-side * 0.01f, 0f, 0f), new Vector3(0.15f, 0.15f, 0.15f), topMat, layer);
                Part(armPivot, PrimitiveType.Cylinder, new Vector3(side * 0.02f, -0.10f, 0f), new Vector3(0.125f, 0.11f, 0.125f), topMat, layer);
                Part(armPivot, PrimitiveType.Capsule, new Vector3(side * 0.02f, -0.33f, 0.02f), new Vector3(0.105f, 0.18f, 0.105f), skinMat, layer);
                var handGo = Part(armPivot, PrimitiveType.Sphere, new Vector3(side * 0.02f, -0.51f, 0.03f), new Vector3(0.095f, 0.11f, 0.095f), skinMat, layer);
                handGo.name = side < 0f ? "LeftHand" : "RightHand";
                if (side < 0f)
                    Part(armPivot, PrimitiveType.Cylinder, new Vector3(side * 0.02f, -0.45f, 0.025f), new Vector3(0.112f, 0.018f, 0.112f), chainMat, layer);
            }

            // Neck, sculpted head, jawline beard, eyes, fade hair & headwear
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, 1.50f, 0f), new Vector3(0.125f, 0.06f, 0.125f), skinMat, layer);
            Part(root, PrimitiveType.Sphere, new Vector3(0f, 1.66f, 0f), new Vector3(0.24f, 0.28f, 0.25f), skinMat, layer);
            Part(root, PrimitiveType.Cube, new Vector3(0f, 1.58f, 0.04f), new Vector3(0.18f, 0.09f, 0.17f), hairMat, layer); // sculpted beard / jawline
            Part(root, PrimitiveType.Sphere, new Vector3(0f, 1.74f, -0.01f), new Vector3(0.25f, 0.15f, 0.26f), hairMat, layer); // fade / afro hair top
            Part(root, PrimitiveType.Cube, new Vector3(0f, 1.68f, 0.12f), new Vector3(0.18f, 0.042f, 0.04f), Mat(new Color(0.06f, 0.08f, 0.11f), 0.92f, 0.4f), layer); // visor/sunglasses

            if (fila.HasValue)
                Part(root, PrimitiveType.Cylinder, new Vector3(0f, 1.81f, 0f), new Vector3(0.25f, 0.055f, 0.25f), Mat(fila.Value, 0.35f), layer);
            return root;
        }

        static GameObject Part(GameObject root, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, int layer, Quaternion? rot = null)
        {
            var p = GameObject.CreatePrimitive(type);
            p.layer = layer; p.transform.SetParent(root.transform, false); p.transform.localPosition = pos; p.transform.localScale = scale;
            if (rot.HasValue) p.transform.localRotation = rot.Value;
            p.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var col = p.GetComponent<Collider>();
            if (Application.isPlaying) Object.Destroy(col); else Object.DestroyImmediate(col);
            return p;
        }

        // Animates the articulated Mannequin limbs (RightArmPivot, LeftArmPivot, RightLegPivot, LeftLegPivot) during punches, kicks, melee slashes, and gun aiming.
        public static void ApplyCombatPose(Transform host, float swing01, float guard01, int comboStep, string weaponId, bool isGun)
        {
            if (host == null) return;
            var body = host.Find("Body") ?? host;
            var rArm = CharacterRig.Find(body, "RightArmPivot");
            var lArm = CharacterRig.Find(body, "LeftArmPivot");
            var rLeg = CharacterRig.Find(body, "RightLegPivot");
            var lLeg = CharacterRig.Find(body, "LeftLegPivot");
            var wRoot = CharacterRig.Find(host, "WeaponProp");
            if (rArm == null && lArm == null) return;

            bool hasWeapon = !string.IsNullOrEmpty(weaponId);
            if (isGun)
            {
                float aim = Mathf.Max(guard01, swing01 > 0f ? 1f : 0.45f);
                float kick = swing01 > 0f ? Mathf.Sin(swing01 * Mathf.PI) : 0f;
                if (rArm != null) rArm.localRotation = Quaternion.Euler(-85f * aim - 18f * kick, -6f * aim, 0f);
                if (lArm != null) lArm.localRotation = Quaternion.Euler(-72f * aim - 10f * kick, 26f * aim, 0f);
                if (rLeg != null) rLeg.localRotation = Quaternion.identity;
                if (lLeg != null) lLeg.localRotation = Quaternion.identity;
                if (wRoot != null) wRoot.localRotation = Quaternion.Euler(75f - 18f * kick, 0f, 0f);
                return;
            }

            if (swing01 > 0.001f)
            {
                float s = Mathf.Sin(swing01 * Mathf.PI);
                int step = Mathf.Abs(comboStep) % 3;
                if (hasWeapon)
                {
                    float slash = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.16f, 0.65f, swing01));
                    if (step == 0)
                    {
                        if (rArm != null) rArm.localRotation = Quaternion.Euler(Mathf.Lerp(-140f, -35f, slash) * s, Mathf.Lerp(26f, -34f, slash) * s, 18f * s);
                        if (lArm != null) lArm.localRotation = Quaternion.Euler(-48f * s, 18f * s, -12f * s);
                    }
                    else if (step == 1)
                    {
                        float yaw = Mathf.Lerp(-46f, 48f, slash) * s;
                        if (rArm != null) rArm.localRotation = Quaternion.Euler(-88f * s, yaw, 24f * s);
                        if (lArm != null) lArm.localRotation = Quaternion.Euler(-45f * s, -22f * s, -14f * s);
                    }
                    else
                    {
                        if (rArm != null) rArm.localRotation = Quaternion.Euler(Mathf.Lerp(-145f, -28f, slash) * s, -12f * s, 14f * s);
                        if (lArm != null) lArm.localRotation = Quaternion.Euler(Mathf.Lerp(-120f, -36f, slash) * s, 18f * s, -14f * s);
                        if (rLeg != null) rLeg.localRotation = Quaternion.Euler(-65f * s, 0f, 0f);
                    }
                    if (wRoot != null) wRoot.localRotation = Quaternion.Euler(75f + Mathf.Lerp(-35f, 80f, slash) * s, -slash * 25f * s, 0f);
                }
                else
                {
                    float punch = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.14f, 0.55f, swing01));
                    if (step == 0)
                    {
                        if (rArm != null) rArm.localRotation = Quaternion.Euler(Mathf.Lerp(-45f, -96f, punch) * s, -18f * punch * s, 12f * s);
                        if (lArm != null) lArm.localRotation = Quaternion.Euler(-68f * s, 22f * s, -10f * s);
                        if (rLeg != null) rLeg.localRotation = Quaternion.Euler(-18f * s, 0f, 0f);
                    }
                    else if (step == 1)
                    {
                        if (lArm != null) lArm.localRotation = Quaternion.Euler(Mathf.Lerp(-45f, -94f, punch) * s, 28f * punch * s, -16f * s);
                        if (rArm != null) rArm.localRotation = Quaternion.Euler(-68f * s, -22f * s, 10f * s);
                        if (lLeg != null) lLeg.localRotation = Quaternion.Euler(-18f * s, 0f, 0f);
                    }
                    else
                    {
                        if (rLeg != null) rLeg.localRotation = Quaternion.Euler(-85f * punch * s, 0f, 0f);
                        if (rArm != null) rArm.localRotation = Quaternion.Euler(-82f * s, -14f * s, 16f * s);
                        if (lArm != null) lArm.localRotation = Quaternion.Euler(-70f * s, 14f * s, -16f * s);
                    }
                }
                return;
            }

            float g = Mathf.Clamp01(guard01);
            if (hasWeapon)
            {
                float w = Mathf.Max(0.42f, g);
                if (rArm != null) rArm.localRotation = Quaternion.Euler(-34f * w, -8f * w, 12f * w);
                if (lArm != null) lArm.localRotation = Quaternion.Euler(-48f * g, 16f * g, -10f * g);
                if (wRoot != null) wRoot.localRotation = Quaternion.Euler(75f, 0f, 0f);
            }
            else if (g > 0.01f)
            {
                if (rArm != null) rArm.localRotation = Quaternion.Euler(-65f * g, -18f * g, 10f * g);
                if (lArm != null) lArm.localRotation = Quaternion.Euler(-72f * g, 20f * g, -10f * g);
            }
            else
            {
                if (rArm != null) rArm.localRotation = Quaternion.identity;
                if (lArm != null) lArm.localRotation = Quaternion.identity;
            }
            if (rLeg != null) rLeg.localRotation = Quaternion.identity;
            if (lLeg != null) lLeg.localRotation = Quaternion.identity;
        }

        // A floating name label that faces the camera horizontally so it never renders mirrored or overlapping.
        public static void Label(Transform parent, string text, Color colour, float height = 2.25f, float size = 0.6f)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, height, 0);
            var tm = go.AddComponent<TextMesh>();
            tm.text = text; tm.font = font; tm.fontSize = 64; tm.characterSize = 0.07f * size; tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = colour;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            go.AddComponent<FacingLabel>();
        }
    }

    public class FacingLabel : MonoBehaviour
    {
        public bool BoardSign;
        public Vector3 BasePos;
        public float OffsetZ = 0.18f;
        public float BoardYaw = 0f;

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            if (BoardSign)
            {
                Quaternion rot = Quaternion.Euler(0f, BoardYaw, 0f);
                Vector3 normal = rot * Vector3.forward;
                bool front = Vector3.Dot(cam.transform.position - BasePos, normal) >= 0f;
                transform.position = BasePos + normal * (front ? OffsetZ : -OffsetZ);
                transform.rotation = front ? Quaternion.Euler(0f, BoardYaw + 180f, 0f) : rot;
                return;
            }
            var d = transform.position - cam.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(d, Vector3.up);
        }
    }
}
