using System.Collections.Generic;
using Kope.EntityIdentity;
using Kope.SaveSystem;
using Kope.Core.Collections.Hashes;
using Kope.Core.EntityComponentRegistry;

namespace Kope.Core.Identity {
	public class PropEntitySaveSystem : EntitySaveSystemBase<PropInstance, PropConfig, PropEntityDetail,
	IPropEntityDiedOrPooled, PropEntitySavePacket>, IPropEntitySavePacketProvider {

		protected override void RegisterToGlobalRegistry() {
			this._savableEntityRegistry.RegisterPropEntity(this);
		}

		protected override PropEntitySavePacket CreateSavePacket(HashedTag uid, PropConfig config, Dictionary<string, ISaveData> data) {
			return new PropEntitySavePacket(uid, config, data);
		}

		protected override Dictionary<string, ISaveData> GetPacketData(PropEntitySavePacket packet) {
			return packet.Data;
		}
	}
}