using Kope.Feature.PathFinding.Node;

public struct MicroPathFindingNode
{
	public MicroGridNode Node;
	public float GCost;
	public float HCost;
	public readonly float FCost => GCost + HCost;
	public Vec2Int parent;
	public MicroPathFindingNode(MicroGridNode node, float gCost, float hCost, Vec2Int parent)
	{
		this.Node = node;
		this.GCost = gCost;
		this.HCost = hCost;
		this.parent = parent;
	}
}
public class AStarMicro
{





}
