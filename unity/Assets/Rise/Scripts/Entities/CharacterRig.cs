using System.Collections.Generic;
using UnityEngine;
using Rise.Data;

namespace Rise.Entities
{
    // A character's look: fabric print index, trousers, skin, hair, cap.
    public struct Look
    {
        public int Fabric; public Color Pants, Skin, Hair; public Color? Cap;
        // Wardrobe indices: outfit, bodyType (Slim/Average/Big), hair style, facial hair, accessory
        public int Outfit, BodyType, HairStyle, FacialHair, Accessory;
        public static Look Default => new Look
        {
            Fabric = 0,
            Pants = Mannequin.Hex("#26312d"),
            Skin = Mannequin.Hex("#714835"),
            Hair = Mannequin.Hex("#120b08"),
            Cap = Mannequin.Hex("#1f3a5f"),
            Outfit = 5,     // T-shirt & jeans streetwear default
            BodyType = 1,
            HairStyle = 1,  // Fade
            FacialHair = 1, // Full Beard
            Accessory = 4   // Cuban Chain
        };
    }

    // One instanced rig: the Animation driving it and the clothing materials to re-dress it.
    public class RigInstance
    {
        public Animation Anim; public GameObject Root; public bool Cesium;
        public readonly List<Material> Top = new List<Material>(), Bottom = new List<Material>(), Skin = new List<Material>(), Hair = new List<Material>(), Cap = new List<Material>();
        public readonly List<Renderer> HairRenderers = new List<Renderer>(), BeardRenderers = new List<Renderer>(), CapRenderers = new List<Renderer>();
        // Multi-zone garment materials created from split submeshes on Wolf3D_Outfit_Top, Wolf3D_Outfit_Bottom, Wolf3D_Outfit_Footwear
        public Material SleeveUpperMat, SleeveForeMat, LegCalfMat, SockMat, ShoeSoleMat, ShoeUpperMat;
        public Transform Overlay, AgbadaWings, JacketRoot, CollarPlacket, BeltRoot;
        public Transform HairShort, HairFade, HairAfro, HairBraids;
        public Transform BeardFull, BeardGoatee, BeardMoustache;
        public Transform FilaCap, FaceCap;
        public Transform Glasses, Chain, Watch, TattooL, TattooR, WeaponRoot;
        public Material OverlayMaterial, JacketMaterial, PlacketMaterial, TattooMaterial;
        public Transform Hips, Spine, Spine1, Spine2, Neck, Head;
        public Transform RightShoulder, RightArm, RightForeArm, RightHand;
        public Transform LeftShoulder, LeftArm, LeftForeArm, LeftHand;
        public Transform RightUpLeg, RightLeg, LeftUpLeg, LeftLeg;
        public float BaseScale = 1f;
        public string State = "idle";

        // Cross-fade to a locomotion or action state (idle, walk, run, jump, turn, enter_vehicle, exit_vehicle, attack); clipSpeed scales the clip to the movement speed.
        public void SetState(string state, float clipSpeed = 1f)
        {
            if (Anim == null) return;
            State = state;
            // During an upper-body/full-body combat strike ("attack"), keep the base idle/walk clip refreshing bone bind poses each frame so LateUpdate procedural combat overrides never accumulate.
            string clipName = state == "attack" ? (clipSpeed > 1.05f ? "walk" : "idle") : state;
            var st = Anim[clipName] ?? Anim["walk"];
            if (st == null) return;
            st.speed = clipName == "idle" && Anim["idle"] == null ? 0f : Mathf.Clamp(clipSpeed, 0.4f, 2.2f);
            if (!Anim.IsPlaying(st.name)) Anim.CrossFade(st.name, 0.14f);
        }

        public bool HasClip(string name) => Anim != null && Anim.GetClip(name) != null;

        static void WorldRot(Transform bone, Vector3 right, Vector3 up, Vector3 fwd, float pitch, float yaw, float roll)
        {
            if (bone == null) return;
            if (Mathf.Abs(pitch) > 0.01f) bone.Rotate(right, pitch, Space.World);
            if (Mathf.Abs(yaw) > 0.01f) bone.Rotate(up, yaw, Space.World);
            if (Mathf.Abs(roll) > 0.01f) bone.Rotate(fwd, roll, Space.World);
        }

