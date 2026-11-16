using System;
using System.Collections.Generic;
namespace Game.Street
{
    public sealed class StreetRoute
    {
        public IReadOnlyList<string> ModuleIds { get; }
        public StreetRoute(IReadOnlyList<string> ids)
        {
            if (ids == null || ids.Count != 5) throw new ArgumentException("Street route requires five modules.");
            var copy = new string[5];
            for (int i = 0; i < copy.Length; i++)
            {
                bool valid = i == 4 ? ids[i] == "street_boss" :
                    ids[i] == "street_a" || ids[i] == "street_b" || ids[i] == "street_c";
                if (!valid) throw new ArgumentException("Unknown or misplaced street module.");
                copy[i] = ids[i];
            }
            ModuleIds = Array.AsReadOnly(copy);
        }
        public static StreetRoute Fixed() => new StreetRoute(new[]
            { "street_a", "street_b", "street_c", "street_a", "street_boss" });
    }
}
