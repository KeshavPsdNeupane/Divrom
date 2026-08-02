using System.Collections.Generic;
using UnityEngine;

namespace Kope.Feature.PathFindingNew.Utility {

	public static class SpatialBitPacker {

		#region Single Position Bit-Packing (Vec2Int <-> long)

		/// <summary>
		/// Bit-packs a 2D integer vector into a single 64-bit signed integer word (<c>long</c>).
		/// </summary>
		/// <param name="v">The 2D vector coordinate to pack.</param>
		/// <returns>A 64-bit long containing X in high 32 bits and Y in low 32 bits.</returns>
		public static long PackVec2(Vec2Int v) {
			// 1. Cast v.X to long first to ensure high-word arithmetic shift doesn't truncate in 32-bit registers.
			// 2. Shift X left by 32 bits to occupy bits 63..32.
			// 3. Mask v.Y with 0xFFFFFFFFL (32 set bits) to prevent negative sign extension from bleeding into the top 32 bits.
			// 4. Combine high and low 32-bit segments using bitwise OR (|).
			return ((long)v.X << 32) | ((long)v.Y & 0xFFFFFFFFL);
		}

		/// <summary>
		/// Reconstructs a strongly-typed <see cref="Vec2Int"/> coordinate from a 64-bit signed packed integer word.
		/// </summary>
		/// <param name="packed">The packed 64-bit word.</param>
		/// <returns>Rehydrated domain vector.</returns>
		public static Vec2Int UnpackVec2(long packed) {
			// Shift right by 32 bits to bring high bits down to low 32 positions.
			// Explicit cast to int truncates upper bits and restores signed 2's complement value for X.
			int x = (int)(packed >> 32);

			// Mask lower 32 bits using 0xFFFFFFFFL, then cast to int to restore signed 2's complement value for Y.
			int y = (int)(packed & 0xFFFFFFFFL);

			return new Vec2Int(x, y);
		}

		/// <summary>
		/// Converts a list of <see cref="Vec2Int"/> positions into a single bit-packed 64-bit <c>long[]</c> stream.
		/// </summary>
		/// <param name="vec2List">Source vector list.</param>
		/// <returns>Packed primitive array for compact serialization.</returns>
		public static long[] PackVec2List(List<Vec2Int> vec2List) {
			int count = vec2List != null ? vec2List.Count : 0;

			// Pre-allocate destination array to avoid GC re-allocations during iteration
			long[] array = new long[count];

			for (int i = 0; i < count; i++) {
				array[i] = PackVec2(vec2List[i]);
			}
			return array;
		}

		/// <summary>
		/// Rehydrates a bit-packed 64-bit <c>long[]</c> stream back into a strongly-typed <c>List&lt;Vec2Int&gt;</c>.
		/// </summary>
		/// <param name="packedArray">Source array of packed 64-bit integers.</param>
		/// <returns>Decoded vector list.</returns>
		public static List<Vec2Int> UnpackVec2List(long[] packedArray) {
			if (packedArray == null) return new List<Vec2Int>();

			int count = packedArray.Length;

			// Pre-allocate List capacity to avoid internal array resizing
			List<Vec2Int> list = new(count);

			for (int i = 0; i < count; i++) {
				list.Add(UnpackVec2(packedArray[i]));
			}
			return list;
		}

		#endregion

		#region Cost Multiplier Bit-Packing (3 x float [minCost, maxCost] <-> int32)

		private const float MIN_COST = 0.1f;
		private const float MAX_COST = 10.0f;
		private const uint MASK_10_BIT = 0x3FF; // 1023 in binary (10 bits)

		/// <summary>
		/// Quantizes and bit-packs three cost multipliers within range [<paramref name="minCost"/>, <paramref name="maxCost"/>] into a single 32-bit integer word.
		/// <para>Allocation Layout: Bits 0–9 = Move, Bits 10–19 = Swim, Bits 20–29 = Fly (Bits 30–31 Unused).</para>
		/// <para>Precision step resolution: <c>(maxCost - minCost) / 1023</c> per channel (~0.0097 step accuracy with default 0.1–10.0 range).</para>
		/// </summary>
		/// <param name="moveCost">Ground movement cost multiplier.</param>
		/// <param name="swimCost">Water movement cost multiplier.</param>
		/// <param name="flyCost">Air movement cost multiplier.</param>
		/// <param name="minCost">Minimum expected cost boundary (defaults to 0.1).</param>
		/// <param name="maxCost">Maximum expected cost boundary (defaults to 10.0).</param>
		/// <returns>A single 32-bit packed integer containing all 3 cost values.</returns>
		public static int PackCostMultipliers(float moveCost, float swimCost, float flyCost, float minCost = MIN_COST, float maxCost = MAX_COST) {
			uint moveQ = QuantizeCost(moveCost, minCost, maxCost);
			uint swimQ = QuantizeCost(swimCost, minCost, maxCost);
			uint flyQ = QuantizeCost(flyCost, minCost, maxCost);

			// Shift and combine into 30 bits total
			uint packed = moveQ | (swimQ << 10) | (flyQ << 20);
			return (int)packed;
		}

		/// <summary>
		/// Unpacks and dequantizes a single 32-bit integer word back into three float cost multipliers using range [<paramref name="minCost"/>, <paramref name="maxCost"/>].
		/// </summary>
		/// <param name="packedInt">The 32-bit packed integer word.</param>
		/// <param name="minCost">Minimum cost boundary used during packing (defaults to 0.1).</param>
		/// <param name="maxCost">Maximum cost boundary used during packing (defaults to 10.0).</param>
		/// <returns>Decoded tuple containing (moveCost, swimCost, flyCost).</returns>
		public static (float moveCost, float swimCost, float flyCost) UnpackCostMultipliers(int packedInt, float minCost = MIN_COST, float maxCost = MAX_COST) {
			uint packed = (uint)packedInt;

			// Extract each 10-bit segment via masking
			uint moveQ = packed & MASK_10_BIT;
			uint swimQ = (packed >> 10) & MASK_10_BIT;
			uint flyQ = (packed >> 20) & MASK_10_BIT;

			return (
				DequantizeCost(moveQ, minCost, maxCost),
				DequantizeCost(swimQ, minCost, maxCost),
				DequantizeCost(flyQ, minCost, maxCost)
			);
		}

		// Quantizes float [minCost, maxCost] -> 10-bit uint [0, 1023]
		private static uint QuantizeCost(float value, float minCost = MIN_COST, float maxCost = MAX_COST) {
			float clamped = Mathf.Clamp(value, minCost, maxCost);
			float normalized = (clamped - minCost) / (maxCost - minCost);
			return (uint)Mathf.RoundToInt(normalized * MASK_10_BIT);
		}

		// Dequantizes 10-bit uint [0, 1023] -> float [minCost, maxCost]
		private static float DequantizeCost(uint quantized, float minCost = MIN_COST, float maxCost = MAX_COST) {
			float normalized = (float)quantized / MASK_10_BIT;
			return minCost + (normalized * (maxCost - minCost));
		}

		#endregion

		#region Primitive Helpers

		/// <summary>
		/// Converts a C# boolean to a single primitive <c>byte</c> (1 for true, 0 for false).
		/// Replaces multi-byte YAML boolean strings with a single byte value.
		/// </summary>
		public static byte ConvertBoolToByte(bool value) => value ? (byte)1 : (byte)0;

		/// <summary>
		/// Converts a serialized primitive <c>byte</c> back into a C# boolean.
		/// </summary>
		public static bool ConvertByteToBool(byte value) => value != 0;

		#endregion
	}
}