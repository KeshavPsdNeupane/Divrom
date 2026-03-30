using System;

namespace Kope.Component.Interfaces
{
    public enum InterruptPriority { Soft, Hard, Death }


    /// <summary>
    /// Interface for components that can interrupt other processes or actions.
    /// This interface defines a contract for interruptible behavior,
    /// allowing implementing classes to signal when an interrupt is requested.
    /// For now it is used in AIExecutor to interrupt current action like getting hit or death.
    /// Defining here to avoid circular dependency between Component and UtilityAi namespaces.
    /// And also because this interface is very generic and can be used by other systems in the future.
    /// Not just AIExecutor.
    /// </summary>
    public interface IInterruptOther
    {
        /// <summary>
        /// Event that is triggered when an interrupt is requested.
        /// </summary>
        event Action<InterruptPriority> OnInterruptRequested;
    }
}
