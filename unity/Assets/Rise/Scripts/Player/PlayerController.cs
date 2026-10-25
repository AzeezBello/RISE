using UnityEngine;
using Rise.Entities;

namespace Rise.Player
{
    // Third-person character: camera-relative movement with ease-in acceleration, sprint, jump and gravity through a
    // CharacterController so kerbs, pavements and bridge decks are walked naturally. Keyboard and gamepad (legacy Input).
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public float WalkSpeed = 4.6f, RunSpeed = 8.5f, Acceleration = 14f, TurnSpeed = 720f, JumpSpeed = 7.2f, Gravity = -24f;
        public Transform CameraTransform;
        public bool InputEnabled = true, CanSprint = true;
        [System.NonSerialized] public bool Swimming;   // set by the session when in water off a bridge deck: slow, no sprint or jump, body low
        [System.NonSerialized] public float? MinFloorY;
        public const float SwimSpeed = 1.9f;
        public const int Layer = 8;

        public float Speed01 { get; private set; }   // 0 idle … 1 full sprint, for camera FOV and animation later
        public bool IsRunning { get; private set; }
        public bool IsGrounded => cc.isGrounded;

        CharacterController cc;
        RigInstance rig;
        Vector3 spawn, velocity;
        float speed;
        public bool HasRig => rig != null;
        public int Fabric => rig != null ? currentLook.Fabric : 0;
        Look currentLook = Look.Default;
        public void ApplyLook(Look look) { currentLook = look; if (rig != null) CharacterRig.Dress(rig, look); }
        public string EquippedWeapon { get; private set; } = "";
        public const float SwingDuration = 0.44f;
        float swingT, guardT, lastSwingTime = -10f;
        public int ComboStep { get; private set; }
        public bool IsSwinging => swingT > 0f;
        Transform combatArcRoot, combatImpactRing;
        readonly System.Collections.Generic.List<Material> combatArcMats = new System.Collections.Generic.List<Material>();
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        void OnDestroy()
        {
            for (int i = 0; i < combatArcMats.Count; i++)
                if (combatArcMats[i] != null) Destroy(combatArcMats[i]);
            combatArcMats.Clear();
        }

        public bool IsEquippedGun =>
            EquippedWeapon == "pistol" || EquippedWeapon == "shotgun" || EquippedWeapon == "smg" ||
            EquippedWeapon == "rifle" || EquippedWeapon == "sniper" || EquippedWeapon == "lmg";

        public void SetWeapon(string weaponId)
        {
            EquippedWeapon = weaponId ?? "";
            CharacterRig.SetWeapon(rig, transform, EquippedWeapon, Layer);
        }

        public void SwingWeapon()
        {
            if (Time.time - lastSwingTime < 1.15f)
                ComboStep = (ComboStep + 1) % 3;
            else
                ComboStep = 0;
            lastSwingTime = Time.time;
            swingT = SwingDuration;
            guardT = 1.45f;
            AnimState = "attack";
            rig?.SetState("attack", 1.1f);
            EnsureCombatArc();
        }

