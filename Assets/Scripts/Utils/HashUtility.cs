public static class HashUtility
{
    private const uint FNV_PRIME = 16777619;
    private const uint FNV_OFFSET_BASIS = 2166136261;

    public static int GetStableHash(string text)
    {
        uint hash = FNV_OFFSET_BASIS;
        for (int i = 0; i < text.Length; i++)
        {
            hash ^= text[i];
            hash *= FNV_PRIME;
        }
        return unchecked((int)hash);
    }
}
