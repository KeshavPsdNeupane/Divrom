using UnityEngine;

public class PlayerBaseState
{
    protected PlayerStateController playerStateController;
    protected PlayerStateManager stateManager;
    protected int animationStateHash;

    public string stateName;

    public PlayerBaseState(PlayerStateManager StateManager,
        PlayerStateController playerStateController, string animationBoolName,
        string name)
    {
        this.stateManager = StateManager;
        this.stateName = name;
        this.playerStateController = playerStateController;
        this.animationStateHash = Animator.StringToHash(animationBoolName);
    }

    public virtual void Enter() { }

    public virtual void Update() { }

    public virtual void PhysicUpdate() { }

    public virtual void Exit() { }

    public virtual void OnAnimationTrigger() { }
}
