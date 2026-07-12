
using UnityEngine;

namespace Kope.Core.Collections.Extensions {


	public static class UnityTypeExtension {
		/// <summary>
		/// This extension method generates a string that represents the full hierarchy 
		/// path of a GameObject in the Unity scene.
		/// It traverses up the GameObject's parent hierarchy, concatenating the names of each 
		/// parent GameObject until it reaches the root. The resulting string includes the scene name and the full hierarchy path, which can be useful for debugging and logging purposes to easily identify the location of a Game
		/// Object within the scene hierarchy. The format of the returned string is:
		/// "(GameObjectPath: SceneName->Parent1->Parent2->...->GameObjectName)".
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public static string GetFullHierarchyPath(this MonoBehaviour behaviour) {
			return $"(GameObjectPath: {behaviour.GetGameObjectHierarchyPath()})";
		}

		/// <summary>
		/// This method generates a string representation of the hierarchy path of a GameObject, 
		/// starting from the root of the scene down to the specified GameObject. It traverses up the
		///  parent hierarchy of the GameObject, concatenating the names of each parent GameObject until
		///  it reaches the root. The resulting string includes the scene name and the full hierarchy path,
		///  which can be useful for debugging and logging purposes to easily identify the location of a 
		/// GameObject within the scene hierarchy. The format of the returned string is:
		/// "SceneName->Parent1->Parent2->...->GameObjectName".
		/// </summary>
		/// <param name="gameObject"></param>
		/// <returns></returns>
		public static string GetGameObjectHierarchyPath(this MonoBehaviour behaviour) {
			System.Text.StringBuilder sb = new();
			Transform cursor = behaviour.gameObject.transform;

			while (cursor != null) {
				if (sb.Length > 0) sb.Insert(0, "->");
				sb.Insert(0, cursor.name);
				cursor = cursor.parent;
			}
			string sceneName = behaviour.gameObject.scene.name ?? "UnknownScene";
			return $"{sceneName}-->{sb}";
		}
	}
}
