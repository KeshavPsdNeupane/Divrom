using UnityEngine;

public class TestMovementController : InitializableBase
{
    [SerializeField] private MovementComponentBase movementComponent;

    public override void Init()
    {
        if (this.movementComponent == null)
        {
            this.movementComponent = GetComponent<MovementComponentBase>();
            Logger.Warn($"TestMovementController ({gameObject.name}): " +
           "MovementComponentBase not assigned, attempting to fetch from same GameObject.");
        }
        SetInitialized();
    }


    private void Update()
    {
        if (!IsInitialized && !this.movementComponent) return;
        this.movementComponent.ApplyMovement();
    }



}
