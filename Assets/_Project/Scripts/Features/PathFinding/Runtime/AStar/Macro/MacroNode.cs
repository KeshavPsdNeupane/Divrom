using System;
using System.Runtime.CompilerServices;
using Kope.Feature.PathFinding.Node;
using ThirdParty.PriorityQueeu;

public readonly struct MacroPathFindingNode : IHasCost<int>, IEquatable<MacroPathFindingNode> {
	public readonly BoundingBox NodeBox;
	public readonly int GCost;
	public readonly int HCost;
	public readonly BoundingBox? Parent;


	public int FCost {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => GCost + HCost;
	}

	public MacroPathFindingNode(BoundingBox nodeBox, int gCost, int hCost, BoundingBox? parent) {
		NodeBox = nodeBox;
		GCost = gCost;
		HCost = hCost;
		Parent = parent;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int GetCost() => FCost;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(MacroPathFindingNode other) {
		if (NodeBox == null) return other.NodeBox == null;
		return NodeBox.Equals(other.NodeBox);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool Equals(object obj) => obj is MacroPathFindingNode otherNode && Equals(otherNode);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode() => NodeBox != null ? NodeBox.GetHashCode() : 0;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator ==(MacroPathFindingNode left, MacroPathFindingNode right) => left.Equals(right);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator !=(MacroPathFindingNode left, MacroPathFindingNode right) => !left.Equals(right);

	public override string ToString() {
		string parentStr = Parent.HasValue ? Parent.Value.ToString() : "None";
		return $"MacroPathFindingNode(Node: {NodeBox}, GCost: {GCost}, HCost: {HCost}, FCost: {FCost}, Parent: {parentStr})";
	}
}