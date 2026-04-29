using System;
using UnityEngine;
using System.Runtime.InteropServices;
using System.Text;
using System.Reflection;
namespace Kope.Core.Extensions {
	public static class TypeExtension {
		/// <summary>
		/// Removes the "Enum" postfix from the type name.
		/// Or a custom suffix if provided.
		/// </summary>
		/// <param name="type"></param>
		/// <param name="suffix"></param>
		/// <returns></returns>
		public static string ToStringRemovePostFix(this Type type, string suffix = "Enum") {

			return StringExtension.RemovePostFix(type.Name, suffix);
		}

		public static void PrintFieldBreakdown(this object obj) {
			if (obj == null) return;
			var type = obj.GetType();
			var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

			StringBuilder sb = new();
			sb.AppendLine($"<b>Memory Breakdown for {type.Name}</b>");
			sb.AppendLine("<color=#888888>-----------------------------------------------------------</color>");
			sb.AppendLine(string.Format("{0,-25} | {1,-15} | {2,-10}", "Field Name", "Type", "Size (B)"));
			sb.AppendLine("<color=#888888>-----------------------------------------------------------</color>");

			long totalDataSize = 0;

			foreach (var f in fields) {
				// Determine size: Value types are measured, Reference types are just a pointer size
				int fieldSize = f.FieldType.IsValueType && !f.FieldType.IsEnum
					? Marshal.SizeOf(f.FieldType)
					: IntPtr.Size;

				totalDataSize += fieldSize;

				sb.AppendLine(string.Format("{0,-25} | {1,-15} | {2,-10}",
					f.Name,
					f.FieldType.Name,
					fieldSize));
			}

			sb.AppendLine("<color=#888888>-----------------------------------------------------------</color>");
			// 16 bytes is the standard overhead for a class instance on x64
			sb.AppendLine($"<b>Instance Overhead:</b> 16 bytes");
			sb.AppendLine($"<b>Total Est. Data:</b> {totalDataSize} bytes");
			sb.AppendLine($"<b>Total Footprint:</b> {totalDataSize + 16} bytes");

			Debug.Log(sb.ToString());
		}
	}


}