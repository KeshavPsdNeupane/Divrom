using UnityEngine;

namespace Kope.Core.Collections.Serialization
{
	/// <summary>
	/// A refactor-safe wrapper that enables robust serialization of interfaces 
	/// in the Unity Inspector for both MonoBehaviours and ScriptableObjects.
	/// </summary>
	/// <typeparam name="TInterface">The explicit contract type required by systems.</typeparam>
	[System.Serializable]
	public class InterfaceReference<TInterface> where TInterface : class
	{
		[SerializeField] private Object underlyingObject;

		/// <summary>
		/// Gets the resolved interface implementation from either a GameObject component or an asset.
		/// </summary>
		public TInterface Value
		{
			get
			{
				if (underlyingObject == null) return null;

				// Handle MonoBehaviour cases via GameObject translation layer
				if (underlyingObject is GameObject go)
				{
					return go.GetComponent<TInterface>();
				}

				// Handle ScriptableObject or raw object interface extraction
				return underlyingObject as TInterface;
			}
		}

		public static implicit operator TInterface(InterfaceReference<TInterface> reference)
		{
			return reference?.Value;
		}
	}
}