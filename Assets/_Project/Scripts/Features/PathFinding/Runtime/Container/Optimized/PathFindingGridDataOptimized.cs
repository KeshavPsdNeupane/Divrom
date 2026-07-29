using System;
using System.Collections.Generic;
using Kope.Core.Collections;
using Kope.EntityIdentity;
using Kope.Feature.PathFinding.Node;
using UnityEngine;

namespace Kope.Feature.PathFinding.Data {
	/*
     * ==============================================================================================
     * ARCHITECTURAL RATIONALE: HIERARCHICAL DATA BUCKETING & FLATTENING
     * ==============================================================================================
     * 
     * [The Problem: Unity YAML Text Serialization Bloat]
     * Unity stores ScriptableObject .asset files as plain ASCII text. When serializing thousands of 
     * standalone micro-nodes as individual dictionary entries or structs, Unity duplicates YAML field 
     * headers (e.g., "_isStaticObstacle:", "key:", "value:") 30,000+ times. This causes text asset sizes 
     * to explode into tens or hundreds of megabytes (e.g., 150MB+ for medium-sized grid graphs).
     * 
     * [The Solution: Bucketized Parallel Primitive Arrays]
     * To eliminate repeating YAML key headers:
     * 1. Micro-nodes are bucketed under their parent Macro Region (BoundingBox). Instead of 30,000 
     *    dictionary keys, Unity serializes only a few dozen Macro Region keys.
     * 2. All micro-nodes within a macro region have their fields flattened into synchronized, 
     *    index-aligned primitive lists (List<Vec2Int> and List<byte>).
     * 3. Booleans are stored as `byte` (0 or 1) to avoid expanded "True"/"False" text serialization.
     * 
     * [Tradeoff: Runtime Re-hydration]
     * This layout optimizes strictly for DISK & BUILD FOOTPRINT (~97-99% reduction). Because the data 
     * is stored in parallel lists, the container rebuilds O(1) runtime lookup Dictionaries lazily on 
     * first access via a single-pass O(N) re-hydration.
     * 
     * ==============================================================================================
     * DATA HIERARCHY GRAPH
     * ==============================================================================================
     * 
     * PathFindingGridDataOptimized (Root Container)
     * ├── _regionAnchorPoints: List<Vec2Int>
     * └── _microGridNodeSaveDataDict: SerializableDictionary<BoundingBox, MacroSaveDataOptimized>
     *     └── [Key: BoundingBox] ────────► [Value: MacroSaveDataOptimized]
     *                                      ├── Macro Node Metadata
     *                                      │   ├── _terrainType: TerrainType
     *                                      │   └── _allowedTraversal: MovementCapability
     *                                      │
     *                                      ├── Flattened Micro Nodes (Synchronized Index-Aligned Lists)
     *                                      │   ├── _macroRegionAnchorPoints[i]: Vec2Int (Position)
     *                                      │   └── _macroRegionStaticObstacleFlags[i]: byte (0/1 Flag)
     *                                      │
     *                                      └── Outgoing Graph Connections
     *                                          └── _neighboringRegionConnections: MacroConnectionSaveData
     *                                              ├── _targetRegions[j]: BoundingBox
     *                                              ├── _allowedTraversals[j]: MovementCapability
     *                                              └── _isNarrativelyAccessibleFlags[j]: byte
     * ==============================================================================================
     */

	[Serializable]
	public struct MacroConnectionSaveData {
		// Shortened private serialized field names reduce YAML ASCII key headers
		[SerializeField] private List<BoundingBox> _tgt;   // WAS: _targetRegions
		[SerializeField] private List<MovementCapability> _trav;  // WAS: _allowedTraversals
		[SerializeField] private List<byte> _narr;  // WAS: _isNarrativelyAccessibleFlags

		// Public API remains unchanged for zero breaking changes across your codebase
		public readonly List<BoundingBox> TargetRegion => this._tgt;
		public readonly List<MovementCapability> AllowedTraversal => this._trav;
		public readonly List<byte> IsNarrativelyAccessible => this._narr;

		public MacroConnectionSaveData(
			List<BoundingBox> targetRegion,
			List<MovementCapability> allowedTraversal,
			List<byte> isNarrativelyAccessible) {
			this._tgt = targetRegion ?? new();
			this._trav = allowedTraversal ?? new();
			this._narr = isNarrativelyAccessible ?? new();
		}
	}

	[Serializable]
	public struct MacroSaveDataOptimized {
		// Micro-node parallel arrays using minimal serialized key lengths
		[SerializeField] private List<Vec2Int> _pts;    // WAS: _macroRegionAnchorPoints
		[SerializeField] private List<byte> _flags;  // WAS: _macroRegionStaticObstacleFlags

		// Macro region metadata
		[SerializeField] private TerrainType _tt;      // WAS: _terrainType
		[SerializeField] private MovementCapability _trav;  // WAS: _allowedTraversal
		[SerializeField] private MacroConnectionSaveData _conns; // WAS: _neighboringRegionConnections

		public readonly List<Vec2Int> MacroRegionAnchorPoints => this._pts;
		public readonly List<byte> MacroRegionStaticObstacleFlags => this._flags;
		public readonly TerrainType TerrainType => this._tt;
		public readonly MovementCapability AllowedTraversal => this._trav;
		public readonly MacroConnectionSaveData NeighboringRegionBoundingBoxes => this._conns;

		public MacroSaveDataOptimized(
			List<Vec2Int> macroRegionAnchorPoints,
			List<byte> macroRegionStaticObstacleFlags,
			TerrainType terrainType,
			MovementCapability allowedTraversal,
			MacroConnectionSaveData neighboringRegionConnections) {

			this._pts = macroRegionAnchorPoints ?? new();
			this._flags = macroRegionStaticObstacleFlags ?? new();
			this._tt = terrainType;
			this._trav = allowedTraversal;
			this._conns = neighboringRegionConnections;
		}


		public static byte ConvertBoolToByte(bool value) => value ? (byte)1 : (byte)0;
	}

	[Serializable]
	public struct PathFindingGridDataOptimized {
		[SerializeField] private List<Vec2Int> _anchors; // WAS: _regionAnchorPoints
		[SerializeField] private SerializableDictionary<BoundingBox, MacroSaveDataOptimized> _dict; // WAS: _microGridNodeSaveDataDict

		public readonly List<Vec2Int> RegionAnchorPoints => this._anchors;
		public readonly SerializableDictionary<BoundingBox, MacroSaveDataOptimized> MicroGridNodeSaveDataDict
			=> this._dict;

		public PathFindingGridDataOptimized(
			List<Vec2Int> regionAnchorPoints,
			SerializableDictionary<BoundingBox, MacroSaveDataOptimized> microGridNodeSaveDataDict) {
			this._anchors = regionAnchorPoints ?? new();
			this._dict = microGridNodeSaveDataDict ?? new();
		}
	}
}