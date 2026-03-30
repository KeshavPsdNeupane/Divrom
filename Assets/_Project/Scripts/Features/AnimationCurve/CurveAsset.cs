using UnityEngine;

namespace Kope.AI {
	[CreateAssetMenu(menuName = "Kope/Curve Asset")]
	public class CurveAsset : ScriptableObject {
		[HideInInspector]
		public Vector2[] controlPoints = new Vector2[]
		{
			new(0f, 0f),
			new(1f, 1f)
		};

		[HideInInspector] public float[] sampledValues;
		public int resolution = 64;

		public void Bake() {
			if (controlPoints == null || controlPoints.Length < 2) return;

			System.Array.Sort(controlPoints, (a, b) => a.x.CompareTo(b.x));
			sampledValues = new float[resolution];

			for (int i = 0; i < resolution; i++) {
				float t = (float)i / (resolution - 1);
				sampledValues[i] = SampleControlPoints(t);
			}
		}

		private float SampleControlPoints(float x) {
			// Find surrounding control points and cubic Hermite interpolate
			for (int i = 0; i < controlPoints.Length - 1; i++) {
				Vector2 p0 = controlPoints[i];
				Vector2 p1 = controlPoints[i + 1];

				if (x >= p0.x && x <= p1.x) {
					float t = (p1.x - p0.x) < 1e-6f ? 0f :
							  (x - p0.x) / (p1.x - p0.x);

					// Cubic Hermite with Catmull-Rom tangents
					Vector2 m0 = i > 0
						? (p1 - controlPoints[i - 1]) * 0.5f
						: p1 - p0;
					Vector2 m1 = i < controlPoints.Length - 2
						? (controlPoints[i + 2] - p0) * 0.5f
						: p1 - p0;

					float t2 = t * t, t3 = t2 * t;
					float h00 = 2 * t3 - 3 * t2 + 1;
					float h10 = t3 - 2 * t2 + t;
					float h01 = -2 * t3 + 3 * t2;
					float h11 = t3 - t2;

					float span = p1.x - p0.x;
					return h00 * p0.y + h10 * span * m0.y
						 + h01 * p1.y + h11 * span * m1.y;
				}
			}
			return controlPoints[^1].y;
		}
	}
}