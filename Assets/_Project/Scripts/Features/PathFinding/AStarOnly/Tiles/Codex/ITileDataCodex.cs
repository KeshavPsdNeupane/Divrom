using System.Collections.Generic;
using Kope.EntityIdentity;
using Kope.Feature.PathFindingNew.Tile;
using Kope.Feature.PathFindingNew.Utility;

namespace Kope.Feature.PathFindingNew.Storage {

	/// <summary>
	/// Input wrapper payload for tile grid baking operations.
	/// </summary>
	public readonly struct TileDataBakeInput {
		public readonly IDictionary<Vec2Int, TileTerrainData> TileDict;

		public TileDataBakeInput(IDictionary<Vec2Int, TileTerrainData> tileDict) {
			this.TileDict = tileDict;
		}
	}

	/// <summary>
	/// Generic contract for tile grid baking and hydration engines.
	/// </summary>
	/// <typeparam name="TStorage">The serialized primitive storage container type.</typeparam>
	public interface ITileDataCodex<TStorage> {
		TStorage Bake(IDictionary<Vec2Int, TileTerrainData> tileDict);
		TStorage Bake(in TileDataBakeInput input);
		Dictionary<Vec2Int, TileTerrainData> Hydrate(in TStorage storageData);
	}
}