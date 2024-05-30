using System;
using System.Collections.Generic;
using System.Linq;
using ClusterLab.Infrastructure.Utils;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace ClusterLab.UseCase.Render
{
    public class TileCameraRenderer: AbstractMultiCameraRenderer
    {
        [SerializeField] RenderTexture tileRenderTexture;
        [SerializeField] RawImage rawImage;
        [SerializeField] int textureSize = 224;
        [SerializeField] float fieldOfView = 60f;

        int renderTextureSize;
        int rowCol;

        protected override void Awake()
        {
            base.Awake();
            AgentCameras.ForEach(c => c.Init(new AgentCameraConfig()
            {
                Width = textureSize,
                Height = textureSize,
                FieldOfView = fieldOfView
            }));

            InitRenderTexture(textureSize);
        }

        public override void UpdateCameraConfig(AgentCameraConfig config)
        {
            Assert.AreEqual(config.Width, config.Height, "Width and Height must be equal");
            base.UpdateCameraConfig(config);
            InitRenderTexture(config.Width);
        }

        void InitRenderTexture(int aTextureSize)
        {
            // テクスチャの幅と高さ
            rowCol = (int) Math.Ceiling(Math.Sqrt(NumCameras));
            textureSize = aTextureSize;
            renderTextureSize = rowCol * aTextureSize;
            if (aTextureSize == textureSize && tileRenderTexture != null && tileRenderTexture.width == renderTextureSize)
                return;
            // レンダリング結果をタイリングするためのテクスチャ
            tileRenderTexture = new RenderTexture(renderTextureSize, renderTextureSize, GraphicsFormat.R8G8B8A8_SRGB, GraphicsFormat.None, 0);
            if (rawImage != null)
                rawImage.texture = tileRenderTexture;

            CommandBuffer.Clear();

            foreach (var y in Enumerable.Range(0, rowCol))
            {
                foreach (var x in Enumerable.Range(0, rowCol))
                {
                    var idx = y * rowCol + x;
                    if (idx > NumCameras - 1)
                        break;

                    // 毎フレームのレンダリング完了後、各カメラのrenderTextureをtileRenderTextureにコピーする命令をcommandBufferに積む
                    CommandBuffer.CopyTexture(
                        AgentCameras[idx].TargetTexture, 0, 0, 0, 0, textureSize, textureSize,
                        this.tileRenderTexture, 0, 0, x * textureSize, textureSize * (rowCol - 1) - y * textureSize
                    );
                }
            }
            // commandBufferをAfterEverythingに積むことで、すべてのレンダリングが終わった後に実行されるようにする
            // Camera.mainにする必要はないが、Render時にenabledな代表カメラ一つに対して積む必要がある
            Camera.main.AddCommandBuffer(CameraEvent.AfterEverything, CommandBuffer);
        }

        protected override UniTask<List<byte[]>> TextureReadback()
        {
            return tileRenderTexture
                .AsyncGPUReadbackRGB24()
                .ContinueWith(t => new List<byte[]>{t});
        }
    }
}
