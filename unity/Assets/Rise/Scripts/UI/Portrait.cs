using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Rise.UI
{
    // The HUD portrait: a small camera on the player's head rendered to a texture, so the look you choose in the
    // Character app shows at the corner of the screen.
    public class Portrait : MonoBehaviour
    {
        public RenderTexture Texture { get; private set; }
        Camera cam; Transform head, player;

        public void Init(Transform playerTransform)
        {
            player = playerTransform;
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;   // headless: no render targets
            Texture = new RenderTexture(160, 160, 16) { name = "Portrait" };
            var go = new GameObject("Portrait Camera"); go.transform.SetParent(transform, false);
            cam = go.AddComponent<Camera>();
            cam.fieldOfView = 24f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 2.2f;
            cam.cullingMask = 1 << Player.PlayerController.Layer;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
            cam.targetTexture = Texture; cam.depth = -9; cam.allowHDR = false;
            var urp = cam.GetUniversalAdditionalCameraData(); urp.renderPostProcessing = false; urp.renderShadows = false; urp.antialiasing = AntialiasingMode.None;
            var fill = go.AddComponent<Light>();
            fill.type = LightType.Point; fill.range = 2.4f; fill.intensity = 1.15f;
            fill.color = new Color(1.0f, 0.95f, 0.88f);
            fill.cullingMask = 1 << Player.PlayerController.Layer;
            fill.shadows = LightShadows.None;
        }

        void LateUpdate()
        {
            if (cam == null || player == null) return;
            if (head == null) head = Entities.CharacterRig.Find(player, "Head");
            var anchor = head != null ? head.position : player.position + Vector3.up * 1.6f;
            cam.transform.position = anchor + player.forward * 0.9f + Vector3.up * 0.05f;
            cam.transform.LookAt(anchor + Vector3.up * 0.02f);
        }
    }
}
