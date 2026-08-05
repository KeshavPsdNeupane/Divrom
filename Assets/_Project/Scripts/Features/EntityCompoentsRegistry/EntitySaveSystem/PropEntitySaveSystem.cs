using System.Collections.Generic;
using Kope.EntityIdentity;
using Kope.SaveSystem;
using Kope.Core.Collections.Hashes;

namespace Kope.Core.Identity {
	public class PropEntitySaveSystem : EntitySaveSystemBase<PropConfig, IPropEntitySavePacketProvider,
	PropEntitySavePacket>, IPropEntitySavePacketProvider {

		protected override void RegisterToGlobalRegistry() {
			this._entityRegistry.RegisterSavableEntity(this);
		}

		protected override PropEntitySavePacket CreateSavePacket(HashedTag uid, PropConfig config, Dictionary<string, ISaveData> data) {
			return new PropEntitySavePacket(config, data);
		}

		protected override Dictionary<string, ISaveData> GetPacketData(PropEntitySavePacket packet) {
			return packet.Data;
		}
	}
}