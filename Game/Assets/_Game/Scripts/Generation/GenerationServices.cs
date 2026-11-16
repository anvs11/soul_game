using System;
using System.Collections.Generic;

namespace Game.Generation
{
    // Pure-data generation. Unity geometry still needs a separate physical check.
    public sealed class RunGenerator
    {
        public const int SupportedGeneratorVersion = 1;
        public const int SupportedContentVersion = 1;
        public const int MaxAttempts = 20;

        public GenerationResult Generate(int seed, StageCatalog catalog, int generatorVersion, int contentVersion)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (generatorVersion <= 0) throw new ArgumentOutOfRangeException(nameof(generatorVersion));
            if (contentVersion <= 0) throw new ArgumentOutOfRangeException(nameof(contentVersion));
            if (generatorVersion != SupportedGeneratorVersion || contentVersion != SupportedContentVersion)
                return GenerationResult.Failure(Context(seed, generatorVersion, contentVersion, 0) + "; unsupported versions; supported versions are 1/1.");

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
                return GenerationResult.Failure(Context(seed, generatorVersion, contentVersion, 0) + "; catalog requires at least one Combat and one Boss module.");

            var random = new StableRandom(unchecked((uint)seed));
            var validator = new LayoutValidator();
            var catalogById = new Dictionary<string, ModuleDescriptor>(StringComparer.Ordinal);
            foreach (var module in catalog.Modules) catalogById.Add(module.Id, module);
            var cache = new Dictionary<string, ValidationResult>(StringComparer.Ordinal);
            string reason = "";
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var segments = new SegmentLayout[5];
                for (int i = 0; i < 4; i++)
                    segments[i] = new SegmentLayout(i + 1, combat[random.Index(combat.Count)].Id);
                segments[4] = new SegmentLayout(5, bosses[random.Index(bosses.Count)].Id);
                var layout = new RunLayout(seed, generatorVersion, contentVersion, segments);
                var validation = validator.ValidateKnown(layout, catalogById, cache);
                if (validation.IsValid)
                    return GenerationResult.Success(layout, false, Context(seed, generatorVersion, contentVersion, attempt) + "; validated");
                reason = string.Join(" | ", validation.Reasons);
            }
            var fallbackIds = new[] { "street_a", "street_b", "street_c", "street_a", "street_boss" };
            var fallbackSegments = new SegmentLayout[5];
            for (int i = 0; i < 5; i++) fallbackSegments[i] = new SegmentLayout(i + 1, fallbackIds[i]);
            var fallback = new RunLayout(seed, generatorVersion, contentVersion, fallbackSegments);
            var fallbackCheck = validator.ValidateKnown(fallback, catalogById, cache);
            string diagnostic = Context(seed, generatorVersion, contentVersion, MaxAttempts) + "; last rejection=" + reason;
            if (!fallbackCheck.IsValid)
                return GenerationResult.Failure(diagnostic + "; fallback invalid: " + string.Join(" | ", fallbackCheck.Reasons));
            return GenerationResult.Success(fallback, true, diagnostic + "; validated fallback");
        }

        private static string Context(int seed, int generatorVersion, int contentVersion, int attempts)
            => FormattableString.Invariant($"seed={seed}; generatorVersion={generatorVersion}; contentVersion={contentVersion}; attempts={attempts}");

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
            var modules = new Dictionary<string, ModuleDescriptor>(StringComparer.Ordinal);
            foreach (var module in catalog.Modules) modules.Add(module.Id, module);
            return ValidateKnown(layout, modules, new Dictionary<string, ValidationResult>(StringComparer.Ordinal));
        }

        internal ValidationResult ValidateKnown(RunLayout layout, Dictionary<string, ModuleDescriptor> modules,
            Dictionary<string, ValidationResult> cache)
        {
            var reasons = new List<string>();
            if (layout.GeneratorVersion != RunGenerator.SupportedGeneratorVersion ||
                layout.ContentVersion != RunGenerator.SupportedContentVersion) reasons.Add("Unsupported layout versions.");
            if (layout.Segments.Count != 5) reasons.Add("Layout requires exactly five segments.");
            for (int i = 0; i < layout.Segments.Count; i++)
            {
                var segment = layout.Segments[i];
                if (segment.Index != i + 1) reasons.Add("Segment index/order mismatch.");
                if (!modules.TryGetValue(segment.ModuleId, out var module))
                { reasons.Add("Unknown module: " + segment.ModuleId); continue; }
                if (module.Kind != (i == 4 ? ModuleKind.Boss : ModuleKind.Combat))
                    reasons.Add("Wrong module kind: " + module.Id);
                if (!cache.TryGetValue(module.Id, out var check))
                { check = CheckModule(module); cache.Add(module.Id, check); }
                foreach (var reason in check.Reasons) reasons.Add(module.Id + ": " + reason);
            }
            return new ValidationResult(reasons);
        }

        private static ValidationResult CheckModule(ModuleDescriptor module)
        {
            var reasons = new List<string>();
            // Shape/bounds/duplicate Id are already checked by the immutable constructors.
            int start = module.Entry.Y * module.Width + module.Entry.X;
            if (!module.Walkable[start])
                return new ValidationResult(new[] { "Entry cell is blocked." });
            if ((module.Kind == ModuleKind.Combat || module.Kind == ModuleKind.Boss) && module.SpawnCells.Count == 0)
                reasons.Add("Combat/Boss requires at least one spawn cell.");
            var reached = new bool[module.Walkable.Count];
            var queue = new Queue<int>(); queue.Enqueue(start); reached[start] = true;
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue(); int x = cell % module.Width; int y = cell / module.Width;
                if (x > 0) Visit(cell - 1, module, reached, queue);
                if (x + 1 < module.Width) Visit(cell + 1, module, reached, queue);
                if (y > 0) Visit(cell - module.Width, module, reached, queue);
                if (y + 1 < module.Height) Visit(cell + module.Width, module, reached, queue);
            }
            if (!reached[module.Exit.Y * module.Width + module.Exit.X]) reasons.Add("Exit is blocked or unreachable.");
            foreach (var spawn in module.SpawnCells)
                if (!reached[spawn.Y * module.Width + spawn.X])
                    reasons.Add(FormattableString.Invariant($"Spawn ({spawn.X},{spawn.Y}) is blocked or unreachable."));
            return new ValidationResult(reasons);
        }

        private static void Visit(int index, ModuleDescriptor module, bool[] reached, Queue<int> queue)
        {
            if (reached[index] || !module.Walkable[index]) return;
            reached[index] = true; queue.Enqueue(index);
        }
    }
}
