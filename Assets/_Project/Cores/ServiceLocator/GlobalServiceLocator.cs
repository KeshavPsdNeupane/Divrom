using UnityEngine;

namespace ServiceLocatorPattern
{
    /// <summary>
    /// A global service locator that persists across scenes and manages global services.
    /// Must be explicitly registered via GloblalBootStrap using RegisterService.
    /// No automatic scene searching or creation is performed.
    /// Here A_RS means "Added via RegisterService".
    /// </summary>
    public class GlobalServiceLocator : ServiceLocator<GlobalServiceLocator, GlobalServiceBase>
    {
        private bool _canRegister = true;
        protected override void Awake()
        {
            this.isPersistent = true;
            base.Awake();
        }
        public void Lock() => this._canRegister = false;

        public bool TryGetService<TService>(out TService service) where TService : GlobalServiceBase
        {
            var type = typeof(TService);

            if (services.TryGetValue(type, out var existing))
            {
                service = (TService)existing;
                return true;
            }
            service = null;
            // We do NOT search the scene or create here. 
            // Global services MUST be registered via Bootstrapper or RegisterService.
            Logger.Error($"[GlobalLocator] Critical Error: {type.Name} is not registered. Check your GlobalBootStrap!");
            return false;
        }
        public void RegisterService<TService>(TService existingInstance = null) where TService : GlobalServiceBase
        {
            if (!this._canRegister)
            {
                Logger.Error("[GlobalLocator] Registration is locked. No further services can be registered at this time.");
                return;
            }
            var type = typeof(TService);
            if (services.ContainsKey(type)) return;

            string creationTag = "A_RS";
            // Use the provided instance, or find it, or create it
            TService instance = existingInstance
                                ?? FindInScene<TService>()
                                ?? new GameObject($"[Global] {typeof(TService).Name}_{creationTag} ").AddComponent<TService>();

            Register(instance, creationTag, "RegisterService");
        }

        private void Register(GlobalServiceBase service, string tag, string source)
        {
            var type = service.GetType();
            if (services.ContainsKey(type)) return;

            // Standardize naming and persistence
            service.name = $"[Global] {type.Name}_{tag}";
            service.transform.SetParent(transform); // Keeps Hierarchy clean

            service.Initialize(service.name + " via " + source);
            services[type] = service;
        }

    }
}