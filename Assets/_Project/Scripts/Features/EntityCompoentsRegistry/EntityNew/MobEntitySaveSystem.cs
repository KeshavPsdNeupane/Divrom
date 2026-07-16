using System.Collections.Generic;
using Kope.EntityIdentity;
using Kope.SaveSystem;
using Kope.Core.Collections.Hashes;
using Kope.Core.EntityComponentRegistry;

namespace Kope.Core.Identity {
	public class MobEntitySaveSystem : EntitySaveSystemBase<MobInstance, MobConfig, MobEntityDetail,
	IMobEntityDiedOrPooled, MobEntitySavePacket>, IMobEntitySavePacketProvider {

		protected override void RegisterToGlobalRegistry() {
			this._savableEntityRegistry.RegisterMobEntity(this);
		}

		protected override MobEntitySavePacket CreateSavePacket(HashedTag uid, MobConfig config, Dictionary<string, ISaveData> data) {
			return new MobEntitySavePacket(uid, config, data);
		}

		protected override Dictionary<string, ISaveData> GetPacketData(MobEntitySavePacket packet) {
			return packet.Data;
		}
	}
}