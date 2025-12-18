using System;

/// <summary>
/// Marks a field or property to be automatically injected by InitManager.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class InjectAttribute : Attribute
{
    /// <summary>
    /// If true, missing dependency will not throw an error.
    /// Useful for optional services.
    /// </summary>
    public bool Optional { get; set; } = false;
}
