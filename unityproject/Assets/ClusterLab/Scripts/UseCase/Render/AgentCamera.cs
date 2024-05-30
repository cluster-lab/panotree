using System;
using ClusterLab.Infrastructure.Agent;
using UniRx;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace ClusterLab.UseCase.Render
{
    [Serializable]
    public struct AgentCameraConfig
    {
        public int Width;
        public int Height;
        public float FieldOfView;
        public float Aspect;

        public static readonly AgentCameraConfig DEFAULT = new AgentCameraConfig
        {
            Width = 224,
            Height = 224,
            FieldOfView = 60f
        };
    }

    public class AgentCamera : MonoBehaviour
    {
        [SerializeField] Camera Camera;
        [SerializeField] RenderTexture RenderTexture;
        [SerializeField] ReactiveProperty<AgentCameraConfig> Parameter;
        public RenderTexture TargetTexture => RenderTexture;

        void Awake()
        {
            Parameter ??= new ReactiveProperty<AgentCameraConfig>();
            Parameter.Value = AgentCameraConfig.DEFAULT;
            Parameter.DistinctUntilChanged().Subscribe(Init);
        }

        public void Init(AgentCameraConfig param)
        {
            // カメラ用ゲームオブジェクトの設定
            Camera ??= gameObject.AddComponent<Camera>();
            var cam = Camera;
            var mask = 1 << LayerMask.NameToLayer("UI");


            if (RenderTexture != null && (RenderTexture.width != param.Width || RenderTexture.height != param.Height))
            {
                RenderTexture.Release();
                Destroy(RenderTexture);
                RenderTexture = null;
            }
            if (RenderTexture == null)
                RenderTexture = new RenderTexture(param.Width, param.Height, GraphicsFormat.R8G8B8A8_SRGB, GraphicsFormat.None, 0);


            cam.targetTexture = RenderTexture;
            cam.cullingMask &= ~mask;
            cam.enabled = false;
            cam.useOcclusionCulling = false; // カメラあたり数十マイクロ秒節約できる
            cam.fieldOfView = param.FieldOfView;
            SetAspect(param.Aspect);
        }

        public void SetEnabled(bool enabled)
        {
            Camera.enabled = enabled;
        }

        public void SetParameter(CameraParameter camParam)
        {
            Camera.transform.SetPositionAndRotation(camParam.Position, camParam.Rotation);
            Camera.fieldOfView = camParam.FieldOfView;
            SetAspect(camParam.Aspect);
        }

        void SetAspect(float aspect)
        {
            if (aspect > 0)
            {
                Camera.aspect = aspect;
            }
            else
            {
                Camera.ResetAspect();
            }
        }

        public void Reset()
        {
            Camera.enabled = false;
            Camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.LookRotation(Vector3.forward, Vector3.up));
        }
    }
}
