using System;
using System.Collections.Generic;
using System.Linq;
using ClusterLab.Infrastructure;
using ClusterLab.Infrastructure.Agent;
using ClusterLab.Infrastructure.Utils;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClusterLab.UseCase.Render
{
    public abstract class AbstractMultiCameraRenderer : MonoBehaviour
    {
        [SerializeField] public int NumCameras = 36;
        [SerializeField] public List<AgentCamera> AgentCameras;
        protected CommandBuffer CommandBuffer;

        public List<RenderTexture> RenderTextures => AgentCameras.Select(ac => ac.TargetTexture).ToList();

        protected virtual void Awake()
        {
            CommandBuffer = new CommandBuffer();
            CommandBuffer.name = "MultiCameraRenderer";
            AgentCameras = Enumerable
                .Range(0, NumCameras)
                .Select(i =>
                {
                    var go = new GameObject();
                    go.name = $"AgentCamera ({i})";
                    go.transform.parent = gameObject.transform;
                    var agentCamera = go.AddComponent<AgentCamera>();
                    agentCamera.Init(new AgentCameraConfig
                    {
                        Width = 224,
                        Height = 224,
                        FieldOfView = 60f
                    });
                    return agentCamera;
                })
                .ToList();
        }

        /// <summary>
        /// 与えられたカメラパラメタに従ってレンダリングし、レンダリング結果をRGB24のバイト列に変換して返す
        /// </summary>
        /// <param name="camParams">カメラパラメタ</param>
        /// <returns></returns>
        public IObservable<byte[]> Render(List<CameraParameter> camParams)
        {
            // divide camParams into numCameras
            int range = (int) Math.Ceiling((double) camParams.Count / NumCameras);

            var dividedCamParams = Enumerable
                .Range(0, range)
                .Select(i => camParams.GetRange(i * NumCameras, Math.Min(NumCameras, camParams.Count - i * NumCameras)))
                .ToList();

            // 毎アップデートごとに、camParamsをnumCameras個ずつ取り出して、RenderInternalを呼び出す
            // 所要時間はMath.Ceiling(camParams.Length / numCameras) フレーム＋AsyncGPUReadbackの待機時間分。
            return Observable
                .EveryUpdate()
                .Zip(dividedCamParams.ToObservable(), (l, cps) => cps)
                .SelectMany(t => RenderInternal(t)
                    .ToObservable()
                    .Flatten()
                );
        }

        /// <summary>
        /// 与えられたカメラパラメタに従ってレンダリングし、変換して返す
        /// Update()内で呼び出されることを想定。
        /// </summary>
        /// <param name="camParams">カメラパラメタ</param>
        /// <returns></returns>
        async UniTask<List<byte[]>> RenderInternal(List<CameraParameter> camParams)
        {
            await UniTask.SwitchToMainThread();
            // メインスレッド上でカメラを有効化し、transformを設定する。
            PrepareCameras(camParams);
            // このフレームのレンダリング完了を待機する
            // #if UNITY_EDITOR
            await UniTask.NextFrame();
            // #else
            // await UniTask.WaitForEndOfFrame();
            // #endif
            // テクスチャを読みに行く。AsyncGPUReadbackなので、環境によって20〜50ms程度の遅延が発生する
            var texture = TextureReadback().ContinueWith(t =>
            {
                ResetCameras();
                return t;
            });
            // await UniTask.SwitchToThreadPool();
            return await texture;
        }

        protected abstract UniTask<List<byte[]>> TextureReadback();

        public virtual void UpdateCameraConfig(AgentCameraConfig config)
        {
            AgentCameras.ForEach(c => c.Init(config));
        }

        public virtual void PrepareCameras(List<CameraParameter> camParams)
        {
            camParams.ZipForeach(AgentCameras, (camParam, camera) =>
            {
                camera.SetEnabled(true);
                camera.SetParameter(camParam);
            });
        }

        public void ResetCameras()
        {
            AgentCameras.ForEach(c => c.Reset());
        }
    }
}
