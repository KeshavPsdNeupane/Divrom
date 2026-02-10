using Kope.AI.Utility;
using Kope.Core.EntityComponentSystem;
using ThirdParty;
using UnityEngine;

[CreateAssetMenu(fileName = "IdleAction", menuName = "Scriptable Objects/AI/Utility/Actions/IdleAction")]
public class IdleAction : ActionSO
{
    [Header("Idle Action Settings\n" +
    "Does nothing just waits for a specified duration for Idle behavior.\n" +
    "Since Idle means doing nothing, this action simply waits for a set duration.\n")]
    [SerializeField] private float idleDuration = 1f;
    private CountdownTimer idleTimer;

    protected override void OnInilialize(EntityComponentRegistry ctx)
    {
        this.idleTimer = new CountdownTimer(this.idleDuration);
        this.idleTimer.Start();
        this.idleTimer.OnTimerStop += MarkCompleted;
    }
    public override void TickUpdate(Context ctx)
    {
        this.idleTimer.Tick(Time.deltaTime);
        return; // no other logic needed for idle action. so we can return early.
    }

    public override void TickFixedUpdate(Context ctx)
    {
        return;// no physics based logic needed for idle action. so we can leave this empty.
    }


    protected override void OnEndOrAbort(EntityComponentRegistry ctx)
    {
        if (this.idleTimer == null) return;
        // if timer is null, it means it was never started, so we can skip stopping and unsubscribing.

        this.idleTimer.OnTimerStop -= MarkCompleted;
        this.idleTimer.Reset();

    }
}


