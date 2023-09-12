namespace CAVX.Bots.Framework.Utilities
{
    public static class TempDebug
    {
#if DEBUG
        public static bool False => false;

        public static void Trace(string message)
        {
            Console.WriteLine(message);
        }

        public static T Temp<T>(T value, T OldValue)
        {
            return value;
        }
#endif
    }
}