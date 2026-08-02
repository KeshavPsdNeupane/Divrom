using System;
using System.Collections.Generic;
using Kope.Feature.PathFindingNew.Utility;

public static class PathSmoother {
	/// <summary>
	/// Extracts only corner nodes from the raw A* path.
	/// Long straight runs are collapsed into their endpoints.
	/// </summary>
	private static List<Vec2Int> ExtractCorners(List<Vec2Int> path) {
		int count = path.Count;

		if (count <= 2)
			return new List<Vec2Int>(path);

		List<Vec2Int> corners = new(count) {
			path[0]
		};

		int prevDx = path[1].X - path[0].X;
		int prevDy = path[1].Y - path[0].Y;

		for (int i = 2; i < count; i++) {
			int dx = path[i].X - path[i - 1].X;
			int dy = path[i].Y - path[i - 1].Y;

			if (dx != prevDx || dy != prevDy) {
				corners.Add(path[i - 1]);
				prevDx = dx;
				prevDy = dy;
			}
		}

		corners.Add(path[count - 1]);

		return corners;
	}

	/// <summary>
	/// Smooths a raw path using greedy string pulling.
	/// First removes redundant straight-line nodes to reduce LOS calls.
	/// </summary>
	public static List<Vec2Int> StringPull(
		List<Vec2Int> rawPath,
		Func<Vec2Int, Vec2Int, bool> hasLineOfSight) {

		if (rawPath == null || rawPath.Count <= 2)
			return rawPath == null
				? new List<Vec2Int>()
				: new List<Vec2Int>(rawPath);

		// Huge reduction in LOS calls on typical A* paths
		List<Vec2Int> path = ExtractCorners(rawPath);

		int count = path.Count;

		if (count <= 2)
			return path;

		List<Vec2Int> smoothedPath = new(count);

		Vec2Int current = path[0];
		smoothedPath.Add(current);

		int lastClearIndex = 1;

		for (int i = 2; i < count; i++) {
			if (hasLineOfSight(current, path[i])) {
				lastClearIndex = i;
			} else {
				current = path[lastClearIndex];
				smoothedPath.Add(current);

				lastClearIndex = i;
			}
		}

		Vec2Int destination = path[count - 1];

		if (smoothedPath[^1] != destination)
			smoothedPath.Add(destination);

		return smoothedPath;
	}

	/// <summary>
	/// Bresenham LOS with corner-cut prevention.
	/// </summary>
	public static bool HasLineOfSight(
		Vec2Int start,
		Vec2Int end,
		Func<Vec2Int, bool> isWalkable) {

		int x0 = start.X;
		int y0 = start.Y;
		int x1 = end.X;
		int y1 = end.Y;

		int dx = Math.Abs(x1 - x0);
		int dy = Math.Abs(y1 - y0);

		// Early-out for adjacent cells
		if (dx <= 1 && dy <= 1)
			return true;

		int sx = x0 < x1 ? 1 : -1;
		int sy = y0 < y1 ? 1 : -1;

		int err = dx - dy;

		while (x0 != x1 || y0 != y1) {
			if (!isWalkable(new Vec2Int(x0, y0)))
				return false;

			int e2 = err << 1;

			bool stepX = e2 > -dy;
			bool stepY = e2 < dx;

			// Diagonal crossing = corner-cut check
			if (stepX && stepY) {
				if (!isWalkable(new Vec2Int(x0 + sx, y0)) ||
					!isWalkable(new Vec2Int(x0, y0 + sy)))
					return false;
			}

			if (stepX) {
				err -= dy;
				x0 += sx;
			}

			if (stepY) {
				err += dx;
				y0 += sy;
			}
		}

		return isWalkable(end);
	}
}