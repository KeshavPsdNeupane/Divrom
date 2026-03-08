
using UnityEngine;

namespace Kope.Core.Extensions
{


	public static class UnityTypeExtension
	{
		public static string ParentGameObjectStackTrace<T>(this T type) where T : MonoBehaviour
		{
			return $"(GameObject: {FindAllParentStackString(type.gameObject)})";
		}
		private static string FindAllParentStackString(GameObject gameObject)
		{
			System.Text.StringBuilder sb = new();
			Transform cursor = gameObject.transform;

			while (cursor != null)
			{
				if (sb.Length > 0) sb.Insert(0, "->");
				sb.Insert(0, cursor.name);
				cursor = cursor.parent;
			}
			string sceneName = gameObject.scene.name ?? "UnknownScene";
			return $"{sceneName}-->{sb}";
		}
	}
}
