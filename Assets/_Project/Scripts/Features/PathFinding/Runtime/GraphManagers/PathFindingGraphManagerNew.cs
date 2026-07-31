// using System;
// using System.Collections.Generic;
// using System.Runtime.CompilerServices;
// using Kope.EntityIdentity;
// using Kope.Feature.PathFinding;
// using Kope.Feature.PathFinding.Node;
// using UnityEngine;

// namespace Project.Scripts.Features.PathFinding.GraphManager {

// 	#region Optimized Struct Definitions

// 	internal readonly struct MacroStructure {
// 		public readonly BoundingBox Box;
// 		public readonly MovementCapability Capability;
// 		public readonly TerrainType Terrain;
// 		public readonly bool IsBlocked;

// 		// Contiguous Array Ranges
// 		public readonly int ConnectionStartIndex;
// 		public readonly int ConnectionCount;
// 		public readonly int MicroStartIndex;
// 		public readonly int MicroCount;

// 		public MacroStructure(
// 			BoundingBox box, MovementCapability capability, TerrainType terrain, bool isBlocked,
// 			int connStart, int connCount, int microStart, int microCount) {
// 			Box = box;
// 			Capability = capability;
// 			Terrain = terrain;
// 			IsBlocked = isBlocked;
// 			ConnectionStartIndex = connStart;
// 			ConnectionCount = connCount;
// 			MicroStartIndex = microStart;
// 			MicroCount = microCount;
// 		}
// 	}

// 	public readonly struct MicroStructure {
// 		public readonly Vec2Int Position;
// 		public readonly bool IsWalkable;
// 		public readonly int ParentMacroIndex;

// 		public MicroStructure(Vec2Int position, bool isWalkable, int parentMacroIndex) {
// 			Position = position;
// 			IsWalkable = isWalkable;
// 			ParentMacroIndex = parentMacroIndex;
// 		}
// 	}

// 	public readonly struct MacroConnectionData1 {
// 		public readonly BoundingBox TargetBound;
// 		public readonly MovementCapability Capability;
// 		public readonly bool IsNarrativelyAccessible;

// 		public MacroConnectionData1(BoundingBox toBound, MovementCapability capability, bool isNarrativelyAccessible) {
// 			this.TargetBound = toBound;
// 			this.Capability = capability;
// 			this.IsNarrativelyAccessible = isNarrativelyAccessible;
// 		}

// 		[MethodImpl(MethodImplOptions.AggressiveInlining)]
// 		public bool IsTraversable(MovementCapability requiredCapability) {
// 			return IsNarrativelyAccessible && (Capability & requiredCapability) != 0;
// 		}
// 	}

// 	#endregion

// 	public class PathfindingGraphManagerHybrid {

// 		#region Contiguous Memory Arrays

// 		private readonly MacroStructure[] _macroStructures;
// 		private readonly MicroStructure[] _microStructures;
// 		private readonly MacroConnectionData1[] _macroConnectionData;

// 		// Fast O(1) Spatial Lookups to convert Box/Position -> Array Index
// 		private readonly Dictionary<BoundingBox, int> _macroBoxToIndex;
// 		private readonly Dictionary<Vec2Int, int> _microPositionToIndex;

// 		#endregion

// 		#region Reusable Zero-Allocation Buffers

// 		private static readonly Vec2Int[] CARDINAL_DIRECTIONS = new[] {
// 			Vec2Int.Up, Vec2Int.Down, Vec2Int.Left, Vec2Int.Right
// 		};

// 		private readonly MicroStructure[] _neighborBuffer = new MicroStructure[16];
// 		private readonly MacroConnectionData1[] _macroConnectionBuffer = new MacroConnectionData1[16];
// 		private readonly HashSet<Vec2Int> _corridorPositions = new HashSet<Vec2Int>();

// 		public int MacroNodeCount => _macroStructures.Length;
// 		public int MicroNodeCount => _microStructures.Length;
// 		public int Version { get; private set; } = 1;

// 		#endregion

// 		#region Baking Constructor

// 		/// <summary>
// 		/// Bakes dynamic node dictionaries into flattened, cache-friendly struct arrays.
// 		/// Call this once generation completes or when loading from a save file.
// 		/// </summary>
// 		public PathfindingGraphManagerHybrid(
// 			Dictionary<Vec2Int, MicroGridNode> microNodes,
// 			Dictionary<BoundingBox, MacroGridNode> macroNodes,
// 			Dictionary<BoundingBox, List<MacroConnectionData>> macroConnections) {

// 			int totalMacro = macroNodes.Count;
// 			int totalMicro = microNodes.Count;
// 			int totalConnections = 0;
// 			foreach (var kvp in macroConnections) {
// 				totalConnections += kvp.Value.Count;
// 			}

// 			_macroStructures = new MacroStructure[totalMacro];
// 			_microStructures = new MicroStructure[totalMicro];
// 			_macroConnectionData = new MacroConnectionData1[totalConnections];

// 			_macroBoxToIndex = new Dictionary<BoundingBox, int>(totalMacro);
// 			_microPositionToIndex = new Dictionary<Vec2Int, int>(totalMicro);

// 			int currentMacroIdx = 0;
// 			int currentMicroIdx = 0;
// 			int currentConnIdx = 0;

// 			foreach (var kvp in macroNodes) {
// 				var box = kvp.Key;
// 				var macroNode = kvp.Value;

