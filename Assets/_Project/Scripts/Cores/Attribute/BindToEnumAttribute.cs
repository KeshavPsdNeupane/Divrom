namespace Kope.Core.Attribute {
	using System;

	[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
	public class BindToEnumAttribute : Attribute {
		public object EnumValue { get; }
		public Type TargetType { get; }

		public BindToEnumAttribute(object enumValue, Type targetType) {
			this.EnumValue = enumValue;
			this.TargetType = targetType;
		}
	}
}