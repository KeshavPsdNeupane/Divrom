
using Kope.Core.Mathfx;
using UnityEngine;

namespace Kope.Component.Movement
{
	/// <summary>
	/// Encapsulates movement intent state and priority-based filtering logic.
	/// </summary>
	public class MovementIntentHandler
	{
		private MovementIntent _currentIntent;
		private Vector3 _lastDirection;

		public MovementIntent Current => _currentIntent;
		public Vector3 LastDirection => _lastDirection;

		public MovementIntentHandler(Vector3 initialFacing)
		{
			this._currentIntent = MovementIntent.Default;
			this._lastDirection = initialFacing;
		}

		public bool TrySetIntent(MovementIntent intent, bool isStunned)
		{
			if (isStunned) return false;

			if (this._currentIntent.Priority != MovementIntentPriority.UnlockNext)
			{
				if (intent.Priority < this._currentIntent.Priority) return false;
			}

			if (intent.Direction.sqrMagnitude > Mathfx.SQUARE_DIRECTION_UPPER_EPSILON)
			{
				intent.Direction.Normalize();
				this._lastDirection = intent.Direction;
			}
			else
			{
				intent.Direction = Vector3.zero;
			}

			this._currentIntent = intent;
			return true;
		}

		public void ForceIntent(MovementIntent intent)
		{
			this._currentIntent = intent;
			if (intent.Direction.sqrMagnitude > Mathfx.SQUARE_DIRECTION_UPPER_EPSILON)
			{
				this._lastDirection = intent.Direction.normalized;
			}
		}
	}
}