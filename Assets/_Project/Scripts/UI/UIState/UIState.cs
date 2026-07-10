using Kope.Core.LifeTimeManagement;
public class UIState : InitializableBase {
	public virtual void EnterState() { }
	public virtual void ExitState() { }
	public virtual void UpdateState() { }

}
