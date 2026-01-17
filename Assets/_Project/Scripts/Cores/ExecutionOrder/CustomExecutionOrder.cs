using System;
using UnityEngine;

namespace Kope.Core.Execution
{
    // Custom attribute inheriting DefaultExecutionOrder
    [AttributeUsage(AttributeTargets.Class)]
    public class CustomExecutionOrderAttribute : DefaultExecutionOrder
    {
        public CustomExecutionOrderAttribute(int order) : base(order) { }
    }
}