using UnityEngine;
using UnityEngine.InputSystem;
using ServiceLocatorPattern;
using Kope.Core.CompilerServices;
using Kope.Component.Movement;
using Kope.SaveSystem;
/// <summary>
/// PlayerMovementComponent.cs<br/>
/// This component handles player movement input and translates it into movement intents for the movement system to process.
/// It subscribes to the InputManager's input events and updates the movement intent accordingly.
/// <br/>
/// This component is designed to be flexible and can be easily extended to support additional movement-related input (e.g., sprinting, crouching) by adding more input action subscriptions and handling them in the corresponding callback methods.
/// <br/>
/// Note: This component assumes that the InputManager is properly set up and that the relevant input actions (e.g., "Move") are defined in the input action asset. Make sure to configure the InputManager and input actions correctly for this component to function as intended.
/// <br/>
/// <inheritdoc cref="MovementComponentBase"/>      
/// </summary>
[SaveId("player_movement")]
public class PlayerMovementComponent : MovementComponentBase {
	private InputManager inputManager;


	protected override bool OnInit() {
		// we need to check if we already sub to the stats
		// since base is not InitializableBase, we need to call it to make sure
		//  we get the stat value from the stats
		if (!base.OnInit()) return false; // impt if the base class is not InilializableBase
		if (GlobalServiceLocator.Instance.TryGetService(out InputManager inputManager)) {
			this.inputManager = inputManager;
		} else {
			MyLogger.Error($"{this.gameObject.name}Controller: InputManager service not found!" + GetParentGameObjectHeirarchyMessage());
			return false;
		}
		return true;
	}
	protected override void OnEnable() {
		base.OnEnable();
		Subscribe();
	}


	protected override void OnDisable() {
		base.OnDisable();
		Unsubscribe();
	}

	private void Subscribe() {
		if (this.inputManager == null) return;
		this.inputManager.SubscribeToInputAction(
			PlayerInputActionMap.Player,
			PlayerInputActionKey.Move.ToString(),
			MoveForInputSystem);
	}

	private void Unsubscribe() {
		if (this.inputManager == null) return;
		this.inputManager.UnsubscribeFromInputAction(
			PlayerInputActionMap.Player,
			PlayerInputActionKey.Move.ToString(),
			MoveForInputSystem);
	}


	public void MoveForInputSystem(InputAction.CallbackContext context)
	=> SetMovementIntent(new MovementIntent(context.ReadValue<Vector2>(), MovementIntentType.Move));

}
