using System;
using System.Collections.Generic;

namespace Game.Generation
{
    // S3-03A generates a candidate only. Runtime must also require validator success.
    public sealed class RunGenerator
    {
        public const int SupportedGeneratorVersion = 1;
        public const int SupportedContentVersion = 1;

        public GenerationResult Generate(int seed, StageCatalog catalog, int generatorVersion, int contentVersion)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (generatorVersion <= 0) throw new ArgumentOutOfRangeException(nameof(generatorVersion));
            if (contentVersion <= 0) throw new ArgumentOutOfRangeException(nameof(contentVersion));
            if (generatorVersion != SupportedGeneratorVersion || contentVersion != SupportedContentVersion)
                return GenerationResult.Failure("Unsupported generator/content version; supported versions are 1/1.");

            var combat = new List<ModuleDescriptor>();
            var bosses = new List<ModuleDescriptor>();
            foreach (var module in catalog.Modules)
            {
                if (module.Kind == ModuleKind.Combat) combat.Add(module);
                else if (module.Kind == ModuleKind.Boss) bosses.Add(module);
            }
            combat.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
            bosses.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
            if (combat.Count == 0 || bosses.Count == 0)
                return GenerationResult.Failure("Catalog requires at least one Combat and one Boss module.");

            var random = new StableRandom(unchecked((uint)seed));
            var segments = new SegmentLayout[5];
            for (int i = 0; i < 4; i++)
                segments[i] = new SegmentLayout(i + 1, combat[random.Index(combat.Count)].Id);
            segments[4] = new SegmentLayout(5, bosses[random.Index(bosses.Count)].Id);
            return GenerationResult.Success(new RunLayout(seed, generatorVersion, contentVersion, segments), false,
                "S3-03A candidate only: reachability validation is required before combat.");
        }

        // Explicit 32-bit arithmetic, independent of System.Random / UnityEngine.Random.
        private struct StableRandom
        {
            private uint state;
            public StableRandom(uint seed) { state = seed; }
            private uint Next()
            {
                unchecked
                {
                    state += 0x9E3779B9u;
                    uint z = state;
                    z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
                    z = (z ^ (z >> 13)) * 0xC2B2AE35u;
                    return z ^ (z >> 16);
                }
            }
            public int Index(int count)
            {
                uint bound = (uint)count;
                uint threshold = unchecked(0u - bound) % bound;
                uint value;
                do { value = Next(); } while (value < threshold);
                return (int)(value % bound);
            }
        }
    }

    public sealed class LayoutValidator
    {
        public ValidationResult Validate(RunLayout layout, StageCatalog catalog)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            return new ValidationResult(new[] { "S3-01: reachability validation is not implemented." });
        }
    }
}
