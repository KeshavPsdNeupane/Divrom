using System;
using System.Collections.Generic;

/// <summary>
/// Attribution: Rectangle Expansion A* (REA*) — Zhang, Li, Bi (2016).
///
/// Instead of expanding one grid cell at a time like standard A*, REA* expands
/// whole unblocked RECTANGLES. All interior points of a rectangle are pruned;
/// only the rectangle's open boundary edges ("walls") become search nodes —
/// think of each rectangle as a room, and its exits as doors to the next room.
///
/// This keeps the open list short (few, large search nodes instead of many
/// individual points) and produces paths that are frequently *better* than
/// standard grid-optimal, since consecutive path points are always connected
/// by an obstacle-free straight line inside a rectangle.
///
/// NOTE: This is a clarity-focused port of the algorithm's structure (Sections
/// 3.1–3.4 of the paper). It omits the paper's O(1) wall-distance shortcut
/// (Section 3.3, which avoids per-point multiplication) and semi-closed-area
/// detection (Section 6.3) — those are pure speed optimizations, not
/// behavioral differences, and can be added later without changing the shape
/// of this code.
/// </summary>
public class ReaStarPathfinder
{
	private const float Straight = 1f;
	private const float Diagonal = 1.41421356f;

	private enum Mode { Unvisited, GPoint, HPoint }
	private enum Dir { North, South, East, West }

	private struct PointData
	{
		public float GVal;
		public Mode Mode;
		public bool HasParent;
		public int ParentX, ParentY;
	}

	private struct Rect
	{
		public int MinX, MaxX, MinY, MaxY;
		public bool Contains(int x, int y) => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
	}

	// A search node is an INTERVAL (a run of open cells along one row or column),
	// not a single point — this is what lets REA* skip interior cells entirely.
	private class SearchNode
	{
		public Dir Direction;       // which way this node's rectangle should expand
		public bool RunsAlongX;     // true: interval varies in x, fixed y (a horizontal wall)
		public int FixedCoord;      // the fixed row (if RunsAlongX) or column (otherwise)
		public int Lo, Hi;          // inclusive range along the varying axis
		public float MinFVal;       // best-case f-value of any point in this interval
	}

	private readonly bool[,] _blocked; // true = obstacle / impassable
	private readonly int _width, _height;
	private PointData[,] _data;
	private (int x, int y) _goal;

	public ReaStarPathfinder(bool[,] blocked)
	{
		_blocked = blocked;
		_width = blocked.GetLength(0);
		_height = blocked.GetLength(1);
	}

	public List<(int x, int y)> FindPath((int x, int y) start, (int x, int y) goal)
	{
		if (IsBlocked(start.x, start.y) || IsBlocked(goal.x, goal.y))
			return null;

		_goal = goal;
		_data = new PointData[_width, _height];
		for (int x = 0; x < _width; x++)
			for (int y = 0; y < _height; y++)
				_data[x, y] = new PointData { Mode = Mode.Unvisited, GVal = float.PositiveInfinity };

		var open = new List<SearchNode>(); // simple list used as a priority queue (see PopBest)

		_data[start.x, start.y] = new PointData { GVal = 0, Mode = Mode.GPoint };

		if (InsertStart(start, open))
			return Reconstruct();

		while (open.Count > 0)
		{
			var cbn = PopBest(open);
			if (Expand(cbn, open))
				return Reconstruct();
		}

		return null; // no path exists
	}

	// ---------------------------------------------------------------
	// 3.1  Insert start point — grow the very first rectangle
	// ---------------------------------------------------------------
	private bool InsertStart((int x, int y) start, List<SearchNode> open)
	{
		Rect rect = ExpandRectangleFrom(start.x, start.y);

		if (rect.Contains(_goal.x, _goal.y))
		{
			SetPoint(_goal.x, _goal.y, Octile(start, _goal), start.x, start.y, Mode.HPoint);
			return true;
		}

		// Every open edge of the original rectangle becomes a candidate search node.
		TrySpawnWall(rect, Dir.North, start, open);
		TrySpawnWall(rect, Dir.South, start, open);
		TrySpawnWall(rect, Dir.East, start, open);
		TrySpawnWall(rect, Dir.West, start, open);
		return false;
	}

	// ---------------------------------------------------------------
	// 3.3  Expand an unblocked rectangle from the current best search node
	// ---------------------------------------------------------------
	private bool Expand(SearchNode cbn, List<SearchNode> open)
	{
		// The rectangle grows in cbn.Direction, staying within [Lo, Hi] on the
		// perpendicular axis (the interval's own width does not change here —
		// widening happens naturally when successor intervals are generated).
		Rect rect = ExpandRectangleFromInterval(cbn);

		if (rect.Contains(_goal.x, _goal.y))
		{
			AssignWallGVals(rect, cbn);
			ConnectGoalWithinRect(rect, cbn);
			return true;
		}

		AssignWallGVals(rect, cbn);

		// The three interior boundaries other than the entry wall become the
		// next round of search-node intervals (the paper's "doors to the next room").
		foreach (var dir in OtherWalls(cbn.Direction))
			TrySpawnWallFromRect(rect, dir, cbn, open);

		return false;
	}

