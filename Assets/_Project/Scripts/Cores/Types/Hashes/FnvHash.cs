using System;
namespace Kope.Core.Types.Hashes {

	public static class FnvHash {
		private const int FNV_PRIME = 16777619;
		private const int FNV_OFFSET = unchecked((int)2166136261u);

		public static int Compute(string input) {
			unchecked {
				int hash = FNV_OFFSET;
				for (int i = 0; i < input.Length; i++) {
					hash = (hash ^ input[i]) * FNV_PRIME;
				}
				return hash;
			}
		}
		/// <summary>
		/// Folds the FNV-1a hash of <paramref name="input"/> into [<paramref name="min"/>, <paramref name="max"/>] inclusive.
		/// </summary>
		public static int ComputeInRange(string input, int min, int max) {
			int hash = Compute(input);
			// Use Math.Abs to ensure the modulo result is positive for the range calculation
			int range = max - min + 1;
			return (Math.Abs(hash) % range) + min;
		}
	}
}