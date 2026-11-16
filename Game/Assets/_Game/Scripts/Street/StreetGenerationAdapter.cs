using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Generation;
namespace Game.Street
{
    public static class StreetGenerationAdapter
    {
        public static StageCatalog BuildCatalog(StreetModuleDefinition[] definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            var modules = new List<ModuleDescriptor>();
            foreach (var data in definitions)
            {
                if (data == null || data.Prefab == null) throw new ArgumentException("Module data or prefab is missing.");
                if (!Enum.IsDefined(typeof(StreetModuleKind), data.Kind)) throw new ArgumentException("Invalid module kind.");
                if (data.SpawnCells == null) throw new ArgumentException("SpawnCells is missing for " + data.Id);
                var cells = new List<Cell>();
                foreach (var cell in data.SpawnCells) cells.Add(new Cell(cell.x, cell.y));
                modules.Add(new ModuleDescriptor(data.Id, (ModuleKind)data.Kind, data.Width, data.Height,
                    data.Walkable, new Cell(data.Entry.x, data.Entry.y), new Cell(data.Exit.x, data.Exit.y), cells));
            }
            return new StageCatalog(modules);
        }
        public static bool TryGenerate(string seedText, StageCatalog catalog, out GenerationResult result, out string error)
        {
            result = null;
            error = "";
            int seed;
            if (string.IsNullOrWhiteSpace(seedText)) seed = Guid.NewGuid().GetHashCode();
            else if (!int.TryParse(seedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out seed))
            {
                error = "Seed должен быть целым числом от -2147483648 до 2147483647.";
                return false;
            }
            try
            {
                result = new RunGenerator().Generate(seed, catalog, 1, 1);
                if (!result.IsSuccess) { error = "Маршрут недоступен: " + result.Diagnostic; return false; }
                var validation = new LayoutValidator().Validate(result.Layout, catalog);
                if (!validation.IsValid) { error = "Маршрут отклонён: " + string.Join(" | ", validation.Reasons); return false; }
                ToRoute(result.Layout);
                return true;
            }
            catch (ArgumentException exception)
            {
                error = "Ошибка каталога: " + exception.Message;
                return false;
            }
        }
        public static StreetRoute ToRoute(RunLayout layout)
        {
            var ids = new List<string>();
            foreach (var segment in layout.Segments) ids.Add(segment.ModuleId);
            return new StreetRoute(ids);
        }
    }
}
