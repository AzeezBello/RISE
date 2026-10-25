using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Rise.UI
{
    // A top-down orthographic camera above the player rendered to a texture the HUD draws bottom-left. North is up.
    public class Minimap : MonoBehaviour
    {
        public RenderTexture Texture { get; private set; }
        public float Size = 75f;
        Camera cam; Transform target;

        public void Init(Transform follow)
        {
            target = follow;
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;   // headless: no render targets
            Texture = new RenderTexture(256, 256, 16) { name = "Minimap" };
            var go = new GameObject("Minimap Camera");
            go.transform.SetParent(transform, false);
            cam = go.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = Size;
            cam.nearClipPlane = 1f; cam.farClipPlane = 260f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.05f, 0.06f, 0.08f);
            cam.targetTexture = Texture; cam.depth = -10;
            cam.allowHDR = false; cam.allowMSAA = false; cam.useOcclusionCulling = false;
            cam.cullingMask = ~(1 << Player.PlayerController.Layer);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // looking down, +z (north) up on the map
            var urp = cam.GetUniversalAdditionalCameraData();
            urp.renderPostProcessing = false; urp.renderShadows = false; urp.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
        }

        public void SetTarget(Transform follow) => target = follow;

        void LateUpdate()
        {
            if (target == null || cam == null) return;
            if (target.position.y > 100f)
            {
                // Indoor room mode: place camera just below the ceiling (y = 164.2) so the interior floorplan & stations render cleanly.
                cam.orthographicSize = 11f;
                cam.nearClipPlane = 0.2f;
                cam.transform.position = new Vector3(target.position.x, 163.8f, target.position.z);
            }
            else
            {
                cam.orthographicSize = Size;
                cam.nearClipPlane = 1f;
                cam.transform.position = target.position + Vector3.up * 150f;
            }
        }
    }
}
