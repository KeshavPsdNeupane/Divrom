public static class EnumExtensions
{
    // You can even make a generic one for all your enums
    public static string ToIdPart(this System.Enum enumValue)
    {
        return enumValue.ToString().ToLower();
    }


    /// <summary>
    /// Removes the specified postfix from the string if it exists.
    /// </summary>
    /// <param name="str"></param>
    /// <param name="postfix"></param>
    /// <returns></returns>
    public static string RemovePostFix(string str, string postfix)
    {
        if (string.IsNullOrEmpty(str)) return string.Empty;
        if (str.EndsWith(postfix))
        {
            return str[..^postfix.Length];
        }
        return str;
    }
    /// <summary>
    /// Removes the "Enum" postfix from the enum type name.
    /// Or a custom suffix if provided.
    /// </summary>
    /// <param name="enumValue"></param>
    /// <param name="suffix"></param>
    /// <returns></returns>
    public static string RemoveEnumTypePostFix(this System.Enum enumValue, string suffix = "Enum")
    {
        return RemovePostFix(enumValue.GetType().Name, suffix);
    }

    /// <summary>
    /// Removes the "Enum" postfix from the type name.
    /// Or a custom suffix if provided.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="suffix"></param>
    /// <returns></returns>
    public static string TypeToStringRemoveEnumPostFix(this System.Type type, string suffix = "Enum")
    {

        return RemovePostFix(type.Name, suffix);
    }

}