using UnityEngine;

namespace Kope.Core.Mathfx {
	public static class Mathfx {
		const float DIRECTION_LOWER_EPSILON = 0.0001f;
		const float DIRECTION_UPPER_EPSILON = 0.1f;

		/// <summary>
		/// Projects a local 2D offset vector into world space by constructing a coordinate basis from a forward vector.
		/// Takes a local coordinate vector and calculates its world-space equivalent by treating the provided forward direction 
		/// as the local Y-axis and its perpendicular component as the local X-axis, scaling both axes by the offset values.
		/// </summary>
		/// <param name="forward">The normalized 2D forward basis vector.</param>
		/// <param name="localOffset">The local coordinate vector (x = right/perpendicular, y = forward).</param>
		public static Vector2 TransformOffset2D(Vector2 forward, Vector2 localOffset) {
			/*
                Linear Algebra Basis Transformation:
                We reconstruct the local coordinate system axes directly from the forward unit vector:
                    Local Y (Forward Axis) = (forward.x, forward.y)
                    Local X (Right Axis)   = (forward.y, -forward.x) -> Perpendicular 2D vector
                
                The world space vector is calculated by scaling these basis axes by the local components:
                    World Vector = (Right Axis * localOffset.x) + (Forward Axis * localOffset.y)
            */
			float worldX = (forward.x * localOffset.y) + (forward.y * localOffset.x);
			float worldY = (forward.y * localOffset.y) - (forward.x * localOffset.x);
			return new Vector2(worldX, worldY);
		}

		/// <summary>
		/// Projects a local 3D offset vector into world space by constructing a coordinate basis via vector cross products.
		/// Takes a local 3D displacement vector and calculates its world-space equivalent by extracting orthogonal 
		/// Right and Up basis axes relative to the forward direction, scaling each calculated axis by the local offset values.
		/// </summary>
		/// <param name="forward">The normalized 3D forward basis vector.</param>
		/// <param name="localOffset">The local coordinate vector (x = right, y = up, z = forward).</param>
		public static Vector3 TransformOffset3D(Vector3 forward, Vector3 localOffset) {
			// Reconstruct a stable perpendicular basis system using a fallback axis to avoid singularity locks
			Vector3 fallbackUp = (Mathf.Abs(forward.y) > 0.9f) ? Vector3.right : Vector3.up;

			Vector3 right = Vector3.Cross(fallbackUp, forward).normalized;
			Vector3 up = Vector3.Cross(forward, right);

			// Linearly combine the constructed basis axes scaled by the local offset components
			return (right * localOffset.x) + (up * localOffset.y) + (forward * localOffset.z);
		}


		/// <summary>
		/// Translates an origin point by a local offset vector mapped onto a normalized 2D forward basis.
		/// Takes a world origin position and appends a local offset after transforming it into world space, 
		/// using the 2D forward vector to orient the offset directions along the horizontal ground plane.
		/// <param name="origin">The world space position to be translated.</param>
		/// <param name="forward">The normalized 2D forward direction used to orient the offset.</param>
		/// <param name="offset">The local offset vector where x = right/perpendicular and y = forward.</param>
		/// </summary>
		public static Vector3 GetRelativePosition2D(Vector3 origin, Vector2 forward, Vector2 offset) {
			Vector2 worldOffset = TransformOffset2D(forward, offset);
			return new Vector3(origin.x + worldOffset.x, origin.y + worldOffset.y, origin.z);
		}

		/// <summary>
		/// Translates an origin point by a local offset vector mapped onto an unnormalized 2D forward basis.
		/// Safely normalizes the raw heading vector before computing the orientation, transforming the local offset 
		/// into a world offset, and shifting the world origin point by that final vector.
		/// <param name="origin">The world space position to be translated.</param>
		/// <param name="forward">The unnormalized 2D forward direction used to orient the offset.</param>
		/// <param name="offset">The local offset vector where x = right/perpendicular and y = forward.</param>
		/// </summary>
		public static Vector3 GetRelativePosition2DUnnormalized(Vector3 origin, Vector2 forward, Vector2 offset) {
			return GetRelativePosition2D(origin, forward.normalized, offset);
		}

		/// <summary>
		/// Translates an origin point by a local offset vector mapped onto a normalized 3D forward basis.
		/// Takes a world origin position and adds a local 3D displacement after it has been fully rotated 
		/// into world space relative to the provided 3D forward direction system.
		/// <param name="origin">The world space position to be translated.</param>
		/// <param name="forward">The normalized 3D forward direction used to orient the offset.</param>
		/// <param name="offset">The local offset vector where x = right, y = up, and z = forward.</param>
		/// </summary>
		public static Vector3 GetRelativePosition3D(Vector3 origin, Vector3 forward, Vector3 offset) {
			return origin + TransformOffset3D(forward, offset);
		}

		/// <summary>
		/// Translates an origin point by a local offset vector mapped onto an unnormalized 3D forward basis.
		/// Safely normalizes the raw 3D input heading to guarantee linear precision, resolves the oriented world offset 
		/// through basis projection, and translates the world origin position by that result.
		/// <param name="origin">The world space position to be translated.</param>
		/// <param name="forward">The unnormalized 3D forward direction used to orient the offset.</param>
		/// <param name="offset">The local offset vector where x = right, y = up, and z = forward.</param>
		/// </summary>
		public static Vector3 GetRelativePosition3DUnnormalized(Vector3 origin, Vector3 forward, Vector3 offset) {
			return GetRelativePosition3D(origin, forward.normalized, offset);
		}
	}
}