        // Full-body 3D combat pose evaluated in LateUpdate after Animation updates the base locomotion pose.
        // swing01: 0 (no active swing) or 0..1 progress through the active attack.
        // guard01: 0..1 blend weight for boxing/weapon ready stance between swings.
        public void ApplyCombatPose(float swing01, float guard01, int comboStep, string weaponId, bool isGun)
        {
            if (Root == null) return;
            bool hasWeapon = !string.IsNullOrEmpty(weaponId);
            if (swing01 <= 0.001f && guard01 <= 0.001f && !hasWeapon) return;

            var tr = Root.transform;
            Vector3 right = tr.right, up = tr.up, fwd = tr.forward;

            // 1. Firearm aiming & recoil stance
            if (isGun)
            {
                bool twoHand = weaponId == "shotgun" || weaponId == "smg" || weaponId == "rifle" || weaponId == "sniper" || weaponId == "lmg";
                float aim = Mathf.Max(guard01, swing01 > 0f ? 1f : 0.42f);
                float kick = swing01 > 0f ? Mathf.Sin(swing01 * Mathf.PI) : 0f;

                WorldRot(Spine ?? Hips, right, up, fwd, 6f * aim - 7f * kick, twoHand ? 14f * aim : 8f * aim, 0f);
                WorldRot(RightArm, right, up, fwd, -82f * aim - 16f * kick, -8f * aim, 10f * aim);
                WorldRot(RightForeArm, right, up, fwd, -16f * aim - 8f * kick, -6f * aim, 0f);
                if (twoHand)
                {
                    WorldRot(LeftArm, right, up, fwd, -74f * aim - 10f * kick, 32f * aim, -12f * aim);
                    WorldRot(LeftForeArm, right, up, fwd, -28f * aim, 18f * aim, 0f);
                }
                if (WeaponRoot != null)
                    WeaponRoot.localRotation = Quaternion.Euler(75f - kick * 18f, 0f, 0f);
                return;
            }

            // 2. Active melee weapon slash or bare-knuckle 3-hit street brawl combo
            if (swing01 > 0.001f)
            {
                float s = Mathf.Sin(swing01 * Mathf.PI); // 0 -> 1 -> 0 envelope
                if (hasWeapon)
                {
                    int step = Mathf.Abs(comboStep) % 3;
                    if (step == 0)
                    {
                        // Overhead diagonal cleave (windup 0..0.28 -> slash 0.28..0.72 -> recover)
                        float slash = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 0.65f, swing01));
                        float torsoYaw = Mathf.Lerp(26f, -38f, slash) * s;
                        float torsoPitch = Mathf.Lerp(-8f, 20f, slash) * s;
                        WorldRot(Spine ?? Hips, right, up, fwd, torsoPitch, torsoYaw, 0f);
                        WorldRot(Spine2, right, up, fwd, torsoPitch * 0.6f, torsoYaw * 0.7f, 0f);
                        WorldRot(RightArm, right, up, fwd, Mathf.Lerp(-138f, -36f, slash) * s, Mathf.Lerp(28f, -34f, slash) * s, 22f * s);
                        WorldRot(RightForeArm, right, up, fwd, Mathf.Lerp(-48f, -8f, slash) * s, 0f, 0f);
                        WorldRot(LeftArm, right, up, fwd, -42f * s, -22f * s, -18f * s);
                        WorldRot(LeftForeArm, right, up, fwd, -55f * s, 0f, 0f);
                        if (WeaponRoot != null)
                            WeaponRoot.localRotation = Quaternion.Euler(75f + Mathf.Lerp(-45f, 82f, slash) * s, -slash * 28f * s, 0f);
                    }
                    else if (step == 1)
                    {
                        // Wide horizontal backhand sweep across chest
                        float sweep = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.68f, swing01));
                        float yaw = Mathf.Lerp(-46f, 48f, sweep) * s;
                        WorldRot(Spine ?? Hips, right, up, fwd, 14f * s, yaw * 0.75f, 0f);
                        WorldRot(RightArm, right, up, fwd, -88f * s, yaw, 28f * s);
                        WorldRot(RightForeArm, right, up, fwd, -20f * s, yaw * 0.35f, 0f);
                        WorldRot(LeftArm, right, up, fwd, -48f * s, -25f * s, -20f * s);
                        if (WeaponRoot != null)
                            WeaponRoot.localRotation = Quaternion.Euler(75f + 55f * s, yaw * 0.6f, 45f * s);
                    }
                    else
                    {
                        // Heavy lunging overhead smash + front kick
                        float smash = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.20f, 0.65f, swing01));
                        WorldRot(Spine ?? Hips, right, up, fwd, Mathf.Lerp(-14f, 24f, smash) * s, -18f * s, 0f);
                        WorldRot(RightArm, right, up, fwd, Mathf.Lerp(-145f, -30f, smash) * s, -14f * s, 16f * s);
                        WorldRot(RightForeArm, right, up, fwd, -18f * s, 0f, 0f);
                        WorldRot(LeftArm, right, up, fwd, Mathf.Lerp(-115f, -40f, smash) * s, 22f * s, -14f * s);
                        WorldRot(RightUpLeg, right, up, fwd, -62f * s, 0f, 0f);
                        WorldRot(RightLeg, right, up, fwd, 24f * s, 0f, 0f);
                        if (WeaponRoot != null)
                            WeaponRoot.localRotation = Quaternion.Euler(75f + Mathf.Lerp(-35f, 90f, smash) * s, 0f, 0f);
                    }
                }
                else
                {
                    // Bare-fists 3-hit street fight combo (Right Cross -> Left Hook -> Street Front Kick & Uppercut)
                    int step = Mathf.Abs(comboStep) % 3;
                    if (step == 0)
                    {
                        // Right Cross / Haymaker
                        float punch = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.55f, swing01));
                        WorldRot(Spine ?? Hips, right, up, fwd, 16f * s, Mathf.Lerp(24f, -38f, punch) * s, 0f);
                        WorldRot(Spine2, right, up, fwd, 8f * s, Mathf.Lerp(16f, -28f, punch) * s, 0f);
                        WorldRot(RightArm, right, up, fwd, Mathf.Lerp(-42f, -96f, punch) * s, -18f * punch * s, 16f * s);
                        WorldRot(RightForeArm, right, up, fwd, Mathf.Lerp(-78f, -8f, punch) * s, -10f * s, 0f);
                        // Left hand tucked high in boxing guard
                        WorldRot(LeftArm, right, up, fwd, -52f * s, 18f * s, -14f * s);
                        WorldRot(LeftForeArm, right, up, fwd, -82f * s, 22f * s, 0f);
                    }
                    else if (step == 1)
                    {
                        // Left Hook / Jab
                        float hook = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.55f, swing01));
                        WorldRot(Spine ?? Hips, right, up, fwd, 16f * s, Mathf.Lerp(-24f, 38f, hook) * s, 0f);
                        WorldRot(Spine2, right, up, fwd, 8f * s, Mathf.Lerp(-16f, 28f, hook) * s, 0f);
                        WorldRot(LeftArm, right, up, fwd, Mathf.Lerp(-45f, -94f, hook) * s, 28f * hook * s, -20f * s);
                        WorldRot(LeftForeArm, right, up, fwd, Mathf.Lerp(-78f, -24f, hook) * s, 24f * hook * s, 0f);
                        // Right hand tucked high in boxing guard
                        WorldRot(RightArm, right, up, fwd, -52f * s, -18f * s, 14f * s);
                        WorldRot(RightForeArm, right, up, fwd, -82f * s, -22f * s, 0f);
                    }
                    else
                    {
                        // Street Front Kick + Double-Handed Uppercut
                        float kick = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.52f, swing01));
                        WorldRot(Spine ?? Hips, right, up, fwd, -10f * s, -14f * s, 0f);
                        WorldRot(RightUpLeg, right, up, fwd, -88f * kick * s, 0f, 6f * s);
                        WorldRot(RightLeg, right, up, fwd, 28f * kick * s, 0f, 0f);
                        WorldRot(RightArm, right, up, fwd, -84f * s, -16f * s, 18f * s);
                        WorldRot(RightForeArm, right, up, fwd, -48f * s, 0f, 0f);
                        WorldRot(LeftArm, right, up, fwd, -65f * s, 16f * s, -18f * s);
                        WorldRot(LeftForeArm, right, up, fwd, -68f * s, 0f, 0f);
                    }
                }
                return;
            }

            // 3. Between swings: Fighter guard stance or melee weapon ready carry
            float g = Mathf.Clamp01(guard01);
            if (hasWeapon)
            {
                float w = Mathf.Max(0.45f, g);
                WorldRot(RightArm, right, up, fwd, -28f * w, -6f * w, 14f * w);
                WorldRot(RightForeArm, right, up, fwd, -36f * w, 0f, 0f);
                if (g > 0.05f)
                {
                    WorldRot(LeftArm, right, up, fwd, -42f * g, 16f * g, -12f * g);
                    WorldRot(LeftForeArm, right, up, fwd, -68f * g, 14f * g, 0f);
                }
                if (WeaponRoot != null)
                    WeaponRoot.localRotation = Quaternion.Euler(75f, 0f, 0f);
            }
            else if (g > 0.01f)
            {
                WorldRot(Spine ?? Hips, right, up, fwd, 8f * g, 12f * g, 0f);
                WorldRot(RightArm, right, up, fwd, -46f * g, -14f * g, 12f * g);
                WorldRot(RightForeArm, right, up, fwd, -78f * g, -18f * g, 0f);
                WorldRot(LeftArm, right, up, fwd, -54f * g, 16f * g, -12f * g);
                WorldRot(LeftForeArm, right, up, fwd, -82f * g, 20f * g, 0f);
            }
        }
    }

    // Real humans: the Ready Player Me avatar imported with glTFast and driven
    // by a legacy Animation with the Ready Player Me Idle / Walk / Run clips plus dedicated Jump, Turn, Vehicle Enter/Exit, and Attack clips.
    // One rig loaded once, instanced per character, dressed per look with procedural Nigerian & streetwear garments.
    public static class CharacterRig
    {
        static GameObject prefab, cesiumPrefab;
        static AnimationClip idle, walk, run, jump, turn, enterVehicle, exitVehicle, attack, cesiumWalk;
        static bool loaded;
        static Mesh splitTopMesh, splitBottomMesh, splitFootwearMesh;
        public const float WalkClipSpeed = 1.5f, RunClipSpeed = 5.5f;   // m/s the clips were authored for
        public static WorldData Data;                                  // for the fabric prints

        static void Load()
        {
            if (loaded) return; loaded = true;
            prefab = Resources.Load<GameObject>("Models/Characters/readyplayer.me");
            idle = Resources.Load<AnimationClip>("Characters/rpm_idle");
            walk = Resources.Load<AnimationClip>("Characters/rpm_walk");
            run = Resources.Load<AnimationClip>("Characters/rpm_run");
            jump = Resources.Load<AnimationClip>("Characters/rpm_jump") ?? MakeJumpClip();
            turn = Resources.Load<AnimationClip>("Characters/rpm_turn") ?? MakeTurnClip();
            enterVehicle = Resources.Load<AnimationClip>("Characters/rpm_enter_vehicle") ?? MakeEnterVehicleClip();
            exitVehicle = Resources.Load<AnimationClip>("Characters/rpm_exit_vehicle") ?? MakeExitVehicleClip();
            attack = Resources.Load<AnimationClip>("Characters/rpm_attack") ?? MakeAttackClip();
            cesiumPrefab = Resources.Load<GameObject>("Models/Characters/CesiumMan");
            cesiumWalk = Resources.Load<AnimationClip>("Characters/cesium_walk");
        }

        static AnimationClip MakeAttackClip()
        {
            var c = new AnimationClip { name = "rpm_attack", legacy = true, wrapMode = WrapMode.Once };
            c.SetCurve("", typeof(Transform), "localPosition.z", new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.16f, 0.14f), new Keyframe(0.42f, 0f)));
            return c;
        }

        static AnimationClip MakeJumpClip()
        {
            var c = new AnimationClip { name = "rpm_jump", legacy = true, wrapMode = WrapMode.Once };
            c.SetCurve("", typeof(Transform), "localPosition.y", new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.12f, -0.08f), new Keyframe(0.32f, 0.22f), new Keyframe(0.55f, 0f)));
            c.SetCurve("Hips", typeof(Transform), "localEulerAngles.x", new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.15f, 14f), new Keyframe(0.35f, -8f), new Keyframe(0.55f, 0f)));
            return c;
        }

        static AnimationClip MakeTurnClip()
        {
            var c = new AnimationClip { name = "rpm_turn", legacy = true, wrapMode = WrapMode.Loop };
            c.SetCurve("", typeof(Transform), "localPosition.y", new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.20f, 0.04f), new Keyframe(0.40f, 0f)));
            c.SetCurve("Hips", typeof(Transform), "localEulerAngles.y", new AnimationCurve(
                new Keyframe(0f, -12f), new Keyframe(0.20f, 12f), new Keyframe(0.40f, -12f)));
            return c;
        }

        static AnimationClip MakeEnterVehicleClip()
        {
            var c = new AnimationClip { name = "rpm_enter_vehicle", legacy = true, wrapMode = WrapMode.Once };
            c.SetCurve("", typeof(Transform), "localPosition.x", new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.25f, 0.18f), new Keyframe(0.50f, 0.32f)));
            c.SetCurve("", typeof(Transform), "localPosition.y", new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.25f, -0.14f), new Keyframe(0.50f, -0.25f)));
            c.SetCurve("Hips", typeof(Transform), "localEulerAngles.x", new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.30f, 24f), new Keyframe(0.50f, 18f)));
            return c;
        }

        static AnimationClip MakeExitVehicleClip()
        {
            var c = new AnimationClip { name = "rpm_exit_vehicle", legacy = true, wrapMode = WrapMode.Once };
            c.SetCurve("", typeof(Transform), "localPosition.x", new AnimationCurve(
                new Keyframe(0f, 0.28f), new Keyframe(0.25f, 0.10f), new Keyframe(0.50f, 0f)));
            c.SetCurve("", typeof(Transform), "localPosition.y", new AnimationCurve(
                new Keyframe(0f, -0.22f), new Keyframe(0.25f, -0.06f), new Keyframe(0.50f, 0f)));
            c.SetCurve("Hips", typeof(Transform), "localEulerAngles.x", new AnimationCurve(
                new Keyframe(0f, 20f), new Keyframe(0.30f, 6f), new Keyframe(0.50f, 0f)));
            return c;
        }

        public static bool Available { get { Load(); return prefab != null && idle != null && walk != null && run != null; } }
        public static bool CesiumAvailable { get { Load(); return (cesiumPrefab != null && cesiumWalk != null) || Available; } }

        // Splits Wolf3D_Outfit_Top into 3 submeshes (0: Torso, 1: Upper Arms / Short Sleeves, 2: Forearms)
        // and remaps UVs cleanly so wedding-tuxedo lapels/bow-tie islands never distort Nigerian prints or streetwear tees.
        static Mesh GetSplitTopMesh(Mesh src)
        {
            if (splitTopMesh != null) return splitTopMesh;
            if (src == null || !src.isReadable) return src;
            var m = Object.Instantiate(src);
            m.name = src.name + "_StreetSplit";
            var verts = m.vertices;
            var uvs = new Vector2[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                var v = verts[i];
                float ax = Mathf.Abs(v.x);
                if (ax <= 0.205f)
                    uvs[i] = new Vector2((v.x + 0.21f) / 0.42f, Mathf.Clamp01((v.y - 0.75f) / 0.82f));
                else
                {
                    float along = Mathf.Clamp01((ax - 0.20f) / 0.29f);
                    float ang = Mathf.Atan2(v.z + 0.04f, v.y - 1.25f) / (2f * Mathf.PI) + 0.5f;
                    uvs[i] = new Vector2(ang, along);
                }
            }
            m.uv = uvs;

            var tris = src.triangles;
            var torso = new List<int>(tris.Length);
            var upperArm = new List<int>(tris.Length / 3);
            var foreArm = new List<int>(tris.Length / 3);
            for (int i = 0; i < tris.Length; i += 3)
            {
                int i0 = tris[i], i1 = tris[i + 1], i2 = tris[i + 2];
                float ax = (Mathf.Abs(verts[i0].x) + Mathf.Abs(verts[i1].x) + Mathf.Abs(verts[i2].x)) / 3f;
                if (ax <= 0.205f) { torso.Add(i0); torso.Add(i1); torso.Add(i2); }
                else if (ax <= 0.288f) { upperArm.Add(i0); upperArm.Add(i1); upperArm.Add(i2); }
                else { foreArm.Add(i0); foreArm.Add(i1); foreArm.Add(i2); }
            }
            m.subMeshCount = 3;
            m.SetTriangles(torso, 0);
            m.SetTriangles(upperArm, 1);
            m.SetTriangles(foreArm, 2);
            splitTopMesh = m;
            return m;
        }

        // Splits Wolf3D_Outfit_Bottom into 3 submeshes (0: Thighs/Shorts, 1: Calves, 2: Crew Socks/Ankles)
        // so outfits can show tailored denim/chinos OR knee-length shorts with bare calves and high white crew socks.
        static Mesh GetSplitBottomMesh(Mesh src)
        {
            if (splitBottomMesh != null) return splitBottomMesh;
            if (src == null || !src.isReadable) return src;
            var m = Object.Instantiate(src);
            m.name = src.name + "_StreetSplit";
            var verts = m.vertices;
            var uvs = new Vector2[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                var v = verts[i];
                uvs[i] = new Vector2((v.x + 0.21f) / 0.42f, Mathf.Clamp01((v.y - 0.08f) / 1.01f));
            }
            m.uv = uvs;

            var tris = src.triangles;
            var thighs = new List<int>(tris.Length);
            var calves = new List<int>(tris.Length / 2);
            var socks = new List<int>(tris.Length / 4);
            for (int i = 0; i < tris.Length; i += 3)
            {
                int i0 = tris[i], i1 = tris[i + 1], i2 = tris[i + 2];
                float ay = (verts[i0].y + verts[i1].y + verts[i2].y) / 3f;
                if (ay >= 0.49f) { thighs.Add(i0); thighs.Add(i1); thighs.Add(i2); }
                else if (ay >= 0.235f) { calves.Add(i0); calves.Add(i1); calves.Add(i2); }
                else { socks.Add(i0); socks.Add(i1); socks.Add(i2); }
            }
            m.subMeshCount = 3;
            m.SetTriangles(thighs, 0);
            m.SetTriangles(calves, 1);
            m.SetTriangles(socks, 2);
            splitBottomMesh = m;
            return m;
        }

        // Splits Wolf3D_Outfit_Footwear into 2 submeshes (0: Crisp White Vulcanized Midsole, 1: Two-Tone Sneaker Upper).
        static Mesh GetSplitFootwearMesh(Mesh src)
        {
            if (splitFootwearMesh != null) return splitFootwearMesh;
            if (src == null || !src.isReadable) return src;
            var m = Object.Instantiate(src);
            m.name = src.name + "_StreetSplit";
            var verts = m.vertices;
            var uvs = new Vector2[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                var v = verts[i];
                uvs[i] = new Vector2((v.x + 0.23f) / 0.46f, Mathf.Clamp01((v.z + 0.12f) / 0.35f));
            }
            m.uv = uvs;

            var tris = src.triangles;
            var sole = new List<int>(tris.Length / 2);
            var upper = new List<int>(tris.Length);
            for (int i = 0; i < tris.Length; i += 3)
            {
                int i0 = tris[i], i1 = tris[i + 1], i2 = tris[i + 2];
                float minY = Mathf.Min(verts[i0].y, Mathf.Min(verts[i1].y, verts[i2].y));
                float avgY = (verts[i0].y + verts[i1].y + verts[i2].y) / 3f;
                if (minY <= 0.024f || avgY <= 0.033f) { sole.Add(i0); sole.Add(i1); sole.Add(i2); }
                else { upper.Add(i0); upper.Add(i1); upper.Add(i2); }
            }
            m.subMeshCount = 2;
            m.SetTriangles(sole, 0);
            m.SetTriangles(upper, 1);
            splitFootwearMesh = m;
            return m;
        }

        static void StripBakedSuitMaps(Material m, float roughness = 0.72f, float metallic = 0.04f)
        {
            if (m == null) return;
            if (m.HasProperty("normalTexture")) m.SetTexture("normalTexture", null);
            if (m.HasProperty("_BumpMap")) m.SetTexture("_BumpMap", null);
            m.DisableKeyword("_NORMALMAP");
            if (m.HasProperty("metallicRoughnessTexture")) m.SetTexture("metallicRoughnessTexture", null);
            if (m.HasProperty("occlusionTexture")) m.SetTexture("occlusionTexture", null);
            if (m.HasProperty("roughnessFactor")) m.SetFloat("roughnessFactor", roughness);
            if (m.HasProperty("metallicFactor")) m.SetFloat("metallicFactor", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 1f - roughness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        }

        public static RigInstance Build(Transform parent, Look look, int layer = 0, float height = 1.8f, bool cesium = false)
        {
            Load();
            var rig = new RigInstance { Cesium = cesium && CesiumAvailable };
            var srcPrefab = prefab != null ? prefab : cesiumPrefab;
            var go = Object.Instantiate(srcPrefab, parent, false);
            go.name = rig.Cesium ? "Rig (CesiumMan)" : "Rig";
            rig.Root = go;
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = srcPrefab.transform.localRotation; go.transform.localScale = Vector3.one;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;

            // Always hide the built-in Ready Player Me Western cowboy hat ("Wolf3D_Headwear") before measuring bounds.
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.Contains("Headwear") || (r.sharedMaterial != null && r.sharedMaterial.name.Contains("Headwear")))
                    r.enabled = false;
            }

            // Scale to the character height and put the feet on the ground.
            var bounds = Bounds(go);
            float k = bounds.size.y > 0.01f ? height / bounds.size.y : 1f;
            go.transform.localScale = Vector3.one * k;
            go.transform.localPosition = new Vector3(0f, -(bounds.min.y - go.transform.position.y) * k, 0f);
            rig.BaseScale = k;

            // Face +z.
            var head = Find(go.transform, "Head"); var le = Find(go.transform, "LeftEye"); var re = Find(go.transform, "RightEye");
            if (head != null && le != null && re != null)
            {
                var forward = go.transform.InverseTransformDirection((le.position + re.position) * 0.5f - head.position);
                if (forward.z < 0f) go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * go.transform.localRotation;
            }

            // Collect clothing materials and upgrade Wolf3D_Outfit_Top, Wolf3D_Outfit_Bottom, Wolf3D_Outfit_Footwear into multi-zone submeshes.
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled && (r.name.Contains("Headwear") || (r.sharedMaterial != null && r.sharedMaterial.name.Contains("Headwear"))))
                    continue;

                var smr = r as SkinnedMeshRenderer;
                string matName = r.sharedMaterial != null ? r.sharedMaterial.name : "";

                if (smr != null && matName.Contains("Outfit_Top") && smr.sharedMesh != null && smr.sharedMesh.isReadable)
                {
                    var baseMat = r.material;
                    StripBakedSuitMaps(baseMat, 0.70f, 0.04f);
                    rig.SleeveUpperMat = new Material(baseMat) { name = "Outfit_Top_UpperSleeve" };
                    rig.SleeveForeMat = new Material(baseMat) { name = "Outfit_Top_ForeArm" };
                    smr.sharedMesh = GetSplitTopMesh(smr.sharedMesh);
                    smr.sharedMaterials = new[] { baseMat, rig.SleeveUpperMat, rig.SleeveForeMat };
                    rig.Top.Add(baseMat);
                    continue;
                }
                if (smr != null && matName.Contains("Outfit_Bottom") && smr.sharedMesh != null && smr.sharedMesh.isReadable)
                {
                    var baseMat = r.material;
                    StripBakedSuitMaps(baseMat, 0.74f, 0.04f);
                    rig.LegCalfMat = new Material(baseMat) { name = "Outfit_Bottom_Calf" };
                    rig.SockMat = new Material(baseMat) { name = "Outfit_Bottom_Sock" };
                    smr.sharedMesh = GetSplitBottomMesh(smr.sharedMesh);
                    smr.sharedMaterials = new[] { baseMat, rig.LegCalfMat, rig.SockMat };
                    rig.Bottom.Add(baseMat);
                    continue;
                }
                if (smr != null && matName.Contains("Outfit_Footwear") && smr.sharedMesh != null && smr.sharedMesh.isReadable)
                {
                    var baseMat = r.material;
                    StripBakedSuitMaps(baseMat, 0.55f, 0.06f);
                    rig.ShoeSoleMat = baseMat;
                    rig.ShoeUpperMat = new Material(baseMat) { name = "Outfit_Footwear_Upper" };
                    smr.sharedMesh = GetSplitFootwearMesh(smr.sharedMesh);
                    smr.sharedMaterials = new[] { rig.ShoeSoleMat, rig.ShoeUpperMat };
                    continue;
                }

                var mats = r.materials;
                foreach (var m in mats)
                {
                    string n = m.name;
                    if (n.Contains("Outfit_Top")) { StripBakedSuitMaps(m); rig.Top.Add(m); }
                    else if (n.Contains("Outfit_Bottom")) { StripBakedSuitMaps(m); rig.Bottom.Add(m); }
                    else if (n.Contains("Beard"))
                    {
                        StripBakedSuitMaps(m, 0.78f, 0.02f);
                        SetTex(m, null);
                        rig.BeardRenderers.Add(r);
                        rig.Hair.Add(m);
                    }
                    else if (n.Contains("Hair")) { rig.Hair.Add(m); rig.HairRenderers.Add(r); }
                    else if ((n.Contains("Body") || n.Contains("Skin")) && !n.Contains("Eye") && !n.Contains("Teeth"))
                    {
                        if (m.HasProperty("roughnessFactor")) m.SetFloat("roughnessFactor", 0.58f);
                        rig.Skin.Add(m);
                    }
                    else if (srcPrefab == cesiumPrefab) rig.Top.Add(m);
                }
            }
            BindBones(rig, go.transform);
            if (srcPrefab == prefab) BuildAttachments(rig, go.transform, k);
            Dress(rig, look);
            var dressedBounds = Bounds(go);
            if (dressedBounds.size.y > 0.01f && Mathf.Abs(dressedBounds.size.y - height) > 0.02f)
            {
                float fix = height / dressedBounds.size.y;
                k *= fix;
                go.transform.localScale = Vector3.one * k;
                var b2 = Bounds(go);
                go.transform.localPosition += new Vector3(0f, go.transform.position.y - b2.min.y, 0f);
                rig.BaseScale = k;
            }

            var anim = go.GetComponent<Animation>();
            if (anim == null) anim = go.AddComponent<Animation>();
            anim.playAutomatically = false; anim.wrapMode = WrapMode.Loop; anim.cullingType = AnimationCullingType.BasedOnRenderers;
            if (srcPrefab == prefab)
            {
                if (anim.GetClip("idle") == null && idle != null) anim.AddClip(idle, "idle");
                if (anim.GetClip("walk") == null && walk != null) anim.AddClip(walk, "walk");
                if (anim.GetClip("run") == null && run != null) anim.AddClip(run, "run");
            }
            else if (cesiumWalk != null && anim.GetClip("walk") == null) anim.AddClip(cesiumWalk, "walk");
            if (jump != null && anim.GetClip("jump") == null) anim.AddClip(jump, "jump");
            if (turn != null && anim.GetClip("turn") == null) anim.AddClip(turn, "turn");
            if (enterVehicle != null && anim.GetClip("enter_vehicle") == null) anim.AddClip(enterVehicle, "enter_vehicle");
            if (exitVehicle != null && anim.GetClip("exit_vehicle") == null) anim.AddClip(exitVehicle, "exit_vehicle");
            if (attack != null && anim.GetClip("attack") == null) anim.AddClip(attack, "attack");
            rig.Anim = anim;
            if (Application.isPlaying) rig.SetState("idle");
            return rig;
        }

        static void BindBones(RigInstance rig, Transform root)
        {
            rig.Hips = Find(root, "Hips");
            rig.Spine = Find(root, "Spine");
            rig.Spine1 = Find(root, "Spine1");
            rig.Spine2 = Find(root, "Spine2");
            rig.Neck = Find(root, "Neck");
            rig.Head = Find(root, "Head");
            rig.RightShoulder = Find(root, "RightShoulder");
            rig.RightArm = Find(root, "RightArm");
            rig.RightForeArm = Find(root, "RightForeArm");
            rig.RightHand = Find(root, "RightHand");
            rig.LeftShoulder = Find(root, "LeftShoulder");
            rig.LeftArm = Find(root, "LeftArm");
            rig.LeftForeArm = Find(root, "LeftForeArm");
            rig.LeftHand = Find(root, "LeftHand");
            rig.RightUpLeg = Find(root, "RightUpLeg");
            rig.RightLeg = Find(root, "RightLeg");
            rig.LeftUpLeg = Find(root, "LeftUpLeg");
            rig.LeftLeg = Find(root, "LeftLeg");
        }

        static void DestroyObj(Object o) { if (o == null) return; if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o); }

        static Material sharedLensMat, sharedFrameMat, sharedSilverMat, sharedWatchMetalMat, sharedWatchDialMat, sharedTattooMat, sharedBeltLeatherMat;
        static readonly Dictionary<string, Material> weaponMatCache = new Dictionary<string, Material>();

        static void EnsureAccessoryMaterials()
        {
            if (sharedLensMat == null) { sharedLensMat = new Material(ShaderLib.Lit) { color = new Color(0.04f, 0.06f, 0.09f) }; sharedLensMat.SetFloat("_Smoothness", 0.96f); sharedLensMat.SetFloat("_Metallic", 0.55f); }
            if (sharedFrameMat == null) { sharedFrameMat = new Material(ShaderLib.Lit) { color = new Color(0.14f, 0.14f, 0.16f) }; sharedFrameMat.SetFloat("_Smoothness", 0.82f); sharedFrameMat.SetFloat("_Metallic", 0.65f); }
            if (sharedSilverMat == null) { sharedSilverMat = new Material(ShaderLib.Lit) { color = new Color(0.92f, 0.87f, 0.72f) }; sharedSilverMat.SetFloat("_Metallic", 0.94f); sharedSilverMat.SetFloat("_Smoothness", 0.90f); }
            if (sharedWatchMetalMat == null) { sharedWatchMetalMat = new Material(ShaderLib.Lit) { color = new Color(0.88f, 0.86f, 0.78f) }; sharedWatchMetalMat.SetFloat("_Metallic", 0.92f); sharedWatchMetalMat.SetFloat("_Smoothness", 0.88f); }
            if (sharedWatchDialMat == null) { sharedWatchDialMat = new Material(ShaderLib.Lit) { color = new Color(0.06f, 0.10f, 0.16f) }; sharedWatchDialMat.SetFloat("_Smoothness", 0.92f); }
            if (sharedBeltLeatherMat == null) { sharedBeltLeatherMat = new Material(ShaderLib.Lit) { color = new Color(0.11f, 0.09f, 0.08f) }; sharedBeltLeatherMat.SetFloat("_Smoothness", 0.58f); sharedBeltLeatherMat.SetFloat("_Metallic", 0.12f); }
            if (sharedTattooMat == null)
            {
                sharedTattooMat = new Material(ShaderLib.Lit) { color = Color.white };
                sharedTattooMat.SetFloat("_Smoothness", 0.34f);
                if (sharedTattooMat.HasProperty("_BaseMap")) sharedTattooMat.SetTexture("_BaseMap", ShaderLib.TattooTex);
                sharedTattooMat.mainTexture = ShaderLib.TattooTex;
            }
        }

        static void BuildAttachments(RigInstance rig, Transform root, float k)
        {
            EnsureAccessoryMaterials();
            var hips = Find(root, "Hips") ?? Find(root, "Spine");
            var spine2 = Find(root, "Spine2") ?? Find(root, "Spine1") ?? hips;
            var head = Find(root, "Head");
            var neck = Find(root, "Neck") ?? head;
            var leftForeArm = Find(root, "LeftForeArm");
            var rightForeArm = Find(root, "RightForeArm");
            int layer = root.gameObject.layer;

            Transform SubPart(Transform parent, string name, PrimitiveType pt, Vector3 pos, Vector3 sc, Material mat, Quaternion? rot = null)
            {
                var p = GameObject.CreatePrimitive(pt); p.name = name; p.layer = layer; DestroyObj(p.GetComponent<Collider>());
                p.transform.SetParent(parent, false); p.transform.localPosition = pos; p.transform.localScale = sc;
                if (rot.HasValue) p.transform.localRotation = rot.Value;
                p.GetComponent<MeshRenderer>().sharedMaterial = mat;
                return p.transform;
            }

            // Shared per-rig hair/beard and cap materials
            var hairMat = new Material(ShaderLib.Lit) { name = "HairMat", color = new Color(0.08f, 0.06f, 0.05f) };
            hairMat.SetFloat("_Smoothness", 0.22f);
            rig.Hair.Add(hairMat);
            var beardMat = new Material(ShaderLib.Lit) { name = "BeardMat", color = new Color(0.08f, 0.06f, 0.05f) };
            beardMat.SetFloat("_Smoothness", 0.22f);
            rig.Hair.Add(beardMat);

            var capMat = new Material(ShaderLib.Lit) { name = "HeadwearCapMat", color = new Color(0.12f, 0.23f, 0.37f) };
            capMat.SetFloat("_Smoothness", 0.32f);
            rig.Cap.Add(capMat);

            if (hips != null)
            {
                // Traditional tunic / gown lower drape ("Outfit overlay" name preserved for tests)
                var ov = GameObject.CreatePrimitive(PrimitiveType.Cylinder); ov.name = "Outfit overlay"; ov.layer = layer; DestroyObj(ov.GetComponent<Collider>());
                ov.transform.SetParent(hips, false);
                rig.OverlayMaterial = new Material(ShaderLib.Lit) { color = Color.white }; rig.OverlayMaterial.SetFloat("_Smoothness", 0.26f);
                ov.GetComponent<MeshRenderer>().sharedMaterial = rig.OverlayMaterial;
                rig.Overlay = ov.transform; ov.SetActive(false);

                // Tailored leather belt + metallic buckle on Hips
                var bRoot = new GameObject("BeltRoot") { layer = layer }.transform;
                bRoot.SetParent(hips, false);
                SubPart(bRoot, "BeltStrap", PrimitiveType.Cylinder, new Vector3(0f, -0.02f, 0.008f), new Vector3(0.305f, 0.020f, 0.225f), sharedBeltLeatherMat);
                SubPart(bRoot, "BeltBuckle", PrimitiveType.Cube, new Vector3(0f, -0.02f, 0.118f), new Vector3(0.052f, 0.038f, 0.014f), sharedSilverMat);
                rig.BeltRoot = bRoot;
            }

            if (spine2 != null)
            {
                // Open streetwear jacket / trench coat panels + lapels over inner shirt ("Jacket & jeans")
                rig.JacketMaterial = new Material(ShaderLib.Lit) { color = new Color(0.14f, 0.16f, 0.20f) };
                rig.JacketMaterial.SetFloat("_Smoothness", 0.36f);
                var jRoot = new GameObject("JacketRoot") { layer = layer }.transform;
                jRoot.SetParent(spine2, false);
                SubPart(jRoot, "JacketPanelL", PrimitiveType.Cube, new Vector3(0.088f, -0.11f, 0.068f), new Vector3(0.115f, 0.42f, 0.095f), rig.JacketMaterial, Quaternion.Euler(0f, -8f, 0f));
                SubPart(jRoot, "JacketPanelR", PrimitiveType.Cube, new Vector3(-0.088f, -0.11f, 0.068f), new Vector3(0.115f, 0.42f, 0.095f), rig.JacketMaterial, Quaternion.Euler(0f, 8f, 0f));
                SubPart(jRoot, "JacketBack", PrimitiveType.Cube, new Vector3(0f, -0.10f, -0.062f), new Vector3(0.31f, 0.43f, 0.085f), rig.JacketMaterial);
                SubPart(jRoot, "LapelL", PrimitiveType.Cube, new Vector3(0.056f, 0.01f, 0.112f), new Vector3(0.042f, 0.22f, 0.022f), rig.JacketMaterial, Quaternion.Euler(0f, 0f, -14f));
                SubPart(jRoot, "LapelR", PrimitiveType.Cube, new Vector3(-0.056f, 0.01f, 0.112f), new Vector3(0.042f, 0.22f, 0.022f), rig.JacketMaterial, Quaternion.Euler(0f, 0f, 14f));
                SubPart(jRoot, "CollarStand", PrimitiveType.Cylinder, new Vector3(0f, 0.115f, -0.008f), new Vector3(0.155f, 0.028f, 0.155f), rig.JacketMaterial);
                rig.JacketRoot = jRoot;
                jRoot.gameObject.SetActive(false);

                // Flowing Agbada shoulder drape wings
                var aRoot = new GameObject("AgbadaWings") { layer = layer }.transform;
                aRoot.SetParent(spine2, false);
                SubPart(aRoot, "WingL", PrimitiveType.Cube, new Vector3(0.17f, -0.06f, 0.01f), new Vector3(0.16f, 0.38f, 0.24f), rig.OverlayMaterial, Quaternion.Euler(0f, 0f, -12f));
                SubPart(aRoot, "WingR", PrimitiveType.Cube, new Vector3(-0.17f, -0.06f, 0.01f), new Vector3(0.16f, 0.38f, 0.24f), rig.OverlayMaterial, Quaternion.Euler(0f, 0f, 12f));
                rig.AgbadaWings = aRoot;
                aRoot.gameObject.SetActive(false);

                // Gold-embroidered Senator / Agbada chest placket
                rig.PlacketMaterial = new Material(ShaderLib.Lit) { color = new Color(0.90f, 0.72f, 0.22f) };
                rig.PlacketMaterial.SetFloat("_Metallic", 0.55f); rig.PlacketMaterial.SetFloat("_Smoothness", 0.62f);
                var pRoot = new GameObject("CollarPlacket") { layer = layer }.transform;
                pRoot.SetParent(spine2, false);
                SubPart(pRoot, "ChestPlacket", PrimitiveType.Cube, new Vector3(0f, 0.01f, 0.108f), new Vector3(0.055f, 0.21f, 0.018f), rig.PlacketMaterial);
                SubPart(pRoot, "MandarinCollar", PrimitiveType.Cylinder, new Vector3(0f, 0.125f, 0.005f), new Vector3(0.138f, 0.022f, 0.138f), rig.PlacketMaterial);
                rig.CollarPlacket = pRoot;
                pRoot.gameObject.SetActive(false);
            }

            if (head != null)
            {
                // ---- 4 Sculpted 3D Hairstyles on Head (all within Head local y <= 0.195f so height remains ~1.80m) ----
                void AddHairPart(Transform group, string n, PrimitiveType pt, Vector3 pos, Vector3 sc, Quaternion? rot = null)
                {
                    var t = SubPart(group, n, pt, pos, sc, hairMat, rot);
                    rig.HairRenderers.Add(t.GetComponent<Renderer>());
                }

                // 0: Short (360 waves cropped cap + crisp front line-up)
                rig.HairShort = new GameObject("HairShort") { layer = layer }.transform;
                rig.HairShort.SetParent(head, false);
                AddHairPart(rig.HairShort, "WaveCap", PrimitiveType.Sphere, new Vector3(0f, 0.116f, -0.004f), new Vector3(0.194f, 0.145f, 0.200f));
                AddHairPart(rig.HairShort, "LineUp", PrimitiveType.Cube, new Vector3(0f, 0.138f, 0.072f), new Vector3(0.128f, 0.028f, 0.052f), Quaternion.Euler(12f, 0f, 0f));

                // 1: Fade (High-top sponge-twist Lagos fade + sharp hairline)
                rig.HairFade = new GameObject("HairFade") { layer = layer }.transform;
                rig.HairFade.SetParent(head, false);
                AddHairPart(rig.HairFade, "FadeCrown", PrimitiveType.Cylinder, new Vector3(0f, 0.148f, 0.002f), new Vector3(0.168f, 0.040f, 0.176f), Quaternion.Euler(-4f, 0f, 0f));
                AddHairPart(rig.HairFade, "FadeFrontLine", PrimitiveType.Cube, new Vector3(0f, 0.146f, 0.068f), new Vector3(0.136f, 0.044f, 0.058f));
                AddHairPart(rig.HairFade, "TwistC", PrimitiveType.Sphere, new Vector3(0f, 0.172f, 0.010f), new Vector3(0.148f, 0.046f, 0.156f));
                AddHairPart(rig.HairFade, "TwistL", PrimitiveType.Sphere, new Vector3(-0.042f, 0.168f, 0.004f), new Vector3(0.085f, 0.042f, 0.135f));
                AddHairPart(rig.HairFade, "TwistR", PrimitiveType.Sphere, new Vector3(0.042f, 0.168f, 0.004f), new Vector3(0.085f, 0.042f, 0.135f));

                // 2: Afro (Full volumetric rounded Afro crown)
                rig.HairAfro = new GameObject("HairAfro") { layer = layer }.transform;
                rig.HairAfro.SetParent(head, false);
                AddHairPart(rig.HairAfro, "AfroMain", PrimitiveType.Sphere, new Vector3(0f, 0.124f, -0.008f), new Vector3(0.226f, 0.146f, 0.226f));
                AddHairPart(rig.HairAfro, "AfroL", PrimitiveType.Sphere, new Vector3(-0.062f, 0.118f, -0.006f), new Vector3(0.115f, 0.118f, 0.165f));
                AddHairPart(rig.HairAfro, "AfroR", PrimitiveType.Sphere, new Vector3(0.062f, 0.118f, -0.006f), new Vector3(0.115f, 0.118f, 0.165f));

                // 3: Braids (Sculpted cornrow rows + hanging back locks)
                rig.HairBraids = new GameObject("HairBraids") { layer = layer }.transform;
                rig.HairBraids.SetParent(head, false);
                AddHairPart(rig.HairBraids, "BraidBase", PrimitiveType.Sphere, new Vector3(0f, 0.114f, -0.006f), new Vector3(0.192f, 0.140f, 0.196f));
                for (int b = -2; b <= 2; b++)
                {
                    float bx = b * 0.032f;
                    AddHairPart(rig.HairBraids, "Cornrow_" + b, PrimitiveType.Capsule, new Vector3(bx, 0.148f, -0.004f), new Vector3(0.024f, 0.092f, 0.024f), Quaternion.Euler(86f, 0f, 0f));
                    AddHairPart(rig.HairBraids, "LockBack_" + b, PrimitiveType.Capsule, new Vector3(bx * 1.1f, 0.042f, -0.086f), new Vector3(0.022f, 0.065f, 0.022f), Quaternion.Euler(12f, 0f, 0f));
                }

                // ---- Sculpted 3D Facial Hair on Head (Beard, Goatee, Moustache) ----
                void AddBeardPart(Transform group, string n, PrimitiveType pt, Vector3 pos, Vector3 sc, Quaternion? rot = null)
                {
                    var t = SubPart(group, n, pt, pos, sc, beardMat, rot);
                    rig.BeardRenderers.Add(t.GetComponent<Renderer>());
                }

                rig.BeardFull = new GameObject("BeardFull") { layer = layer }.transform;
                rig.BeardFull.SetParent(head, false);
                AddBeardPart(rig.BeardFull, "ChinBeard", PrimitiveType.Cube, new Vector3(0f, -0.036f, 0.082f), new Vector3(0.094f, 0.056f, 0.068f), Quaternion.Euler(10f, 0f, 0f));
                AddBeardPart(rig.BeardFull, "JawLineL", PrimitiveType.Cube, new Vector3(-0.060f, -0.012f, 0.038f), new Vector3(0.034f, 0.050f, 0.102f), Quaternion.Euler(18f, -14f, 0f));
                AddBeardPart(rig.BeardFull, "JawLineR", PrimitiveType.Cube, new Vector3(0.060f, -0.012f, 0.038f), new Vector3(0.034f, 0.050f, 0.102f), Quaternion.Euler(18f, 14f, 0f));
                AddBeardPart(rig.BeardFull, "SideburnL", PrimitiveType.Cube, new Vector3(-0.082f, 0.036f, 0.010f), new Vector3(0.022f, 0.065f, 0.032f));
                AddBeardPart(rig.BeardFull, "SideburnR", PrimitiveType.Cube, new Vector3(0.082f, 0.036f, 0.010f), new Vector3(0.022f, 0.065f, 0.032f));
                AddBeardPart(rig.BeardFull, "SoulPatch", PrimitiveType.Cube, new Vector3(0f, -0.008f, 0.108f), new Vector3(0.028f, 0.024f, 0.024f));

                rig.BeardGoatee = new GameObject("BeardGoatee") { layer = layer }.transform;
                rig.BeardGoatee.SetParent(head, false);
                AddBeardPart(rig.BeardGoatee, "GoateeChin", PrimitiveType.Cube, new Vector3(0f, -0.036f, 0.088f), new Vector3(0.064f, 0.048f, 0.052f), Quaternion.Euler(8f, 0f, 0f));
                AddBeardPart(rig.BeardGoatee, "GoateePatch", PrimitiveType.Cube, new Vector3(0f, -0.008f, 0.108f), new Vector3(0.024f, 0.024f, 0.022f));

                rig.BeardMoustache = new GameObject("BeardMoustache") { layer = layer }.transform;
                rig.BeardMoustache.SetParent(head, false);
                AddBeardPart(rig.BeardMoustache, "StacheBar", PrimitiveType.Cube, new Vector3(0f, 0.018f, 0.114f), new Vector3(0.078f, 0.018f, 0.024f));

                // ---- Authentic 3D Headwear (Yoruba Fila Cap & Streetwear Baseball Face Cap) ----
                rig.FilaCap = new GameObject("FilaCap") { layer = layer }.transform;
                rig.FilaCap.SetParent(head, false);
                var fCrown = SubPart(rig.FilaCap, "FilaCrown", PrimitiveType.Cylinder, new Vector3(0f, 0.145f, 0.002f), new Vector3(0.194f, 0.044f, 0.200f), capMat, Quaternion.Euler(-4f, 0f, 6f));
                var fFold = SubPart(rig.FilaCap, "FilaFold", PrimitiveType.Cube, new Vector3(0.035f, 0.176f, 0.002f), new Vector3(0.115f, 0.024f, 0.155f), capMat, Quaternion.Euler(0f, 0f, -16f));
                rig.CapRenderers.Add(fCrown.GetComponent<Renderer>());
                rig.CapRenderers.Add(fFold.GetComponent<Renderer>());
                rig.FilaCap.gameObject.SetActive(false);

                rig.FaceCap = new GameObject("FaceCap") { layer = layer }.transform;
                rig.FaceCap.SetParent(head, false);
                var cDome = SubPart(rig.FaceCap, "CapDome", PrimitiveType.Sphere, new Vector3(0f, 0.128f, -0.002f), new Vector3(0.196f, 0.132f, 0.204f), capMat);
                var cBrim = SubPart(rig.FaceCap, "CapBrim", PrimitiveType.Cube, new Vector3(0f, 0.120f, 0.118f), new Vector3(0.155f, 0.012f, 0.110f), capMat, Quaternion.Euler(10f, 0f, 0f));
                rig.CapRenderers.Add(cDome.GetComponent<Renderer>());
                rig.CapRenderers.Add(cBrim.GetComponent<Renderer>());
                rig.FaceCap.gameObject.SetActive(false);

                // ---- Designer Sunglasses at exact eye level (Head local y = 0.088, z = 0.096) ----
                var gRoot = new GameObject("Glasses") { layer = layer }.transform;
                gRoot.SetParent(head, false);
                SubPart(gRoot, "LeftLens", PrimitiveType.Cube, new Vector3(-0.30f, 0f, 0f), new Vector3(0.42f, 0.86f, 0.24f), sharedLensMat);
                SubPart(gRoot, "RightLens", PrimitiveType.Cube, new Vector3(0.30f, 0f, 0f), new Vector3(0.42f, 0.86f, 0.24f), sharedLensMat);
                SubPart(gRoot, "TopBar", PrimitiveType.Cube, new Vector3(0f, 0.42f, 0.04f), new Vector3(1.04f, 0.20f, 0.28f), sharedSilverMat);
                SubPart(gRoot, "Bridge", PrimitiveType.Cube, new Vector3(0f, 0.08f, 0.06f), new Vector3(0.22f, 0.18f, 0.26f), sharedSilverMat);
                SubPart(gRoot, "LeftTemple", PrimitiveType.Cube, new Vector3(-0.51f, 0.12f, -1.6f), new Vector3(0.08f, 0.22f, 3.2f), sharedFrameMat);
                SubPart(gRoot, "RightTemple", PrimitiveType.Cube, new Vector3(0.51f, 0.12f, -1.6f), new Vector3(0.08f, 0.22f, 3.2f), sharedFrameMat);
                rig.Glasses = gRoot; gRoot.gameObject.SetActive(false);
            }
            if (neck != null)
            {
                var cRoot = new GameObject("Chain") { layer = layer }.transform;
                cRoot.SetParent(neck, false);
                SubPart(cRoot, "LeftLink", PrimitiveType.Cylinder, new Vector3(-0.28f, -0.35f, 0.32f), new Vector3(0.10f, 0.48f, 0.10f), sharedSilverMat, Quaternion.Euler(28f, 0f, 26f));
                SubPart(cRoot, "RightLink", PrimitiveType.Cylinder, new Vector3(0.28f, -0.35f, 0.32f), new Vector3(0.10f, 0.48f, 0.10f), sharedSilverMat, Quaternion.Euler(28f, 0f, -26f));
                SubPart(cRoot, "BottomCurve", PrimitiveType.Cylinder, new Vector3(0f, -0.78f, 0.50f), new Vector3(0.11f, 0.24f, 0.11f), sharedSilverMat, Quaternion.Euler(0f, 0f, 90f));
                SubPart(cRoot, "Medallion", PrimitiveType.Cylinder, new Vector3(0f, -0.94f, 0.54f), new Vector3(0.24f, 0.04f, 0.24f), sharedSilverMat, Quaternion.Euler(90f, 0f, 0f));
                rig.Chain = cRoot; cRoot.gameObject.SetActive(false);
            }
            if (leftForeArm != null)
            {
                var wRoot = new GameObject("Wristwatch") { layer = layer }.transform;
                wRoot.SetParent(leftForeArm, false);
                SubPart(wRoot, "Band", PrimitiveType.Cylinder, Vector3.zero, new Vector3(1.0f, 0.35f, 1.0f), sharedWatchMetalMat);
                SubPart(wRoot, "Face", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.48f), new Vector3(0.65f, 0.12f, 0.65f), sharedWatchDialMat, Quaternion.Euler(90f, 0f, 0f));
                rig.Watch = wRoot;

                rig.TattooMaterial = sharedTattooMat;
                rig.TattooL = SubPart(leftForeArm, "TattooSleeveL", PrimitiveType.Cylinder, Vector3.zero, Vector3.one, rig.TattooMaterial);
                if (rightForeArm != null)
                    rig.TattooR = SubPart(rightForeArm, "TattooSleeveR", PrimitiveType.Cylinder, Vector3.zero, Vector3.one, rig.TattooMaterial);
            }
        }

        public static Transform BuildWeaponProp(Transform hand, int layer = 0)
        {
            var wRoot = new GameObject("WeaponProp") { layer = layer }.transform;
            wRoot.SetParent(hand, false);
            var ls = hand.lossyScale;
            float inv = 1f / Mathf.Max(0.2f, Mathf.Abs(ls.y) > 0.01f ? Mathf.Abs(ls.y) : 1f);
            wRoot.localScale = Vector3.one * inv;
            wRoot.localPosition = new Vector3(0.02f * inv, 0.05f * inv, 0.04f * inv);
            wRoot.localRotation = Quaternion.Euler(75f, 0f, 0f);

            Transform Prim(Transform p, string n, PrimitiveType pt, Vector3 pos, Vector3 sc, Color col, float metal = 0.1f, float smooth = 0.4f, Quaternion? rot = null)
            {
                var g = GameObject.CreatePrimitive(pt); g.name = n; g.layer = layer; DestroyObj(g.GetComponent<Collider>());
                g.transform.SetParent(p, false); g.transform.localPosition = pos; g.transform.localScale = sc;
                if (rot.HasValue) g.transform.localRotation = rot.Value;
                string mKey = $"{ColorUtility.ToHtmlStringRGB(col)}:{metal:0.00}:{smooth:0.00}";
                if (!weaponMatCache.TryGetValue(mKey, out var m) || m == null)
                {
                    m = new Material(ShaderLib.Lit) { color = col }; m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", smooth);
                    weaponMatCache[mKey] = m;
                }
                g.GetComponent<MeshRenderer>().sharedMaterial = m;
                return g.transform;
            }
            Transform Group(string id)
            {
                var g = new GameObject(id) { layer = layer }.transform;
                g.SetParent(wRoot, false); g.gameObject.SetActive(false);
                return g;
            }
            bool GlbWeapon(Transform grp, string glbSubpath, float targetLength)
            {
                var inst = InstantiateGlb("Weapons/" + glbSubpath, grp, targetLength, sizeByHeight: true, layer: layer, centerBaseAtZero: false);
                return inst != null;
            }

            // 1. 2x4 Nail Plank / Claw Hammer
            var plank = Group("plank");
            if (!GlbWeapon(plank, "claw_hammer", 0.42f))
            {
                Prim(plank, "Board", PrimitiveType.Cube, new Vector3(0f, 0.24f, 0f), new Vector3(0.045f, 0.58f, 0.09f), new Color(0.58f, 0.38f, 0.20f), 0f, 0.2f);
                Prim(plank, "Nail1", PrimitiveType.Cylinder, new Vector3(0f, 0.46f, 0.06f), new Vector3(0.012f, 0.05f, 0.012f), new Color(0.78f, 0.80f, 0.84f), 0.8f, 0.7f, Quaternion.Euler(90f, 0f, 0f));
                Prim(plank, "Nail2", PrimitiveType.Cylinder, new Vector3(0f, 0.40f, -0.06f), new Vector3(0.012f, 0.05f, 0.012f), new Color(0.78f, 0.80f, 0.84f), 0.8f, 0.7f, Quaternion.Euler(90f, 0f, 0f));
            }

            // 2. Pepper Spray
            var pepper = Group("pepper");
            Prim(pepper, "Canister", PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 0f), new Vector3(0.045f, 0.07f, 0.045f), new Color(0.84f, 0.16f, 0.16f), 0.3f, 0.7f);
            Prim(pepper, "Nozzle", PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(0.032f, 0.02f, 0.032f), new Color(0.10f, 0.10f, 0.12f), 0.1f, 0.5f);

            // 3. Koboko Horsewhip
            var koboko = Group("koboko");
            Prim(koboko, "Grip", PrimitiveType.Cylinder, new Vector3(0f, 0.06f, 0f), new Vector3(0.038f, 0.11f, 0.038f), new Color(0.28f, 0.15f, 0.08f), 0f, 0.35f);
            Prim(koboko, "Lash1", PrimitiveType.Capsule, new Vector3(0f, 0.28f, 0.03f), new Vector3(0.024f, 0.18f, 0.024f), new Color(0.18f, 0.10f, 0.05f), 0f, 0.3f, Quaternion.Euler(14f, 0f, 0f));
            Prim(koboko, "Lash2", PrimitiveType.Capsule, new Vector3(0f, 0.54f, 0.12f), new Vector3(0.016f, 0.16f, 0.016f), new Color(0.16f, 0.09f, 0.04f), 0f, 0.3f, Quaternion.Euler(28f, 0f, 0f));

            // 4. Brass Knuckles / Claw Hammer
            var knuckles = Group("knuckles");
            if (!GlbWeapon(knuckles, "claw_hammer", 0.34f))
                Prim(knuckles, "Band", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.04f), new Vector3(0.035f, 0.11f, 0.045f), new Color(0.85f, 0.68f, 0.20f), 0.85f, 0.75f);

            // 5. Iron Rod / Baton (Rusty Lead Pipe GLB)
            var baton = Group("baton");
            if (!GlbWeapon(baton, "lead_pipe", 0.64f))
            {
                Prim(baton, "Grip", PrimitiveType.Cylinder, new Vector3(0f, 0.05f, 0f), new Vector3(0.038f, 0.09f, 0.038f), new Color(0.12f, 0.12f, 0.14f), 0.1f, 0.3f);
                Prim(baton, "Rod", PrimitiveType.Cylinder, new Vector3(0f, 0.30f, 0f), new Vector3(0.030f, 0.24f, 0.030f), new Color(0.32f, 0.35f, 0.39f), 0.75f, 0.65f);
            }

            // 6. Cutlass / Crowbar
            var cutlass = Group("cutlass");
            if (!GlbWeapon(cutlass, "crowbar", 0.66f))
            {
                Prim(cutlass, "Handle", PrimitiveType.Cube, new Vector3(0f, 0.04f, 0f), new Vector3(0.032f, 0.12f, 0.042f), new Color(0.32f, 0.18f, 0.09f), 0f, 0.3f);
                Prim(cutlass, "Guard", PrimitiveType.Cube, new Vector3(0f, 0.11f, 0f), new Vector3(0.042f, 0.018f, 0.085f), new Color(0.78f, 0.62f, 0.18f), 0.7f, 0.6f);
                Prim(cutlass, "Blade", PrimitiveType.Cube, new Vector3(0f, 0.36f, 0.01f), new Vector3(0.012f, 0.48f, 0.068f), new Color(0.82f, 0.85f, 0.89f), 0.85f, 0.8f);
            }

            // 7. 9mm Beretta Pistol (Beretta Silver GLB)
            var pistol = Group("pistol");
            if (!GlbWeapon(pistol, "bereta_silver", 0.24f))
            {
                Prim(pistol, "Grip", PrimitiveType.Cube, new Vector3(0f, 0.03f, -0.01f), new Vector3(0.032f, 0.11f, 0.046f), new Color(0.14f, 0.15f, 0.17f), 0.4f, 0.5f, Quaternion.Euler(-14f, 0f, 0f));
                Prim(pistol, "Slide", PrimitiveType.Cube, new Vector3(0f, 0.13f, 0.03f), new Vector3(0.034f, 0.20f, 0.042f), new Color(0.22f, 0.24f, 0.28f), 0.85f, 0.75f);
                Prim(pistol, "Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0.24f, 0.03f), new Vector3(0.018f, 0.04f, 0.018f), new Color(0.10f, 0.10f, 0.12f), 0.9f, 0.8f);
            }

            // 8. Pump-Action Shotgun (Gewehr 41 GLB)
            var shotgun = Group("shotgun");
            if (!GlbWeapon(shotgun, "gewehr_41", 0.86f))
            {
                Prim(shotgun, "Stock", PrimitiveType.Cube, new Vector3(0f, -0.04f, -0.02f), new Vector3(0.038f, 0.18f, 0.065f), new Color(0.42f, 0.24f, 0.11f), 0.05f, 0.35f, Quaternion.Euler(-10f, 0f, 0f));
                Prim(shotgun, "Receiver", PrimitiveType.Cube, new Vector3(0f, 0.12f, 0f), new Vector3(0.040f, 0.18f, 0.055f), new Color(0.18f, 0.20f, 0.22f), 0.8f, 0.7f);
                Prim(shotgun, "Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0.40f, 0.012f), new Vector3(0.022f, 0.24f, 0.022f), new Color(0.15f, 0.16f, 0.18f), 0.85f, 0.75f);
                Prim(shotgun, "Pump", PrimitiveType.Cylinder, new Vector3(0f, 0.32f, -0.014f), new Vector3(0.034f, 0.10f, 0.034f), new Color(0.45f, 0.26f, 0.12f), 0.05f, 0.35f);
            }

            // 9. Tactical Assault Rifle (HK416 GLB)
            var rifle = Group("rifle");
            if (!GlbWeapon(rifle, "hk416", 0.78f))
            {
                Prim(rifle, "Stock", PrimitiveType.Cube, new Vector3(0f, -0.06f, -0.02f), new Vector3(0.036f, 0.18f, 0.062f), new Color(0.46f, 0.25f, 0.11f), 0.05f, 0.35f, Quaternion.Euler(-8f, 0f, 0f));
                Prim(rifle, "Receiver", PrimitiveType.Cube, new Vector3(0f, 0.12f, 0f), new Vector3(0.040f, 0.22f, 0.058f), new Color(0.16f, 0.18f, 0.20f), 0.82f, 0.7f);
                Prim(rifle, "Handguard", PrimitiveType.Cube, new Vector3(0f, 0.30f, 0f), new Vector3(0.038f, 0.14f, 0.050f), new Color(0.48f, 0.26f, 0.12f), 0.05f, 0.35f);
                Prim(rifle, "Magazine", PrimitiveType.Cube, new Vector3(0f, 0.16f, 0.075f), new Vector3(0.028f, 0.065f, 0.13f), new Color(0.22f, 0.22f, 0.25f), 0.75f, 0.65f, Quaternion.Euler(22f, 0f, 0f));
                Prim(rifle, "Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0.48f, 0.008f), new Vector3(0.018f, 0.18f, 0.018f), new Color(0.12f, 0.13f, 0.15f), 0.9f, 0.8f);
            }

            // 10. Navy Tactical SMG / Automatic Pistol (Glock GLB)
            var smg = Group("smg");
            if (!GlbWeapon(smg, "glock_pistol", 0.32f))
            {
                Prim(smg, "Stock", PrimitiveType.Cube, new Vector3(0f, -0.04f, -0.01f), new Vector3(0.030f, 0.14f, 0.050f), new Color(0.12f, 0.14f, 0.18f), 0.7f, 0.6f);
                Prim(smg, "Receiver", PrimitiveType.Cube, new Vector3(0f, 0.12f, 0f), new Vector3(0.038f, 0.20f, 0.054f), new Color(0.15f, 0.18f, 0.24f), 0.85f, 0.75f);
                Prim(smg, "Magazine", PrimitiveType.Cube, new Vector3(0f, 0.16f, 0.07f), new Vector3(0.024f, 0.05f, 0.12f), new Color(0.10f, 0.12f, 0.15f), 0.8f, 0.7f, Quaternion.Euler(16f, 0f, 0f));
                Prim(smg, "Suppressor", PrimitiveType.Cylinder, new Vector3(0f, 0.34f, 0.005f), new Vector3(0.028f, 0.12f, 0.028f), new Color(0.08f, 0.10f, 0.12f), 0.8f, 0.65f);
            }

            // 11. M24 Marksman Sniper Rifle (Gewehr 41 GLB)
            var sniper = Group("sniper");
            if (!GlbWeapon(sniper, "gewehr_41", 0.96f))
            {
                Prim(sniper, "Stock", PrimitiveType.Cube, new Vector3(0f, -0.08f, -0.02f), new Vector3(0.036f, 0.22f, 0.064f), new Color(0.22f, 0.28f, 0.18f), 0.2f, 0.4f);
                Prim(sniper, "Receiver", PrimitiveType.Cube, new Vector3(0f, 0.16f, 0f), new Vector3(0.038f, 0.26f, 0.052f), new Color(0.16f, 0.18f, 0.18f), 0.85f, 0.75f);
                Prim(sniper, "Scope", PrimitiveType.Cylinder, new Vector3(0f, 0.16f, -0.045f), new Vector3(0.028f, 0.11f, 0.028f), new Color(0.08f, 0.09f, 0.10f), 0.9f, 0.85f);
                Prim(sniper, "Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0.52f, 0.006f), new Vector3(0.018f, 0.26f, 0.018f), new Color(0.12f, 0.13f, 0.15f), 0.9f, 0.85f);
            }

            // 12. PKM Heavy Machine Gun (HK416 Heavy GLB)
            var lmg = Group("lmg");
            if (!GlbWeapon(lmg, "hk416", 0.88f))
            {
                Prim(lmg, "Stock", PrimitiveType.Cube, new Vector3(0f, -0.07f, -0.02f), new Vector3(0.042f, 0.20f, 0.068f), new Color(0.42f, 0.24f, 0.10f), 0.1f, 0.35f);
                Prim(lmg, "Receiver", PrimitiveType.Cube, new Vector3(0f, 0.15f, 0f), new Vector3(0.048f, 0.26f, 0.064f), new Color(0.18f, 0.20f, 0.22f), 0.82f, 0.7f);
                Prim(lmg, "AmmoBox", PrimitiveType.Cube, new Vector3(0.032f, 0.16f, 0.065f), new Vector3(0.055f, 0.10f, 0.095f), new Color(0.24f, 0.32f, 0.20f), 0.6f, 0.5f);
                Prim(lmg, "Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0.48f, 0.008f), new Vector3(0.024f, 0.22f, 0.024f), new Color(0.12f, 0.14f, 0.16f), 0.9f, 0.8f);
            }

            wRoot.gameObject.SetActive(false);
            return wRoot;
        }

        public static void SetWeapon(RigInstance rig, Transform host, string weaponId, int layer = 0)
        {
            string key = string.IsNullOrEmpty(weaponId) || weaponId == "fists" ? "" : (weaponId == "weapon" ? "baton" : weaponId);
            Transform wRoot = rig != null ? rig.WeaponRoot : null;
            if (wRoot == null && host != null)
                wRoot = Find(host, "WeaponProp");
            if (wRoot == null)
            {
                if (string.IsNullOrEmpty(key)) return;
                Transform attach = (rig != null ? rig.RightHand ?? rig.RightForeArm ?? rig.Hips : null) ?? (host != null ? Find(host, "RightHand") ?? host : null);
                if (attach == null) return;
                wRoot = BuildWeaponProp(attach, layer);
                if (host != null && attach == host)
                {
                    wRoot.localPosition = new Vector3(0.34f, 0.95f, 0.26f);
                    wRoot.localRotation = Quaternion.Euler(45f, 0f, 0f);
                }
                if (rig != null) rig.WeaponRoot = wRoot;
            }
            bool any = false;
            for (int i = 0; i < wRoot.childCount; i++)
            {
                var ch = wRoot.GetChild(i);
                bool match = !string.IsNullOrEmpty(key) && ch.name == key;
                ch.gameObject.SetActive(match);
                if (match) any = true;
            }
            wRoot.gameObject.SetActive(any);
        }

        // Attachments are children of scaled bones; use the magnitude of the bone scale so rotated bones never invert or explode the attachment.
        static void Fit(Transform t, Transform bone, Vector3 worldSize, Vector3 worldOffset, float fallbackScale = 1f)
        {
            var ls = bone.lossyScale;
            float sx = Mathf.Max(0.25f, Mathf.Abs(ls.x) > 0.01f ? Mathf.Abs(ls.x) : fallbackScale);
            float sy = Mathf.Max(0.25f, Mathf.Abs(ls.y) > 0.01f ? Mathf.Abs(ls.y) : fallbackScale);
            float sz = Mathf.Max(0.25f, Mathf.Abs(ls.z) > 0.01f ? Mathf.Abs(ls.z) : fallbackScale);
            t.localScale = new Vector3(worldSize.x / sx, worldSize.y / sy, worldSize.z / sz);
            t.localPosition = new Vector3(worldOffset.x / sx, worldOffset.y / sy, worldOffset.z / sz);
            t.localRotation = Quaternion.identity;
        }

        static readonly string[] OutfitNames = { "Ankara shirt", "Senator", "Buba & Sokoto", "Agbada", "Super Eagles", "T-shirt & jeans", "Singlet & jeans", "Tank top & shorts", "Jacket & jeans", "Gown", "Skirt & blouse" };

        static void SetTex(Material m, Texture tex, float tileX = 1f, float tileY = 1f)
        {
            if (m == null) return;
            if (m.HasProperty("baseColorTexture"))
            {
                m.SetTexture("baseColorTexture", tex);
                if (m.HasProperty("baseColorTexture_ST")) m.SetVector("baseColorTexture_ST", new Vector4(tileX, tileY, 0f, 0f));
            }
            if (m.HasProperty("_BaseMap"))
            {
                m.SetTexture("_BaseMap", tex);
                m.SetTextureScale("_BaseMap", new Vector2(tileX, tileY));
            }
            m.mainTexture = tex;
        }

        // Re-dress a rig: the fabric print on the top, multi-zone sleeves, trousers/shorts/socks, two-tone sneakers,
        // skin, sculpted 3D hair, 3D beard, and Fila/Face cap/Glasses/Chain accessories.
        public static void Dress(RigInstance rig, Look look)
        {
            string outfit = look.Outfit >= 0 && look.Outfit < OutfitNames.Length ? OutfitNames[look.Outfit] : "Ankara shirt";
            Color richSkin = Color.Lerp(look.Skin, new Color(0.84f, 0.58f, 0.40f), 0.10f);
            var print = Data != null ? Fabrics.Get(Data, look.Fabric) : null;
            Color fabricColour = Data != null && Data.fabrics.Length > 0
                ? Mannequin.Hex(Data.fabrics[Mathf.Clamp(look.Fabric, 0, Data.fabrics.Length - 1)].baseColor)
                : new Color(0.88f, 0.48f, 0.1f);
            bool isJersey = outfit == "Super Eagles" || (Data != null && look.Fabric >= 0 && look.Fabric < Data.fabrics.Length && Data.fabrics[look.Fabric].pattern == "jersey");
            float topTile = isJersey ? 1f : 2f;

            DressSlots(rig, look, outfit, richSkin, print);

            // 1. Torso Top (always keeps baseColorTexture == print on rig.Top[0] so SmokeTest & fabric prints work everywhere)
            foreach (var m in rig.Top)
            {
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.28f);
                if (print == null) { Tint(m, fabricColour); continue; }
                Tint(m, Color.white);
                SetTex(m, print, topTile, topTile);
            }

            // 2. Multi-zone Sleeves (Sleeveless vs Short-Sleeve with bare tattooed forearms vs Long-Sleeve / Open Jacket)
            bool sleeveless = outfit == "Singlet & jeans" || outfit == "Tank top & shorts";
            bool shortSleeve = outfit == "T-shirt & jeans" || outfit == "Ankara shirt" || outfit == "Super Eagles" || outfit == "Gown" || outfit == "Skirt & blouse";
            bool isJacket = outfit == "Jacket & jeans";
            Color jacketCol = Color.Lerp(look.Pants, new Color(0.12f, 0.14f, 0.18f), 0.55f);

            if (rig.SleeveUpperMat != null)
            {
                if (sleeveless)
                {
                    SetTex(rig.SleeveUpperMat, null);
                    Tint(rig.SleeveUpperMat, richSkin);
                }
                else if (isJacket)
                {
                    SetTex(rig.SleeveUpperMat, null);
                    Tint(rig.SleeveUpperMat, jacketCol);
                }
                else
                {
                    SetTex(rig.SleeveUpperMat, print, topTile, topTile);
                    Tint(rig.SleeveUpperMat, print != null ? Color.white : fabricColour);
                }
            }
            if (rig.SleeveForeMat != null)
            {
                if (sleeveless || shortSleeve)
                {
                    // Bare forearms with subtle forearm tattoo weave
                    SetTex(rig.SleeveForeMat, ShaderLib.TattooTex, 1f, 1f);
                    Tint(rig.SleeveForeMat, richSkin);
                }
                else if (isJacket)
                {
                    SetTex(rig.SleeveForeMat, null);
                    Tint(rig.SleeveForeMat, jacketCol);
                }
                else
                {
                    SetTex(rig.SleeveForeMat, print, topTile, topTile);
                    Tint(rig.SleeveForeMat, print != null ? Color.white : fabricColour);
                }
            }

            // 3. Trousers / Shorts / Bare Calves / High White Crew Socks
            bool isShorts = outfit == "Tank top & shorts" || outfit == "Super Eagles";
            bool isDress = outfit == "Gown" || outfit == "Skirt & blouse";
            bool isDenim = outfit == "T-shirt & jeans" || outfit == "Singlet & jeans" || outfit == "Jacket & jeans" || outfit == "Ankara shirt";
            var pantsTex = isDress && print != null ? print : Fabrics.GetPantsTexture(look.Pants, isDenim, isShorts);

            foreach (var m in rig.Bottom)
            {
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.26f);
                if (pantsTex != null)
                {
                    SetTex(m, pantsTex, isDress ? 2f : 1f, isDress ? 2f : 1f);
                    Tint(m, Color.white);
                }
                else Tint(m, look.Pants);
            }
            if (rig.LegCalfMat != null)
            {
                if (isShorts || isDress)
                {
                    // Bare calves
                    SetTex(rig.LegCalfMat, null);
                    Tint(rig.LegCalfMat, richSkin);
                }
                else
                {
                    SetTex(rig.LegCalfMat, pantsTex, 1f, 1f);
                    Tint(rig.LegCalfMat, pantsTex != null ? Color.white : look.Pants);
                }
            }
            if (rig.SockMat != null)
            {
                if (isShorts)
                {
                    // Crisp white athletic crew socks
                    SetTex(rig.SockMat, null);
                    Tint(rig.SockMat, new Color(0.95f, 0.96f, 0.98f));
                }
                else if (isDress)
                {
                    SetTex(rig.SockMat, null);
                    Tint(rig.SockMat, richSkin);
                }
                else
                {
                    SetTex(rig.SockMat, pantsTex, 1f, 1f);
                    Tint(rig.SockMat, pantsTex != null ? Color.white : look.Pants);
                }
            }

            // 4. Two-tone Footwear (Vulcanized White Sneaker Midsole + Contrasting Suede/Leather Upper)
            bool formalShoes = outfit == "Senator" || outfit == "Agbada";
            if (rig.ShoeSoleMat != null)
            {
                SetTex(rig.ShoeSoleMat, null);
                Tint(rig.ShoeSoleMat, formalShoes ? new Color(0.14f, 0.11f, 0.09f) : new Color(0.95f, 0.96f, 0.98f));
            }
            if (rig.ShoeUpperMat != null)
            {
                Color shoeCol = formalShoes
                    ? new Color(0.18f, 0.12f, 0.09f)
                    : Color.Lerp(look.Pants, new Color(0.16f, 0.18f, 0.22f), 0.55f);
                SetTex(rig.ShoeUpperMat, Fabrics.GetSneakerTexture(shoeCol), 1f, 1f);
                Tint(rig.ShoeUpperMat, Color.white);
            }

            // 5. Skin, Hair, Beard & Cap colours
            foreach (var m in rig.Skin)
            {
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.38f);
                Tint(m, richSkin);
            }
            if (rig.TattooMaterial != null) Tint(rig.TattooMaterial, richSkin);
            foreach (var m in rig.Hair)
            {
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.24f);
                Tint(m, look.Hair);
            }
            Color capCol = look.Cap ?? new Color(0.12f, 0.23f, 0.37f);
            var filaTex = Fabrics.GetFilaTexture(capCol);
            foreach (var m in rig.Cap)
            {
                SetTex(m, filaTex, 1f, 1f);
                Tint(m, Color.white);
            }
        }

        static void DressSlots(RigInstance rig, Look look, string outfit, Color richSkin, Texture2D print)
        {
            // Body type: Slim / Average / Big widen or narrow the whole rig.
            float wide = look.BodyType == 2 ? 1.12f : look.BodyType == 0 ? 0.92f : 1f;
            rig.Root.transform.localScale = new Vector3(rig.BaseScale * wide, rig.BaseScale, rig.BaseScale * wide);

            string acc = Data != null && look.Accessory >= 0 && look.Accessory < Data.accessories.Length ? Data.accessories[look.Accessory] : "None";
            bool showFila = acc == "Fila";
            bool showFaceCap = acc == "Face cap";
            bool hasHat = showFila || showFaceCap;

            if (rig.FilaCap != null) rig.FilaCap.gameObject.SetActive(showFila);
            if (rig.FaceCap != null) rig.FaceCap.gameObject.SetActive(showFaceCap);
            foreach (var r in rig.CapRenderers)
                if (r != null) r.enabled = hasHat;

            // Hair style: Short, Fade, Afro, Braids, Bald
            string hair = Data != null && look.HairStyle >= 0 && look.HairStyle < Data.hairStyles.Length ? Data.hairStyles[look.HairStyle] : "Short";
            bool isBald = hair == "Bald";
            if (rig.HairShort != null) rig.HairShort.gameObject.SetActive(!isBald && (hair == "Short" || hasHat));
            if (rig.HairFade != null) rig.HairFade.gameObject.SetActive(!isBald && !hasHat && hair == "Fade");
            if (rig.HairAfro != null) rig.HairAfro.gameObject.SetActive(!isBald && !hasHat && hair == "Afro");
            if (rig.HairBraids != null) rig.HairBraids.gameObject.SetActive(!isBald && !hasHat && hair == "Braids");
            foreach (var r in rig.HairRenderers)
                if (r != null) r.enabled = !isBald;

            // Facial hair: None, Beard, Goatee, Moustache
            string beard = Data != null && look.FacialHair >= 0 && look.FacialHair < Data.facialHair.Length ? Data.facialHair[look.FacialHair] : "None";
            bool hasBeard = beard != "None";
            if (rig.BeardFull != null) rig.BeardFull.gameObject.SetActive(beard == "Beard");
            if (rig.BeardGoatee != null) rig.BeardGoatee.gameObject.SetActive(beard == "Goatee");
            if (rig.BeardMoustache != null) rig.BeardMoustache.gameObject.SetActive(beard == "Moustache" || beard == "Beard" || beard == "Goatee");
            foreach (var r in rig.BeardRenderers)
                if (r != null) r.enabled = hasBeard;

            // Accessories & layered garments
            bool showGlasses = acc == "Glasses";
            bool showChain = acc == "Chain" || outfit == "T-shirt & jeans" || outfit == "Singlet & jeans" || outfit == "Tank top & shorts" || outfit == "Super Eagles";
            bool bareArms = outfit == "T-shirt & jeans" || outfit == "Singlet & jeans" || outfit == "Tank top & shorts" || outfit == "Super Eagles" || outfit == "Ankara shirt";
            bool showBelt = outfit == "T-shirt & jeans" || outfit == "Singlet & jeans" || outfit == "Jacket & jeans" || outfit == "Ankara shirt";
            bool showJacket = outfit == "Jacket & jeans";
            bool showPlacket = outfit == "Senator" || outfit == "Agbada" || outfit == "Buba & Sokoto";

            if (rig.Glasses != null)
            {
                rig.Glasses.gameObject.SetActive(showGlasses);
                if (showGlasses) Fit(rig.Glasses, rig.Glasses.parent, new Vector3(0.148f, 0.034f, 0.030f), new Vector3(0f, 0.088f, 0.098f), rig.BaseScale);
            }
            if (rig.Chain != null)
            {
                rig.Chain.gameObject.SetActive(showChain);
                if (showChain) Fit(rig.Chain, rig.Chain.parent, new Vector3(0.14f, 0.09f, 0.14f), new Vector3(0f, -0.02f, 0.035f), rig.BaseScale);
            }
            if (rig.Watch != null)
            {
                rig.Watch.gameObject.SetActive(true);
                Fit(rig.Watch, rig.Watch.parent, new Vector3(0.068f, 0.028f, 0.068f), new Vector3(0f, 0.14f, 0f), rig.BaseScale);
            }
            if (rig.TattooL != null)
            {
                rig.TattooL.gameObject.SetActive(bareArms);
                if (bareArms) Fit(rig.TattooL, rig.TattooL.parent, new Vector3(0.068f, 0.082f, 0.068f), new Vector3(0f, 0.06f, 0f), rig.BaseScale);
            }
            if (rig.TattooR != null)
            {
                rig.TattooR.gameObject.SetActive(bareArms);
                if (bareArms) Fit(rig.TattooR, rig.TattooR.parent, new Vector3(0.068f, 0.082f, 0.068f), new Vector3(0f, 0.06f, 0f), rig.BaseScale);
            }
            if (rig.BeltRoot != null)
                rig.BeltRoot.gameObject.SetActive(showBelt);
            if (rig.JacketRoot != null)
            {
                rig.JacketRoot.gameObject.SetActive(showJacket);
                if (showJacket && rig.JacketMaterial != null)
                    rig.JacketMaterial.color = Color.Lerp(look.Pants, new Color(0.14f, 0.16f, 0.20f), 0.55f);
            }
            if (rig.CollarPlacket != null)
                rig.CollarPlacket.gameObject.SetActive(showPlacket);
            if (rig.AgbadaWings != null)
                rig.AgbadaWings.gameObject.SetActive(outfit == "Agbada");

            // Outfit silhouette overlays in the fabric print, scaled snugly to the waist so they never protrude like a barrel.
            if (rig.Overlay != null)
            {
                var hips = rig.Overlay.parent;
                bool show = outfit == "Senator" || outfit == "Agbada" || outfit == "Gown" || outfit == "Skirt & blouse" || outfit == "Buba & Sokoto";
                rig.Overlay.gameObject.SetActive(show);
                if (show)
                {
                    Vector3 size = outfit == "Agbada" ? new Vector3(0.33f, 0.22f, 0.22f) : outfit == "Gown" ? new Vector3(0.31f, 0.30f, 0.21f) : new Vector3(0.30f, 0.20f, 0.20f);
                    Vector3 offset = new Vector3(0f, -0.02f, 0.005f);
                    Fit(rig.Overlay, hips, size, offset, rig.BaseScale);
                    rig.Overlay.localScale = new Vector3(rig.Overlay.localScale.x, rig.Overlay.localScale.y * 0.5f, rig.Overlay.localScale.z);   // unit cylinder is 2 high
                    if (print != null && rig.OverlayMaterial != null)
                    {
                        SetTex(rig.OverlayMaterial, print, 2f, 2f);
                        rig.OverlayMaterial.color = Color.white;
                    }
                }
            }
        }

        static void Tint(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty("baseColorFactor")) m.SetColor("baseColorFactor", c);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.color = c;
        }

        public static Bounds Bounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            var b = new Bounds(go.transform.position, Vector3.zero);
            bool any = false;
            foreach (var r in rends)
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return any ? b : new Bounds(go.transform.position, Vector3.one);
        }

        static readonly Dictionary<string, GameObject> glbPrefabCache = new Dictionary<string, GameObject>();
        static readonly Dictionary<Material, Material> glbLitMatCache = new Dictionary<Material, Material>();

        public static void RemapGlbMaterialsToLit(GameObject go, float smoothness = 0.35f)
        {
            if (go == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null) continue;
                    if (src.renderQueue >= 2800 || src.name.Contains("Transp") || (src.HasProperty("baseColorFactor") && src.GetColor("baseColorFactor").a < 0.96f))
                        continue;
                    if (!glbLitMatCache.TryGetValue(src, out var lit) || lit == null)
                    {
                        lit = new Material(ShaderLib.Lit) { name = src.name + " (URP_Lit)" };
                        if (src.HasProperty("baseColorTexture"))
                        {
                            var tex = src.GetTexture("baseColorTexture");
                            lit.SetTexture("_BaseMap", tex);
                            lit.mainTexture = tex;
                        }
                        else if (src.mainTexture != null)
                        {
                            lit.SetTexture("_BaseMap", src.mainTexture);
                            lit.mainTexture = src.mainTexture;
                        }
                        Color col = src.HasProperty("baseColorFactor") ? src.GetColor("baseColorFactor") : src.color;
                        lit.color = col;
                        if (lit.HasProperty("_BaseColor")) lit.SetColor("_BaseColor", col);
                        if (src.HasProperty("bumpTexture"))
                        {
                            var bump = src.GetTexture("bumpTexture");
                            if (bump != null && lit.HasProperty("_BumpMap")) lit.SetTexture("_BumpMap", bump);
                        }
                        lit.SetFloat("_Smoothness", smoothness);
                        lit.enableInstancing = true;
                        glbLitMatCache[src] = lit;
                    }
                    mats[i] = lit;
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        // Instantiates an optimized GLB prefab from Resources/Models/<subpath>, remaps its materials to URP Lit,
        // strips child colliders, and fits it to targetSize (by height or horizontal footprint).
        public static GameObject InstantiateGlb(string subpath, Transform parent, float targetSize, bool sizeByHeight = true, int layer = 0, bool centerBaseAtZero = true)
        {
            if (!glbPrefabCache.TryGetValue(subpath, out var p))
            {
                p = Resources.Load<GameObject>("Models/" + subpath);
                glbPrefabCache[subpath] = p;
            }
            if (p == null) return null;
            var go = Object.Instantiate(p, parent, false);
            go.name = p.name;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
            {
                if (Application.isPlaying) Object.Destroy(c);
                else Object.DestroyImmediate(c);
            }
            RemapGlbMaterialsToLit(go);
            if (targetSize > 0.001f)
            {
                var b = Bounds(go);
                float refSpan = sizeByHeight ? b.size.y : Mathf.Max(b.size.x, b.size.z);
                if (refSpan > 0.001f)
                {
                    float s = targetSize / refSpan;
                    go.transform.localScale = go.transform.localScale * s;
                }
                if (centerBaseAtZero && parent != null)
                {
                    b = Bounds(go);
                    Vector3 worldDelta = new Vector3(parent.position.x - b.center.x, parent.position.y - b.min.y, parent.position.z - b.center.z);
                    go.transform.position += worldDelta;
                }
            }
            return go;
        }

        public static GameObject InstantiateGlb(string resourcePath, string name, Vector3 pos, Vector3 targetSize, Transform parent, float yaw = 0f, bool addCollider = true)
        {
            string fullPath = resourcePath.StartsWith("Models/") ? resourcePath : "Models/" + resourcePath;
            if (!glbPrefabCache.TryGetValue(fullPath, out var p))
            {
                p = Resources.Load<GameObject>(fullPath);
                glbPrefabCache[fullPath] = p;
            }
            if (p == null) return null;
            var go = Object.Instantiate(p, parent);
            go.name = name;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * p.transform.localRotation;
            go.transform.localScale = Vector3.one;
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
            {
                if (Application.isPlaying) Object.Destroy(c);
                else Object.DestroyImmediate(c);
            }
            RemapGlbMaterialsToLit(go);
            var b = Bounds(go);
            float fit = 1f;
            if (b.size.x > 0.001f && b.size.y > 0.001f && b.size.z > 0.001f)
            {
                fit = Mathf.Min(targetSize.x / b.size.x, targetSize.y / b.size.y, targetSize.z / b.size.z);
                go.transform.localScale = Vector3.one * fit;
                b = Bounds(go);
                go.transform.position += new Vector3(pos.x - b.center.x, pos.y - b.min.y, pos.z - b.center.z);
                b = Bounds(go);
            }
            if (addCollider && fit > 0.0001f)
            {
                var col = go.AddComponent<BoxCollider>();
                col.center = go.transform.InverseTransformPoint(b.center);
                col.size = new Vector3(b.size.x / fit, b.size.y / fit, b.size.z / fit);
            }
            return go;
        }

        public static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }
    }
}
