using System;
using System.Collections.Generic;
using ClusterLab.Infrastructure.Agent;
using ClusterLab.Infrastructure.Utils;
using ClusterLab.UseCase.Render;

namespace ClusterLab.UseCase
{
    /// <summary>
    /// TileRendererとISceneRendererのアダプター
    /// </summary>
    public class SceneRendererImpl : ISceneRenderer
    {
        readonly TileCameraRenderer tileRenderer;

        public SceneRendererImpl(TileCameraRenderer tileRenderer)
        {
            this.tileRenderer = tileRenderer;
        }

        /// <summary>
        /// 指定されたカメラパラメタに従いシーンのレンダリングを行い、pngファイルのバイト列を返す
        /// 処理は次のカメラパラメタ数+1回のUpdate()に渡って行われる
        /// 呼び出し元スレッドはどこでも良い
        /// </summary>
        /// <param name="cameraParameters"></param>
        /// <returns></returns>
        public IObservable<byte[]> RenderScene(List<CameraParameter> cameraParameters)
        {
            return tileRenderer.Render(cameraParameters);
        }

        public void UpdateConfig(RendererConfig config)
        {
            Loan.RunOnMainthreadSynchronized(() =>
            {
                tileRenderer.UpdateCameraConfig(new AgentCameraConfig()
                {
                    Width = config.Width,
                    Height = config.Height
                });
            });
        }
    }
}
