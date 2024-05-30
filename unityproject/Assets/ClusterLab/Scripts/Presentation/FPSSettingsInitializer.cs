using UnityEngine;

namespace ClusterLab.Scripts.Presentation
{
    public sealed class FPSSettingsInitializer : MonoBehaviour
    {
        [SerializeField] int targetFrameRate = 90;
        void Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFrameRate;
        }
    }
}
