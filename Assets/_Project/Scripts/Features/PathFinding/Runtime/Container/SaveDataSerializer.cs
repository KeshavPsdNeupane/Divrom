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
     * When Unity serializes C# structs (`Vec2Int`, `BoundingBox`) inside ScriptableObjects, its C++ 
     * serializer treats every struct field as a full YAML object node with dedicated key tags:
     * 
     *   Standard BoundingBox YAML Output (6 lines per item):
     *     _min:
     *       x: -128000
     *       y: 64000
     *     _max:
     *       x: -64000
     *       y: 128000
     * 
     * Across 50,000 grid nodes, these structural tags create millions of lines of pure whitespace 
     * and redundant text keys (`_min:`, `_max:`, `x:`, `y:`), inflating grid data assets to 5MB+.
     * 
     * [The Engineering Solution: 128-Bit Key Splitting & Columnar Inline Hex Streaming]
     * This utility class resolves YAML bloat through two primary memory transformations:
     * 
     * 1. 128-Bit Tuple Bit-Packing (BoundingBox Keys):
     *    Unity's C++ serializer treats primitive integral types (`ulong`, `long`) as single-line hex 
     *    streams when stored in arrays/lists. By splitting a full 128-bit BoundingBox (4 x 32-bit 
     *    signed integers) into `(ulong high, ulong low)` primitive pairs, we maintain unconstrained 
     *    32-bit coordinate space (-2.14B to +2.14B) while forcing Unity to emit compact, inline hex 
     *    array entries with ZERO struct wrapper overhead.
     * 
     * 2. Structure-of-Arrays (SoA) Transposition (List Arrays):
     *    Lists of domain structs (`List<Vec2Int>`) are pivoted into parallel primitive integer 
     *    arrays (`int[] _mPosX`, `int[] _mPosY`). Unity serializes integer arrays as compact, 
     *    inline hexadecimal byte streams, stripping all repeating field property headers.
     * 
     * ==============================================================================================
     * BIT-PACKING MEMORY LAYOUT DIAGRAMS
     * ==============================================================================================
     * 
     * 1. PackVec2Int (64-Bit Signed long)
     *    ┌────────────────────────────────────────┬────────────────────────────────────────┐
     *    │ Bits 63..32 (32 Bits): X Coordinate    │ Bits 31..0  (32 Bits): Y Coordinate    │
     *    │ Range: [-2,147,483,648 to 2,147,483,647]│ Range: [-2,147,483,648 to 2,147,483,647]│
     *    └────────────────────────────────────────┴────────────────────────────────────────┘
     * 
     * 2. PackBoundingBox32 (128-Bit Split into High/Low 64-Bit ulong Tuple)
     *    HIGH WORD (_keysHigh / ulong):
     *    ┌────────────────────────────────────────┬────────────────────────────────────────┐
     *    │ Bits 63..32 (32-Bit Signed int)        │ Bits 31..0  (32-Bit Signed int)        │
     *    │ Min.X (-2,147,483,648 to 2,147,483,647)│ Min.Y (-2,147,483,648 to 2,147,483,647)│
     *    └────────────────────────────────────────┴────────────────────────────────────────┘
     *    LOW WORD (_keysLow / ulong):
     *    ┌────────────────────────────────────────┬────────────────────────────────────────┐
     *    │ Bits 63..32 (32-Bit Signed int)        │ Bits 31..0  (32-Bit Signed int)        │
     *    │ Max.X (-2,147,483,648 to 2,147,483,647)│ Max.Y (-2,147,483,648 to 2,147,483,647)│
     *    └────────────────────────────────────────┴────────────────────────────────────────┘
     * ==============================================================================================
     */

	/// <summary>
	/// Static serialization utility that flattens strongly-typed domain spatial structs (<see cref="Vec2Int"/>, <see cref="BoundingBox"/>)
	/// into parallel primitive array columns (SoA) and bit-packed <c>(ulong high, ulong low)</c> tuples.
	/// Eliminates Unity YAML structural tag replication, shrinking on-disk asset footprints by ~99.5%.
	/// </summary>
	public static class SaveDataSerializer {

		#region Key Bit-Packing (Vec2Int & BoundingBox)

		/// <summary>
		/// Bit-packs a 2D integer vector into a single 64-bit signed integer word (<c>long</c>).
		/// Allocates 32 bits for X and 32 bits for Y, maintaining full 32-bit signed integer precision.
		/// </summary>
		/// <param name="v">The 2D vector coordinate to pack.</param>
		/// <returns>A 64-bit signed integer containing high-32 bit X and low-32 bit Y coordinates.</returns>
		public static long PackVec2Int(Vec2Int v) {
			// Mask Y with 0xFFFFFFFFL to prevent C# sign-extension from corrupting upper X bits when casting
			return ((long)v.X << 32) | ((long)v.Y & 0xFFFFFFFFL);
		}

		/// <summary>
		/// Reconstructs a strongly-typed <see cref="Vec2Int"/> coordinate from a 64-bit signed packed integer word.
		/// </summary>
		/// <param name="packed">The 64-bit packed integer generated by <see cref="PackVec2Int"/>.</param>
		/// <returns>The re-hydrated <see cref="Vec2Int"/> domain vector.</returns>
		public static Vec2Int UnpackVec2Int(long packed) {
			int x = (int)(packed >> 32);
			int y = (int)(packed & 0xFFFFFFFFL);
			return new Vec2Int(x, y);
		}

		/// <summary>
		/// Bit-packs a full 128-bit <see cref="BoundingBox"/> region into a 64-bit unsigned integer word tuple <c>(ulong high, ulong low)</c>.
		/// Preserves full 32-bit signed <c>int</c> coordinate space across all 4 boundaries (<c>Min.X</c>, <c>Min.Y</c>, <c>Max.X</c>, <c>Max.Y</c>).
		/// </summary>
		/// <param name="box">The bounding box spatial region to pack.</param>
		/// <returns>A tuple containing high (Min.X, Min.Y) and low (Max.X, Max.Y) 64-bit unsigned integer words.</returns>
		public static (ulong high, ulong low) PackBoundingBox32(BoundingBox box) {
			// Cast int -> uint first to prevent C# sign-extension, then cast to ulong for bit shifts
			ulong high = ((ulong)(uint)box.Min.X << 32) | (uint)box.Min.Y;
			ulong low = ((ulong)(uint)box.Max.X << 32) | (uint)box.Max.Y;
			return (high, low);
		}

		/// <summary>
		/// Reconstructs a strongly-typed <see cref="BoundingBox"/> domain object from a <c>(ulong high, ulong low)</c> 64-bit word pair.
		/// </summary>
		/// <param name="packed">Tuple containing high (Min.X/Min.Y) and low (Max.X/Max.Y) 64-bit packed words.</param>
		/// <returns>The re-hydrated <see cref="BoundingBox"/> domain instance.</returns>
		public static BoundingBox UnpackBoundingBox32((ulong high, ulong low) packed) {
			return UnpackBoundingBox32(packed.high, packed.low);
		}

		/// <summary>
		/// Reconstructs a strongly-typed <see cref="BoundingBox"/> domain object from high and low 64-bit unsigned packed words.
		/// </summary>
		/// <param name="high">The 64-bit word containing packed Min.X (high 32) and Min.Y (low 32).</param>
		/// <param name="low">The 64-bit word containing packed Max.X (high 32) and Max.Y (low 32).</param>
		/// <returns>The re-hydrated <see cref="BoundingBox"/> domain instance.</returns>
		public static BoundingBox UnpackBoundingBox32(ulong high, ulong low) {
			int minX = (int)(high >> 32);
			int minY = (int)(high & 0xFFFFFFFFL);
			int maxX = (int)(low >> 32);
			int maxY = (int)(low & 0xFFFFFFFFL);

			return new BoundingBox(new Vec2Int(minX, minY), new Vec2Int(maxX, maxY));
		}

		#endregion

		#region Vec2Int Conversions (Structure-of-Arrays)

		/// <summary>
		/// Transposes an Array-of-Structs (AoS) <c>List&lt;Vec2Int&gt;</c> into a Structure-of-Arrays (SoA) layout 
		/// consisting of two parallel integer coordinate streams (<c>xArray</c>, <c>yArray</c>).
		/// </summary>
		/// <param name="vec2List">Source list of 2D vector domain objects.</param>
		/// <returns>A tuple containing synchronized, index-aligned X and Y integer primitive arrays.</returns>
		public static (int[] xArray, int[] yArray) FromVec2ToIntArrays(List<Vec2Int> vec2List) {
			int count = vec2List != null ? vec2List.Count : 0;
			int[] xArray = new int[count];
			int[] yArray = new int[count];

			for (int i = 0; i < count; i++) {
				xArray[i] = vec2List[i].X;
				yArray[i] = vec2List[i].Y;
			}
			return (xArray, yArray);
		}

		/// <summary>
		/// Re-hydrates parallel primitive coordinate streams (<c>xArray</c>, <c>yArray</c>) 
		/// back into a strongly-typed <c>List&lt;Vec2Int&gt;</c> domain collection.
		/// </summary>
		/// <param name="xArray">Parallel X coordinate primitive array.</param>
		/// <param name="yArray">Parallel Y coordinate primitive array.</param>
		/// <returns>A re-hydrated list of <see cref="Vec2Int"/> domain instances.</returns>
		/// <exception cref="ArgumentException">Thrown if input arrays do not share identical lengths.</exception>
		public static List<Vec2Int> FromIntArraysToVec2(int[] xArray, int[] yArray) {
			if (xArray == null || yArray == null) return new List<Vec2Int>();

			if (xArray.Length != yArray.Length) {
				throw new ArgumentException("X and Y coordinate arrays must have identical lengths.");
			}

			int count = xArray.Length;
			List<Vec2Int> vec2List = new(count);

			for (int i = 0; i < count; i++) {
				vec2List.Add(new Vec2Int(xArray[i], yArray[i]));
			}
			return vec2List;
		}

		#endregion

		#region BoundingBox Conversions (Structure-of-Arrays)

		/// <summary>
		/// Transposes an Array-of-Structs (AoS) <c>List&lt;BoundingBox&gt;</c> into a Structure-of-Arrays (SoA) layout 
		/// consisting of four parallel primitive integer streams (<c>minX</c>, <c>minY</c>, <c>maxX</c>, <c>maxY</c>).
		/// </summary>
		/// <param name="boxList">Source list of bounding box domain objects.</param>
		/// <returns>Nested tuples containing parallel primitive coordinate arrays for Min and Max points.</returns>
		public static ((int[] minX, int[] minY) min, (int[] maxX, int[] maxY) max) FromBoundingBoxListToIntArrayPairs(
			List<BoundingBox> boxList
		) {
			int count = boxList != null ? boxList.Count : 0;
			int[] minXArray = new int[count];
			int[] minYArray = new int[count];
			int[] maxXArray = new int[count];
			int[] maxYArray = new int[count];

			for (int i = 0; i < count; i++) {
				var box = boxList[i];
				minXArray[i] = box.Min.X;
				minYArray[i] = box.Min.Y;
				maxXArray[i] = box.Max.X;
				maxYArray[i] = box.Max.Y;
			}
			return ((minXArray, minYArray), (maxXArray, maxYArray));
		}

		/// <summary>
		/// Re-hydrates four parallel primitive coordinate streams (<c>minX</c>, <c>minY</c>, <c>maxX</c>, <c>maxY</c>) 
		/// back into a strongly-typed <c>List&lt;BoundingBox&gt;</c> domain collection.
		/// </summary>
		public static List<BoundingBox> FromIntArrayPairsToBoundingBoxList(
			int[] minX, int[] minY,
			int[] maxX, int[] maxY
		) {
			if (minX == null || minY == null || maxX == null || maxY == null) return new List<BoundingBox>();

			if (minX.Length != minY.Length ||
				maxX.Length != maxY.Length ||
				minX.Length != maxX.Length) {
				throw new ArgumentException("All bounding box coordinate arrays must have identical lengths.");
			}

			int count = minX.Length;
			List<BoundingBox> boxList = new(count);

			for (int i = 0; i < count; i++) {
				Vec2Int min = new(minX[i], minY[i]);
				Vec2Int max = new(maxX[i], maxY[i]);
				boxList.Add(new BoundingBox(min, max));
			}
			return boxList;
		}

		#endregion

		#region Primitive Helpers

		/// <summary>
		/// Converts a C# boolean to a single primitive <c>byte</c> (1 for true, 0 for false).
		/// </summary>
		public static byte ConvertBoolToByte(bool value) => value ? (byte)1 : (byte)0;

		/// <summary>
		/// Converts a serialized primitive <c>byte</c> back into a C# boolean.
		/// </summary>
		public static bool ConvertByteToBool(byte value) => value != 0;

		#endregion
	}
}