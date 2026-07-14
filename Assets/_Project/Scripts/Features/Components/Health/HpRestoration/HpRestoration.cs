using Kope.EntityComponentSystem;
using UnityEngine;

namespace Kope.Component.Health.Temp {
	/// <summary>
	/// This is a test class for HP restoration. 
	/// It can be used to restore HP to a character or entity in the game.
	/// The restore amount can be set in the inspector.
	/// </summary>
	[RequireComponent(typeof(CircleCollider2D))]
	public class HpRestoration : ComponentBase {
		[SerializeField, Tooltip("If this is true then restoreAmount will be treated as a percentage")]
		private bool isPercentage = false;
		[SerializeField, Min(1.0f)] private float restoreAmount = 10f;


		public float RestoreAmount {
			get {
				if (this.isPercentage) {
					return this.restoreAmount / 100f;
				}
				return this.restoreAmount;
			}
		}

		public bool IsPercentage => this.isPercentage;
	}
}
