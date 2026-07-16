using Kope.Core.Collections.UniqueId;
using Kope.Core.EntityComponentRegistry;
using Kope.EntityIdentity;

namespace Kope.Core.Identity {
	/// <summary>
	/// Polymorphic entry point for all runtime entities. 
	/// Allows AI/LLM context layers to scan basic descriptors without caring about specialized types.
	/// </summary>
	public interface IEntityDetail {
		UniqueID UniqueID { get; }
		IReadOnlyComponentRegistry ComponentRegistry { get; }
	}

	/// <summary>
	/// Pure, decoupled data container housing core infrastructure components for an entity instance.
	/// </summary>
	public abstract class EntityDetailBase<TConfig, TEventProvider> : IEntityDetail {
		public UniqueID UniqueID { get; }
		public TConfig Config { get; }
		public IReadOnlyComponentRegistry ComponentRegistry { get; }
		public TEventProvider EventProvider { get; }

		protected EntityDetailBase(
			UniqueID uniqueID,
			TConfig config,
			IReadOnlyComponentRegistry componentRegistry,
			TEventProvider eventProvider) {

			this.UniqueID = uniqueID;
			this.Config = config;
			this.ComponentRegistry = componentRegistry;
			this.EventProvider = eventProvider;
		}
	}
	public class MobEntityDetail : EntityDetailBase<MobConfig, IMobEntityDiedOrPooled> {
		public MobConfig MobConfig => Config;
		public MobEntityDetail(
			UniqueID uniqueID,
			MobConfig mobConfig,
			IReadOnlyComponentRegistry entityComponentRegistry,
			IMobEntityDiedOrPooled onEntityDiedOrPooled)
			: base(uniqueID, mobConfig, entityComponentRegistry, onEntityDiedOrPooled) { }
	}
	public class PropEntityDetail : EntityDetailBase<PropConfig, IPropEntityDiedOrPooled> {
		public PropConfig PropConfig => Config;
		public PropEntityDetail(
			UniqueID uniqueID,
			PropConfig propConfig,
			IReadOnlyComponentRegistry entityComponentRegistry,
			IPropEntityDiedOrPooled onEntityDiedOrPooled)
			: base(uniqueID, propConfig, entityComponentRegistry, onEntityDiedOrPooled) { }
	}
}