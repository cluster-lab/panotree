using System;
using ModestTree;
#if !NOT_UNITY3D
using UnityEngine;
#endif

// Zenject本体に対しては、RoslynAnalyzerを実行しない（必要があれば、link.xmlでpreserveする）
#pragma warning disable ZNJ0001
#pragma warning disable ZNJ0002

namespace Zenject
{
    public static class SubContainerCreatorUtil
    {
        public static void ApplyBindSettings(
            SubContainerCreatorBindInfo subContainerBindInfo, DiContainer subContainer)
        {
            if (subContainerBindInfo.CreateKernel)
            {
                var parentContainer = subContainer.ParentContainers.OnlyOrDefault();
                Assert.IsNotNull(parentContainer, "Could not find unique container when using WithKernel!");

                if (subContainerBindInfo.KernelType != null)
                {
                    parentContainer.Bind(typeof(Kernel).Interfaces()).To(subContainerBindInfo.KernelType)
                        .FromSubContainerResolve()
                        .ByInstance(subContainer).AsCached();
                    subContainer.Bind(subContainerBindInfo.KernelType).AsCached();
                }
                else
                {
                    parentContainer.BindInterfacesTo<Kernel>().FromSubContainerResolve()
                        .ByInstance(subContainer).AsCached();
                    subContainer.Bind<Kernel>().AsCached();
                }

#if !NOT_UNITY3D
                if (subContainerBindInfo.DefaultParentName != null)
                {
                    DefaultGameObjectParentInstaller.Install(
                        subContainer, subContainerBindInfo.DefaultParentName);
                }
#endif
            }
        }
    }
}

#pragma warning restore ZNJ0002
#pragma warning restore ZNJ0001
