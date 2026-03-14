using UnityEngine;

namespace Kope.AI.Utility
{
	public abstract class ConsiderationSO : ScriptableObject
	{
		public abstract string ConsiderationName { get; }

		/// <summary>
		/// Evaluates the consideration to determine its contribution to the utility score.
		/// <para>
		/// Returns a tuple containing the evaluated score and the updated total multiplication count.
		/// The multiplication count is used for compensated utility calculations to normalize the score 
		/// as more considerations are factored in.
		/// </para>
		/// <remarks>
		/// Guidelines for implementation:
		/// <list type="bullet">
		/// <item>
		/// <description>Increment <paramref name="totalMultiplicationCount"/> if this consideration returns a 
		/// normalized value (0-1) that is multiplied into the final utility score.</description>
		/// </item>
		/// <item>
		/// <description>Do NOT increment the count if the consideration returns a constant or additive value 
		/// that does not scale with the number of factors (e.g., a base priority boost).</description>
		/// </item>
		/// </list>
		/// </remarks>
		/// </summary>
		/// <param name="context">The read-only AI context containing current world and entity state.</param>
		/// <param name="totalMultiplicationCount">The current number of multiplying factors applied to the utility score.</param>
		/// <returns>A tuple where the <c>float</c> is the consideration score and the <c>int</c> is the potentially incremented multiplication count.</returns>
		public abstract (float, int) Evaluate(IReadOnlyContext context, int totalMultiplicationCount);
	}

}