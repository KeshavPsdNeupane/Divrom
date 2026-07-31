using System;
using Kope.EntityIdentity;
using Kope.Feature.PathFinding.Interface;
using UnityEngine;

namespace Kope.Feature.PathFinding.Tile {
	/// <summary>
	/// Represents the principal terrain data for micro-level pathfinding tiles. 
	/// Implements the <see cref="ITerrainData{T}"/> contract to ensure strict type safety, 
	/// value-based equality comparison, and reliable dictionary key hashing.<br/>
	/// Encapsulates static obstacle states and editor visualization properties, including 
	/// an automated alpha safeguard that normalizes zero or uninitialized alpha channels 
	/// to fully opaque to prevent invisible tile confusion in the Unity Editor.
	/// </summary>
	[Serializable]
	public struct MicroTerrainData : ITerrainData<MicroTerrainData> {
		[SerializeField] private bool isStaticObstacle;
		[SerializeField] private Color tileColor;

		public MicroTerrainData(bool isStaticObstacle, Color tileColor) {
			this.isStaticObstacle = isStaticObstacle;
			this.tileColor = tileColor;
		}

		public readonly bool IsStaticObstacle => this.isStaticObstacle;
		public readonly Color TileColor => new(
			tileColor.r,
			tileColor.g,
			tileColor.b,
			tileColor.a <= 0 ? 1f : tileColor.a
		);


		public readonly bool Equals(MicroTerrainData other) {
			return this.isStaticObstacle == other.isStaticObstacle
			&& this.tileColor.Equals(other.tileColor);
		}

		public override readonly bool Equals(object obj) => obj is MicroTerrainData other && Equals(other);

		public override readonly int GetHashCode() => this.isStaticObstacle.GetHashCode();

		public static bool operator ==(MicroTerrainData left, MicroTerrainData right) => left.Equals(right);

		public static bool operator !=(MicroTerrainData left, MicroTerrainData right) => !left.Equals(right);

		public override readonly string ToString() => $"IsStaticObstacle: {isStaticObstacle}";
	}

	/// <summary>
	/// Represents the principal terrain data for macro-level pathfinding tiles. 
	/// Implements the <see cref="ITerrainData{T}"/> contract to ensure strict type safety, 
	/// value-based equality comparison, and reliable dictionary key hashing.<br/>
	/// Encapsulates higher-level simulation metrics including terrain classifications, 
	/// movement capabilities, and narrative accessibility rules, including 
	/// an automated alpha safeguard that normalizes zero or uninitialized alpha channels 
	/// to fully opaque to prevent invisible tile confusion in the Unity Editor.
	/// </summary>
	[Serializable]
	public struct MacroTerrainData : ITerrainData<MacroTerrainData> {
		[SerializeField] private TerrainType terrainType;
		[SerializeField] private MovementCapability movementType;
		[SerializeField] private bool isBlocked;
		[SerializeField] private Color tileColor;

		public readonly Color TileColor => new(
			tileColor.r,
			tileColor.g,
			tileColor.b,
			tileColor.a <= 0 ? 1f : tileColor.a
		);

		public readonly TerrainType TerrainType => this.terrainType;
		public readonly MovementCapability MovementType => this.movementType;
		public readonly bool IsBlocked => this.isBlocked;

		public readonly bool Equals(MacroTerrainData other) =>
			terrainType == other.terrainType &&
			movementType == other.movementType &&
			isBlocked == other.isBlocked &&
			tileColor.Equals(other.tileColor);

		public override readonly bool Equals(object obj) => obj is MacroTerrainData other && Equals(other);

		public override readonly int GetHashCode() =>
			HashCode.Combine(terrainType, movementType, isBlocked, tileColor);

		public static bool operator ==(MacroTerrainData left, MacroTerrainData right) => left.Equals(right);

		public static bool operator !=(MacroTerrainData left, MacroTerrainData right) => !left.Equals(right);

		public override readonly string ToString() =>
			$"TerrainType: {terrainType}, MovementType: {movementType}, IsNarrativelyAccessible: {isBlocked}, TileColor: {tileColor}";
	}
}