using Kope.AI.Utility;
using Kope.Core.EntityComponentSystem;
using ThirdParty;
using UnityEngine;

[CreateAssetMenu(fileName = "RangeAction", menuName = "Scriptable Objects/AI/Utility/Actions/RangeAction")]
public class RangeAction : ActionSO {
	/// <summary>
	/// This is dummy implementation Action that simply waits for a short duration
	///  before completing. This is used to demonstrate the Utility AI system and should 
	/// be replaced with actual logic for performing an action based on range considerations.
	/// </summary>
	private readonly float tempIdleDuration = 2f;
	private CountdownTimer idleTimer;

	protected override void OnInilialize(ComponentRegistry ctx) {
		this.idleTimer = new CountdownTimer(this.tempIdleDuration);
		this.idleTimer.Start();
		this.idleTimer.OnTimerStop += MarkCompleted;
	}
	public override void TickUpdate(Context ctx) {
		this.idleTimer.Tick(Time.deltaTime);
		return;
	}

	public override void TickFixedUpdate(Context ctx) {
		return;
	}


	protected override void OnEndOrAbort(ComponentRegistry ctx) {
		if (this.idleTimer == null) return;
		this.idleTimer.OnTimerStop -= MarkCompleted;
		this.idleTimer.Reset();

	}
}