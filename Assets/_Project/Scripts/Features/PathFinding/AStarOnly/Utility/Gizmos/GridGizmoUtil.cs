using UnityEngine;

namespace Kope.Feature.PathFindingNew.Utility {

	/// <summary>
	/// Shared Gizmo-drawing helpers for the pathfinding visualizers. Draws a single grid cell as a
	/// thin flat slab in the X-Y plane (thickness along Z) with a wireframe border, so the
	/// open/closed/current/final-path layers can be stacked via <c>zOffset</c> without z-fighting —
	/// see how <see cref="PathfinderGizmos"/> (and the old suite's MicroPathfinderGizmos) call this
	/// with progressively larger offsets (0f, 0.01f, 0.02f, 0.03f) per layer.
	/// <para>
	/// Matches the old hierarchical suite's convention exactly: grid X -> world X, grid Y -> world Y,
	/// Z reserved purely for layer offsets. There's no vertical "base height" lift — this project's
	/// pathfinding grid lives in the X-Y plane (camera along Z), same as the old suite, so lifting
	/// cells along Y only pushes them out of the plane the camera can actually see.
	/// </para>
	/// </summary>
	public static class GridGizmoUtil {
		/// <summary>World-space size of one grid cell along X and Y. Change this if your grid isn't unit-sized.</summary>
		public const float CellSize = 1f;

		/// <summary>Fraction of CellSize actually drawn, leaving a thin gap between adjacent 
		/// cells so the grid reads clearly in the Scene view.</summary>
		private const float FillRatio = 0.95f;

		/// <summary>Depth thickness of the flat slab along Z — just enough for DrawCube to 
		/// render as a visible plate rather than a zero-depth plane.</summary>
		private const float SlabThickness = 0.02f;

		/// <summary>
		/// True floating-point center of a grid cell in the X-Y plane, before layer offset — mirrors
		/// the old suite's <c>GridGizmoUtil.FloatCenter(Vec2Int)</c> exactly (cell <c>x</c> spans
		/// world-space range <c>[x, x + 1)</c>, so + 0.5 lands on the visual center).
		/// </summary>
		public static Vector3 FloatCenter(Vec2Int cell) =>
			new(cell.X + 0.5f, cell.Y + 0.5f, 0f);

		/// <summary>
		/// Same as <see cref="FloatCenter"/> but with <paramref name="zOffset"/> written into Z —
		/// matches where <see cref="DrawFlatRect"/> actually draws that cell's slab. Use this (not
		/// FloatCenter) for anything meant to visually line up with the drawn slab: labels, or the
		/// lines connecting final-path cells.
		/// </summary>
		public static Vector3 LayeredCenter(Vec2Int cell, float zOffset) {
			Vector3 center = FloatCenter(cell);
			center.z = zOffset;
			return center;
		}

		/// <summary>
		/// Draws one grid cell as a filled, flat slab (X-Y plane) with a wireframe border.
		/// </summary>
		/// <param name="cell">Grid coordinate to draw.</param>
		/// <param name="zOffset">Small Z offset — stack layers (closed/open/current/path) at increasing offsets to avoid z-fighting between them. Not a height lift.</param>
		/// <param name="fillColor">Color of the filled slab.</param>
		/// <param name="borderColor">Color of the wireframe border.</param>
		public static void DrawFlatRect(Vec2Int cell, float zOffset, Color fillColor, Color borderColor) {
			Vector3 center = LayeredCenter(cell, zOffset);
			Vector3 size = new(CellSize * FillRatio, CellSize * FillRatio, SlabThickness);

			Gizmos.color = fillColor;
			Gizmos.DrawCube(center, size);

			Gizmos.color = borderColor;
			Gizmos.DrawWireCube(center, size);
		}
	}
}