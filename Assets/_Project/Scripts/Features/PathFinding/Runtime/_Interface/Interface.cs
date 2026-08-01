using System;
using UnityEngine;

namespace Kope.Feature.PathFindingOld.Interface {
	/// <summary>
	/// Defines the interface for terrain data used in pathfinding tiles. Implementing this
	///  interface allows for the creation of custom terrain types with specific properties,
	///  such as color, that can be used in pathfinding algorithms.<br/>
	/// Note: <br/>
	/// TileColor is used for color coating the tile in the editor. It is not used for any gameplay logic.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public interface ITerrainData<T> : IEquatable<T> {
		Color TileColor { get; }
	}
}
