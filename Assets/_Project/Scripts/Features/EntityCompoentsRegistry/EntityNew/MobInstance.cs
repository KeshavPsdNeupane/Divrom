using UnityEngine;
using Kope.EntityIdentity;
using Kope.Core.EntityComponentRegistry;
using System;

namespace Kope.Core.Identity {
	public interface IMobEntityDetail {
		MobEntityDetail MobEntityDetail { get; }
		void InvokeOnEntityDiedOrPooledEvent();
	}


	public class MobInstance : EntityInstanceNew, IMobEntityDetail, IMobEntityDiedOrPooled {
		[SerializeField] private EntityRelation relation;
		[SerializeField] private RaceEnum race;
		[SerializeField] private GenderEnum gender;

		private MobConfig _mobConfig;
		private MobEntityDetail _mobEntityDetail;
		private event Action<MobEntityDetail> OnMobEntityDiedOrPooled;


		public EntityRelation Relation => relation;
		public RaceEnum Race => race;
		public GenderEnum Gender => gender;

		public override EntityType Type => EntityType.MOB;
		public MobEntityDetail MobEntityDetail => this._mobEntityDetail ??= new MobEntityDetail(
			this.uniqueID,
			this.MobConfig,
			this.ecr.ComponentRegistry,
			this
		);

		public MobConfig MobConfig =>
			this._mobConfig ??= new MobConfig(entityName, relation, race, gender);

		public void OnEntityDiedOrPooledEvent(Action<MobEntityDetail> callback, bool isSubscribe) {
			OnMobEntityDiedOrPooled -= callback;
			if (isSubscribe) OnMobEntityDiedOrPooled += callback;
		}
		public void InvokeOnEntityDiedOrPooledEvent() {
			OnMobEntityDiedOrPooled?.Invoke(this.MobEntityDetail);
		}
	}

}