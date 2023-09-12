﻿namespace CAVX.Bots.Framework.Models
{
    public enum EphemeralRule
    {
        EphemeralOrFail,
        EphemeralOrFallback,
        Permanent
    }

    public static class EphemeralRuleExtensions
    {
        public static bool ToEphemeral(this EphemeralRule action)
        {
            return action != EphemeralRule.Permanent;
        }
    }
}