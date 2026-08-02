using System;
using System.Collections.Generic;
using Kope.EntityIdentity;
using Kope.Feature.PathFindingNew.Base;
using Kope.Feature.PathFindingNew.Graph;
using Kope.Feature.PathFindingNew.Interface;
using Kope.Feature.PathFindingNew.Utility;
using ThirdParty.PriorityQueeu;
using UnityEngine;

namespace Kope.Feature.PathFindingNew.PathFinding {

	/// <summary>
	/// Bidirectional Weighted A*: runs two simultaneous searches — forward from <c>start</c> toward
	/// <c>end</c>, and backward from <c>end</c> toward <c>start</c> — always expanding whichever
	/// frontier is currently "further behind" by the cardinality criterion (see <em>Direction
	/// selection</em> below), until they meet. Typically expands far fewer nodes than unidirectional
	/// <see cref="AStar"/> for the same query, since each side only has to cover roughly half the
	/// search radius to reach the midpoint.
	/// <para>
	/// <strong>Directional cost asymmetry (read this before touching pricing logic):</strong>
	/// <see cref="GridNode.TryGetTraversalCost"/> prices a step using the <em>destination</em> tile's
	/// cost multiplier. That's directional: a forward edge <c>A→B</c> is priced by B's multiplier, but
	/// there's no guarantee <c>B→A</c> costs the same, since that would be priced by A's multiplier
	/// instead. The backward frontier here doesn't traverse a mirrored/symmetric graph — it explores
	/// the exact same directed graph in reverse. When the backward search is standing at node
	/// <c>X</c> and considers neighbor <c>Y</c>, that step represents the forward edge <c>Y→X</c> (an
	/// agent walking forward from Y arrives at X), so it must be priced by <c>X</c>'s multiplier — the
	/// node the backward search is leaving, not the one it's discovering. Get this backwards and the
	/// two frontiers' G-costs stop corresponding to any real path cost, and stitching a "cheapest"
	/// meeting point together can produce a route that's wrong, or doesn't correspond to an actual
	/// forward-traversable path at all. See <see cref="ExpandNode"/>.
	/// </para>
	/// <para>
	/// <strong>Direction selection — and why "meet in the middle" costs more than it's worth here:</strong>
	/// A first attempt at this class ordered each frontier's open set by
	/// <c>pr(n) = max(FCost(n), 2 * GCost(n))</c> — the priority function from Holte, Felner, Sharon
	/// &amp; Sturtevant's <c>MM</c> algorithm ("Bidirectional Search That Is Guaranteed to Meet in the
	/// Middle", AAAI 2016 / <em>Artificial Intelligence</em> 252, 2017), generalized to weighted search
	/// as <c>WMM</c> (Atzmon et al., ICAPS 2023). It provably bounds how lopsided the two frontiers can
	/// get — but that bound is on <em>GCost</em>, not on directedness. Once a frontier's GCost exceeds
	/// roughly its own HCost (which for a typical path is around the halfway point, in cost), the
	/// <c>2 * GCost</c> term dominates <c>pr</c> and the heuristic drops out of the ordering entirely —
	/// the search degenerates to plain Dijkstra (a uniform "blob" instead of a directed cone) for the
	/// remainder of that frontier's work. This isn't a corner case: Holte et al. themselves report MM
	/// expanding up to 4x more nodes than its own zero-heuristic brute-force equivalent (MM0) in some
	/// experiments, precisely because the heuristic's guidance gets neutralized just where it would
	/// otherwise help most. Combined with this graph's directional cost asymmetry above — where one
	/// side's terrain being cheaper means it legitimately needs to cover far more tiles to reach the
	/// same <em>cost</em> radius — the practical result was one frontier visibly stalling while the
	/// other bloomed outward with no directional guidance. For a real-time grid search with a strong
	/// heuristic (Octile), that trade is backwards: directedness matters far more day-to-day than a
	/// worst-case guarantee on GCost balance.
	/// <br/><br/>
	/// So each frontier's <c>Open</c> stays ordered by plain <c>FCost</c> — fully heuristic-directed,
	/// exactly like unidirectional <see cref="AStar"/>, no blob — and direction is instead chosen with
	/// the <strong>cardinality criterion</strong> (Pohl 1971; used by Kwa's <c>BS*</c>, 1989): expand
	/// whichever frontier currently holds <em>fewer open nodes</em>. Unlike comparing peeked FCosts
	/// directly (which a persistently "cheaper" direction can win indefinitely, see the note in
	/// <see cref="FindPath"/>), open-set size is a proxy for total exploration progress that isn't
	/// skewed by an absolute cost-scale difference between the two directions, so it balances the two
	/// searches' node counts without ever touching either frontier's internal, heuristic-guided
	/// ordering. It has no formal "never expands past the midpoint" proof the way MM does, but it's a
	/// well-established, practical technique that keeps both frontiers fully directed the entire time.
	/// </para>
	/// <para>
	/// <strong>Termination:</strong> the first position touched by both frontiers is not guaranteed to
	/// be the optimal meeting point (a cheaper join could still be sitting deeper in either open set).
	/// This keeps a running best combined cost (<c>bestMeetCost</c>) across every touch seen, and only
	/// stops once neither frontier's cheapest remaining candidate could possibly beat it — the standard
	/// sufficient condition <c>peekForward.FCost + peekBackward.FCost >= bestMeetCost</c>. As with
	/// unidirectional <see cref="AStar"/>, a non-default <c>greediness</c> can make the heuristic
	/// inadmissible, in which case (exactly as for AStar) this stopping rule is no longer a strict
	/// optimality proof — just consistent with the same trade-off AStar already makes.
	/// </para>
	/// </summary>
	public class BidirectionalAStar : PathFinderBase {

