namespace Kope.Feature.PathFindingOld {

	/// <summary>
	/// Defines the terrain characteristics and traversal cost categories for a pathfinding region.
	/// </summary>
	/// <remarks>
	/// Currently structured as a fixed integer-backed enumeration to map baseline costs;
	/// designed for future extension into a dynamic runtime terrain weight system.
	/// Later will be replaced with Runtime Enum.
	/// </remarks>
	public enum TerrainType {
		OpenGround = 0,
		Mountain = 10,
		DeepWater = 20,
		Forest = 30
	}
}