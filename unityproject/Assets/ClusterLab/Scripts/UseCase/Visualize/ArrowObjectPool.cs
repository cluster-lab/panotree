using UniRx.Toolkit;
using UnityEngine;

namespace ClusterLab.UseCase
{
    public class ArrowObjectPool : ObjectPool<ArrowObject>
    {
        readonly ArrowObject prefab;
        readonly Transform parentTransform;

        public ArrowObjectPool(Transform transform, ArrowObject prefab)
        {
            this.parentTransform = transform;
            this.prefab = prefab;
        }

        protected override ArrowObject CreateInstance()
        {
            var ao = Object.Instantiate(prefab, parentTransform, true);
            ao.InitializeArrowRenderer(ao.gameObject);
            return ao;
        }

        protected override void OnBeforeReturn(ArrowObject instance)
        {
            Transform transform;
            (transform = instance.transform).SetParent(parentTransform);
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            base.OnBeforeReturn(instance);
        }
    }
}
