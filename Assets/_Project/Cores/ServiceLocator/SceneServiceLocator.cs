using UnityEngine;
using System.Collections.Generic;

namespace ServiceLocatorPattern
{

    [CustomExecutionOrder(-50)]
    public class SceneServiceLocator : ServiceLocator<SceneServiceLocator>
    {
        [SerializeField] private List<MonoBehaviour> sceneServices = new();

        protected override void Awake()
        {
            this.isPersistent = false;
            base.Awake();
            Init();
        }

        private void Init()
        {
            foreach (var service in sceneServices)
            {
                if (service == null) continue;
                RegisterService(service);
            }
        }

        protected override void OnDestroy()
        {

            base.OnDestroy();
            this.sceneServices.Clear();
        }
    }
}