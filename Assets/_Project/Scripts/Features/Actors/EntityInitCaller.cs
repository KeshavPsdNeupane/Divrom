using Kope.Core.Execution;
using Kope.Core.LifeTimeManagement;
/// <summary>
/// Player-specific InitCallerManager to group 
/// Player-related Initializables separately from other Initializables
/// </summary>


[CustomExecutionOrder(-40)]
public class EntityInitCaller : LifecycleManager {
}
