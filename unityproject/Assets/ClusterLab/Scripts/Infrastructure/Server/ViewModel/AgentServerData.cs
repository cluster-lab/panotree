using System;
using System.Linq;
using UnityEngine;

namespace ClusterLab.Infrastructure.Server.ViewModel
{
    // jsonにするのでlowerCamelCase
    // Web APIでのみ使用するViewModel
    // UseCaseで構造体を使用するのは避けてほしいのでアクセス修飾子はinternal
    [Serializable]
    struct GetWorldBoundingBoxResponse
    {
        [SerializeField]
        public BBoxS bbox;
    }

    [Serializable]
    struct UpdateNodesRequest
    {
        public NodeData[] nodes;
    }

    [Serializable]
    struct Vector3F
    {
        public float x;
        public float y;
        public float z;

        public Vector3F(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public Vector3F(Vector3 v)
        {
            x = v.x;
            y = v.y;
            z = v.z;
        }

        public static implicit operator Vector3(Vector3F v)
        {
            return new Vector3(v.x, v.y, v.z);
        }

        public bool Equals(Vector3F other)
        {
            return x.Equals(other.x) && y.Equals(other.y) && z.Equals(other.z);
        }

        public override bool Equals(object obj)
        {
            return obj is Vector3F other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(x, y, z);
        }
    }


    [Serializable]
    struct Vector4F
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public Vector4 ToVector4()
        {
            return new Vector4(x, y, z, w);
        }

        public static readonly Vector4F Zero = new();

        public bool Equals(Vector4F other)
        {
            return x.Equals(other.x) && y.Equals(other.y) && z.Equals(other.z) && w.Equals(other.w);
        }

        public override bool Equals(object obj)
        {
            return obj is Vector4F other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(x, y, z, w);
        }
    }

    /// <summary>
    /// for test
    /// </summary>
    [Serializable]
    struct Vector3S
    {
        public string x;
        public string y;
        public string z;
    }

    [Serializable]
    struct BBoxS
    {
        public Vector3S min;
        public Vector3S max;
    }

    [Serializable]
    struct BBox
    {
        public Vector3F min;
        public Vector3F max;
    }

    [Serializable]
    struct CameraParameter
    {
        public Vector3F position;
        public Vector3F direction;
        public Vector4F quaternion; // 0,0,0,0の場合はdirectionを使う
        public float fieldOfView;
        public float aspect;

        public static implicit operator Agent.CameraParameter(CameraParameter cp)
        {
            var ret = new Agent.CameraParameter
            {
                Position = cp.position,
                FieldOfView = cp.fieldOfView,
                Aspect = cp.aspect
            };
            if (!cp.quaternion.Equals(Vector4F.Zero))
            {
                var q = cp.quaternion;
                ret.Rotation = new Quaternion(q.x, q.y, q.z, q.w);
            }
            else
            {
                ret.Rotation = Quaternion.LookRotation(cp.direction, Vector3.up);
            }
            return ret;
        }
    }

    [Serializable]
    struct RenderSceneRequest
    {
        public CameraParameter[] cameraParameters;
    }

    [Serializable]
    struct RendererConfig
    {
        public int textureSize;
    }

    [Serializable]
    struct UpdateConfigRequest
    {
        public RendererConfig rendererConfig;
    }


    [Serializable]
    struct PhotoScoring
    {
        public CameraParameter cameraParameter;
        public float score;
        public static implicit operator Agent.PhotoScoring(PhotoScoring ps)
        {
            return new Agent.PhotoScoring
            {
                CameraParameter = ps.cameraParameter,
                Score = ps.score
            };
        }
    }

    [Serializable]
    struct LeafGridNode
    {
        public string gridId;
        public string nodeId;
        public Vector3F position;
        public PhotoScoring[] photoScorings;
        public static implicit operator Agent.LeafGridNode(LeafGridNode lgn)
        {
            return new Agent.LeafGridNode
            {
                GridId = lgn.gridId,
                NodeId = lgn.nodeId,
                Position = lgn.position,
                PhotoScorings = lgn.photoScorings.Select(ps => (Agent.PhotoScoring) ps).ToList()
            };
        }
    }

    [Serializable]
    struct NodeData
    {
        public string id;
        public string branchId;
        public string parentId;
        public int depth;
        public Vector3F min;
        public Vector3F max;
        public float score;
        public PhotoScoring[] photoScorings;
        public LeafGridNode[] leafGridNodes;
    }

    [Serializable]
    struct VersionInfo
    {
        public int majorVersion;
        public int minorVersion;
        public int buildNumber;
        public int revisionNumber;

        public VersionInfo(int majorVersion, int minorVersion, int buildNumber, int revisionNumber)
        {
            this.majorVersion = majorVersion;
            this.minorVersion = minorVersion;
            this.buildNumber = buildNumber;
            this.revisionNumber = revisionNumber;
        }

        public override string ToString()
        {
            return $"{majorVersion}.{minorVersion}.{buildNumber}.{revisionNumber}";
        }
    }

    [Serializable]
    struct GetServerInfoResponse
    {
        public string version;
        public VersionInfo versionInfo;
        public string platform;
    }
}
