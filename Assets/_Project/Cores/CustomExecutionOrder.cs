using System;
using UnityEngine;

// Custom attribute inheriting DefaultExecutionOrder
[AttributeUsage(AttributeTargets.Class)]
public class CustomExecutionOrderAttribute : DefaultExecutionOrder
{
    public CustomExecutionOrderAttribute(int order) : base(order) { }
}
