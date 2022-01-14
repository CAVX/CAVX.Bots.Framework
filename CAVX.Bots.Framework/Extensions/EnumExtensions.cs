using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Extensions
{
    public static class EnumExtensions
    {
        public static T GetAttributeDetails<T>(this Enum enumValue) where T : Attribute
        {
            var parameterProperties = enumValue.GetType().GetMember(enumValue.ToString()).SelectMany(m => m.GetCustomAttributes(false).OfType<T>());

            if (parameterProperties == null || !parameterProperties.Any())
                return null;

            return parameterProperties.FirstOrDefault();
        }
    }
}
