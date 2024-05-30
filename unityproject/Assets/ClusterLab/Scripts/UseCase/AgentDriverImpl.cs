using System;
using System.Collections.Generic;
using ClusterLab.Infrastructure.Agent;
using ClusterLab.Infrastructure.Utils;
using UnityEngine;

namespace ClusterLab.UseCase
{
    /// <summary>
    /// Unityシーンのレンダリングの責務を負う
    /// </summary>
    public interface ISceneRenderer
    {
        public IObservable<byte[]> RenderScene(List<CameraParameter> cameraParameters);

        public void UpdateConfig(RendererConfig config);
    }

    public class AgentDriverImpl: IAgentDriver
    {
        readonly ISceneRenderer sceneRenderer;
        readonly INodeRenderer nodeRenderer;

        public AgentDriverImpl(ISceneRenderer sceneRenderer, INodeRenderer nodeRenderer)
        {
            this.sceneRenderer = sceneRenderer;
            this.nodeRenderer = nodeRenderer;
        }

        public Bounds CalculateWorldBoundingBox()
        {
            return Loan.RunOnMainthreadSynchronized(WorldUtils.GetWorldBoundingBox);
        }

        public IObservable<byte[]> RenderScene(RenderSceneParameters renderSceneParams)
        {
            return sceneRenderer
                .RenderScene(renderSceneParams.CameraParameters);
        }

        public void UpdateConfig(AgentConfig config)
        {
            sceneRenderer.UpdateConfig(config.RendererConfig);
        }

        public void UpdateNodes(NodeViewModel[] nodes)
        {
            nodeRenderer.UpdateNodes(nodes);
        }

        public void ResetNodes()
        {
            nodeRenderer.ResetNodes();
        }
    }
}
