using System.Collections.Generic;
using UnityEngine;

namespace Game.AI
{
    internal sealed class CoverNavigation
    {
        private const int Side = 29;
        private const int Half = Side / 2;
        private static readonly Vector2Int[] Neighbors =
        {
            Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down,
            new(1, 1), new(-1, 1), new(-1, -1), new(1, -1)
        };
        private readonly Rigidbody2D body;
        private readonly Collider2D shape;
        private readonly ContactFilter2D world;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[1];
        private readonly float[] costs = new float[Side * Side];
        private readonly int[] previous = new int[Side * Side];
        private readonly List<int> frontier = new();
        private readonly List<Vector2> waypoints = new();
        private float nextSearch;
        private float radius;

        internal CoverNavigation(Rigidbody2D body)
        {
            this.body = body;
            shape = body.GetComponent<Collider2D>();
            world = new ContactFilter2D { useLayerMask = true, useTriggers = false, layerMask = LayerMask.GetMask("World") };
        }

        internal Vector2 Heading(Vector2 target)
        {
            Vector2 start = body.position;
            Vector2 size = shape == null ? Vector2.one * .8f : (Vector2)shape.bounds.size;
            radius = Mathf.Max(.2f, Mathf.Max(size.x, size.y) * .5f - .025f);
            if (Clear(start, target))
            {
                waypoints.Clear();
                nextSearch = 0;
                return (target - start).normalized;
            }
            if (Time.fixedTime >= nextSearch)
            {
                nextSearch = Time.fixedTime + .45f;
                FindRoute(start, target);
            }
            foreach (var waypoint in waypoints)
                if ((waypoint - start).sqrMagnitude > .035f && Clear(start, waypoint))
                    return (waypoint - start).normalized;
            return Vector2.zero;
        }

        private bool Clear(Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            return delta.sqrMagnitude < .0001f ||
                Physics2D.CircleCast(from, radius, delta.normalized, world, hits, delta.magnitude) == 0;
        }

        private void FindRoute(Vector2 start, Vector2 target)
        {
            // Test edges against live colliders, including closed gates.
            Vector2 origin = (Vector2)Vector2Int.FloorToInt(start) - Vector2.one * (Half - .5f);
            for (int i = 0; i < costs.Length; i++) costs[i] = float.PositiveInfinity;
            frontier.Clear();
            waypoints.Clear();
            int best = -1;
            float bestDistance = (start - target).sqrMagnitude;
            for (int y = Half - 1; y <= Half + 1; y++)
            for (int x = Half - 1; x <= Half + 1; x++)
            {
                int index = y * Side + x;
                Vector2 point = Point(origin, index);
                if (!Clear(start, point)) continue;
                costs[index] = Vector2.Distance(start, point);
                previous[index] = -1;
                frontier.Add(index);
            }

            while (frontier.Count > 0)
            {
                int lowest = 0;
                for (int i = 1; i < frontier.Count; i++)
                    if (Score(frontier[i], origin, target) < Score(frontier[lowest], origin, target)) lowest = i;
                int current = frontier[lowest];
                frontier.RemoveAt(lowest);
                Vector2 point = Point(origin, current);
                float distance = (point - target).sqrMagnitude;
                if (Clear(point, target))
                {
                    best = current;
                    break;
                }
                if (distance < bestDistance)
                {
                    best = current;
                    bestDistance = distance;
                }
                foreach (var offset in Neighbors)
                {
                    int x = current % Side + offset.x, y = current / Side + offset.y;
                    if (x < 0 || x >= Side || y < 0 || y >= Side) continue;
                    int next = y * Side + x;
                    float cost = costs[current] + (offset.x == 0 || offset.y == 0 ? 1f : 1.414214f);
                    if (cost >= costs[next] || !Clear(point, Point(origin, next))) continue;
                    if (!frontier.Contains(next)) frontier.Add(next);
                    costs[next] = cost;
                    previous[next] = current;
                }
            }

            for (int i = best; i >= 0; i = previous[i]) waypoints.Add(Point(origin, i));
        }

        private float Score(int index, Vector2 origin, Vector2 target) =>
            costs[index] + Vector2.Distance(Point(origin, index), target);

        private static Vector2 Point(Vector2 origin, int index) => origin + new Vector2(index % Side, index / Side);
    }
}
