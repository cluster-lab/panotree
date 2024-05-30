using System;
using ClusterLab.Infrastructure.Agent;
using UnityEngine;
using UnityEngine.Assertions;

namespace ClusterLab.UseCase
{
    public class CameraParameterPreview: MonoBehaviour
    {
        [Tooltip("任意のカメラパラメタをここにペーストすると、そのカメラパラメタに従ってカメラの位置と回転が更新されます")]
        [SerializeField] CameraParameter CameraParameter;
        [SerializeField] Camera Camera;

        void Awake()
        {
            Assert.IsNotNull(Camera, "Camera must be set");
        }

        void Update()
        {
            if (Camera == null) return;
            transform.SetPositionAndRotation(CameraParameter.Position, CameraParameter.Rotation);
        }
    }
}
