using System;
using Kope.Core.Attribute;
using Kope.EntityIdentity;
using Kope.Feature.PathFinding.Node;
using UnityEngine;

namespace Project.Scripts.Features.PathFinding.GraphManager {

	/// <summary>
	/// Represents directed edge connection data between two macro grid nodes.
	/// </summary>
	[Serializable]
	public struct MacroConnectionData : IEquatable<MacroConnectionData> {
		[SerializeField, ReadOnly] private BoundingBox _toBound;
		[SerializeField, ReadOnly] private MovementCapability _allowedTraversal;
		[SerializeField, ReadOnly] private bool _isNarrativelyAccessible;

		public readonly BoundingBox ToBound => this._toBound;
		public readonly MovementCapability AllowedTraversal => this._allowedTraversal;
		public readonly bool IsNarrativelyAccessible => this._isNarrativelyAccessible;

		public MacroConnectionData(
			BoundingBox targetBounds,
			MovementCapability allowedTraversal,
			bool isNarrativelyAccessible = true) {
			this._toBound = targetBounds;
			this._allowedTraversal = allowedTraversal;
			this._isNarrativelyAccessible = isNarrativelyAccessible;
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
			return IsNarrativelyAccessible && (AllowedTraversal & capability) != MovementCapability.None;
		}

		public readonly bool Equals(MacroConnectionData other) {
			return ToBound == other.ToBound &&
				   AllowedTraversal == other.AllowedTraversal &&
				   IsNarrativelyAccessible == other.IsNarrativelyAccessible;
		}

		public override readonly bool Equals(object obj) => obj is MacroConnectionData other && this.Equals(other);
		public override readonly int GetHashCode() => HashCode.Combine(ToBound, AllowedTraversal, IsNarrativelyAccessible);

		public static bool operator ==(MacroConnectionData left, MacroConnectionData right) => left.Equals(right);
		public static bool operator !=(MacroConnectionData left, MacroConnectionData right) => !left.Equals(right);

		public override readonly string ToString() =>
			$"MacroConnectionData(To: {ToBound}, AllowedTraversal: {AllowedTraversal}, IsNarrativelyAccessible: {IsNarrativelyAccessible})";
	}
}