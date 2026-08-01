using UnityEngine;
using UnityEngine.Tilemaps;
using Kope.Feature.PathFinding.Utility;

namespace Kope.Feature.PathFinding.Tile {
	using Kope.Feature.PathFinding.Interface;

	/// <summary>
	/// The universal base class for all pathfinding tiles in the framework. 
	/// Extends Unity's <see cref="TileBase"/> to encapsulate generic terrain 
	/// data of type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">The underlying terrain data structure, governed by the primary data contract (<see cref="ITerrainData{T}"/>).</typeparam>
	public abstract class PathFindingTile<T> : TileBase where T : struct, ITerrainData<T> {
		[SerializeField] protected T data;
		/// <summary>
		/// Gets the principal terrain data associated with this tile instance.
		/// </summary>
		public T Data => data;
	}

	/// <summary>
	/// Specialized base class for HHSI implementation tiles. Extends <see cref="PathFindingTile{T}"/> 
	/// to provide automated, procedural color caching and runtime sprite generation via <see cref="TileSpriteCache"/>.
	/// </summary>
	/// <typeparam name="T">The underlying terrain data structure,
	///  governed by the primary data contract (<see cref="ITerrainData{T}"/>).</typeparam>
	public abstract class HSIPathFindingTileBase<T> : PathFindingTile<T> where T : struct, ITerrainData<T> {
		public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData) {
			tileData.sprite = TileSpriteCache.GetOrCreate(this.data.TileColor);
			tileData.color = Color.white;
			tileData.flags = TileFlags.None;
		}

		public override void RefreshTile(Vector3Int position, ITilemap tilemap) {
			tilemap.RefreshTile(position);
		}

		public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap, ref TileAnimationData tileAnimationData) {
			return base.GetTileAnimationData(position, tilemap, ref tileAnimationData);
		}
	}
}