using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using Kope.Feature.PathFinding.Node;

namespace Project.Scripts.Features.PathFindingOld.GraphManager {
	/// <summary>
	/// Worker handling tile-level (micro) pathfinding logic. Operates on data passed by PathFindingGridManager.
	/// </summary>
	public class MicroGraphWorker {
		// for the sake of performance the below two arrays must be in sync with each other, 
		// veryuse ful to aboud hashSet lookups and allocations
		public static readonly Vec2Int[] NEIGHBOR_OFFSET = new[] {
			// making them clockwise to match the neighbor rules
			new Vec2Int(-1, 1) ,  // Up-Left
			new Vec2Int(0, 1),   // Up
			new Vec2Int(1, 1),   // Up-Right
            new Vec2Int(1, 0),   // Right
            new Vec2Int(1, -1),  // Down-Right
            new Vec2Int(0, -1),  // Down
            new Vec2Int(-1, -1), // Down-Left
            new Vec2Int(-1, 0),  // Left
        };
		public static readonly bool[] CHECK_FOR_BLOCK_RULE = {
			true, // for Up-Left
			false, // for Up
			true, // for Up-Right
			false, // for Right
			true, // for Down-Right
			false, // for Down
			true, // for Down-Left
			false // for Left
		};
		public static readonly (int req1, int req2)[] NEIGHBOR_RULES = {
			// these are the array idx of the two required neighbors that must be 
			// walkable for diagonal movement to be allowed
			(0, 7), // for Up-Left
			(1, 3), // for Up-Right
			(3, 5), // for Down-Right
			(5, 7), // for Down-Left
		};


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetNode(
			Dictionary<Vec2Int, MicroGridNode> microNodes,
			Vec2Int position,
			[MaybeNullWhen(false)] out MicroGridNode microNode) {

			return microNodes.TryGetValue(position, out microNode);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ReadOnlySpan<MicroGridNode> GetWalkableNeighboringNodesWithRules(
			Dictionary<Vec2Int, MicroGridNode> microNodes,
			MicroGridNode[] neighborBuffer,
			MicroGridNode[] fetchedNeighbors,
			Vec2Int position) {

			byte walkableMask = 0;

			for (int i = 0; i < 8; i++) {
				Vec2Int nPos = position + NEIGHBOR_OFFSET[i];

				if (microNodes.TryGetValue(nPos, out MicroGridNode nNode) && !nNode.IsStaticObstacle) {
					fetchedNeighbors[i] = nNode;
					walkableMask |= (byte)(1 << i);
				}
			}

			int count = 0;

			for (int i = 0; i < 8; i++) {
				if ((walkableMask & (1 << i)) == 0) continue;

				if (CHECK_FOR_BLOCK_RULE[i]) {
					var (req1, req2) = NEIGHBOR_RULES[i >> 1];

					bool req1Walkable = (walkableMask & (1 << req1)) != 0;
					bool req2Walkable = (walkableMask & (1 << req2)) != 0;

					if (!req1Walkable || !req2Walkable) continue;
				}

				neighborBuffer[count++] = fetchedNeighbors[i];
			}

			return neighborBuffer.AsSpan(0, count);
		}

#if UNITY_EDITOR
		public IEnumerable<(Vec2Int startPos, Vec2Int endPos)> GiveRandomTestPoints(
			Dictionary<Vec2Int, MicroGridNode> microNodes,
			int randomPathCount,
			int seed = 0) {

			if (microNodes == null || microNodes.Count < 2) yield break;

			var keys = microNodes.Keys.ToArray();
			int count = keys.Length;
			UnityEngine.Random.InitState(seed);

			for (int i = 0; i < randomPathCount; i++) {
				int startIdx = UnityEngine.Random.Range(0, count);
				int endIdx;
				do {
					endIdx = UnityEngine.Random.Range(0, count);
				} while (endIdx == startIdx && count > 1);

				yield return (keys[startIdx], keys[endIdx]);
			}
		}
#endif
	}
}