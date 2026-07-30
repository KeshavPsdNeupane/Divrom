using System;
using System.Runtime.CompilerServices;
using Kope.Feature.PathFinding.Node;
using ThirdParty.PriorityQueeu;

public readonly struct MicroPathFindingNode : IHasCost<int>, IEquatable<MicroPathFindingNode> {
	public readonly Vec2Int NodePosition;
	public readonly int GCost;
	public readonly int HCost;
	public readonly Vec2Int? Parent;

	public int FCost {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => GCost + HCost;
	}

	public MicroPathFindingNode(Vec2Int nodePostion, int gCost, int hCost, Vec2Int? parent) {
		NodePosition = nodePostion;
		GCost = gCost;
		HCost = hCost;
		Parent = parent;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int GetCost() => FCost;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(MicroPathFindingNode other) {
		if (NodePosition == null) return other.NodePosition == null;
		return NodePosition.Equals(other.NodePosition);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool Equals(object obj) => obj is MicroPathFindingNode otherNode && Equals(otherNode);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode() => NodePosition != null ? NodePosition.GetHashCode() : 0;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator ==(MicroPathFindingNode left, MicroPathFindingNode right) => left.Equals(right);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator !=(MicroPathFindingNode left, MicroPathFindingNode right) => !left.Equals(right);

	public override string ToString() {
		string parentStr = Parent.HasValue ? Parent.Value.ToString() : "None";
		return $"MicroPathFindingNode(Node: {NodePosition}, GCost: {GCost}, HCost: {HCost}, FCost: {FCost}, Parent: {parentStr})";
	}
}