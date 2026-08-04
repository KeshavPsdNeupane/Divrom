using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Kope.Feature.PathFindingNew.Tile;
using Kope.Feature.PathFindingNew.Utility;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kope.Feature.PathFindingNew.Baking {
	/// <summary>
	/// Utility class for rendering pathfinding region visualizations in the Unity Scene view.
	/// </summary>
	public static class RegionGizmoUtility {
		private const float GOLDEN_RATIO_CONJUGATE = 0.618033988749895f;

		// If a generated region color lands within this RGB distance of the
		// reserved non-traversable color, it gets nudged away from it. This is
		// what guarantees no traversable region can ever visually collide with
		// the (user-configurable) Region 0 color.
		private const float COLOR_COLLISION_THRESHOLD = 0.25f;

		// Fixed vertical thickness for the region fill cubes. No longer exposed
		// as a parameter — label placement is derived from this internally, so
		// callers don't need to reason about it.
		private const float REGION_FILL_HEIGHT = 0.05f;

		// How far above the top face of the fill cubes a label floats. Keeps
		// labels clear of the fill regardless of REGION_FILL_HEIGHT.
		private const float LABEL_Z_PADDING = 0.5f;

		// A 1x1 texture reused across draws for the label's background box —
		// cheaper than an actual text outline, and only regenerated when the
		// requested background color actually changes.
		private static Texture2D _labelBackgroundTexture;
		private static Color _labelBackgroundTextureColor;

		/// <summary>
		/// Draws colored Gizmos for all regions, including Region 0 (non-traversable),
		/// which is rendered using the caller-supplied <paramref name="nonTraversableColor"/>.
		/// </summary>
		/// <param name="regionNodePosition">The region dictionary mapping region IDs to (tile, position) lists.</param>
		/// <param name="tilemap">Source tilemap, used to keep cube placement in sync with CellToWorld.</param>
		/// <param name="nonTraversableColor">Reserved color for Region 0. No other region will be assigned this color.</param>
		/// <param name="alpha">Transparency of the region fill overlays (0.0 to 1.0). Labels are unaffected by this — they're always fully solid.</param>
		/// <param name="showLabels">If true, renders text labels for each region ID in Scene view.</param>
		/// <param name="labelColor">Color of the region labels. Alpha is always forced to 1 (labels never render transparent), regardless of what's passed in. Defaults to white.</param>
		/// <param name="labelBackgroundColor">Background box color behind each label, for readability over the fill. Null uses the default translucent black; pass Color.clear to disable the background entirely.</param>
		public static void OnGizmoDraw(
			IReadOnlyDictionary<ushort, Vec2Int[]> regionNodePosition,
			Tilemap tilemap,
			Color nonTraversableColor,
			float alpha = 0.6f,
			bool showLabels = false,
			Color? labelColor = null,
			Color? labelBackgroundColor = null) {
			if (regionNodePosition == null || regionNodePosition.Count == 0 || tilemap == null) return;

			// Negative/out-of-range alpha (e.g. a stray -0.1 in the inspector)
			// would otherwise silently make every cube fully invisible.
			alpha = Mathf.Clamp01(alpha);

#if UNITY_EDITOR
			GUIStyle labelStyle = null;
			if (showLabels) {
				// Labels are always fully solid — force alpha to 1 no matter what
				// color was passed in, so they stay readable over the fill.
				Color solidLabelColor = labelColor ?? Color.white;
				solidLabelColor.a = 1f;

				Color backgroundColor = labelBackgroundColor ?? new Color(0f, 0f, 0f, 1f);

				labelStyle = new GUIStyle {
					normal = {
						textColor = solidLabelColor,
						background = GetOrCreateLabelBackgroundTexture(backgroundColor)
					},
					fontSize = 12,
					fontStyle = FontStyle.Bold,
					alignment = TextAnchor.MiddleCenter,
					padding = new RectOffset(4, 4, 2, 2)
				};
			}
#endif

			// Full cell size, no shrink padding — a gap between same-colored
			// neighboring tiles is what reads as "grid lines" instead of one
			// solid filled region.
			Vector3 cubeSize = new(tilemap.cellSize.x, tilemap.cellSize.y, REGION_FILL_HEIGHT);

			// Labels always float above the top face of the fill cubes, plus a
			// fixed padding — independent of any caller-supplied value.
			Vector3 labelOffset = Vector3.up * (REGION_FILL_HEIGHT + LABEL_Z_PADDING);

			// Pass 1: fill every region's tiles first.
			foreach (var (regionId, positions) in regionNodePosition) {
				if (positions == null || positions.Length == 0) continue;

				bool isNonTraversable = regionId == TileTerrainData.NON_TRAVERSABLE_REGION_ID;

				Gizmos.color = isNonTraversable
					? WithAlpha(nonTraversableColor, alpha)
					: GetDistinctRegionColor(regionId, alpha, nonTraversableColor);

				foreach (var position in positions) {
					Vector3 worldPos = GridToWorldPosition(position, tilemap);
					Gizmos.DrawCube(worldPos, cubeSize);
				}
			}

#if UNITY_EDITOR
			// Pass 2: labels submitted last, after every fill, so they always
			// draw above the color instead of risking being covered by it.
			// Region 0 is a sentinel for "not part of any region", so it never
			// gets a label.
			if (showLabels) {
				foreach (var (regionId, positions) in regionNodePosition) {
					if (positions == null || positions.Length == 0) continue;
					if (regionId == TileTerrainData.NON_TRAVERSABLE_REGION_ID) continue;

					Vec2Int centroidPosition = GetSampledCentroidPosition(positions);
					Vector3 labelPos = GridToWorldPosition(centroidPosition, tilemap) + labelOffset;
					Handles.Label(labelPos, $"Region {regionId}", labelStyle);
				}
			}
#endif
		}

		/// <summary>
		/// Generates a visually distinct RGB color for any integer Region ID, guaranteed
		/// to never land too close to <paramref name="reservedColor"/> (Region 0's color).
		/// </summary>
		public static Color GetDistinctRegionColor(ushort regionId, float alpha, Color reservedColor) {
			float hue = regionId * GOLDEN_RATIO_CONJUGATE % 1.0f;
			Color color = Color.HSVToRGB(hue, 0.8f, 0.9f);

			if (ColorsAreTooSimilar(color, reservedColor, COLOR_COLLISION_THRESHOLD)) {
				// Rotate to the opposite side of the hue wheel and try again.
				hue = (hue + 0.5f) % 1.0f;
				color = Color.HSVToRGB(hue, 0.8f, 0.9f);
			}

			color.a = alpha;
			return color;
		}

		private static bool ColorsAreTooSimilar(Color a, Color b, float threshold) {
			float dr = a.r - b.r;
			float dg = a.g - b.g;
			float db = a.b - b.b;
			return (dr * dr) + (dg * dg) + (db * db) < threshold * threshold;
		}

		private static Color WithAlpha(Color color, float alpha) {
			color.a = alpha;
			return color;
		}

		/// <summary>
		/// Finds the actual tile position closest to the geometric centroid of
		/// the region. Sampling the nearest real tile — instead of using the raw
		/// average directly, or just grabbing the middle list entry — keeps the
		/// label anchored inside the region even when it's concave, ring-shaped,
		/// or otherwise non-convex, where the raw average could land in a gap
		/// that isn't actually part of the region.
		/// </summary>
		private static Vec2Int GetSampledCentroidPosition(Vec2Int[] positionArray) {
			long sumX = 0;
			long sumY = 0;
			foreach (var position in positionArray) {
				sumX += position.X;
				sumY += position.Y;
			}

			float centroidX = (float)sumX / positionArray.Length;
			float centroidY = (float)sumY / positionArray.Length;

			Vec2Int closestPosition = positionArray[0];
			float closestSqrDist = float.MaxValue;

			foreach (var position in positionArray) {
				float dx = position.X - centroidX;
				float dy = position.Y - centroidY;
				float sqrDist = (dx * dx) + (dy * dy);

				if (sqrDist < closestSqrDist) {
					closestSqrDist = sqrDist;
					closestPosition = position;
				}
			}
			return closestPosition;
		}

#if UNITY_EDITOR
		/// <summary>
		/// Returns a shared 1x1 texture tinted to <paramref name="color"/>, used as
		/// the label's background box. Regenerated only when the requested color
		/// actually changes, so repeated Gizmo draws (every frame in Scene view)
		/// don't reallocate a texture each time.
		/// </summary>
		private static Texture2D GetOrCreateLabelBackgroundTexture(Color color) {
			if (_labelBackgroundTexture != null && _labelBackgroundTextureColor == color) {
				return _labelBackgroundTexture;
			}

			if (_labelBackgroundTexture == null) {
				_labelBackgroundTexture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
			}

			_labelBackgroundTexture.SetPixel(0, 0, color);
			_labelBackgroundTexture.Apply();
			_labelBackgroundTextureColor = color;

			return _labelBackgroundTexture;
		}
#endif

		private static Vector3 GridToWorldPosition(Vec2Int gridPos, Tilemap tilemap) {
			Vector3 cellWorld = tilemap.CellToWorld(new Vector3Int(gridPos.X, gridPos.Y, 0));
			return cellWorld + new Vector3(tilemap.cellSize.x * 0.5f, tilemap.cellSize.y * 0.5f, 0f);
		}
	}
}