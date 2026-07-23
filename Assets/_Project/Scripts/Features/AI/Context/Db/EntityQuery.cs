using Kope.EntityIdentity;
using System;
using UnityEngine;
using Kope.Core.Type.Generic;
using Kope.Core.Attribute;

namespace Kope.AI {

	[Serializable]
	public class MobQueryFactory {
		[Header("--- FILTER LOGIC: INTERSECTION (AND) ---")]
		[SerializeField]
		[Message("All active filters are combined with 'AND' logic.\n" +
			"• Race & Relation: If both are enabled, the entity must match BOTH criteria.\n" +
			"• Player/Unique Entities: Keep Race 'HasValue' unchecked to prevent exclusion, " +
			"unless specifically checking for racial aggression.")]
		[Tooltip("Filters by Race. Note: Active filters are cumulative (AND logic).")]
		public SerializableNullable<RaceEnum> race;

		[Tooltip("Filters by EntityRelation. Note: Active filters are cumulative (AND logic).")]
		public SerializableNullable<EntityRelation> relation;
		public MobQuery ToQuery() {
			return new MobQuery(
				relation.HasValue ? relation.Value : null,
				race.HasValue ? race.Value : null
			);
		}
	}

	[Serializable]
	public class PropTypeFactory {
		public SerializableNullable<PropType> propType;

		public PropQuery ToQuery() {
			return new PropQuery(
				propType.HasValue ? propType.Value : null
			);
		}
	}

	/// <summary>
	/// Configuration bridge between the Unity Inspector and the AI Logic layer.
	/// Manages the translation of serializable data into immutable, runtime-safe queries.
	/// </summary>
	[Serializable]
	public struct EntityQuery {
		[Header("Classification")]
		[Tooltip("The base type of the entity. Used to route the query to the correct registry cache.")]
		[Message("The base type of the entity. Used to route the query to the correct registry cache.\n" +
			"• Mob: Queries the MobRegistry for entities matching the specified filters.\n" +
			"• Prop: Queries the PropRegistry for entities matching the specified filters.")]
		[SerializeField] private EntityType type;

		[Header("Filter Settings")]
		[SerializeField] private MobQueryFactory MobQueryFactory;
		[SerializeField] private PropTypeFactory PropTypeFactory;

		public readonly EntityType Type => type;

		/// <summary> 
		/// Returns an immutable MobQuery. If multiple fields are set, 
		/// the database interprets this as a logical AND (Intersection). 
		/// </summary>
		public readonly MobQuery GetMobQuery() => MobQueryFactory.ToQuery();

		/// <summary> 
		/// Returns an immutable PropQuery. 
		/// </summary>
		public readonly PropQuery GetPropQuery() => PropTypeFactory.ToQuery();
	}
}