	// ---------------------------------------------------------------
	// Rectangle growth helpers
	// ---------------------------------------------------------------

	// Original-rectangle rule (Sec. 3.1): expand vertically from the point first,
	// then sweep horizontally as far as the whole vertical span stays open.
	private Rect ExpandRectangleFrom(int x, int y)
	{
		int minY = y, maxY = y;
		while (!IsBlocked(x, minY - 1)) minY--;
		while (!IsBlocked(x, maxY + 1)) maxY++;

		int minX = x, maxX = x;
		while (ColumnOpen(minX - 1, minY, maxY)) minX--;
		while (ColumnOpen(maxX + 1, minY, maxY)) maxX++;

		return new Rect { MinX = minX, MaxX = maxX, MinY = minY, MaxY = maxY };
	}

	// Grow a rectangle outward from a search-node interval in its expansion direction.
	private Rect ExpandRectangleFromInterval(SearchNode node)
	{
		if (node.RunsAlongX)
		{
			int y = node.FixedCoord;
			int step = node.Direction == Dir.North ? -1 : 1;
			int edge = y;
			while (RowOpen(edge + step, node.Lo, node.Hi)) edge += step;

			int minY = Math.Min(y, edge), maxY = Math.Max(y, edge);
			return new Rect { MinX = node.Lo, MaxX = node.Hi, MinY = minY, MaxY = maxY };
		}
		else
		{
			int x = node.FixedCoord;
			int step = node.Direction == Dir.West ? -1 : 1;
			int edge = x;
			while (ColumnOpen(edge + step, node.Lo, node.Hi)) edge += step;

			int minX = Math.Min(x, edge), maxX = Math.Max(x, edge);
			return new Rect { MinX = minX, MaxX = maxX, MinY = node.Lo, MaxY = node.Hi };
		}
	}

	private bool ColumnOpen(int x, int minY, int maxY)
	{
		for (int y = minY; y <= maxY; y++)
			if (IsBlocked(x, y)) return false;
		return true;
	}

	private bool RowOpen(int y, int minX, int maxX)
	{
		for (int x = minX; x <= maxX; x++)
			if (IsBlocked(x, y)) return false;
		return true;
	}

	// ---------------------------------------------------------------
	// 3.2  Generate successor search nodes from a rectangle's open walls
	// ---------------------------------------------------------------
	private void TrySpawnWall(Rect rect, Dir dir, (int x, int y) origin, List<SearchNode> open)
		=> TrySpawnWallFromRect(rect, dir, null, origin, open);

	private void TrySpawnWallFromRect(Rect r, Dir dir, SearchNode parent, List<SearchNode> open)
		=> TrySpawnWallFromRect(r, dir, parent, null, open);

	// parent = the interval the entrant path is coming from (null only for the very first rectangle)
	// origin = the raw start point, used only when parent is null
	private void TrySpawnWallFromRect(Rect r, Dir dir, SearchNode parent, (int x, int y)? origin, List<SearchNode> open)
	{
		// Compute the wall line just outside the rectangle in direction `dir`,
		// split it into contiguous unblocked runs (free sub-intervals), and
		// push each run whose points actually improve as a new search node.
		bool runsAlongX = dir == Dir.North || dir == Dir.South;
		int fixedCoord = dir switch
		{
			Dir.North => r.MinY - 1,
			Dir.South => r.MaxY + 1,
			Dir.West => r.MinX - 1,
			Dir.East => r.MaxX + 1,
			_ => 0
		};

		if (fixedCoord < 0 || (runsAlongX && fixedCoord >= _height) || (!runsAlongX && fixedCoord >= _width))
			return; // off map

		int lo = runsAlongX ? r.MinX : r.MinY;
		int hi = runsAlongX ? r.MaxX : r.MaxY;

		int runStart = -1;
		for (int i = lo; i <= hi + 1; i++)
		{
			bool cellOpen = i <= hi && !(runsAlongX ? IsBlocked(i, fixedCoord) : IsBlocked(fixedCoord, i));
			if (cellOpen && runStart == -1) runStart = i;
			if (!cellOpen && runStart != -1)
			{
				EmitRun(runsAlongX, fixedCoord, runStart, i - 1, dir, parent, origin, open);
				runStart = -1;
			}
		}
	}

	private void EmitRun(bool runsAlongX, int fixedCoord, int lo, int hi, Dir dir,
						  SearchNode parent, (int x, int y)? origin, List<SearchNode> open)
	{
		float bestF = float.PositiveInfinity;
		bool anyImproved = false;

		for (int i = lo; i <= hi; i++)
		{
			int x = runsAlongX ? i : fixedCoord;
			int y = runsAlongX ? fixedCoord : i;

			var (candidateG, px, py) = BestParent(x, y, parent, origin);
			if (candidateG < _data[x, y].GVal)
			{
				anyImproved = true;
				SetPoint(x, y, candidateG, px, py, Mode.HPoint);
			}

			if (_data[x, y].Mode != Mode.Unvisited)
			{
				float f = _data[x, y].GVal + Octile((x, y), _goal);
				if (f < bestF) bestF = f;
			}
		}

		if (!anyImproved) return;

		open.Add(new SearchNode
		{
			Direction = dir,
			RunsAlongX = runsAlongX,
			FixedCoord = fixedCoord,
			Lo = lo,
			Hi = hi,
			MinFVal = bestF
		});
	}

