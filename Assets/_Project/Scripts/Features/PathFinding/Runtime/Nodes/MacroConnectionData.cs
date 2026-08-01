using System;
using Kope.Core.Attribute;
using Kope.EntityIdentity;
using Kope.Feature.PathFindingOld.Node;
using UnityEngine;

namespace Project.Scripts.Features.PathFindingOld.GraphManager {

	/// <summary>
	/// Represents directed edge connection data between two macro grid nodes.
	/// </summary>
	[Serializable]
	public struct MacroConnectionData : IEquatable<MacroConnectionData> {
		[SerializeField, ReadOnly] private BoundingBox _toBound;
		[SerializeField, ReadOnly] private MovementCapability _allowedTraversal;
		[SerializeField, ReadOnly] private bool _isBlocked;

		public readonly BoundingBox ToBound => this._toBound;
		public readonly MovementCapability AllowedTraversal => this._allowedTraversal;
		public readonly bool IsBlocked => this._isBlocked;

		public MacroConnectionData(
			BoundingBox targetBounds,
			MovementCapability allowedTraversal,
			bool isNarrativelyAccessible = true) {
			this._toBound = targetBounds;
			this._allowedTraversal = allowedTraversal;
			this._isBlocked = isNarrativelyAccessible;
		}

		public static MacroConnectionData CreateConnection(
			BoundingBox to, MovementCapability toCapability,
			MovementCapability fromCapability, bool toIsNarrativelyAccessible,
			bool fromIsNarrativelyAccessible) {
			MovementCapability combinedCapability = toCapability | fromCapability;
			bool combinedNarrativeAccess = toIsNarrativelyAccessible && fromIsNarrativelyAccessible;
			return new MacroConnectionData(to, combinedCapability, combinedNarrativeAccess);
		}

		public readonly MacroConnectionData WithNarrativeAccess(bool isNarrativelyAccessible) {
			return new MacroConnectionData(this.ToBound, this.AllowedTraversal, isNarrativelyAccessible);
		}

		public readonly bool IsTraversable(MovementCapability capability) {
			// must not be blocked and must have at least one overlapping movement capability with the provided capability
			return !this.IsBlocked && (AllowedTraversal & capability) != MovementCapability.None;
		}

		public readonly bool Equals(MacroConnectionData other) {
			return ToBound == other.ToBound &&
				   AllowedTraversal == other.AllowedTraversal &&
				   IsBlocked == other.IsBlocked;
		}

		public override readonly bool Equals(object obj) => obj is MacroConnectionData other && this.Equals(other);
		public override readonly int GetHashCode() => HashCode.Combine(ToBound, AllowedTraversal, IsBlocked);

		public static bool operator ==(MacroConnectionData left, MacroConnectionData right) => left.Equals(right);
		public static bool operator !=(MacroConnectionData left, MacroConnectionData right) => !left.Equals(right);

		public override readonly string ToString() =>
			$"MacroConnectionData(To: {ToBound}, AllowedTraversal: {AllowedTraversal}, IsBlocked: {IsBlocked})";
	}
}