using System;

namespace Game.Generation
{
    // S3-01 contract only: safe failures until S3-03 implements the algorithms.
    public sealed class RunGenerator
    {
        public GenerationResult Generate(int seed, StageCatalog catalog, int generatorVersion, int contentVersion)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (generatorVersion <= 0) throw new ArgumentOutOfRangeException(nameof(generatorVersion));
            if (contentVersion <= 0) throw new ArgumentOutOfRangeException(nameof(contentVersion));
            return GenerationResult.Failure("S3-01: generation is not implemented; do not start a run.");
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
