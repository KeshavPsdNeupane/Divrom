namespace Kope.Core.Extensions
{
    public static class StringExtension
    {
        /// <summary>
        /// Removes the specified postfix from the string if it exists.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="postfix"></param>
        /// <returns>
        ///     The string with the specified postfix removed if it exists; otherwise, the original string.
        /// </returns>
        public static string RemovePostFix(string str, string postfix)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            if (str.EndsWith(postfix))
            {
                return str[..^postfix.Length];
            }
            return str;
        }
    }
}
