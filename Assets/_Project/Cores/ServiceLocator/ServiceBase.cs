using UnityEngine;

namespace ServiceLocatorPattern
{
    public class ServiceBase : MonoBehaviour
    {
        private bool isInitialized = false;
        public bool IsInitialized => isInitialized;

        public virtual void Initialize(string info, bool isWarn = false, GameObject gameObject = null)
        {
            if (isWarn)
            {
                Logger.Warn($"[Service] Initialized {GetType().Name}: {info}", gameObject);
            }
            else
            {
                Logger.Log($"[Service] Initialized {GetType().Name}: {info}", gameObject);
            }
            this.isInitialized = true;
        }

    }

    public class GlobalServiceBase : ServiceBase { }
    public class SceneServiceBase : ServiceBase
    {

        public virtual void Awake()
        {
            CheckForDuplicates();
        }
        private void CheckForDuplicates()
        {
            // Finds all instances of the specific concrete class (e.g., AudioManager)
            var type = GetType();
            var instances = Object.FindObjectsByType(type, FindObjectsSortMode.None);

            if (instances.Length > 1)
            {
                Logger.Warn($"[ServiceLocator] Multiple instances of <b>{type.Name}</b> found on scene! For now Game will use first one...", this.gameObject);
            }
        }


    }

}