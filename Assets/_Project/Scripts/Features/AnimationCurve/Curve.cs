using UnityEngine;

namespace Kope.AI {
	[System.Serializable]
	public struct Curve {
		[SerializeField] private CurveAsset _asset;
		public Curve(CurveAsset asset) => _asset = asset;

		public readonly float Evaluate(float x) {
			if (_asset == null || _asset.sampledValues == null || _asset.sampledValues.Length < 4)
				return x;

			float[] v = _asset.sampledValues;
			float t = Mathf.Clamp01(x) * (v.Length - 1);
			int i = Mathf.FloorToInt(t);
			float f = t - i;

			// 4-Point Cubic Interpolation
			int i0 = Mathf.Max(0, i - 1);
			int i1 = i;
			int i2 = Mathf.Min(v.Length - 1, i + 1);
			int i3 = Mathf.Min(v.Length - 1, i + 2);

			float p0 = v[i0], p1 = v[i1], p2 = v[i2], p3 = v[i3];

			return 0.5f * (
				(2f * p1) +
				(-p0 + p2) * f +
				(2f * p0 - 5f * p1 + 4f * p2 - p3) * f * f +
				(-p0 + 3f * p1 - 3f * p2 + p3) * f * f * f
			);
		}
	}
}