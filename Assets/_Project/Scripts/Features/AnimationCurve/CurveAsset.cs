using UnityEngine;

namespace Kope.AI {
	[System.Serializable]
	public struct BezierPoint {
		public Vector2 pos;
		public Vector2 hIn;  // Local offset
		public Vector2 hOut; // Local offset

		public BezierPoint(Vector2 position) {
			pos = position;
			hIn = new Vector2(-0.1f, 0);
			hOut = new Vector2(0.1f, 0);
		}
	}

	[CreateAssetMenu(menuName = "Kope/AI Bezier Asset")]
	public class CurveAsset : ScriptableObject {
		public BezierPoint[] points = new BezierPoint[] {
			new(new Vector2(0, 0)),
			new(new Vector2(1, 1))
		};

		[HideInInspector] public float[] sampledValues;
		[HideInInspector] public string lastBakeTime = "Never";
		public int resolution = 32; // Default for AI

		public void Bake(bool isAuto = false) {
			if (points == null || points.Length < 2) return;

			// Ensure points stay in chronological order
			System.Array.Sort(points, (a, b) => a.pos.x.CompareTo(b.pos.x));

			sampledValues = new float[resolution];
			for (int i = 0; i < resolution; i++) {
				float x = (float)i / (resolution - 1);
				sampledValues[i] = SampleBezier(x);
			}

			if (!isAuto) lastBakeTime = System.DateTime.Now.ToString("HH:mm:ss");
		}

		private float SampleBezier(float x) {
			for (int i = 0; i < points.Length - 1; i++) {
				BezierPoint p0 = points[i];
				BezierPoint p1 = points[i + 1];

				if (x < p0.pos.x || x > p1.pos.x) continue;

				// Binary search for t that gives us the target x
				float t = BinarySearchT(p0, p1, x);

				// Now evaluate y at that t
				float invT = 1f - t;
				float b0 = invT * invT * invT;
				float b1 = 3f * invT * invT * t;
				float b2 = 3f * invT * t * t;
				float b3 = t * t * t;

				return b0 * p0.pos.y
					 + b1 * (p0.pos.y + p0.hOut.y)
					 + b2 * (p1.pos.y + p1.hIn.y)
					 + b3 * p1.pos.y;
			}
			return points[^1].pos.y;
		}

		private float BinarySearchT(BezierPoint p0, BezierPoint p1, float targetX, int iterations = 16) {
			float lo = 0f, hi = 1f, t = 0.5f;

			for (int i = 0; i < iterations; i++) {
				float invT = 1f - t;
				float b0 = invT * invT * invT;
				float b1 = 3f * invT * invT * t;
				float b2 = 3f * invT * t * t;
				float b3 = t * t * t;

				float cx = b0 * p0.pos.x
						 + b1 * (p0.pos.x + p0.hOut.x)
						 + b2 * (p1.pos.x + p1.hIn.x)
						 + b3 * p1.pos.x;

				if (Mathf.Abs(cx - targetX) < 1e-5f) break;

				if (cx < targetX) lo = t;
				else hi = t;

				t = (lo + hi) * 0.5f;
			}

			return t;
		}
	}
}