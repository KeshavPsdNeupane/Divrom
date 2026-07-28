using System;
using System.Runtime.CompilerServices;
using Kope.Feature.PathFinding.Node;
using ThirdParty.PriorityQueeu;

public readonly struct MacroPathFindingNode : IHasCost<int>, IEquatable<MacroPathFindingNode> {
	public readonly MacroGridNode Node;
	public readonly int GCost;
	public readonly int HCost;
	public readonly BoundingBox? Parent;


	public int FCost {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => GCost + HCost;
	}

	public MacroPathFindingNode(MacroGridNode node, int gCost, int hCost, BoundingBox? parent) {
		Node = node;
		GCost = gCost;
		HCost = hCost;
		Parent = parent;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int GetCost() => FCost;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(MacroPathFindingNode other) {
		if (Node == null) return other.Node == null;
		return Node.Equals(other.Node);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool Equals(object obj) => obj is MacroPathFindingNode otherNode && Equals(otherNode);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode() => Node != null ? Node.GetHashCode() : 0;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator ==(MacroPathFindingNode left, MacroPathFindingNode right) => left.Equals(right);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator !=(MacroPathFindingNode left, MacroPathFindingNode right) => !left.Equals(right);

	public override string ToString() {
		string parentStr = Parent.HasValue ? Parent.Value.ToString() : "None";
		return $"MacroPathFindingNode(Node: {Node}, GCost: {GCost}, HCost: {HCost}, FCost: {FCost}, Parent: {parentStr})";
	}
}