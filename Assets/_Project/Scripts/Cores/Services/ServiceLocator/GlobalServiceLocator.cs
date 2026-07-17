using System;
using System.Diagnostics.CodeAnalysis;
using Kope.Logging;
using UnityEngine;

namespace Kope.Core.ServiceLocator {
	/// <summary>
	/// A global service locator that persists across scenes and manages global services.
	/// Must be explicitly registered via GloblalBootStrap using RegisterService.
	/// No automatic scene searching or creation is performed.
	/// Here BS_RS means "Added via Bootstrapper RegisterService".
	/// </summary>
	public class GlobalServiceLocator : ServiceLocator<GlobalServiceLocator, GlobalServiceBase> {
		private bool _canRegister = true;
		private static AxisMode _dimension = AxisMode.TwoD;
		private bool _registerCompletedLoggingEnable = true;
		protected override void Awake() {
			this.isPersistent = true;
			base.Awake();
		}
		public void Lock() => this._canRegister = false;
		public void RegistrationCompletedLogEnable(bool enable) => this._registerCompletedLoggingEnable = enable;
		public static void InjectDimension(AxisMode dimension) => _dimension = dimension;
		public static AxisMode Dimension => _dimension;


		public bool TryGetService<TService>([MaybeNullWhen(false)] out TService service) where TService : GlobalServiceBase {
			var type = typeof(TService);

			if (services.TryGetValue(type, out var existing)) {
				service = (TService)existing;
				return true;
			}
			service = null;
			// We do NOT search the scene or create here. 
			// Global services MUST be registered via Bootstrapper or RegisterService.

			Debug.LogError($"[GlobalLocator] Critical Error: {type.Name} is not registered. Check your GlobalBootStrap!");

			return false;
		}
		public void RegisterService<TService>(Func<TService> factory) where TService : GlobalServiceBase {
			if (factory == null) {
				KLog.LogError($"[GlobalLocator] RegisterService failed: The factory delegate for {typeof(TService).Name} is null.");
				return;
			}
			if (!this._canRegister) {
				KLog.LogError($"[GlobalLocator] Locked. Factory for {typeof(TService).Name} will not be invoked.");
				return;
			}
			var type = typeof(TService);
			if (this.services.ContainsKey(type)) {
				KLog.LogWarning($"[GlobalLocator] {type.Name} is already registered. Skipping factory invocation.");
				return;
			}

			TService service = factory.Invoke();
			if (service == null) {
				KLog.LogError($"[GlobalLocator] Factory for {type.Name} returned null. Registration aborted.");
				return;
			}
			Register(service, "Registered_On_Bootstrap", "Bootstrapper Registration Service");
		}
		private void Register(GlobalServiceBase service, string tag, string source) {
			var type = service.GetType();
			if (services.ContainsKey(type)) return;


			service.name = $"[Global] {type.Name}_{tag}";
			service.transform.SetParent(transform);
			if (this._registerCompletedLoggingEnable)
				KLog.Log($"[Service] Initialized {type.Name}: {service.name} via {source}", service.gameObject);

			service.InitializeService();
			services[type] = service;
		}
	}
}