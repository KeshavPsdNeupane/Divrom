using System;
using Newtonsoft.Json;

namespace Kope.Core.Collections {
	/// <summary>
	/// A high-performance, memory-efficient value type that encapsulates a continuous progression pipeline.
	/// It accumulates high-frequency floating-point inputs into a robust, unshakeable integer backbone while
	/// preserving fractional precision in a tiny, localized shock-absorber buffer.
	/// </summary>
	/// <remarks>
	/// This design provides structural protection against two classic architectural issues:
	/// <list type="bullet">
	/// <item><description><b>Micro-Truncation Loss:</b> Small values (like 0.1f) are preserved rather than being discarded down to 0.</description></item>
	/// <item><description><b>Macro-Precision Degradation:</b> The primary running value is stored as an integer, eliminating the blind-spots and precision drift common to 32-bit floats at large numbers.</description></item>
	/// </list>
	/// </remarks>
	[Serializable]
	public struct AccumulatorInt {
		[JsonProperty("value")]
		private int _value;
		[JsonProperty("buffer")]
		private float _buffer;

		/// <summary>
		/// The consolidated whole integer representing your core source of truth.
		/// </summary>
		public readonly int Value => _value;

		/// <summary>
		/// The current floating-point residual remainder waiting to accumulate into a whole number.
		/// </summary>
		public readonly float Residual => _buffer;

		/// <summary>
		/// Returns a clean instance starting at an integer value of zero.
		/// </summary>
		public static AccumulatorInt Default => new(0);

		/// <summary>
		/// Initializes a new instance with a starting whole number and a clean decimal buffer.
		/// </summary>
		public AccumulatorInt(int initialValue) {
			this._value = initialValue;
			this._buffer = 0f;
		}

		/// <summary>
		/// Initializes a new instance by processing a float through Banker's Rounding,
		/// assigning the rounded result to the whole integer and capturing the residual discrepancy.
		/// </summary>
		public AccumulatorInt(float initialValue) {
			this._value = RoundBankers(initialValue);
			this._buffer = initialValue - this._value;
		}

		/// <summary>
		/// Explicit constructor mapped precisely for Newtonsoft JSON deserialization.
		/// Parameter names 'value' and 'buffer' must match the JSON property keys exactly 
		/// to support direct allocation without an extra double-rounding performance tax.
		/// </summary>
		[JsonConstructor]
		public AccumulatorInt(int value, float buffer) {
			this._value = value;
			this._buffer = buffer;
		}

		/// <summary>
		/// Clears the floating-point residual tank and resets the source of truth to a specified whole number.
		/// </summary>
		public void Reset(int newValue = 0) {
			this._value = newValue;
			this._buffer = 0f;
		}

		// --- Operator Overloads ---

		/// <summary>
		/// Combines incoming float gains with the existing internal buffer, determines whole number 
		/// thresholds using Banker's Rounding, and yields a fresh data state. Automatically unlocks '+=' syntax.
		/// </summary>
		public static AccumulatorInt operator +(AccumulatorInt current, float amount) {
			if (amount <= 0f) return current;

			float newBuffer = current._buffer + amount;
			int rounded = RoundBankers(newBuffer);

			AccumulatorInt result = new() {
				_value = current._value + rounded,
				_buffer = newBuffer - rounded
			};

			return result;
		}

		/// <summary>
		/// Subtracts incoming float values from the existing internal buffer, processes thresholds 
		/// with Banker's Rounding, and returns a fresh data state. Automatically unlocks '-=' syntax.
		/// </summary>
		public static AccumulatorInt operator -(AccumulatorInt current, float amount) {
			if (amount <= 0f) return current;

			float newBuffer = current._buffer - amount;
			int rounded = RoundBankers(newBuffer);

			AccumulatorInt result = new() {
				_value = current._value + rounded,
				_buffer = newBuffer - rounded
			};

			return result;
		}

		/// <summary>
		/// Standardizes midpoint evaluations using native CPU instructions to implement unbiased statistical rounding.
		/// </summary>
		private static int RoundBankers(float value) {
			return (int)MathF.Round(value, MidpointRounding.ToEven);
		}

		/// <summary>
		/// Implicitly unmasks and casts an AccumulatorInt into a standard 32-bit signed integer.
		/// </summary>
		/// <remarks>
		/// <b>The Compiler Trick:</b> Because this implicit conversion is available, the C# compiler handles
		/// direct logic expressions like <c>if (myAccumulator == myNormalInt)</c> seamlessly without requiring explicit 
		/// <c>==</c> operator overloads or verbose <c>.Value</c> declarations. 
		/// 
		/// When comparing this struct to an ordinary int, the compiler uses this operator to extract the internal 
		/// <c>_value</c> for comparison. This provides a highly useful logical filtering trick: comparison checks 
		/// evaluate ONLY macro-level whole integer changes, completely and safely ignoring micro-level adjustments 
		/// in the decimal buffer tank.
		/// </remarks>
		public static implicit operator int(AccumulatorInt accumulator) => accumulator._value;

		public override readonly string ToString() =>
			$"{this._value} (Remnant: {this._buffer:F2})";
	}
}