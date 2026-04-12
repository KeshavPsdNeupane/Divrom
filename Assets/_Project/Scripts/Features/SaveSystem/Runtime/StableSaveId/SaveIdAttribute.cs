using System;

namespace Kope.SaveSystem {
	[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
	public sealed class SaveIdAttribute : Attribute {
		public string Id { get; }

		public SaveIdAttribute(string id) {
			this.Id = id;
		}
	}
}
