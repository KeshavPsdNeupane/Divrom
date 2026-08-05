using Kope.Feature.PathFindingNew.Interface;
using Kope.Feature.PathFindingNew.Utility;
using UnityEngine;
using UnityEngine.Tilemaps;


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
			tileData.sprite = SpriteTextureCache.GetOrCreate(this.data.TileColor);
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