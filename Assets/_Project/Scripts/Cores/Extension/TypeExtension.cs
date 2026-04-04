using System;
namespace Kope.Core.Extensions
{
	public static class TypeExtension
	{
		/// <summary>
		/// Removes the "Enum" postfix from the type name.
		/// Or a custom suffix if provided.
		/// </summary>
		/// <param name="type"></param>
		/// <param name="suffix"></param>
		/// <returns></returns>
		public static string ToStringRemovePostFix(this Type type, string suffix = "Enum")
		{

			return StringExtension.RemovePostFix(type.Name, suffix);
		}
	}



}