using UnityEngine;
namespace Game.Street
{
    public enum StreetModuleKind { Entry, Combat, Rest, Boss }
    public sealed class StreetModuleDefinition : ScriptableObject
    {
        public string Id;
        public StreetModuleKind Kind;
        public GameObject Prefab;
        public Vector2 GridOrigin = new Vector2(-18, -10);
        public int Width = 36;
        public int Height = 20;
        public float CellSize = 1;
        public float ActorClearance = .45f;
        public bool[] Walkable;
        public Vector2Int Entry;
        public Vector2Int Exit;
        public Vector2Int[] SpawnCells;
        public Vector2 CellCenter(Vector2Int cell) => GridOrigin +
            new Vector2((cell.x + .5f) * CellSize, (cell.y + .5f) * CellSize);
    }
}
