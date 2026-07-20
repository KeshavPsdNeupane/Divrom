using UnityEngine;

using System.Collections.Generic;
using Kope.Core.Collections;
using Kope.Feature.PathFinding.Tile;
namespace Kope.Feature.PathFinding.Interface {

	public interface IRegionExtractor {
		Dictionary<Vector2Int, List<Vector2Int>> Extract(
			SerializableDictionary<Vector2Int, HHSIMacroPathFindingTile> _macroTileDictionary);
	}

	public interface IRectangleRegionSlicer {
		Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> Slice(
			Dictionary<Vector2Int, List<Vector2Int>> isolatedRegions, Vector2Int maxBoundSize);
	}
}