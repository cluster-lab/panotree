using UnityEngine;

namespace ClusterLab.UseCase
{
    public class ArrowObject : MonoBehaviour, IArrowRenderer
    {
        static readonly int SEmissionColorPropId = Shader.PropertyToID("_EmissionColor");
        static readonly int SColorPropId = Shader.PropertyToID("_Color");

        #region IArrowRenderer

        public Transform ArrowTransform => transform;
        public Material ArrowMaterial { get; set; }
        public MeshRenderer ArrowRenderer { get; set; }
        public int EmissionColorPropId => SEmissionColorPropId;
        public int ColorPropertyPropId => SColorPropId;

        #endregion
    }
}
