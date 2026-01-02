using System;
using UnityEngine;

namespace ServiceLocatorPattern
{
    /// <summary>
    /// A global service locator that persists across scenes and manages global services.
    /// Must be explicitly registered via GloblalBootStrap using RegisterService.
    /// No automatic scene searching or creation is performed.
    /// Here BS_RS means "Added via Bootstrapper RegisterService".
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
        public void RegisterService<TService>(Func<TService> factory) where TService : GlobalServiceBase
        {
            if (factory == null)
            {
                Logger.Error($"[GlobalLocator] RegisterService failed: The factory delegate for {typeof(TService).Name} is null.");
                return;
            }
            if (!this._canRegister)
            {
                Logger.Error($"[GlobalLocator] Locked. Factory for {typeof(TService).Name} will not be invoked.");
                return;
            }
            var type = typeof(TService);
            if (this.services.ContainsKey(type))
            {
                Logger.Warn($"[GlobalLocator] {type.Name} is already registered. Skipping factory invocation.");
                return;
            }

            TService service = factory.Invoke();
            if (service == null)
            {
                Logger.Error($"[GlobalLocator] Factory for {type.Name} returned null. Registration aborted.");
                return;
            }
            Register(service, "BS_RS", "Bootstrapper Registration Service");
        }
        private void Register(GlobalServiceBase service, string tag, string source)
        {
            var type = service.GetType();
            if (services.ContainsKey(type)) return;


            service.name = $"[Global] {type.Name}_{tag}";
            service.transform.SetParent(transform);

            service.Initialize(service.name + " via " + source, false, service.gameObject);
            services[type] = service;
        }



    }
}