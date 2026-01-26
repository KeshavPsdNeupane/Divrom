using UnityEngine;

namespace Kope.Core.Extensions
{
    public static class FloatExtension
    {
        /// <summary>
        /// Applies a compensation to the utility score based on the number of considerations.
        /// This helps to prevent very low scores when multiple considerations are multiplied together.
        /// Uses the Algorithm Written byt Mr.Dave Mark in his book 
        /// Behavioral Mathematics for Game AI (Applied Mathematics).<br/>
        /// Highly Recommended! https://www.amazon.com/Behavioral-Mathematics-Game-AI-Applied/dp/1584506849
        /// To read more about the technique.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public static float GetCompensatedUtility(this float value, int size)
        {
            float orginal = value;
            float modFactor = 1f - (1f / size);
            float makeup = (1f - orginal) * modFactor;
            float finalScore = orginal + (makeup * orginal);
            // The clamp is really not needed but using it to be safe, since float operations can be tricky.
            // in many cases the value will already be in 0-1 range. since we are multiplying normalized values.
            // But just in case of precision errors, we clamp it.
            return Mathf.Clamp01(finalScore);
        }

        /// <summary>
        /// Checks if two float values are approximately equal within a specified tolerance.
        /// Most time wont be used because the == operator is usually sufficient for game dev purposes.
        /// Just added for reference. 
        /// </summary>
        public static bool IsApproximately(this float a, float b, float tolerance = 0.0001f)
        {
            return Mathf.Abs(a - b) <= tolerance;
        }
    }
}