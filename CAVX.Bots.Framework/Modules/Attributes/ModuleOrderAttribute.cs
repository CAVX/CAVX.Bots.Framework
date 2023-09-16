using System;

namespace CAVX.Bots.Framework.Modules.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class ModuleOrderAttribute(int order) : Attribute
{
    public int Order { get; set; } = order;
}