		// H-cost estimation only — never used to price an actual step. See ExpandNode.
		private static readonly Dictionary<CostCalculationType, Func<Vec2Int, Vec2Int, float, int>>
		HEURISTIC_FUNCTIONS = new() {
			{ CostCalculationType.Manhattan, GridNode.ManhattanDistanceTo },
			{ CostCalculationType.Euclidean, GridNode.EuclideanDistanceTo },
			{ CostCalculationType.Octile, GridNode.OctileDistanceTo }
		};

		/// <summary>
		/// Self-contained state for one direction of the bidirectional search: its own open set,
		/// closed set, and a position-keyed node-record store (same non-index pattern as AStar's
		/// <c>_nodeRecords</c> — see AStar.cs for the rationale). Two instances of this — one per
		/// direction — replace what a single AStar instance owns.
		/// </summary>
		private sealed class SearchFrontier {
			public readonly Dictionary<Vec2Int, PathFindingNode> Records;
			public readonly QuadPriorityQueue<PathFindingNode, int> Open;
			public readonly HashSet<Vec2Int> Closed;

			public SearchFrontier(int capacity) {
				this.Records = new Dictionary<Vec2Int, PathFindingNode>(capacity);
				this.Open = new QuadPriorityQueue<PathFindingNode, int>(capacity);
				this.Closed = new HashSet<Vec2Int>(capacity);
			}

			public void Clear() {
				this.Records.Clear();
				this.Open.Clear();
				this.Closed.Clear();
			}
		}

		private readonly int _maxIterations;

		private readonly SearchFrontier _forward;
		private readonly SearchFrontier _backward;

		// Shared scratch buffers: safe to share because expansion is strictly sequential — only one
		// frontier is ever mid-expansion at a time, alternation happens between whole expansions, not
		// interleaved within one.
		private readonly GridNode[] _fetchBuffer = new GridNode[8];
		private readonly GridNode[] _neighbourBuffer = new GridNode[8];
		private GridNode _startNode, _endNode;

#if UNITY_EDITOR
		private readonly List<Vec2Int> _recorderOpenListCache;
		private readonly HashSet<Vec2Int> _recorderClosedSetCache;
#endif

		/// <summary>
		/// Initializes a new instance of the <see cref="BidirectionalAStar"/> pathfinder.
		/// </summary>
		/// <param name="graphManager">The graph topology provider.</param>
		/// <param name="config">Configuration settings controlling search limits, heuristic types, and greediness.</param>
		/// <returns>A new instance of the <see cref="BidirectionalAStar"/> pathfinder.</returns>
		public BidirectionalAStar(GraphManager graphManager, PathFindingConfig config)
		: base(graphManager, config) {

			int capacity = config.InitialCapacity;
			this._forward = new SearchFrontier(capacity);
			this._backward = new SearchFrontier(capacity);

			int totalNodes = this._graphManager.TotalNodeCount;
			// Reuses AStar's node-count-derived sizing formula as a safety cap on *combined* work
			// across both frontiers. Since each side of a bidirectional search typically only has to
			// cover roughly half the search radius to meet in the middle, actual combined expansions
			// for a solvable query are usually well under this budget — it's a backstop against
			// pathological/disconnected cases, not the expected steady-state cost.
			this._maxIterations = Mathf.Max(
				PathFindingConfig.MIN_NODE_SEARCH,
				Mathf.CeilToInt(PathFindingConfig.MIN_NODE_SEARCH_RATIO * totalNodes),
				Mathf.CeilToInt(PathFindingConfig.MAX_NODE_SEARCH_RATIO * totalNodes)
			);

#if UNITY_EDITOR
			this._recorderOpenListCache = new List<Vec2Int>(capacity * 2);
			this._recorderClosedSetCache = new HashSet<Vec2Int>(capacity * 2);
#endif
		}


