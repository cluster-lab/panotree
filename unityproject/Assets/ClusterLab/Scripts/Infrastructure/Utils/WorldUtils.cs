using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ClusterLab.Infrastructure.Utils
{
    public static class WorldUtils
    {
        public static IEnumerable<Collider> GetAllColliders()
        {
            return GetAllScenes()
                .SelectMany(scene => scene.GetRootGameObjects())
                .SelectMany(go => go.GetComponentsInChildren<Collider>());
        }

        public static Bounds GetWorldBoundingBox()
        {
            var bounds = new Bounds();
            // すべてのシーンのルートオブジェクトの子供のアバターレイヤーではないコライダーの集合に対してBounds.Encapsulateを実行する
            GetAllColliders()
                .Where(c => ((1 << c.gameObject.layer) & LayerUsages.OwnAvatarPhysicsMask) != 0)
                .ForEach(c => bounds.Encapsulate(c.bounds));
            return bounds;
        }

        static IEnumerable<Scene> GetAllScenes()
        {
            var numScenes = SceneManager.sceneCount;
            return Enumerable
                .Range(0, numScenes)
                .Select(SceneManager.GetSceneAt);
        }
    }
}
