using UnityEngine;
using Kope.Core.Attribute;
using System.Diagnostics; // Required for Stopwatch

namespace Kope.AI {
	public class CurveTester : MonoBehaviour {
		[SerializeField] private CurveAsset asset;
		[SerializeField] private AnimationCurve unityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
		[SerializeField, Range(0f, 1f)] private float x;

		[Header("Results")]
		[SerializeField, ReadOnly] public float result;
		[SerializeField, ReadOnly] public float unityResult;

		[Header("Benchmarking (ms)")]
		[SerializeField, ReadOnly] public double customEvalTime;
		[SerializeField, ReadOnly] public double unityEvalTime;

		private Curve _curve;
		private Stopwatch _sw = new();

#if UNITY_EDITOR
		void OnValidate() {
			this._curve = new Curve(asset);
			Evaluate();
		}
#endif

		public void Evaluate() {
			if (this._curve.Equals(default(Curve))) this._curve = new Curve(asset);

			// Measure Custom Curve
			_sw.Restart();
			result = this._curve.Evaluate(x);
			_sw.Stop();
			customEvalTime = _sw.Elapsed.TotalMilliseconds;

			// Measure Unity Curve
			_sw.Restart();
			unityResult = 1 - unityCurve.Evaluate(x);
			_sw.Stop();
			unityEvalTime = _sw.Elapsed.TotalMilliseconds;
		}
	}
}