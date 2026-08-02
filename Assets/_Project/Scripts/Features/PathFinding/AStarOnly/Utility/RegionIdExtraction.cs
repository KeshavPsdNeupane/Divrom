using System.Collections.Generic;
using Kope.Feature.PathFindingNew.Tile;

namespace Kope.Feature.PathFindingNew.Utility {
	public class RegionIdExtraction {
		private const int INITIAL_CAPACITY = 512;

		// Neighbor cardinal directions (North, East, South, West)
		private static readonly Vec2Int[] CardinalDirections = {
			new(0, 1),  // North
            new(1, 0),  // East
            new(0, -1), // South
            new(-1, 0)  // West
        };

		private readonly Queue<Vec2Int> _queue = new(INITIAL_CAPACITY);
		private readonly HashSet<Vec2Int> _closedSet = new(INITIAL_CAPACITY);



		public Dictionary<ushort, List<(Vec2Int position, TileTerrainData tile)>> ExtractRegion(
			Dictionary<Vec2Int, TileTerrainData> tileMap) {

			this._queue.Clear();
			this._closedSet.Clear();

			ushort NON_TRAVERSABLE_REGION_ID = TileTerrainData.NON_TRAVERSABLE_REGION_ID;
			//start from Non-traversable region ID "0", increment for each new traversable region,
			ushort regionID = NON_TRAVERSABLE_REGION_ID;

			Dictionary<ushort, List<(Vec2Int position, TileTerrainData tile)>> results = new(tileMap.Count / 10);
			List<(Vec2Int position, TileTerrainData tile)> nonTraversableRegion = new();

			foreach (KeyValuePair<Vec2Int, TileTerrainData> kvp in tileMap) {
				Vec2Int startPos = kvp.Key;

				// Skip if this tile was already processed in a previous BFS expansion
				if (this._closedSet.Contains(startPos)) continue;

				TileTerrainData startTile = kvp.Value;

				// Non-traversable tiles belong to Region 0
				if (!IsTraversable(startTile)) {
					this._closedSet.Add(startPos);
					nonTraversableRegion.Add((startPos, startTile));
					continue;
				}

				// Increment ID for a new traversable region landmass
				regionID++;
				List<(Vec2Int position, TileTerrainData tile)> currentRegionTiles = new();

				// Start BFS Flood-Fill
				this._queue.Enqueue(startPos);
				this._closedSet.Add(startPos);

				while (this._queue.Count > 0) {
					Vec2Int currentTilePos = this._queue.Dequeue();

					if (tileMap.TryGetValue(currentTilePos, out TileTerrainData currentTile)) {
						currentRegionTiles.Add((currentTilePos, currentTile));

						// Check 4-directional neighbors
						foreach (Vec2Int dir in CardinalDirections) {
							Vec2Int neighborPos = currentTilePos + dir;

							if (this._closedSet.Contains(neighborPos)) continue;

							if (tileMap.TryGetValue(neighborPos, out TileTerrainData neighborTile)) {
								if (IsTraversable(neighborTile)) {
									// Mark as visited immediately upon enqueueing to avoid duplicate queue entries
									this._closedSet.Add(neighborPos);
									this._queue.Enqueue(neighborPos);
								} else {
									// Non-traversable neighbors get assigned to region 0
									this._closedSet.Add(neighborPos);
									nonTraversableRegion.Add((neighborPos, neighborTile));
								}
							}
						}
					}
				}

				results.Add(regionID, currentRegionTiles);
			}

			// Register all non-traversable tiles under region ID 0
			if (nonTraversableRegion.Count > 0) {
				results.Add(NON_TRAVERSABLE_REGION_ID, nonTraversableRegion);
			}

			return results;
		}

		private bool IsTraversable(TileTerrainData tile) {
			return tile.TileType != TileType.VOID && tile.IsTraversable;
		}
	}
}