using UnityEngine;

namespace Kope.Core {
	/// <summary>
	/// Provides a standardized way to retrieve a specific data type from any source.
	/// Used by <see cref="InspectorWireOrScriptableObjectConfig{TInspector, TScriptableObject, TReturn}"/> 
	/// to bridge local and global data providers.
	/// </summary>
	/// <typeparam name="TReturn">The type of data to be retrieved.</typeparam>
	public interface IReturn<TReturn> {
		TReturn GetValue();
	}

	/// <summary>
	/// A toggleable configuration wrapper that allows switching between a local inspector-defined value 
	/// and a shared ScriptableObject asset without changing the consuming logic.
	/// </summary>
	/// <typeparam name="TInspector">The class handling local serialization (must implement <see cref="IReturn{TReturn}"/>).</typeparam>
	/// <typeparam name="TScriptableObject">The ScriptableObject asset (must implement <see cref="IReturn{TReturn}"/>).</typeparam>
	/// <typeparam name="TReturn">The final data type returned by both sources.</typeparam>
	[System.Serializable]
	public abstract class InspectorWireOrScriptableObjectConfig<TInspector, TScriptableObject, TReturn>
		where TInspector : class, IReturn<TReturn>
		where TScriptableObject : ScriptableObject, IReturn<TReturn> {

		[Tooltip("Switch between using the local 'Inspector' reference or the global 'ScriptableObject' asset.")]
		[SerializeField] private bool useScriptableObject;

		public TInspector inspector;
		public TScriptableObject scriptableObject;

		/// <summary>
		/// Returns the value from the currently active source. 
		/// Falls back to default if the active source is null.
		/// </summary>
		public TReturn Value {
			get {
				if (this.useScriptableObject) {
					return this.scriptableObject != null ? this.scriptableObject.GetValue() : default;
				}
				return this.inspector != null ? this.inspector.GetValue() : default;
			}
		}
	}
}