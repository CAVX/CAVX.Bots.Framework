namespace CAVX.Bots.Framework.Extensions;

public static class Int32Extensions
{
    public static string S(this int i)
    {
        return i is 1 or -1 ? "" : "s";
    }
}