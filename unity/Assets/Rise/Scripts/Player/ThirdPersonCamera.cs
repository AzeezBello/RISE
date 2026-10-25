using UnityEngine;

namespace Rise.Player
{
    // Follows the player with a little lag, eased look-at, mouse / Z-C orbit, occlusion pull-in that eases back out,
    // a wider field of view when sprinting or driving fast, and a longer, tighter frame behind a vehicle that swings
    // round over a second instead of snapping: the feel described in README "Camera and controller feel".
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform Target;
        public PlayerController Player;
        public float Distance = 6.8f, Height = 1.9f, Yaw = 180f, Pitch = 14f;
        public float MouseSensitivity = 2.2f, KeyYawSpeed = 110f, BaseFov = 60f, SprintFov = 69f;
        public bool IndoorMode { get; private set; }

        Camera cam;
        Vehicles.VehicleController vehicle;
        Transform driving;
        Vector3 posVelocity, lookPoint;
        float currentDistance, lastLookInput, smoothTime = 0.07f, targetDistance, targetHeight;
        bool lookPointReady;

        void Awake()
        {
            cam = GetComponent<Camera>();
            currentDistance = targetDistance = Distance; targetHeight = Height;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void SetIndoor(bool indoor)
        {
            IndoorMode = indoor;
            if (driving == null)
            {
                targetDistance = indoor ? 4.4f : 6.8f;
                targetHeight = indoor ? 1.55f : 1.9f;
            }
            Snap();
        }

        public void Snap()
        {
            if (Target == null) return;
            Distance = currentDistance = targetDistance;
            Height = targetHeight;
            posVelocity = Vector3.zero;
            var pivot = Target.position + Vector3.up * Height;
            var rot = Quaternion.Euler(Pitch, Yaw, 0f);
            transform.position = pivot + (rot * Vector3.back) * currentDistance;
            lookPoint = pivot;
            lookPointReady = true;
            transform.rotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
        }

        public void SetDriving(Transform vehicleTransform)
        {
            driving = vehicleTransform;
            vehicle = vehicleTransform != null ? vehicleTransform.GetComponent<Vehicles.VehicleController>() : null;
            Target = driving != null ? driving : Player.transform;
            targetDistance = driving != null ? 9.5f + (vehicle != null ? vehicle.Class.len * 0.4f : 0f) : (IndoorMode ? 4.4f : 6.8f);
            targetHeight = driving != null ? 3.0f : (IndoorMode ? 1.55f : 1.9f);
            smoothTime = driving != null ? 0.045f : 0.07f;
            lastLookInput = -10f;   // let the camera swing behind the vehicle
        }

        public static bool KeepCursorUnlocked;

        void Update()
        {
            if (KeepCursorUnlocked)
            {
                if (Cursor.lockState == CursorLockMode.Locked) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
                return;
            }
            if (GameInput.EscapePressed) { bool locked = Cursor.lockState == CursorLockMode.Locked; Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = locked; }
        }

        void LateUpdate()
        {
            if (Target == null) return;
            float dt = Time.deltaTime;
            var look = GameInput.LookDelta;
            float mx = look.x, my = look.y;
            if (GameInput.LookLeftKey) mx -= KeyYawSpeed * dt / MouseSensitivity;
            if (GameInput.LookRightKey) mx += KeyYawSpeed * dt / MouseSensitivity;
            if (Mathf.Abs(mx) > 0.01f || Mathf.Abs(my) > 0.01f) lastLookInput = Time.time;
            Yaw += mx * MouseSensitivity;
            Pitch -= my * MouseSensitivity * 0.8f;
            Pitch = Mathf.Clamp(Pitch, -18f, 62f);

            // Behind a vehicle the camera settles behind it when you are not looking around.
            if (driving != null && Time.time - lastLookInput > 1.2f)
            {
                Yaw = Mathf.LerpAngle(Yaw, driving.eulerAngles.y, 1f - Mathf.Exp(-2.2f * dt));
                Pitch = Mathf.Lerp(Pitch, 12f, 1f - Mathf.Exp(-2f * dt));
            }

            Distance = Mathf.Lerp(Distance, targetDistance, 1f - Mathf.Exp(-3f * dt));
            Height = Mathf.Lerp(Height, targetHeight, 1f - Mathf.Exp(-3f * dt));

            var pivot = Target.position + Vector3.up * Height;
            var rot = Quaternion.Euler(Pitch, Yaw, 0f);
            var back = rot * Vector3.back;

            // Occlusion: pull in quickly when something is between the pivot and the camera, ease back out.
            float wanted = Distance;
            int mask = ~(1 << PlayerController.Layer);
            if (Physics.SphereCast(pivot, 0.3f, back, out var hit, Distance, mask, QueryTriggerInteraction.Ignore) && (driving == null || !hit.transform.IsChildOf(driving)))
                wanted = Mathf.Max(1.2f, hit.distance - 0.15f);
            currentDistance = wanted < currentDistance ? Mathf.Lerp(currentDistance, wanted, 1f - Mathf.Exp(-22f * dt)) : Mathf.Lerp(currentDistance, wanted, 1f - Mathf.Exp(-3.5f * dt));

            var desired = pivot + back * currentDistance;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref posVelocity, smoothTime);

            if (!lookPointReady) { lookPoint = pivot; lookPointReady = true; }
            lookPoint = Vector3.Lerp(lookPoint, pivot, 1f - Mathf.Exp(-12f * dt));
            transform.rotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);

            if (cam != null)
            {
                float speed01 = vehicle != null ? Mathf.Clamp01(vehicle.Kmh / 110f) : (Player != null && Player.IsRunning ? Player.Speed01 : 0f);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, Mathf.Lerp(BaseFov, SprintFov, speed01), 1f - Mathf.Exp(-4f * dt));
            }
        }
    }
}
