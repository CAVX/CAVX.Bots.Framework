using System;
using System.Linq;

namespace CAVX.Bots.Framework.Extensions
{
    public static class EnumExtensions
    {
        public static T GetAttributeDetails<T>(this Enum enumValue) where T : Attribute
        {
            var parameterProperties = enumValue.GetType().GetMember(enumValue.ToString()).SelectMany(m => m.GetCustomAttributes(false).OfType<T>());

            var properties = parameterProperties as T[] ?? parameterProperties.ToArray();
            return !properties.Any() ? null : properties.FirstOrDefault();
        }
    }
}