// 				_macroBoxToIndex[box] = currentMacroIdx;

// 				// 1. Bake Connections
// 				int connStart = currentConnIdx;
// 				if (macroConnections.TryGetValue(box, out var connections)) {
// 					for (int i = 0; i < connections.Count; i++) {
// 						_macroConnectionData[currentConnIdx++] = new MacroConnectionData1(
// 							connections[i].ToBound,
// 							connections[i].AllowedTraversal,
// 							connections[i].IsNarrativelyAccessible
// 						);
// 					}
// 				}
// 				int connCount = currentConnIdx - connStart;

// 				// 2. Bake Micro Nodes Contiguously
// 				int microStart = currentMicroIdx;
// 				foreach (var microPos in macroNode.MicroGridNodePositions) {
// 					if (microNodes.TryGetValue(microPos, out var microNode)) {
// 						_microPositionToIndex[microPos] = currentMicroIdx;
// 						_microStructures[currentMicroIdx] = new MicroStructure(
// 							microPos,
// 							!microNode.IsStaticObstacle,
// 							currentMacroIdx
// 						);
// 						currentMicroIdx++;
// 					}
// 				}
// 				int microCount = currentMicroIdx - microStart;

// 				// 3. Bake Macro Structure
// 				_macroStructures[currentMacroIdx] = new MacroStructure(
// 					box,
// 					macroNode.AllowedTraversal,
// 					macroNode.TerrainType,
// 					macroNode.IsBlocked,
// 					connStart, connCount,
// 					microStart, microCount
// 				);

// 				currentMacroIdx++;
// 			}
// 		}

// 		#endregion

// 		#region Hybrid API Implementations

// 		/// <summary>
// 		/// OPTIMIZED: Aggregates corridor positions using direct linear array bounds.
// 		/// Replaces individual dictionary hashing per position with continuous Span iteration.
// 		/// </summary>
// 		public HashSet<Vec2Int> GetAllCorridorPositions(List<BoundingBox> macroNodes) {
// 			this._corridorPositions.Clear();

// 			for (int i = 0; i < macroNodes.Count; i++) {
// 				if (_macroBoxToIndex.TryGetValue(macroNodes[i], out int macroIdx)) {
// 					ref readonly var macro = ref _macroStructures[macroIdx];

// 					// Direct, zero-lookup linear scan over contiguous memory
// 					int end = macro.MicroStartIndex + macro.MicroCount;
// 					for (int j = macro.MicroStartIndex; j < end; j++) {
// 						this._corridorPositions.Add(_microStructures[j].Position);
// 					}
// 				}
// 			}
// 			return this._corridorPositions;
// 		}

// 		/// <summary>
// 		/// OPTIMIZED: Retrieves cardinal neighbors as a ReadOnlySpan of MicroStructure structs.
// 		/// </summary>
// 		[MethodImpl(MethodImplOptions.AggressiveInlining)]
// 		public ReadOnlySpan<MicroStructure> GetWalkableMicroNeighboringNodes(Vec2Int position) {
// 			int count = 0;
// 			for (int i = 0; i < CARDINAL_DIRECTIONS.Length; i++) {
// 				Vec2Int neighborPos = position + CARDINAL_DIRECTIONS[i];

// 				if (_microPositionToIndex.TryGetValue(neighborPos, out int microIdx)) {
// 					ref readonly var neighbor = ref _microStructures[microIdx];
// 					if (neighbor.IsWalkable) {
// 						this._neighborBuffer[count++] = neighbor;
// 					}
// 				}
// 			}
// 			return this._neighborBuffer.AsSpan(0, count);
// 		}

// 		/// <summary>
// 		/// OPTIMIZED: Returns neighboring macro connection data via ReadOnlySpan without allocating.
// 		/// </summary>
// 		public ReadOnlySpan<MacroConnectionData1> GetNeighboringMacroNodesConnectionData(BoundingBox box,
// 		MovementCapability capability) {
// 			if (!_macroBoxToIndex.TryGetValue(box, out int macroIdx)) {
// 				return ReadOnlySpan<MacroConnectionData1>.Empty;
// 			}

// 			ref readonly var macro = ref _macroStructures[macroIdx];
// 			int count = 0;

// 			if (this._macroConnectionBuffer.Length < macro.ConnectionCount) {
// 				Array.Resize(ref this._macroConnectionBuffer, macro.ConnectionCount);
// 			}

// 			int connEnd = macro.ConnectionStartIndex + macro.ConnectionCount;
// 			for (int i = macro.ConnectionStartIndex; i < connEnd; i++) {
// 				ref readonly var conn = ref _macroConnectionData[i];
// 				if (conn.IsTraversable(capability)) {
// 					this._macroConnectionBuffer[count++] = conn;
// 				}
// 			}

// 			return this._macroConnectionBuffer.AsSpan(0, count);
// 		}

// 		/// <summary>
// 		/// Attempts to get a micro node struct by position.
// 		/// </summary>
// 		[MethodImpl(MethodImplOptions.AggressiveInlining)]
// 		public bool TryGetMicroNode(Vec2Int position, out MicroStructure microNode) {
// 			if (_microPositionToIndex.TryGetValue(position, out int idx)) {
// 				microNode = _microStructures[idx];
// 				return true;
// 			}
// 			microNode = default;
// 			return false;
// 		}

// 		#endregion
// 	}
// }