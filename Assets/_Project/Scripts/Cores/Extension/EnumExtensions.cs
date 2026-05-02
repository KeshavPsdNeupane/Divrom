namespace Kope.Core.Extensions {
	public static class EnumExtensions {
		/// <summary>
		/// Converts the enum value to a lowercase string suitable for use as an ID part.
		/// 
		/// </summary>
		/// <param name="enumValue"></param>
		/// <returns> 
		///     The lowercase string representation of the enum value.
		/// </returns>
		public static string ToIdPart(this System.Enum enumValue) {
			return enumValue.ToString().ToLower();
		}

		/// <summary>
		/// Removes the "Enum" postfix from the enum type name.
		/// Or a custom suffix if provided.
		/// </summary>
		/// <param name="enumValue"></param>
		/// <param name="suffix"></param>
		/// <returns>
		///     The string representation of the enum type name with the specified suffix removed.
		/// </returns>
		public static string ToStringRemoveTypePostFix(this System.Enum enumValue, string suffix = "Enum") {
			return StringExtension.RemovePostFix(enumValue.GetType().Name, suffix);
		}
	}
}