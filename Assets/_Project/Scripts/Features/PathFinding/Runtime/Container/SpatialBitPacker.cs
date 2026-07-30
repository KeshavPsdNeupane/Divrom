using System;
using System.Collections.Generic;
using Kope.Feature.PathFinding.Node;

namespace Kope.Feature.PathFinding.Data {

	/*
     * ==============================================================================================
     * ARCHITECTURAL RATIONALE: BIT-PACKING & PRIMITIVE COLUMNAR SERIALIZATION
     * ==============================================================================================
     * 
     * [The Core Problem: Unity YAML ASCII Markup Explosion]
     * Unity serializes standard spatial structs (`Vec2Int`, `BoundingBox`) into multiple YAML property tags,
     * inflating asset footprint and creating millions of lines of pure whitespace.
     * 
     * [The Engineering Solution: 64-Bit & 128-Bit Bit-Packing]
     * This utility class flattens domain spatial structs into compact `long` (64-bit) and dual `long` (128-bit) primitives:
     * 
     * 1. PackVec2 (64-Bit Signed long):
     *    Packs 32-bit X and 32-bit Y coordinates into a single 64-bit `long`. Unity serializes `long[]` arrays 
     *    as single-line hex byte streams.
     * 
     * 2. PackBoundingBox (2 x 64-Bit Signed long):
     *    Packs a full `BoundingBox` into two 64-bit `long` words (`minPacked` and `maxPacked`), replacing 
     *    four `int[]` parallel arrays with two compact `long[]` primitive streams.
     * 
     * ==============================================================================================
     * BIT-PACKING MEMORY LAYOUT DIAGRAMS
     * ==============================================================================================
     * 
     * 1. PackVec2 (64-Bit Signed long) -> 1 long per position
     *    ┌────────────────────────────────────────┬────────────────────────────────────────┐
     *    │ Bits 63..32 (32 Bits): X Coordinate    │ Bits 31..0  (32 Bits): Y Coordinate    │
     *    │ Range: [-2,147,483,648 to 2,147,483,647]│ Range: [-2,147,483,648 to 2,147,483,647]│
     *    └────────────────────────────────────────┴────────────────────────────────────────┘
     * 
     * 2. PackBoundingBox (2 x 64-Bit Signed long) -> 2 longs per region
     *    WORD 1 (_minPos / long):
     *    ┌────────────────────────────────────────┬────────────────────────────────────────┐
     *    │ Bits 63..32: Min.X (-2.14B to +2.14B)  │ Bits 31..0: Min.Y (-2.14B to +2.14B)  │
     *    └────────────────────────────────────────┴────────────────────────────────────────┘
     *    WORD 2 (_maxPos / long):
     *    ┌────────────────────────────────────────┬────────────────────────────────────────┐
     *    │ Bits 63..32: Max.X (-2.14B to +2.14B)  │ Bits 31..0: Max.Y (-2.14B to +2.14B)  │
     *    └────────────────────────────────────────┴────────────────────────────────────────┘
     * ==============================================================================================
     */

	/// <summary>
	/// Static serialization utility that flattens strongly-typed domain spatial structs (<see cref="Vec2Int"/>, <see cref="BoundingBox"/>)
	/// into packed 64-bit single-<c>long</c> and dual-<c>long</c> primitive arrays.
	/// </summary>
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

		#region BoundingBox Bit-Packing (BoundingBox <-> 2 Longs)

		/// <summary>
		/// Bit-packs a full <see cref="BoundingBox"/> into a pair of 64-bit signed long words <c>(long minPacked, long maxPacked)</c>.
		/// </summary>
		/// <param name="box">Domain bounding box region.</param>
		/// <returns>A tuple containing packed min position and packed max position.</returns>
		public static (long minPacked, long maxPacked) PackBoundingBox(BoundingBox box) {
			return (PackVec2(box.Min), PackVec2(box.Max));
		}

