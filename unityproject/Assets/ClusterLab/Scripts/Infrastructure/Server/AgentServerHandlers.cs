using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using ClusterLab.Infrastructure.Agent;
using ClusterLab.Infrastructure.Server.ViewModel;
using ClusterLab.Infrastructure.Utils;
using UniRx;
using UnityEngine;
using UnityEngine.Assertions;
using PhotoScoring = ClusterLab.Infrastructure.Agent.PhotoScoring;
using RendererConfig = ClusterLab.Infrastructure.Agent.RendererConfig;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ClusterLab.Infrastructure.Server
{
    public partial class AgentServer
    {
        void SetUpHandlers()
        {
            handlers = new List<AgentServerHandler>
            {
                new(HttpMethod.Get, "/world/bbox$", GetWorldBoundingBoxHandler),
                new(HttpMethod.Post, "/world/render$", PostRenderSceneRequestHandler),
                new(HttpMethod.Post, "/world/renderpng$", PostRenderPngSceneRequestHandler),
                new(HttpMethod.Post, "/world/node$", PostUpdateNode),
                new(HttpMethod.Post, "/world/node/reset$", PostResetNode),
                new(HttpMethod.Post, "/config$", PostUpdateConfig),
                new(HttpMethod.Get, "/info$", GetServerInfo),
                new(HttpMethod.Post, "/server/shutdown$", PostShutdown)
            }.Select(h =>
            {
                return new AgentServerHandler(h.Method, h.PathPattern, SynchronizedAction);

                void SynchronizedAction(HttpListenerContext ctx)
                {
                    if (!semaphore.WaitOne(TimeSpan.FromMilliseconds(SemaphoreTimeoutMillis)))
                    {
                        ReturnInternalError(ctx.Response, new SemaphoreFullException($"Unable to acquire semaphore in {SemaphoreTimeoutMillis} ms"));
                        return;
                    }
                    try
                    {
                        h.Action(ctx);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }
            }).ToList();
        }


        void GetWorldBoundingBoxHandler(HttpListenerContext context)
        {
            var worldBoundingBox = agentDriver.CalculateWorldBoundingBox();
            var response = context.Response;
            response.StatusCode = 200;
            var responseBody = new GetWorldBoundingBoxResponse
            {
                bbox = new BBoxS
                {
                    min = new Vector3S
                    {
                        x = ConvertToCompatFloat(worldBoundingBox.min.x),
                        y = ConvertToCompatFloat(worldBoundingBox.min.y),
                        z = ConvertToCompatFloat(worldBoundingBox.min.z)
                    },
                    max = new Vector3S
                    {
                        x = ConvertToCompatFloat(worldBoundingBox.max.x),
                        y = ConvertToCompatFloat(worldBoundingBox.max.y),
                        z = ConvertToCompatFloat(worldBoundingBox.max.z)
                    }
                }
            };
            WriteResponseBody(response, responseBody);
            response.Close();
        }

        static void WriteResponseBody<T>(HttpListenerResponse response, T responseBody)
        {
            using var writer = new StreamWriter(response.OutputStream, Encoding.UTF8);
            writer.Write(JsonUtility.ToJson(responseBody));
        }

        void PostRenderSceneRequestHandler(HttpListenerContext context)
        {
            var request = ParseJson<RenderSceneRequest>(context);
            var transforms = request.cameraParameters
                .Select(cp => (Agent.CameraParameter) cp)
                .ToList();
            var renderSceneParams = new RenderSceneParameters(transforms);
            var textureBinaries = agentDriver.RenderScene(renderSceneParams).ToList().Wait();
            var boundary = Guid.NewGuid().ToString();
            var multipartContent = new MultipartFormDataContent(boundary);

            // Add each render texture as a separate part in the multipart content
            for (var i = 0; i < textureBinaries.Count; i++)
            {
                var renderTexturePart = new ByteArrayContent(textureBinaries[i]);
                renderTexturePart.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                multipartContent.Add(renderTexturePart, "renderTexture" + i, "renderTexture" + i + ".png");
            }

            // Set the response content type to multipart/form-data
            context.Response.ContentType = $"multipart/form-data;boundary=\"{boundary}\"";

            // Write the multipart content to the response output stream
            multipartContent.CopyToAsync(context.Response.OutputStream).Wait();

            // Close the response
            context.Response.Close();
        }

        void PostRenderPngSceneRequestHandler(HttpListenerContext context)
        {
            var request = ParseJson<RenderSceneRequest>(context);
            var transforms = request.cameraParameters
                .Select(cp => (Agent.CameraParameter) cp)
                .ToList();
            var renderSceneParams = new RenderSceneParameters(transforms);
            var textureBinaries = agentDriver.RenderScene(renderSceneParams).ToList().Wait();
            if (textureBinaries.Count == 0)
            {
                context.Response.StatusCode = 204;
                context.Response.Close();
                return;
            }
            var response = context.Response;
            response.ContentType = "image/png";
            Loan.RunOnMainthreadSynchronized(() =>
            {
                var tex = new Texture2D(1344, 1344, TextureFormat.RGB24, 0, false);
                tex.SetPixelData(textureBinaries[0], 0, 0);
                tex.Apply();
                var pngBin = tex.EncodeToPNG();
                context.Response.OutputStream.Write(pngBin);
                context.Response.OutputStream.Flush();
            });


            // Close the response
            context.Response.Close();
        }

        void PostUpdateNode(HttpListenerContext context)
        {
            HandlePostReturnEmpty<UpdateNodesRequest>(context, request =>
            {
                var nodeData = request.nodes.Select(n =>
                {
                    return new NodeViewModel()
                    {
                        Id = n.id,
                        BranchId = n.branchId,
                        ParentId = n.parentId,
                        Depth = n.depth,
                        Min = n.min,
                        Max = n.max,
                        Score = n.score,
                        PhotoScorings = n.photoScorings.Select(ps => (Agent.PhotoScoring) ps).ToList(),
                        LeafGridNodes = n.leafGridNodes?.Select(lgn => (Agent.LeafGridNode) lgn).ToList()
                    };
                }).ToArray();

                agentDriver.UpdateNodes(nodeData);
            });
        }

        void PostResetNode(HttpListenerContext context)
        {
            agentDriver.ResetNodes();

            context.Response.StatusCode = 200;
            context.Response.Close();
        }

        void PostUpdateConfig(HttpListenerContext context)
        {
            HandlePostReturnEmpty<UpdateConfigRequest>(context, request =>
            {
                agentDriver.UpdateConfig(new AgentConfig
                {
                    RendererConfig = new RendererConfig()
                    {
                        Width = request.rendererConfig.textureSize,
                        Height = request.rendererConfig.textureSize,
                    }
                });
            });
        }

        public static readonly Dictionary<RuntimePlatform, string> PLATFORM_MAP = new()
        {
            { RuntimePlatform.WindowsEditor, "WindowsEditor" },
            { RuntimePlatform.WindowsPlayer, "WindowsPlayer" },
            { RuntimePlatform.OSXEditor, "OSXEditor" },
            { RuntimePlatform.OSXPlayer, "OSXPlayer" },
            { RuntimePlatform.LinuxEditor, "LinuxEditor" },
            { RuntimePlatform.LinuxPlayer, "LinuxPlayer" },
            { RuntimePlatform.Android, "Android" },
            { RuntimePlatform.IPhonePlayer, "IPhonePlayer" },
        };

        void GetServerInfo(HttpListenerContext context)
        {
            var response = context.Response;
            response.StatusCode = 200;
            var platform = Application.platform;
            var platformExistsInMap = PLATFORM_MAP.TryGetValue(platform, out var platformName);
            Assert.IsTrue(platformExistsInMap, $"Platform {platform} is not supported");

            var responseBody = new GetServerInfoResponse
            {
                version = VERSION.ToString(),
                versionInfo = VERSION,
                platform = platformName
            };

            using (var writer = new StreamWriter(response.OutputStream, Encoding.UTF8))
            {
                writer.Write(JsonUtility.ToJson(responseBody));
            }
            response.Close();
        }

        void WebSock(HttpListenerContext context)
        {
            if (!context.Request.IsWebSocketRequest)
            {
                ReturnInternalError(context.Response, new ArgumentException("Not a WebSocket request"));
                return;
            }
            WebSockInternal(context);
        }

        async void WebSockInternal(HttpListenerContext context)
        {
            var wsc = await context.AcceptWebSocketAsync(null);
            var ws = wsc.WebSocket;
            //レスポンスのテストメッセージとして、現在時刻の文字列を取得
            var time = DateTime.Now.ToLongTimeString();

            //文字列をByte型に変換
            var buffer = Encoding.UTF8.GetBytes(time);
            var segment = new ArraySegment<byte>(buffer);

            await ws.SendAsync(segment, WebSocketMessageType.Text,
                true, CancellationToken.None);
        }

        void PostShutdown(HttpListenerContext context)
        {
            context.Response.StatusCode = 200;
            context.Response.Close();
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }
    }
}
