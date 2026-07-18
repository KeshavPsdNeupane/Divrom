using System;
using System.Collections.Generic;
using Kope.EntityIdentity;
using UnityEngine;

namespace Kope.Feature.PathFinding {
	/// <summary>
	/// Defines the terrain characteristics of a region.
	/// Currently uses a fixed enum, intended for future transition to a runtime-dynamic system.
	/// </summary>
	public enum TerrainType {
		OpenGround = 0,
		Mountain = 10,
		DeepWater = 20,
		Forest = 30
	}

	/// <summary>
	/// Represents a single node in the micro grid, providing a fine-grained representation of the world.
	/// <para>This is a <b>Tier-2</b> node, functioning as the high-detail counterpart to the 
	/// <see cref="MacroGridNode"/> (Tier-1). Once a <see cref="MacroGridNode"/> validates that 
	/// a path exists, the <see cref="MicroGridNode"/> system is used to calculate the precise 
	/// trajectory around obstacles and terrain features.</para>
	/// </summary>
	public sealed class MicroGridNode {
		public Vector2Int Position { get; }
		public bool IsStaticObstacle { get; set; }
		public MacroGridNode ParentMacroGrid { get; set; }

		public MicroGridNode(Vector2Int position, bool isStaticObstacle) {
			Position = position;
			IsStaticObstacle = isStaticObstacle;
		}

		public override string ToString() {
			return $"MicroGridNode(Position: {Position}, IsStaticObstacle: {IsStaticObstacle})";
		}

		public override int GetHashCode() {
			// Position is unique per node, serving as the primary key.
			return this.Position.GetHashCode();
		}
	}

	/// <summary>
	/// Represents a single node in the macro grid, providing a coarse-grained representation of the world.
	/// <para>This is a <b>Tier-1</b> node, functioning as a high-level abstraction layer. 
	/// <see cref="MacroGridNode"/>s are used to determine if a valid path exists between 
	/// distant points. Once the macro-level path is confirmed, the system transitions to 
	/// <see cref="MicroGridNode"/>s for specific, low-level navigation.</para>
	/// </summary>
	public sealed class MacroGridNode {
		public BoundingBox Bounds { get; set; }
		public TerrainType TerrainType { get; set; }
		public MovementCapability AllowedTraversal { get; set; }
		public List<MacroConnection> Connections { get; } = new();
		public List<MicroGridNode> MicroGridsNodes { get; } = new();

		public int TotalMicroGrids => MicroGridsNodes.Count;

		public MacroGridNode(
			BoundingBox bounds, TerrainType terrainType,
			List<MacroConnection> connections, MovementCapability allowedTraversal) {
			this.Connections = connections;
			this.Bounds = bounds;
			this.TerrainType = terrainType;
			this.AllowedTraversal = allowedTraversal;
		}

		public override string ToString() {
			return $"MacroGridNode(Bounds: {Bounds}, TerrainType: {TerrainType}, AllowedTraversal: {AllowedTraversal}, TotalMicroGrids: {TotalMicroGrids})";
		}

		public override int GetHashCode() {
			// Bounds are unique identifiers for MacroGridNodes.
			return this.Bounds.GetHashCode();
		}
	}

	/// <summary>
	/// Represents a connection between two MacroGridNodes, allowing traversal between 
	/// them based on the specified MovementCapability. 
	/// <br/><br/>
	/// The <see cref="IsNarrativelyAccessible"/> property acts as a master toggle for 
	/// storytelling purposes. If set to false, traversal is blocked regardless of the 
	/// <see cref="AllowedTraversal"/> capability. This allows connections to remain part 
	/// of the graph structure while being temporarily or permanently disabled for 
	/// gameplay progression.
	/// </summary>
	public sealed class MacroConnection {
		public MacroGridNode From { get; set; }
		public MacroGridNode To { get; set; }
		public MovementCapability AllowedTraversal { get; set; }

		/// <summary>
		/// Gets or sets whether this connection is accessible for gameplay traversal.
		/// Defaults to true.
		/// </summary>
		public bool IsNarrativelyAccessible { get; set; } = true;

		public override string ToString() {
			return $"MacroConnection(From: {From.Bounds}, To: {To.Bounds}, AllowedTraversal: {AllowedTraversal}, IsNarrativelyAccessible: {IsNarrativelyAccessible})";
		}

		public override int GetHashCode() {
			return HashCode.Combine(From, To, AllowedTraversal, IsNarrativelyAccessible);
		}
	}

	/// <summary>
	/// Represents a very lightweight bounding box in 2D space, defined by its minimum and maximum corners.
	/// Using a custom struct instead of Unity's Bounds for performance and memory efficiency, as
	/// this is a value type and avoids unnecessary overhead.
	/// </summary>
	public readonly struct BoundingBox {
		public Vector2Int Min { get; }
		public Vector2Int Max { get; }
		// just derive this not worth storing separately
		public Vector2Int Size => this.Max - this.Min;
		public BoundingBox(Vector2Int min, Vector2Int max) {
			this.Min = min;
			this.Max = max;

		}

		/// <summary>Checks if a point exists within the bounds.</summary>
		public bool Contains(Vector2Int point) {
			return point.x >= Min.x && point.x <= Max.x &&
				   point.y >= Min.y && point.y <= Max.y;
		}

		/// <summary>Checks if this box overlaps with another.</summary>
		public bool Intersects(BoundingBox other) {
			return !(other.Min.x > Max.x || other.Max.x < Min.x ||
					 other.Min.y > Max.y || other.Max.y < Min.y);
		}

		public override string ToString() {
			return $"BoundingBox(Min: {Min}, Max: {Max})";
		}

		public override int GetHashCode() {
			return HashCode.Combine(Min, Max);
		}
	}
}