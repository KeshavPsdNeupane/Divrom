using UnityEngine;

namespace Kope.Feature.PathFinding.Tile {
	/// <summary>
	/// A concrete implementation of a pathfinding tile that uses MacroTerrainData 
	/// as its terrain data type.
	/// This tile can be used in a tilemap to represent different types of terrain 
	/// for pathfinding algorithms, with each terrain type having its own properties
	/// defined in MacroTerrainData.
	/// The tile can be created as a ScriptableObject asset in the Unity editor, 
	/// allowing for easy customization and reuse across different tilemaps and scenes.
	/// </summary>
	[CreateAssetMenu(fileName = "New HHSI Macro Pathfinding Tile", menuName = "2D/Custom/HHSI Macro Pathfinding Tile")]
	public class HHSIMacroPathFindingTile : HSIPathFindingTileBase<MacroTerrainData> { }
}