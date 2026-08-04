using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Kope.Feature.PathFindingNew.Utility {

	public static class SpatialBitPacker {
		public const float MIN_COST_MULT = 0.1f;
		public const float MAX_COST_MULT = 10.0f;
		private const uint MASK_10_BIT = 0x3FF;
		private const int LUT_SIZE = 1024;      // 2^10 possible quantized values

		// 4 KB Look-Up Table residing directly in L1 CPU Cache
		private static readonly float[] CostLUT = PrecomputeCostLUT();




		#region Single Position Bit-Packing (Vec2Int <-> long)

		/// <summary>
		/// Bit-packs a 2D integer vector into a single 64-bit signed integer word (<c>long</c>).
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long PackVec2(Vec2Int v) {
			return ((long)v.X << 32) | ((long)v.Y & 0xFFFFFFFFL);
		}

		/// <summary>
		/// Reconstructs a strongly-typed <see cref="Vec2Int"/> coordinate from a 64-bit signed packed integer word.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vec2Int UnpackVec2(long packed) {
			int x = (int)(packed >> 32);
			int y = (int)(packed & 0xFFFFFFFFL);
			return new Vec2Int(x, y);
		}

		/// <summary>
		/// Converts a list of <see cref="Vec2Int"/> positions into a single bit-packed 64-bit <c>long[]</c> stream.
		/// </summary>
		public static long[] PackVec2List(List<Vec2Int> vec2List) {
			int count = vec2List != null ? vec2List.Count : 0;
			long[] array = new long[count];

			for (int i = 0; i < count; i++) {
				array[i] = PackVec2(vec2List[i]);
			}
			return array;
		}

		/// <summary>
		/// Rehydrates a bit-packed 64-bit <c>long[]</c> stream back into a strongly-typed <c>List&lt;Vec2Int&gt;</c>.
		/// </summary>
		public static List<Vec2Int> UnpackVec2List(long[] packedArray) {
			if (packedArray == null) return new List<Vec2Int>();

			int count = packedArray.Length;
			List<Vec2Int> list = new(count);

			for (int i = 0; i < count; i++) {
				list.Add(UnpackVec2(packedArray[i]));
			}
			return list;
		}

		#endregion

		#region Cost Multiplier Bit-Packing & LUT Initialization





		private static float[] PrecomputeCostLUT() {
			float[] lut = new float[LUT_SIZE];
			float scale = (MAX_COST_MULT - MIN_COST_MULT) / (float)MASK_10_BIT;
			for (int i = 0; i < LUT_SIZE; i++) {
				lut[i] = MIN_COST_MULT + (i * scale);
			}
			return lut;
		}

		/// <summary>
		/// Quantizes and bit-packs three cost multipliers within range [<paramref name="minCost"/>, <paramref name="maxCost"/>] into a single 32-bit integer word.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int PackCostMultipliers(float moveCost, float swimCost, float flyCost, float minCost = MIN_COST_MULT, float maxCost = MAX_COST_MULT) {
			uint moveQ = QuantizeCost(moveCost, minCost, maxCost);
			uint swimQ = QuantizeCost(swimCost, minCost, maxCost);
			uint flyQ = QuantizeCost(flyCost, minCost, maxCost);

			return (int)(moveQ | (swimQ << 10) | (flyQ << 20));
		}

		/// <summary>
		/// Unpacks and dequantizes a single 32-bit integer word back into three float cost multipliers.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (float moveCost, float swimCost, float flyCost) UnpackCostMultipliers(int packedInt) {
			uint packed = (uint)packedInt;

			uint moveQ = packed & MASK_10_BIT;
			uint swimQ = (packed >> 10) & MASK_10_BIT;
			uint flyQ = (packed >> 20) & MASK_10_BIT;

			return (
				CostLUT[moveQ],
				CostLUT[swimQ],
				CostLUT[flyQ]
			);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static uint QuantizeCost(float value, float minCost = MIN_COST_MULT, float maxCost = MAX_COST_MULT) {
			float clamped = Mathf.Clamp(value, minCost, maxCost);
			float normalized = (clamped - minCost) / (maxCost - minCost);
			return (uint)Mathf.RoundToInt(normalized * MASK_10_BIT);
		}

		#endregion

		#region DeQuantization Helpers (Single-Channel Ultra-Fast Lookups)

		/// <summary>
		/// Unpacks only the Move cost multiplier using a zero-math L1 cache array lookup.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float UnpackMoveCost(int packedInt) {
			uint moveQ = (uint)packedInt & MASK_10_BIT;
			return CostLUT[moveQ];
		}

		/// <summary>
		/// Unpacks only the Swim cost multiplier using a zero-math L1 cache array lookup.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float UnpackSwimCost(int packedInt) {
			uint swimQ = ((uint)packedInt >> 10) & MASK_10_BIT;
			return CostLUT[swimQ];
		}

		/// <summary>
		/// Unpacks only the Fly cost multiplier using a zero-math L1 cache array lookup.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float UnpackFlyCost(int packedInt) {
			uint flyQ = ((uint)packedInt >> 20) & MASK_10_BIT;
			return CostLUT[flyQ];
		}

		#endregion

		#region Primitive Helpers

		/// <summary>
		/// Converts a C# boolean to a single primitive <c>byte</c> (1 for true, 0 for false).
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte ConvertBoolToByte(bool value) => value ? (byte)1 : (byte)0;

		/// <summary>
		/// Converts a serialized primitive <c>byte</c> back into a C# boolean.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool ConvertByteToBool(byte value) => value != 0;

		#endregion
	}
}