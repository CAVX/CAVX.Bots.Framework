using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Extensions
{
    public static class DoubleExtensions
    {
        public static int IntLop<T>(this double number, Func<double, T> mathFunc)
        {
            return Convert.ToInt32(mathFunc.Invoke(number));
        }

        public static string S(this double i)
        {
            if (i == 1 || i == -1)
                return "";
            else
                return "s";
        }
    }
}
