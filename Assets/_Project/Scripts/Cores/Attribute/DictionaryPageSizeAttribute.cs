using System;
using UnityEngine;

namespace Kope.Core.Attribute {
	/// <summary>
	/// Put on a SerializableDictionary&lt;,&gt; field to override how many rows
	/// SerializableDictionaryDrawer shows per page. Without this attribute the drawer defaults
	/// to 20 rows per page.
	///
	///   [DictionaryPageSize(50)]
	///   public SerializableDictionary&lt;string, int&gt; scores;
	///
	/// Values &lt;= 0 are ignored and the drawer falls back to its default. Has no effect on
	/// fields that aren't a SerializableDictionary&lt;,&gt; (or a type deriving from one).
	///
	/// Lives outside the Editor assembly (unlike the drawer itself) since it needs to be
	/// attachable to fields in ordinary runtime scripts — PropertyAttribute and its subclasses
	/// are UnityEngine types, not editor-only ones, so this compiles and is safe to reference
	/// from player builds even though it only has an effect in the Inspector.
	/// </summary>
	[AttributeUsage(AttributeTargets.Field)]
	public sealed class DictionaryPageSizeAttribute : PropertyAttribute {
		public readonly int PageSize;

		public DictionaryPageSizeAttribute(int pageSize) {
			PageSize = pageSize;
		}
	}
}