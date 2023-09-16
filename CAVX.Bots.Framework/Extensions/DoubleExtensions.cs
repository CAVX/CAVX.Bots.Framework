using System;

namespace CAVX.Bots.Framework.Extensions;

public static class DoubleExtensions
{
    public static int IntLop<T>(this double number, Func<double, T> mathFunc)
    {
        return Convert.ToInt32(mathFunc.Invoke(number));
    }

    public static string S(this double i)
    {
        return i is 1 or -1 ? "" : "s";
    }
}