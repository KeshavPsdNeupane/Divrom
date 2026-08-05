using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Kope.Feature.PathFindingNew.Utility {
	/// <summary>
	/// A utility class that caches and manages sprites for pathfinding 
	/// tiles based on their colors.
	/// This is used to optimize the rendering of pathfinding tiles by reusing 
	/// sprites for tiles with the same color, reducing memory usage and improving performance.
	/// The cache is cleared when the application is loaded to ensure that no 
	/// stale sprites are kept in memory.
	/// </summary>
	public static partial class SpriteTextureCache {
		[AutoStaticsCleanup]
		private static readonly Dictionary<Color32, Sprite> Cache = new();
		/// <summary>
		/// Gets or creates a sprite for the specified color. If a sprite for the color 
		/// already exists in the cache, it is returned; otherwise, a new sprite
		/// is created, added to the cache, and then returned.
		/// The lifetime of the created sprites is managed by the cache, and they 
		/// will be destroyed when the domain is reloaded or the application is closed, to free up memory.
		/// </summary>
		/// <param name="color"></param>
		/// <returns></returns>
		public static Sprite GetOrCreate(Color color) {
			Color32 color32 = color;
			if (Cache.TryGetValue(color32, out var sprite)) {
				return sprite;
			}

			Texture2D tex = new(1, 1, TextureFormat.RGBA32, false) {
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp
			};
			tex.SetPixel(0, 0, color);
			tex.Apply();

			Sprite newSprite = Sprite.Create(
				tex,
				new Rect(0f, 0f, 1f, 1f),
				new Vector2(0.5f, 0.5f),
				1f
			);

			newSprite.name = $"SPRITE_TEXT_{color32.r}_{color32.g}_{color32.b}";
			Cache[color32] = newSprite;

			return newSprite;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Texture2D GetOrCreateTexture(Color color) {
			// so that we don't create a new sprite, but just return the texture
			// this is useful for the region gizmo utility, which needs to draw a texture
			// instead of a sprite, and we don't want to create a new sprite for every
			// region, since that would be wasteful and unnecessary
			// we can just use the same texture for all regions of the same color
			return GetOrCreate(color).texture;
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		public static void ClearCache() {
			foreach (var sprite in Cache.Values) {
				if (sprite == null) continue;
#if UNITY_EDITOR
				if (!Application.isPlaying) {
					if (sprite.texture != null) Object.DestroyImmediate(sprite.texture);
					Object.DestroyImmediate(sprite);
					continue;
				}
#endif
				if (sprite.texture != null) Object.Destroy(sprite.texture);
				Object.Destroy(sprite);
			}
			Cache.Clear();
		}

		public static void DisplayCacheContents() {
			StringBuilder sb = new();
			sb.AppendLine($"SpriteTextureCache contains {Cache.Count} entries:");
			foreach (var kvp in Cache) {
				sb.AppendLine($"Color: {kvp.Key}, Sprite: {kvp.Value.name}");
			}
			Debug.Log(sb.ToString());
		}
	}
}