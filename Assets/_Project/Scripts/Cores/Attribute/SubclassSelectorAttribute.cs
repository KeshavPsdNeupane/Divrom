using System;
using UnityEngine;

namespace Kope.Core.Attributes {
	/// <summary>
	/// Attribute to allow picking subclasses for [SerializeReference] fields.
	/// </summary>
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
	public class SubclassSelectorAttribute : PropertyAttribute { }
}