		/// <summary>
		/// Performs a pre-check to determine if the pathfinding operation is feasible.
		/// </summary>
		/// <param name="start">Starting grid coordinate.</param>
		/// <param name="end">Destination target grid coordinate.</param>
		/// <param name="movementCapability">Bitmask of movement modes supported by the querying agent.</param>
		/// <returns>A <see cref="PathFindingResult"/> containing status metadata and execution metrics.</returns>
		public PathFindingResult PreCheckFeasibility(Vec2Int start, Vec2Int end, MovementCapability movementCapability) {
			if (this._startNode.Position == this._endNode.Position) {
				return CreateErrorResult(PathFindingStatus.StartEqualsEnd);
			}
			// 2. Reachability check
			if (this._startNode.RegionId != this._endNode.RegionId) {
				return CreateErrorResult(PathFindingStatus.UnReachableTarget);
			}

			// 3. Node Existence & Traversability
			if (!this._graphManager.TryGetNode(start, out this._startNode) ||
				!this._graphManager.TryGetNode(end, out this._endNode) ||
				!this._startNode.IsTraversable(movementCapability) ||
				!this._endNode.IsTraversable(movementCapability)) {
				return CreateErrorResult(PathFindingStatus.InvalidStartOrEnd);
			}
			return CreateErrorResult(PathFindingStatus.Success);
		}



