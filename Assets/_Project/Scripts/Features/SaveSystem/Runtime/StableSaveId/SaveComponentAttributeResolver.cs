using System;
using System.Reflection;
using Kope.SaveSystem.Attributes;
namespace Kope.SaveSystem {
	public static class SaveComponentAttributeResolver {
		public enum ResolutionFailureReason {
			None,
			NotTagged,
			InheritDepthExceeded
		}

		/// <summary>
		/// Resolves the effective SaveComponent id for a type, following InheritSaveId
		/// up the base chain (bounded by SearchNParent) if the type doesn't declare its own.
		/// </summary>
		public static bool TryGetEffectiveId(Type type, out string id, out Type declaringType) {
			return TryGetEffectiveId(type, out id, out declaringType, out _);
		}

		public static bool TryGetEffectiveId(Type type, out string id, out Type declaringType, out ResolutionFailureReason failureReason) {
			var direct = type.GetCustomAttribute<SaveComponentAttribute>(inherit: false);
			if (direct != null) {
				id = direct.Id;
				declaringType = type;
				failureReason = ResolutionFailureReason.None;
				return true;
			}

			var inherit = type.GetCustomAttribute<InheritSaveIdAttribute>(inherit: false);
			if (inherit == null) {
				id = null;
				declaringType = null;
				failureReason = ResolutionFailureReason.NotTagged;
				return false;
			}

			var current = type.BaseType;
			int stepsRemaining = inherit.SearchNParent;

			while (current != null && current != typeof(object) && stepsRemaining > 0) {
				var baseAttr = current.GetCustomAttribute<SaveComponentAttribute>(inherit: false);
				if (baseAttr != null) {
					id = baseAttr.Id;
					declaringType = current;
					failureReason = ResolutionFailureReason.None;
					return true;
				}
				current = current.BaseType;
				stepsRemaining--;
			}

			id = null;
			declaringType = null;
			failureReason = ResolutionFailureReason.InheritDepthExceeded;
			return false;
		}
	}
}