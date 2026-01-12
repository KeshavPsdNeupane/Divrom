using Kope.Core.CompilerServices;
using UnityEngine;
using Kope.Core.Init;

public class TestMovementController : InitializableBase
{
    [SerializeField] private MovementComponentBase movementComponent;

    public override void Init()
    {
        if (this.movementComponent == null)
        {
            this.movementComponent = GetComponent<MovementComponentBase>();
            MyLogger.Warn($"TestMovementController ({gameObject.name}): " +
           "MovementComponentBase not assigned, attempting to fetch from same GameObject.");
        }
        SetInitialized();
    }


    private void Update()
    {
        if (!IsInitialized || this.movementComponent == null) return;
        this.movementComponent.ApplyMovement();
    }



}
