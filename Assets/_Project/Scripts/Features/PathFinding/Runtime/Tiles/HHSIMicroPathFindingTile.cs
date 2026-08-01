using UnityEngine;

namespace Kope.Feature.PathFindingOld.Tile {
	/// <summary>
	/// A concrete implementation of a pathfinding tile that uses MicroTerrainData 
	/// as its terrain data type.
	/// This tile can be used in a tilemap to represent different types of terrain 
	/// for pathfinding algorithms, with each terrain type having its own properties
	/// defined in MicroTerrainData.
	/// The tile can be created as a ScriptableObject asset in the Unity editor, 
	/// allowing for easy customization and reuse across different tilemaps and scenes.
	/// </summary>
	[CreateAssetMenu(fileName = "New HHSI Micro Pathfinding Tile", menuName = "2D/Custom/HHSI Micro Pathfinding Tile")]
	public class HHSIMicroPathFindingTile : HSIPathFindingTileBase<MicroTerrainData> {
	}
}