		/// <summary>
		/// Reconstructs a strongly-typed <see cref="BoundingBox"/> domain object from a dual 64-bit signed long word pair.
		/// </summary>
		/// <param name="minPacked">Packed min vector.</param>
		/// <param name="maxPacked">Packed max vector.</param>
		/// <returns>Reconstructed bounding box.</returns>
		public static BoundingBox UnpackBoundingBox(long minPacked, long maxPacked) {
			return new BoundingBox(UnpackVec2(minPacked), UnpackVec2(maxPacked));
		}

		/// <summary>
		/// Bit-packs a full 128-bit <see cref="BoundingBox"/> region into a 64-bit unsigned integer word tuple <c>(ulong high, ulong low)</c>.
		/// Designed specifically for zero-allocation dictionary key indexing and spatial hashing.
		/// </summary>
		/// <param name="box">Domain bounding box region.</param>
		/// <returns>Unsigned 128-bit composite key split across two ulong words.</returns>
		public static (ulong high, ulong low) PackBoundingBoxUnsigned(BoundingBox box) {
			// Double cast (ulong)(uint) guarantees raw bit pattern preservation without signed integer sign-extension
			ulong high = ((ulong)(uint)box.Min.X << 32) | (uint)box.Min.Y;
			ulong low = ((ulong)(uint)box.Max.X << 32) | (uint)box.Max.Y;
			return (high, low);
		}

		/// <summary>
		/// Reconstructs a strongly-typed <see cref="BoundingBox"/> domain object from high and low 64-bit unsigned packed words.
		/// </summary>
		/// <param name="high">High word containing Min coordinates.</param>
		/// <param name="low">Low word containing Max coordinates.</param>
		/// <returns>Reconstructed bounding box.</returns>
		public static BoundingBox UnpackBoundingBoxUnsigned(ulong high, ulong low) {
			// Extract signed 32-bit components directly from unsigned raw bit streams
			int minX = (int)(high >> 32);
			int minY = (int)(high & 0xFFFFFFFFL);
			int maxX = (int)(low >> 32);
			int maxY = (int)(low & 0xFFFFFFFFL);

			return new BoundingBox(new Vec2Int(minX, minY), new Vec2Int(maxX, maxY));
		}

		/// <summary>
		/// Transposes a list of <see cref="BoundingBox"/> objects into dual parallel 64-bit <c>long[]</c> streams (<c>minPos</c>, <c>maxPos</c>).
		/// Flattens structure-of-arrays for columnar serialization.
		/// </summary>
		/// <param name="boxList">List of domain bounding boxes.</param>
		/// <returns>Parallel min and max packed arrays.</returns>
		public static (long[] minPos, long[] maxPos) PackBoundingBoxList(List<BoundingBox> boxList) {
			int count = boxList != null ? boxList.Count : 0;

			// Allocate dual parallel primitive buffers
			long[] minArray = new long[count];
			long[] maxArray = new long[count];

			for (int i = 0; i < count; i++) {
				var box = boxList[i];
				minArray[i] = PackVec2(box.Min);
				maxArray[i] = PackVec2(box.Max);
			}
			return (minArray, maxArray);
		}

		/// <summary>
		/// Rehydrates dual parallel 64-bit <c>long[]</c> streams (<c>minPos</c>, <c>maxPos</c>) 
		/// back into a strongly-typed <c>List&lt;BoundingBox&gt;</c> domain collection.
		/// </summary>
		/// <param name="minPos">Packed min vector stream.</param>
		/// <param name="maxPos">Packed max vector stream.</param>
		/// <returns>Decoded list of bounding box regions.</returns>
		/// <exception cref="ArgumentException">Thrown when minPos and maxPos array lengths mismatch.</exception>
		public static List<BoundingBox> UnpackBoundingBoxList(long[] minPos, long[] maxPos) {
			if (minPos == null || maxPos == null) return new List<BoundingBox>();

			// Validate array alignment to prevent out-of-sync spatial corruption
			if (minPos.Length != maxPos.Length) {
				throw new ArgumentException("Min and Max position arrays must have identical lengths.");
			}

			int count = minPos.Length;
			List<BoundingBox> list = new(count);

			for (int i = 0; i < count; i++) {
				list.Add(UnpackBoundingBox(minPos[i], maxPos[i]));
			}
			return list;
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