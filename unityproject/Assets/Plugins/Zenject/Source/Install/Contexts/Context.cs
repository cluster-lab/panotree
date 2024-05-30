#if !NOT_UNITY3D

using System;
using System.Collections.Generic;
using System.Linq;
using ModestTree;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Zenject
{
    public abstract class Context : MonoBehaviour
    {
        [SerializeField]
        List<ScriptableObjectInstaller> _scriptableObjectInstallers = new List<ScriptableObjectInstaller>();

        [FormerlySerializedAs("Installers")]
        [FormerlySerializedAs("_installers")]
        [SerializeField]
        List<MonoInstaller> _monoInstallers = new List<MonoInstaller>();

        [SerializeField]
        List<MonoInstaller> _installerPrefabs = new List<MonoInstaller>();

        List<InstallerBase> _normalInstallers = new List<InstallerBase>();
        List<Type> _normalInstallerTypes = new List<Type>();

        public IEnumerable<MonoInstaller> Installers
        {
            get { return _monoInstallers; }
            set
            {
                _monoInstallers.Clear();
                _monoInstallers.AddRange(value);
            }
        }

        public IEnumerable<MonoInstaller> InstallerPrefabs
        {
            get { return _installerPrefabs; }
            set
            {
                _installerPrefabs.Clear();
                _installerPrefabs.AddRange(value);
            }
        }

        public IEnumerable<ScriptableObjectInstaller> ScriptableObjectInstallers
        {
            get { return _scriptableObjectInstallers; }
            set
            {
                _scriptableObjectInstallers.Clear();
                _scriptableObjectInstallers.AddRange(value);
            }
        }

        // Unlike other installer types this has to be set through code
        public IEnumerable<Type> NormalInstallerTypes
        {
            get { return _normalInstallerTypes; }
            set
            {
                Assert.That(value.All(x => x != null && x.DerivesFrom<InstallerBase>()));

                _normalInstallerTypes.Clear();
                _normalInstallerTypes.AddRange(value);
            }
        }

        // Unlike other installer types this has to be set through code
        public IEnumerable<InstallerBase> NormalInstallers
        {
            get { return _normalInstallers; }
            set
            {
                _normalInstallers.Clear();
                _normalInstallers.AddRange(value);
            }
        }

        public abstract DiContainer Container
        {
            get;
        }
        public abstract IEnumerable<GameObject> GetRootGameObjects();


        public void AddNormalInstallerType(Type installerType)
        {
            Assert.IsNotNull(installerType);
            Assert.That(installerType.DerivesFrom<InstallerBase>());

            _normalInstallerTypes.Add(installerType);
        }

        public void AddNormalInstaller(InstallerBase installer)
        {
            _normalInstallers.Add(installer);
        }

        void CheckInstallerPrefabTypes(List<MonoInstaller> installers, List<MonoInstaller> installerPrefabs)
        {
            foreach (var installer in installers)
            {
                Assert.IsNotNull(installer, "Found null installer in Context '{0}'", name);

#if UNITY_EDITOR
#if UNITY_2019_1_OR_NEWER
                // TODO - Is there a way to check this using GetPrefabAssetType in 2019+?
#else
#if UNITY_2018_3
                Assert.That(PrefabUtility.GetPrefabAssetType(installer.gameObject) == PrefabAssetType.NotAPrefab,
#else
                Assert.That(PrefabUtility.GetPrefabType(installer.gameObject) != PrefabType.Prefab,
#endif
                    "Found prefab with name '{0}' in the Installer property of Context '{1}'.  You should use the property 'InstallerPrefabs' for this instead.", installer.name, name);
#endif
#endif
            }

            foreach (var installerPrefab in installerPrefabs)
            {
                Assert.IsNotNull(installerPrefab, "Found null prefab in Context");

                // We'd like to do this but this is actually a valid case sometimes
                // (eg. loading an asset bundle with a scene containing a scene context when inside unity editor)
//#if UNITY_EDITOR
                //Assert.That(PrefabUtility.GetPrefabType(installerPrefab.gameObject) == PrefabType.Prefab,
                    //"Found non-prefab with name '{0}' in the InstallerPrefabs property of Context '{1}'.  You should use the property 'Installer' for this instead",
                    //installerPrefab.name, this.name);
//#endif
                Assert.That(installerPrefab.GetComponent<MonoInstaller>() != null,
                    "Expected to find component with type 'MonoInstaller' on given installer prefab '{0}'", installerPrefab.name);
            }
        }

        protected void InstallInstallers()
        {
            InstallInstallers(
                _normalInstallers, _normalInstallerTypes, _scriptableObjectInstallers, _monoInstallers, _installerPrefabs);
        }

        protected void InstallInstallers(
            List<InstallerBase> normalInstallers,
            List<Type> normalInstallerTypes,
            List<ScriptableObjectInstaller> scriptableObjectInstallers,
            List<MonoInstaller> installers,
            List<MonoInstaller> installerPrefabs)
        {
            CheckInstallerPrefabTypes(installers, installerPrefabs);

            // Ideally we would just have one flat list of all the installers
            // since that way the user has complete control over the order, but
            // that's not possible since Unity does not allow serializing lists of interfaces
            // (and it has to be an inteface since the scriptable object installers only share
            // the interface)
            //
            // So the best we can do is have a hard-coded order in terms of the installer type
            //
            // The order is:
            //      - Normal installers given directly via code
            //      - ScriptableObject installers
            //      - MonoInstallers in the scene
            //      - Prefab Installers
            //
            // We put ScriptableObject installers before the MonoInstallers because
            // ScriptableObjectInstallers are often used for settings (including settings
            // that are injected into other installers like MonoInstallers)

            var allInstallers = normalInstallers.Cast<IInstaller>()
                .Concat(scriptableObjectInstallers.Cast<IInstaller>())
                .Concat(installers.Cast<IInstaller>()).ToList();

            foreach (var installerPrefab in installerPrefabs)
            {
                Assert.IsNotNull(installerPrefab, "Found null installer prefab in '{0}'", GetType());

                GameObject installerGameObject;

#if ZEN_INTERNAL_PROFILING
                using (ProfileTimers.CreateTimedBlock("GameObject.Instantiate"))
#endif
                {
                    installerGameObject = GameObject.Instantiate(installerPrefab.gameObject);
                }

                installerGameObject.transform.SetParent(transform, false);
                var installer = installerGameObject.GetComponent<MonoInstaller>();

                Assert.IsNotNull(installer, "Could not find installer component on prefab '{0}'", installerPrefab.name);

                allInstallers.Add(installer);
            }

            foreach (var installerType in normalInstallerTypes)
            {
                var installer = (InstallerBase)Container.Instantiate(installerType);

#if ZEN_INTERNAL_PROFILING
                using (ProfileTimers.CreateTimedBlock("User Code"))
#endif
                {
                    installer.InstallBindings();
                }
            }

            foreach (var installer in allInstallers)
            {
                Assert.IsNotNull(installer,
                    "Found null installer in '{0}'", GetType());

                Container.Inject(installer);

#if ZEN_INTERNAL_PROFILING
                using (ProfileTimers.CreateTimedBlock("User Code"))
#endif
                {
                    installer.InstallBindings();
                }
            }
        }

        // NOTE:
        // - CPUスパイクを回避するために、InstallSceneBindingsメソッドを削除した
        // - InstallSceneBindingsメソッドで実行されていた`Resources.FindObjectsOfTypeAll<ZenjectBinding>()`がCPUスパイクの原因になっていた

        // NOTE:
        // - InstallSceneBindingsメソッドの削除と合わせて、不要になったInstallZenjectBindingメソッドを削除した

        protected abstract void GetInjectableMonoBehaviours(List<MonoBehaviour> components);
    }
}

#endif