        void EnsureCombatArc()
        {
            if (combatArcRoot != null) return;
            combatArcRoot = new GameObject("CombatArc").transform;
            combatArcRoot.SetParent(transform, false);
            combatArcRoot.localPosition = new Vector3(0f, 1.16f, 0.72f);

            var unlit = ShaderLib.Unlit;
            for (int i = 0; i < 3; i++)
            {
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = "ArcSeg_" + i;
                seg.layer = Layer;
                Destroy(seg.GetComponent<Collider>());
                seg.transform.SetParent(combatArcRoot, false);
                float ang = (i - 1) * 28f;
                seg.transform.localPosition = new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad) * 0.42f, (1 - i) * 0.08f, Mathf.Cos(ang * Mathf.Deg2Rad) * 0.18f);
                seg.transform.localRotation = Quaternion.Euler(14f, ang, -22f);
                seg.transform.localScale = new Vector3(0.36f, 0.045f, 0.09f);
                var m = new Material(unlit) { color = new Color(1f, 0.86f, 0.22f, 0.92f) };
                seg.GetComponent<MeshRenderer>().sharedMaterial = m;
                seg.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                combatArcMats.Add(m);
            }

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "ImpactBurst";
            ring.layer = Layer;
            Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(combatArcRoot, false);
            ring.transform.localPosition = new Vector3(0f, -0.05f, 0.28f);
            ring.transform.localScale = new Vector3(0.55f, 0.015f, 0.55f);
            var rm = new Material(unlit) { color = new Color(1f, 0.92f, 0.45f, 0.9f) };
            ring.GetComponent<MeshRenderer>().sharedMaterial = rm;
            ring.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            combatArcMats.Add(rm);
            combatImpactRing = ring.transform;

            combatArcRoot.gameObject.SetActive(false);
        }

        public string AnimState { get; private set; } = "idle";
        float jumpAnimT, vehicleAnimT, lastYaw;
        string vehicleAnimState = "";

        public bool HasExtendedClips => rig != null && rig.HasClip("jump") && rig.HasClip("turn") && rig.HasClip("enter_vehicle") && rig.HasClip("exit_vehicle") && rig.HasClip("attack");

        public void TriggerJumpAnim()
        {
            jumpAnimT = 0.48f;
            AnimState = "jump";
            rig?.SetState("jump", 1f);
        }

        public void TriggerTurnAnim()
        {
            AnimState = "turn";
            rig?.SetState("turn", 1.15f);
        }

        public void TriggerVehicleEnterAnim()
        {
            vehicleAnimState = "enter_vehicle";
            vehicleAnimT = 0.50f;
            AnimState = "enter_vehicle";
            rig?.SetState("enter_vehicle", 1f);
        }

        public void TriggerVehicleExitAnim()
        {
            vehicleAnimState = "exit_vehicle";
            vehicleAnimT = 0.50f;
            AnimState = "exit_vehicle";
            rig?.SetState("exit_vehicle", 1f);
        }

        public static PlayerController Create(Vector3 position, float yaw) => Create(position, yaw, Look.Default);
        public static PlayerController Create(Vector3 position, float yaw, Look look)
        {
            var go = new GameObject("Player") { layer = Layer };
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.38f; cc.center = new Vector3(0, 0.9f, 0); cc.stepOffset = 0.45f; cc.slopeLimit = 50f; cc.skinWidth = 0.04f;
            var pc = go.AddComponent<PlayerController>();
            pc.spawn = position;
            // The default look: ankara top, dark trousers, fila. A real rig when the character assets are present.
            if (CharacterRig.Available) pc.rig = CharacterRig.Build(go.transform, look, Layer);
            else Mannequin.Build(go.transform, Mannequin.Hex("#e07b1a"), look.Pants, look.Skin, look.Cap, Layer);
            return pc;
        }

        void Awake() { cc = GetComponent<CharacterController>(); }

        public void Teleport(Vector3 position, float yaw)
        {
            bool wasEnabled = cc.enabled; cc.enabled = false;
            transform.position = position; transform.rotation = Quaternion.Euler(0, yaw, 0);
            velocity = Vector3.zero; speed = 0f;
            Physics.SyncTransforms();
            cc.enabled = wasEnabled;
        }

        void Update()
        {
            var input = InputEnabled ? GameInput.Move : Vector2.zero;
            Vector3 fwd = Vector3.forward, right = Vector3.right;
            if (CameraTransform != null)
            {
                fwd = Vector3.ProjectOnPlane(CameraTransform.forward, Vector3.up).normalized;
                right = Vector3.Cross(Vector3.up, fwd);
            }
            var dir = (fwd * input.y + right * input.x);
            IsRunning = CanSprint && input.sqrMagnitude > 0.01f && GameInput.Sprint;
            if (Swimming) IsRunning = false;
            float target = input.magnitude * (Swimming ? SwimSpeed : IsRunning ? RunSpeed : WalkSpeed);
            speed = Mathf.MoveTowards(speed, target, Acceleration * Time.deltaTime * (target < speed ? 1.8f : 1f));
            Speed01 = Mathf.InverseLerp(0f, RunSpeed, speed);

            if (dir.sqrMagnitude > 0.001f)
            {
                var look = Quaternion.LookRotation(dir.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, TurnSpeed * Time.deltaTime);
            }
            float yawDelta = Mathf.Abs(Mathf.DeltaAngle(lastYaw, transform.eulerAngles.y));
            lastYaw = transform.eulerAngles.y;
            float turnRate = Time.deltaTime > 0.0001f ? yawDelta / Time.deltaTime : 0f;
            var horizontal = (dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward) * speed;

            if (cc.isGrounded)
            {
                if (velocity.y < 0f) velocity.y = -2f;
                if (InputEnabled && !Swimming && GameInput.JumpPressed) { velocity.y = JumpSpeed; jumpAnimT = 0.48f; }
            }
            velocity.y += Gravity * Time.deltaTime;

            if (swingT > 0f)
                swingT = Mathf.Max(0f, swingT - Time.deltaTime);
            if (guardT > 0f)
                guardT = Mathf.Max(0f, guardT - Time.deltaTime);

            float swing01 = swingT > 0f ? 1f - (swingT / SwingDuration) : 0f;
            Vector3 combatLunge = Vector3.zero;
            if (!Swimming && swingT > 0f && !IsEquippedGun && speed < 2.5f && swing01 >= 0.14f && swing01 <= 0.62f)
            {
                combatLunge = transform.forward * (1.75f * Mathf.Sin(swing01 * Mathf.PI));
            }
            cc.Move((horizontal + combatLunge + Vector3.up * velocity.y) * Time.deltaTime);
            if (MinFloorY.HasValue && transform.position.y < MinFloorY.Value)
            {
                bool en = cc.enabled; cc.enabled = false;
                var p = transform.position; p.y = MinFloorY.Value; transform.position = p;
                cc.enabled = en;
                if (velocity.y < 0f) velocity.y = -2f;
            }

            // Locomotion & action state → clip (idle, walk, run, jump, turn, enter_vehicle, exit_vehicle, attack cross-fades).
            if (vehicleAnimT > 0f)
            {
                vehicleAnimT = Mathf.Max(0f, vehicleAnimT - Time.deltaTime);
                AnimState = vehicleAnimState;
            }
            else if (swingT > 0f && !Swimming)
            {
                AnimState = "attack";
            }
            else if (!Swimming && (!cc.isGrounded || jumpAnimT > 0f))
            {
                jumpAnimT = Mathf.Max(0f, jumpAnimT - Time.deltaTime);
                AnimState = "jump";
            }
            else if (!Swimming && turnRate > 120f && speed < 5.0f)
            {
                AnimState = "turn";
            }
            else
            {
                AnimState = speed < 0.3f ? "idle" : IsRunning && speed > 5.5f ? "run" : "walk";
            }
            // Swimming: the body sits in the water and the walk plays slowly as a stroke.
            if (rig != null)
            {
                var rt = rig.Root.transform;
                var lp = rt.localPosition;
                float targetY = Swimming ? -0.95f : 0f;
                float targetZ = (!Swimming && swingT > 0f && !IsEquippedGun) ? Mathf.Sin(swing01 * Mathf.PI) * 0.16f : 0f;
                lp.y = Mathf.Lerp(lp.y, targetY, 1f - Mathf.Exp(-6f * Time.deltaTime));
                lp.z = Mathf.Lerp(lp.z, targetZ, 1f - Mathf.Exp(-14f * Time.deltaTime));
                rt.localPosition = lp;
            }
            if (Swimming) AnimState = speed < 0.3f ? "idle" : "walk";
            float clipSpd = Swimming ? 0.6f
                          : AnimState == "walk" ? speed / CharacterRig.WalkClipSpeed
                          : AnimState == "run" ? speed / CharacterRig.RunClipSpeed
                          : AnimState == "attack" ? (speed > 0.4f ? 1.25f : 1.0f)
                          : 1f;
            rig?.SetState(AnimState, clipSpd);

            UpdateCombatArcVfx(swing01);

            if (transform.position.y < -40f) Teleport(spawn, transform.eulerAngles.y);
        }

        void UpdateCombatArcVfx(float swing01)
        {
            if (combatArcRoot == null) return;
            bool showArc = !Swimming && swingT > 0f && !IsEquippedGun && swing01 >= 0.12f && swing01 <= 0.78f;
            combatArcRoot.gameObject.SetActive(showArc);
            if (!showArc) return;

            float u = Mathf.InverseLerp(0.12f, 0.78f, swing01);
            float env = Mathf.Sin(u * Mathf.PI);
            int step = Mathf.Abs(ComboStep) % 3;
            bool hasWeapon = !string.IsNullOrEmpty(EquippedWeapon);

            Color arcCol = EquippedWeapon == "cutlass" || EquippedWeapon == "baton" || EquippedWeapon == "weapon"
                ? new Color(0.82f, 0.94f, 1.0f, 0.88f * env)
                : EquippedWeapon == "pepper"
                ? new Color(1.0f, 0.32f, 0.16f, 0.90f * env)
                : new Color(1.0f, 0.84f, 0.18f, 0.88f * env);
            foreach (var m in combatArcMats)
            {
                if (m == null) continue;
                m.color = arcCol;
                if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, arcCol);
            }

            float sweepYaw = step == 1 ? Mathf.Lerp(-52f, 52f, u) : Mathf.Lerp(48f, -48f, u);
            float roll = step == 0 ? (hasWeapon ? -32f : -12f) : step == 1 ? 8f : -45f;
            float height = step == 2 && !hasWeapon ? 0.92f : 1.18f;
            float reach = hasWeapon ? 0.92f : 0.74f;
            combatArcRoot.localPosition = new Vector3(0f, height, reach);
            combatArcRoot.localRotation = Quaternion.Euler(0f, sweepYaw, roll);
            combatArcRoot.localScale = Vector3.one * ((hasWeapon ? 1.18f : 0.92f) * (0.75f + 0.35f * env));
            if (combatImpactRing != null)
            {
                float ringScale = Mathf.Lerp(0.28f, hasWeapon ? 0.95f : 0.72f, u);
                combatImpactRing.localScale = new Vector3(ringScale, 0.015f, ringScale);
            }
        }

        void LateUpdate()
        {
            float swing01 = swingT > 0f ? 1f - (swingT / SwingDuration) : 0f;
            float guard01 = guardT > 0f ? Mathf.Clamp01(guardT / 0.55f) : 0f;
            if (rig != null)
                rig.ApplyCombatPose(swing01, guard01, ComboStep, EquippedWeapon, IsEquippedGun);
            else
                Mannequin.ApplyCombatPose(transform, swing01, guard01, ComboStep, EquippedWeapon, IsEquippedGun);
        }
    }
}
