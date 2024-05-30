using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClusterLab.Infrastructure.Agent
{
    /// <summary>
    /// ワールド内エージェントを操作するためのインターフェース
    /// このインターフェースでWeb APIとの依存を切る
    /// </summary>
    public interface IAgentDriver
    {
        /// <summary>
        /// エージェントにロードされているワールドのバウンディングボックスの計算を要求し、結果を得る
        /// </summary>
        /// <returns></returns>
        Bounds CalculateWorldBoundingBox();

        /// <summary>
        /// エージェントに指定されたパラメタに従ってワールドのレンダリングを要求し、結果を得る
        /// </summary>
        /// <param name="renderSceneParams"></param>
        /// <returns></returns>
        IObservable<byte[]> RenderScene(RenderSceneParameters renderSceneParams);

        void UpdateConfig(AgentConfig config);

        void UpdateNodes(NodeViewModel[] nodes);

        void ResetNodes();
    }

    [Serializable]
    public struct RendererConfig
    {
        public int Width;
        public int Height;
    }

    [Serializable]
    public struct AgentConfig
    {
        public RendererConfig RendererConfig;
    }

    [Serializable]
    public struct CameraParameter
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float FieldOfView;
        public float Aspect;
    }

    [Serializable]
    public struct RenderSceneParameters
    {
        public List<CameraParameter> CameraParameters;

        public RenderSceneParameters(List<CameraParameter> cameraParameters)
        {
            CameraParameters = cameraParameters;
        }
    }

    [Serializable]
    public struct PhotoScoring
    {
        public CameraParameter CameraParameter;
        public float Score;
    }

    [Serializable]
    public struct LeafGridNode
    {
        public string GridId;
        public string NodeId;
        public Vector3 Position;
        public List<PhotoScoring> PhotoScorings;
    }

    [Serializable]
    public struct NodeViewModel
    {
        public string Id;
        public string BranchId;
        public string ParentId;
        public int Depth;
        public Vector3 Min;
        public Vector3 Max;
        public float Score;
        public List<PhotoScoring> PhotoScorings;
        public List<LeafGridNode> LeafGridNodes;
    }
}
