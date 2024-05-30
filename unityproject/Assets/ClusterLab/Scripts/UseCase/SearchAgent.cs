using ClusterLab.Infrastructure.Agent;
using ClusterLab.Infrastructure.Server;
using ClusterLab.UseCase.Render;
using UnityEngine;
using UnityEngine.Assertions;
#if UNITY_EDITOR
#endif

namespace ClusterLab.UseCase
{
    /// <summary>
    /// AgentDriverとAgentServerを起動する
    /// </summary>
    public sealed class SearchAgent : MonoBehaviour
    {
        [SerializeField] string assetBundleJsonPath;
        [SerializeField] string assetBundleUrl;
        [SerializeField] string unitySceneName;
        [SerializeField] TextAsset assetBundleJsonTextAsset;
        [SerializeField] GameObject TileRendererGameObject;
        [SerializeField] GameObject NodeRendererGameObject;

        IAgentDriver agentDriver;
        ISceneRenderer sceneRenderer;
        INodeRenderer nodeRenderer;
        AgentServer agentServer;

        void Awake()
        {
            SetupAgent();
        }

        void SetupAgent()
        {
            nodeRenderer = NodeRendererGameObject.GetComponent<INodeRenderer>();
            ;
            Assert.IsNotNull(nodeRenderer, "NodeRendererGameObject must have a component that implements INodeRenderer");

            var tileCameraRenderer = TileRendererGameObject.GetComponent<TileCameraRenderer>();
            Assert.IsNotNull(tileCameraRenderer, "TileRendererGameObject must have a component that implements TileCameraRenderer");

            sceneRenderer = new SceneRendererImpl(tileCameraRenderer);
            agentDriver = new AgentDriverImpl(sceneRenderer, nodeRenderer);

            agentServer = new AgentServer(agentDriver);
            agentServer.StartServer();
        }

        void OnDestroy()
        {
            agentServer?.StopServer();
        }
    }
}
