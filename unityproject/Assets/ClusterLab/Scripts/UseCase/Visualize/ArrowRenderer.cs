using ClusterLab.Infrastructure.Agent;
using UnityEngine;

namespace ClusterLab.UseCase
{

    public interface IArrowRenderer
    {
        public Transform ArrowTransform { get; }
        public Material ArrowMaterial { get; set; }
        public MeshRenderer ArrowRenderer { get; set; }

        public int EmissionColorPropId { get; }
        public int ColorPropertyPropId { get; }
    }

    public static class ArrowRendererExtension
    {
        public static void InitializeArrowRenderer(this IArrowRenderer arrowRenderer, GameObject arrowObject)
        {
            arrowRenderer.ArrowRenderer = arrowObject.GetComponent<MeshRenderer>();
            arrowRenderer.ArrowMaterial = new Material(arrowRenderer.ArrowRenderer.material);
            arrowRenderer.ArrowRenderer.material = arrowRenderer.ArrowMaterial;
            arrowRenderer.ArrowRenderer.receiveShadows = false;
            arrowRenderer.ArrowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public static void SetArrowVisible(this IArrowRenderer arrowRenderer, bool visible)
        {
            arrowRenderer.ArrowRenderer.enabled = visible;
        }

        public static void SetArrowTransform(this IArrowRenderer arrowRenderer, CameraParameter cp)
        {
            arrowRenderer.ArrowTransform.SetPositionAndRotation(cp.Position, cp.Rotation);
        }

        public static void SetArrowScale(this IArrowRenderer arrowRenderer, float scale)
        {
            arrowRenderer.ArrowTransform.localScale = new Vector3(scale, scale, scale);
        }

        public static void SetArrowColor(this IArrowRenderer arrowRenderer, Color color)
        {
            var material = arrowRenderer.ArrowMaterial;
            var alpha = material.color.a;
            color.a = alpha;
            material.color = color;
            material.SetColor(arrowRenderer.EmissionColorPropId, color);
            material.SetColor(arrowRenderer.ColorPropertyPropId, color);
        }
    }
}
