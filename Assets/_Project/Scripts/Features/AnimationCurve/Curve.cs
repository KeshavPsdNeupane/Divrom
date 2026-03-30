using UnityEngine;

namespace Kope.AI {
	[System.Serializable]
	public struct Curve {
		private CurveAsset _asset;
		public Curve(CurveAsset asset) {
			_asset = asset;
		}

		public readonly float Evaluate(float x) {
			if (_asset == null || _asset.sampledValues == null
				|| _asset.sampledValues.Length == 0)
				return x;

			float[] v = _asset.sampledValues;
			int n = v.Length;
			float t = Mathf.Clamp01(x) * (n - 1);
			int lo = Mathf.FloorToInt(t);
			int hi = Mathf.Min(lo + 1, n - 1);
			return Mathf.Lerp(v[lo], v[hi], t - lo);
		}
	}
}