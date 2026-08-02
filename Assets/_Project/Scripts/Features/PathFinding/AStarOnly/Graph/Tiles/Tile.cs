using Kope.Feature.PathFindingNew.Interface;
using Kope.Feature.PathFindingNew.TBase;
using UnityEngine;

namespace Kope.Feature.PathFindingNew.Tile {
	[CreateAssetMenu(fileName = "AStarTile", menuName = "Scriptable Objects/PathFindingNew/AStarTile", order = 1)]
	public class AStarTile : GridTileBase<TileTerrainData>, ITerrainDataTile {
		public TileTerrainData GetTerrainData() {
			return this.data;
		}
	}
}