	// Distance from the nearest point on the entry interval (or the raw start
	// point, for the very first rectangle) to (x, y). Returns (gVal, parentX, parentY).
	// Simplified stand-in for the paper's O(1) diagonal-sweep trick (Sec. 3.3):
	// scans the parent interval directly. Correct, just not asymptotically optimal.
	private (float g, int px, int py) BestParent(int x, int y, SearchNode parent, (int x, int y)? origin)
	{
		if (parent == null)
		{
			var o = origin!.Value;
			return (Octile((x, y), o), o.x, o.y);
		}

		float best = float.PositiveInfinity;
		(int x, int y) bestPt = default;
		for (int i = parent.Lo; i <= parent.Hi; i++)
		{
			int qx = parent.RunsAlongX ? i : parent.FixedCoord;
			int qy = parent.RunsAlongX ? parent.FixedCoord : i;
			if (_data[qx, qy].Mode == Mode.Unvisited) continue;
			float g = _data[qx, qy].GVal + Octile((qx, qy), (x, y));
			if (g < best) { best = g; bestPt = (qx, qy); }
		}
		return (best, bestPt.x, bestPt.y);
	}

	private void AssignWallGVals(Rect rect, SearchNode cbn)
	{
		// g-values for interior boundary points are assigned inline inside
		// EmitRun/BestParent as each wall's successor intervals are generated.
	}

	private void ConnectGoalWithinRect(Rect rect, SearchNode cbn)
	{
		float best = float.PositiveInfinity;
		(int x, int y) bestParent = default;
		for (int i = cbn.Lo; i <= cbn.Hi; i++)
		{
			int px = cbn.RunsAlongX ? i : cbn.FixedCoord;
			int py = cbn.RunsAlongX ? cbn.FixedCoord : i;
			if (_data[px, py].Mode == Mode.Unvisited) continue;
			float g = _data[px, py].GVal + Octile((px, py), _goal);
			if (g < best) { best = g; bestParent = (px, py); }
		}
		SetPoint(_goal.x, _goal.y, best, bestParent.x, bestParent.y, Mode.HPoint);
	}

	// Given the direction a rectangle expanded in, returns the 3 walls that can
	// spawn successor search nodes: the far wall (continuing the same way) plus
	// both perpendicular walls. The near wall (Opposite(direction)) is excluded
	// because that's the entry side — generating it would just walk back into
	// the rectangle we came from.
	private static Dir[] OtherWalls(Dir direction) => direction switch
	{
		Dir.North => new[] { Dir.North, Dir.East, Dir.West },
		Dir.South => new[] { Dir.South, Dir.East, Dir.West },
		Dir.East => new[] { Dir.East, Dir.North, Dir.South },
		Dir.West => new[] { Dir.West, Dir.North, Dir.South },
		_ => Array.Empty<Dir>()
	};

	// ---------------------------------------------------------------
	// Bookkeeping
	// ---------------------------------------------------------------
	private void SetPoint(int x, int y, float gVal, int parentX, int parentY, Mode mode)
	{
		_data[x, y] = new PointData
		{
			GVal = gVal,
			Mode = mode,
			HasParent = true,
			ParentX = parentX,
			ParentY = parentY
		};
	}

	private bool IsBlocked(int x, int y)
	{
		if (x < 0 || y < 0 || x >= _width || y >= _height) return true;
		return _blocked[x, y];
	}

	private static float Octile((int x, int y) a, (int x, int y) b)
	{
		int dx = Math.Abs(a.x - b.x), dy = Math.Abs(a.y - b.y);
		int dMin = Math.Min(dx, dy), dMax = Math.Max(dx, dy);
		return dMin * Diagonal + (dMax - dMin) * Straight;
	}

	// Open list stand-in — swap for a binary heap keyed on MinFVal in production.
	private static SearchNode PopBest(List<SearchNode> open)
	{
		int bestIdx = 0;
		for (int i = 1; i < open.Count; i++)
			if (open[i].MinFVal < open[bestIdx].MinFVal) bestIdx = i;
		var node = open[bestIdx];
		open.RemoveAt(bestIdx);
		return node;
	}

	private List<(int x, int y)> Reconstruct()
	{
		var path = new List<(int x, int y)>();
		int x = _goal.x, y = _goal.y;
		path.Add((x, y));
		while (_data[x, y].HasParent)
		{
			var d = _data[x, y];
			x = d.ParentX;
			y = d.ParentY;
			path.Add((x, y));
		}
		path.Reverse();
		return path;
	}
}