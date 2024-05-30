using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClusterLab.Infrastructure.Utils
{
    public static class RenderUtils
    {

        public static byte[] SyncGPUReadbackRGB24(this RenderTexture renderTexture)
        {
            var currentRT = RenderTexture.active;

            RenderTexture.active = renderTexture;

            // Texture2D.ReadPixels()によりアクティブなレンダーテクスチャのピクセル情報をテクスチャに格納する
            var texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, 0, false);
            texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture.Apply();
            var ret = texture.GetPixelData<byte>(0);
            var binary = new byte[renderTexture.width * renderTexture.height * 3];
            ret.CopyTo(binary);

            // アクティブなレンダーテクスチャを元に戻す
            RenderTexture.active = currentRT;
            return binary;
        }
        public static UniTask<byte[]> AsyncGPUReadbackRGB24(this RenderTexture renderTexture)
        {
            var source = new UniTaskCompletionSource<byte[]>();
            // #if UNITY_EDITOR
            // source.TrySetResult(renderTexture.SyncGPUReadbackRGB24());
            // return source.Task;
            // #endif

            // 本当は RequestIntoNativeArray が使いたいが、このバージョンでは修正されているはずのバグが再現してしまう
            // https://forum.unity.com/threads/asyncgpureadback-requestintonativearray-causes-invalidoperationexception-on-nativearray.1011955/
            AsyncGPUReadback.Request(renderTexture, 0, TextureFormat.RGB24, request =>
            {
                var binary = new byte[renderTexture.width * renderTexture.height * 3];
                if (!request.done)
                {
                    source.TrySetException(new Exception("AsyncGPUReadback not done."));
                    Debug.LogError("AsyncGPUReadback not done.");
                    return;
                }
                if (request.hasError)
                {
                    source.TrySetException(new Exception("Unable to read back tileRenderTexture."));
                    Debug.LogError("Unable to read back tileRenderTexture.");
                    return;
                }
                var data = request.GetData<byte>();
                data.CopyTo(binary);
                source.TrySetResult(binary);
            });

            return source.Task;
        }

        public static void RenderDebugPointsGizmos(IEnumerable<DebugPoint> debugPoints)
        {
            foreach (var debugPoint in debugPoints)
            {
                Gizmos.color = debugPoint.Color;
                Gizmos.DrawSphere(debugPoint.Position, debugPoint.Radius);
            }
        }
    }



    public struct DebugPoint
    {
        public Vector3 Position;
        public Color Color;
        public float Radius;

        public DebugPoint(Vector3 position, Color color, float radius = 0.2f)
        {
            Position = position;
            Color = color;
            Radius = radius;
        }
    }
}
