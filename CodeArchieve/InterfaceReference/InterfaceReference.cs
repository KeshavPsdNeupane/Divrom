using UnityEngine;

namespace Kope.Core.Collections.Serialization {
	[System.Serializable]
	public class InterfaceReference<TInterface> where TInterface : class {
		[SerializeField] private Object underlyingObject;
		/// <summary>
		/// Gets the resolved interface implementation. 
		/// Even if the name of TInterface changes, Unity preserves the underlying object reference.
		/// </summary>
		public TInterface Value {
			get {
				if (underlyingObject == null) return null;

				// If it's a GameObject, grab the component implementing the interface
				if (underlyingObject is GameObject go) {
					return go.GetComponent<TInterface>();
				}

				return underlyingObject as TInterface;
			}
		}
		// Implicit operator allows you to treat the reference like the interface directly in code
		public static implicit operator TInterface(InterfaceReference<TInterface> reference) {
			return reference?.Value;
		}
	}
}