		/// <summary>
		/// Finds the shortest walkable path between two points using bidirectional Weighted A*.
		/// </summary>
		/// <param name="start">Starting grid coordinate.</param>
		/// <param name="end">Destination target grid coordinate.</param>
		/// <param name="movementCapability">Movement modes supported by the querying agent.</param>
		/// <param name="recorder">Optional editor recorder for step-by-step pathfinding visualization.</param>
		/// <returns>A <see cref="PathFindingResult"/> containing status metadata and execution metrics.</returns>
		public override PathFindingResult FindPath(
			Vec2Int start,
			Vec2Int end,
			MovementCapability movementCapability,
			PathfindingRecorder recorder = null) {
			this._startNode = default;
			this._endNode = default;

			this._forward.Clear();
			this._backward.Clear();

#if UNITY_EDITOR
			recorder?.Clear();
#endif

			if (start == end) {
				return new PathFindingResult(
					PathFindingStatus.Success,
					new List<Vec2Int> { start },
					this._graphManager.TotalNodeCount,
					0,
					0,
					this._config.CostCalculationType,
					this._config.GreedyNess
				);
			}

			CostCalculationType costType = this._config.CostCalculationType;
			float greediness = this._config.GreedyNess;
			Func<Vec2Int, Vec2Int, float, int> heuristicFunc = HEURISTIC_FUNCTIONS[costType];
			bool useGreediness = greediness != PathFindingConfig.DEFAULT_GREEDINESS;

			SeedFrontier(this._forward, start, end, heuristicFunc, useGreediness, greediness);
			SeedFrontier(this._backward, end, start, heuristicFunc, useGreediness, greediness);

			int totalNodeSearched = 0;
			int totalEvaluations = 0;

			int bestMeetCost = int.MaxValue;
			Vec2Int bestMeetPosition = default;
			bool hasMeetPoint = false;

			while (this._forward.Open.Count > 0 && this._backward.Open.Count > 0 && totalNodeSearched < this._maxIterations) {

				// Standard bidirectional stopping rule, checked against the current cheapest node of
				// BOTH frontiers before consuming either (both are guaranteed non-empty here by the
				// while-condition). Once neither side's best remaining candidate can possibly complete
				// a path cheaper than the best meeting point already found, no further expansion can
				// improve on it.
				if (hasMeetPoint && this._forward.Open.Peek().FCost + this._backward.Open.Peek().FCost >= bestMeetCost) {
					break;
				}

				// Cardinality criterion (Pohl 1971 / Kwa's BS*, 1989): expand whichever frontier
				// currently holds fewer open nodes, i.e. whichever is "further behind" in overall
				// progress. Deliberately does NOT compare peeked FCosts directly — a direction whose
				// terrain is simply cheaper (see the directional-cost-asymmetry remarks above) would
				// keep winning that comparison indefinitely and end up doing nearly all the work. Open
				// count isn't skewed by absolute cost scale, so it balances the two frontiers' progress
				// without ever touching either one's internal FCost ordering — each frontier stays
				// fully heuristic-directed the entire search. See the class remarks on direction
				// selection for why this replaced an earlier MM/WMM-based attempt.
				bool expandForward = this._forward.Open.Count <= this._backward.Open.Count;

				SearchFrontier active = expandForward ? this._forward : this._backward;
				SearchFrontier other = expandForward ? this._backward : this._forward;
				Vec2Int goal = expandForward ? end : start;

				PathFindingNode currentRecord = active.Open.Dequeue();
				totalEvaluations++;
				totalNodeSearched++;

#if UNITY_EDITOR
				//	Debug.Log($"[BidirectionalAStar] Expanding {(expandForward ? "forward" : "backward")} node {currentRecord.Position} (G={currentRecord.GCost}, H={currentRecord.HCost}, F={currentRecord.FCost})");
				if (recorder != null) {
					this._recorderOpenListCache.Clear();
					foreach (PathFindingNode node in this._forward.Open.GetElements()) this._recorderOpenListCache.Add(node.Position);
					foreach (PathFindingNode node in this._backward.Open.GetElements()) this._recorderOpenListCache.Add(node.Position);

					this._recorderClosedSetCache.Clear();
					this._recorderClosedSetCache.UnionWith(this._forward.Closed);
					this._recorderClosedSetCache.UnionWith(this._backward.Closed);

					recorder.RecordStep(currentRecord.Position, this._recorderOpenListCache, this._recorderClosedSetCache);
				}
#endif

				// A position touched by both frontiers is a candidate meeting point. `other` may not
				// have closed this position yet, so its G-cost here could still improve later — if it
				// does, `other` will re-check this exact position itself when IT eventually closes it
				// (every closed position is checked from both sides at most once each), naturally
				// tightening bestMeetCost toward the true optimum by the time the stopping rule fires.
				if (other.Records.TryGetValue(currentRecord.Position, out PathFindingNode otherRecord)) {
					int candidateCost = currentRecord.GCost + otherRecord.GCost;
					if (candidateCost < bestMeetCost) {
						bestMeetCost = candidateCost;
						bestMeetPosition = currentRecord.Position;
						hasMeetPoint = true;
					}
				}

				ExpandNode(active, currentRecord, goal, expandForward, movementCapability, heuristicFunc, useGreediness, greediness, ref totalEvaluations);
			}

			if (!hasMeetPoint) {
				return CreateErrorResult(PathFindingStatus.NoPathFound);
			}

			return new PathFindingResult(
				PathFindingStatus.Success,
				StitchPath(bestMeetPosition),
				this._graphManager.TotalNodeCount,
				totalEvaluations,
				totalNodeSearched,
				costType,
				greediness
			);
		}

		private static void SeedFrontier(
			SearchFrontier frontier,
			Vec2Int root,
			Vec2Int goal,
			Func<Vec2Int, Vec2Int, float, int> heuristicFunc,
			bool useGreediness,
			float greediness) {

			int rawH = heuristicFunc(root, goal, 1.0f);
			int weightedH = useGreediness ? Mathf.FloorToInt(rawH * greediness) : rawH;

			PathFindingNode rootNode = new(root, 0, weightedH, PathFindingNode.NoParent);
			frontier.Records[root] = rootNode;
			frontier.Open.EnqueueOrUpdate(rootNode, rootNode.FCost);
		}

