using UnityEngine;

namespace Kope.Core {
	/// <summary>
	/// this vector 2 implementation is used for
	/// save file serialization, because unity's Vector2 and Vector3 are bloated 
	/// for the save system.
	/// </summary>
	public struct Vec2 {
		public float x, y;
		public Vec2(float x, float y) {
			this.x = x;
			this.y = y;
		}
		public Vec2(Vector2 v) {
			this.x = v.x;
			this.y = v.y;
		}
		public readonly Vector2 ToVector2() {
			return new Vector2(x, y);
		}
	}
	/// <summary>
	/// this vector 3 implementation is used for
	/// save file serialization, because unity's Vector2 and Vector3 are bloated 
	/// for the save system.
	/// </summary>
	public struct Vec3 {
		public float x, y, z;
		public Vec3(float x, float y, float z) {
			this.x = x;
			this.y = y;
			this.z = z;
		}
		public Vec3(Vector3 v) {
			this.x = v.x;
			this.y = v.y;
			this.z = v.z;
		}
		public readonly Vector3 ToVector3() {
			return new Vector3(x, y, z);
		}
	}
}
