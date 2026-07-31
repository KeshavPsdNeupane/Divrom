using Kope.Feature.PathFinding.Node;
using UnityEngine;

/// <summary>
/// Shared 2D-grid gizmo helpers for both macro (BoundingBox-based) and micro (Vec2Int-based)
/// visualizers. Draws flat, Z-layered rectangles via Handles instead of solid Gizmos.DrawCube,
/// since everything sits at Z=0 (Vec2Int/BoundingBox -> Vector3 zeroes Z) and overlapping flat
/// cubes on an identical depth plane z-fight. Small explicit Z offsets per layer give a
/// deterministic draw order instead.
/// </summary>
internal static class GridGizmoUtil {
	private static readonly Vector3[] Verts = new Vector3[4];

	#region BoundingBox (Macro)

	/// <summary>
	/// True floating-point center. BoundingBox.Center uses integer division and truncates.
	/// </summary>
	/// <remarks>
	/// A cell index is a half-open interval — index <c>x</c> spans world-space range
	/// <c>[x, x + 1)</c> under this project's <see cref="Mathf.FloorToInt"/> world-to-grid
	/// convention. Averaging <c>Min</c> and <c>Max</c> indices alone lands on the box's corner,
	/// not its visual center, so a trailing <c>+ 0.5</c> is required to align with how the
	/// Tilemap actually renders cells (matches <see cref="UnityEngine.Tilemaps.Tilemap.GetCellCenterWorld"/>
	/// for cell size 1, tilemap at origin).
	/// </remarks>
	public static Vector3 FloatCenter(BoundingBox box) =>
		new(
			(box.Min.X + box.Max.X) * 0.5f + 0.5f,
			(box.Min.Y + box.Max.Y) * 0.5f + 0.5f,
			0f
		);

	/// <summary>Inclusive cell footprint. BoundingBox.Size is Max-Min, which
	/// undercounts a single-cell box as (0,0).</summary>
	public static Vector3 FootprintSize(BoundingBox box) =>
		new(box.Size.X + 1, box.Size.Y + 1, 0f);

	public static void DrawFlatRect(BoundingBox box, float zDepth, Color fill, Color outline) {
#if UNITY_EDITOR
		Vector3 center = FloatCenter(box);
		center.z = zDepth;
		Vector3 half = FootprintSize(box) * 0.5f;

		DrawRect(center, half, fill, outline);
#endif
	}

	#endregion

	#region Vec2Int (Micro)

	/// <summary>True floating-point center of a single grid cell — same convention as the
	/// BoundingBox overload above (cell <c>x</c> spans <c>[x, x + 1)</c>), just for one cell
	/// instead of a span, since micro pathfinding operates on individual tiles.</summary>
	public static Vector3 FloatCenter(Vec2Int cell) =>
		new(cell.X + 0.5f, cell.Y + 0.5f, 0f);

	/// <summary>A single cell's footprint is always 1x1.</summary>
	public static readonly Vector3 SingleCellFootprint = new(1f, 1f, 0f);

	public static void DrawFlatRect(Vec2Int cell, float zDepth, Color fill, Color outline) {
#if UNITY_EDITOR
		Vector3 center = FloatCenter(cell);
		center.z = zDepth;
		Vector3 half = SingleCellFootprint * 0.5f;

		DrawRect(center, half, fill, outline);
#endif
	}

	#endregion

#if UNITY_EDITOR
	private static void DrawRect(Vector3 center, Vector3 half, Color fill, Color outline) {
		Verts[0] = center + new Vector3(-half.x, -half.y, 0f);
		Verts[1] = center + new Vector3(-half.x, half.y, 0f);
		Verts[2] = center + new Vector3(half.x, half.y, 0f);
		Verts[3] = center + new Vector3(half.x, -half.y, 0f);

		UnityEditor.Handles.DrawSolidRectangleWithOutline(Verts, fill, outline);
	}
#endif
}