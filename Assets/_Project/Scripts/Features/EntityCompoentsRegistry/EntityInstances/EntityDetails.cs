using Kope.Core.Collections.UniqueId;
using Kope.Core.EntityComponentRegistry;
using Kope.EntityIdentity;

namespace Kope.Core.Identity
{
	/// <summary>
	/// Polymorphic entry point for all runtime entities. 
	/// Allows AI/LLM context layers to scan basic descriptors without caring about specialized types.
	/// </summary>
	public interface IEntityDetail
	{
		UniqueID UniqueID { get; }
		IReadOnlyComponentRegistry ComponentRegistry { get; }
	}

	/// <summary>
	/// Pure, decoupled data container housing core infrastructure components for an entity instance.
	/// </summary>
	public abstract class EntityDetailBase : IEntityDetail
	{
		public UniqueID UniqueID { get; }
		public IReadOnlyComponentRegistry ComponentRegistry { get; }
		public IEntityDiedOrPooledNew EventProvider { get; protected set; }
		protected EntityDetailBase(
			UniqueID uniqueID,
			IReadOnlyComponentRegistry componentRegistry,
			IEntityDiedOrPooledNew eventProvider = null)
		{
			this.UniqueID = uniqueID;
			this.ComponentRegistry = componentRegistry;
			this.EventProvider = eventProvider;
		}
	}
	public class MobEntityDetail : EntityDetailBase
	{
		public MobConfig MobConfig { get; }
		public MobEntityDetail(
	UniqueID uniqueID,
	MobConfig mobConfig,
	IReadOnlyComponentRegistry entityComponentRegistry,
	IEntityDiedOrPooledNew onEntityDiedOrPooled)
	: base(uniqueID, entityComponentRegistry, onEntityDiedOrPooled)
		{
			this.MobConfig = mobConfig;
			this.EventProvider = onEntityDiedOrPooled;
		}
	}
	public class PropEntityDetail : EntityDetailBase
	{
		public PropConfig PropConfig { get; }
		public PropEntityDetail(
			UniqueID uniqueID,
			PropConfig propConfig,
			IReadOnlyComponentRegistry entityComponentRegistry,
			IEntityDiedOrPooledNew onEntityDiedOrPooled)
			: base(uniqueID, entityComponentRegistry, onEntityDiedOrPooled)
		{
			this.PropConfig = propConfig;
			this.EventProvider = onEntityDiedOrPooled;
		}
	}
}