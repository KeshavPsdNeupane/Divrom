using Kope.Feature.PathFindingNew.Interface;
using Kope.Feature.PathFindingNew.Utility;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Kope.Feature.PathFindingNew.Interface {
	/// <summary>
	/// Defines the interface for terrain data used in pathfinding tiles. Implementing this
	///  interface allows for the creation of custom terrain types with specific properties,
	///  such as color, that can be used in pathfinding algorithms.<br/>
	/// Note: <br/>
	/// TileColor is used for color coating the tile in the editor. It is not used for any gameplay logic.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public interface ITerrainData<T> : System.IEquatable<T> {
		Color TileColor { get; }
	}
}

namespace Kope.Feature.PathFindingNew.TBase {
	public abstract class GridTile<T> : TileBase where T : struct, ITerrainData<T> {
		[SerializeField] protected T data;
		/// <summary>
		/// Gets the principal terrain data associated with this tile instance.
		/// </summary>
		public T Data => data;
	}
	public abstract class GridTileBase<T> : GridTile<T> where T : struct, ITerrainData<T> {
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