		/// <summary>
		/// Expands one node of one frontier: generates its walkable neighbors, prices each edge, and
		/// relaxes/enqueues improved records — the same job AStar.FindPath's inner neighbor loop does,
		/// parameterized by direction.
		/// <para>
		/// <c>isForwardDirection</c> controls ONLY which node's cost multiplier prices the edge — see
		/// the class-level remarks on directional cost asymmetry. It does NOT change which node's
		/// traversability gates validity: that's always the newly-discovered neighbor, in both
		/// directions, because (by induction) every node in either frontier except that frontier's own
		/// root must be traversable to have legally entered the corresponding real path in the first
		/// place — exactly mirroring how AStar.FindPath never re-validates its own start's
		/// traversability but does validate every neighbor it discovers.
		/// </para>
		/// </summary>
		private void ExpandNode(
			SearchFrontier frontier,
			PathFindingNode currentRecord,
			Vec2Int searchGoal,
			bool isForwardDirection,
			MovementCapability movementCapability,
			Func<Vec2Int, Vec2Int, float, int> heuristicFunc,
			bool useGreediness,
			float greediness,
			ref int totalEvaluations) {

			frontier.Closed.Add(currentRecord.Position);

			ReadOnlySpan<GridNode> neighbors = this._graphManager.TryGetNeighbors(
				currentRecord.Position,
				this._fetchBuffer,
				this._neighbourBuffer,
				out byte diagonalMask
			);

			// Backward-only: fetch the CURRENT node's own multiplier once per expansion (not per
			// edge) — this is what prices every outgoing edge in this direction. Falls back to 1f if
			// the current node itself isn't traversable/compatible, which only matters for the very
			// first expansion of the backward frontier (its root == original `end`) and mirrors
			// AStar's permissive treatment of its own start's traversability.
			float currentNodeMultiplier = 1f;
			if (!isForwardDirection) {
				if (this._graphManager.TryGetNode(currentRecord.Position, out GridNode currentGridNode)) {
					if (!currentGridNode.TryGetTraversalCost(movementCapability, out float fetchedMultiplier)) {
						fetchedMultiplier = 1f;
					}
					currentNodeMultiplier = fetchedMultiplier;
				}
			}

			for (int i = 0; i < neighbors.Length; i++) {
				ref readonly GridNode neighborNode = ref neighbors[i];
				Vec2Int neighborPos = neighborNode.Position;

				if (frontier.Closed.Contains(neighborPos)) {
					continue;
				}

				// Validity gate: always the newly-discovered neighbor's traversability, regardless of
				// direction. See the method doc for why this doesn't flip with isForwardDirection even
				// though the cost source does.
				if (!neighborNode.TryGetTraversalCost(movementCapability, out float neighborMultiplier)) {
					continue;
				}

				bool isDiagonal = (diagonalMask & (1 << i)) != 0;
				int baseStepCost = isDiagonal ? GridNode.DIAGONAL_COST : GridNode.DIRECT_COST;
				float pricingMultiplier = isForwardDirection ? neighborMultiplier : currentNodeMultiplier;
				int stepCost = Mathf.RoundToInt(baseStepCost * pricingMultiplier);

				int tentativeGCost = currentRecord.GCost + stepCost;

				bool hasExisting = frontier.Records.TryGetValue(neighborPos, out PathFindingNode existingRecord);

				if (!hasExisting || tentativeGCost < existingRecord.GCost) {
					int rawHCost = heuristicFunc(neighborPos, searchGoal, 1.0f);
					int weightedHCost = useGreediness ? Mathf.FloorToInt(rawHCost * greediness) : rawHCost;

					PathFindingNode neighborRecord = new(
						neighborPos.X,
						neighborPos.Y,
						tentativeGCost,
						weightedHCost,
						currentRecord.Position
					);

					frontier.Records[neighborPos] = neighborRecord;
					frontier.Open.EnqueueOrUpdate(neighborRecord, neighborRecord.FCost);
				}

				totalEvaluations++;
			}
		}

		/// <summary>
		/// Joins the forward chain (start → meetPosition) with the backward chain
		/// (meetPosition → end) into one continuous path. The forward half is walked root-first via
		/// ParentPosition then reversed; the backward half is already oriented meet→end (each
		/// ParentPosition step in the backward frontier moves one step closer to its own root, which
		/// is `end`), so it appends directly without needing a second reversal.
		/// </summary>
		private List<Vec2Int> StitchPath(Vec2Int meetPosition) {
			List<Vec2Int> path = new(32);

			Vec2Int currentPos = meetPosition;
			while (currentPos != PathFindingNode.NoParent) {
				PathFindingNode node = this._forward.Records[currentPos];
				path.Add(node.Position);
				currentPos = node.ParentPosition;
			}
			path.Reverse(); // now start -> ... -> meetPosition

			currentPos = this._backward.Records[meetPosition].ParentPosition; // skip meetPosition itself — already the last element above

			while (currentPos != PathFindingNode.NoParent) {
				PathFindingNode node = this._backward.Records[currentPos];
				path.Add(node.Position);
				currentPos = node.ParentPosition;
			}

			return path;
		}
	}
}