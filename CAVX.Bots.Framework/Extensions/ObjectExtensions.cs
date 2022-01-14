using System;
using System.Collections.Generic;
using System.Text;

namespace CAVX.Bots.Framework.Extensions
{
    public static class ObjectExtensions
    {
        public static bool IsOfType<T>(this object obj) => obj is T;
        public static bool IsOfAType<T1, T2>(this object obj) => obj is T1 || obj is T2;
        public static bool IsOfAType<T1, T2, T3>(this object obj) => obj is T1 || obj is T2 || obj is T3;
    }
}
