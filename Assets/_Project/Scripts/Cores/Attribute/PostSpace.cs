using UnityEngine;
using System;

namespace Kope.Core.Attribute {
	[AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = true)]
	public class PostSpaceAttribute : PropertyAttribute {
		public float spaceHeight;
		public PostSpaceAttribute(float height = 8f) {
			this.spaceHeight = height;
		}
	}
}