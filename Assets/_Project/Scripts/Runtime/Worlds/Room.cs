using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The real room, from the headset's room scan: its floor, ceiling, walls, tables and furniture. Meta's MR Utility
    /// Kit fills it in once the scan loads (RoomScanFeed); the editor's screenshots fill it with their stand-in room.
    /// Until then Scanned is false and every query falls back to a stand-in (the floor 1.5 m below the eyes, a wall
    /// at a fixed distance), so the worlds work the same without a scan.
    /// </summary>
    public static class Room
    {
        /// <summary>
        /// A flat rectangle in the room. Normal points into the room (up for table tops); Right and Up span the
        /// rectangle (Up is the depth direction on a table), and Size is its width along Right and height along Up.
        /// </summary>
        public struct Surface
        {
            public Vector3 Center, Normal, Right, Up;
            public Vector2 Size;

            public Surface(Vector3 center, Vector3 normal, Vector3 right, Vector2 size)
            {
                Center = center;
                Normal = normal.normalized;
                Right = Vector3.ProjectOnPlane(right, Normal).normalized;
                Up = Vector3.Cross(Normal, Right);
                Size = size;
            }

            /// <summary>The point's position on the surface, in metres along Right and Up from the centre.</summary>
            public Vector2 Local(Vector3 p) => new Vector2(Vector3.Dot(p - Center, Right), Vector3.Dot(p - Center, Up));

            public bool Contains(Vector3 p, float margin = 0f)
            {
                var l = Local(p);
                return Mathf.Abs(l.x) <= Size.x * 0.5f - margin && Mathf.Abs(l.y) <= Size.y * 0.5f - margin;
            }

            /// <summary>The nearest point on the surface, kept <paramref name="margin"/> metres inside its edges.</summary>
            public Vector3 Clamp(Vector3 p, float margin = 0f)
            {
                var l = Local(p);
                float hx = Mathf.Max(0f, Size.x * 0.5f - margin), hy = Mathf.Max(0f, Size.y * 0.5f - margin);
                return Center + Right * Mathf.Clamp(l.x, -hx, hx) + Up * Mathf.Clamp(l.y, -hy, hy);
            }
        }

        static readonly List<Surface> walls = new List<Surface>();
        static readonly List<Surface> tables = new List<Surface>();
        static readonly List<Surface> tops = new List<Surface>();

        /// <summary>True once a room scan has loaded.</summary>
        public static bool Scanned { get; private set; }

        /// <summary>Goes up each time the room changes, so worlds can place themselves again.</summary>
        public static int Version { get; private set; }

        public static float FloorY { get; private set; }
        public static float CeilingY { get; private set; }

        /// <summary>Wall faces, facing into the room.</summary>
        public static IReadOnlyList<Surface> Walls => walls;

        /// <summary>Table tops (desks and tables), facing up.</summary>
        public static IReadOnlyList<Surface> Tables => tables;

        /// <summary>The tops of everything you could set something down on: tables, sofas, beds, shelves and cabinets.</summary>
        public static IReadOnlyList<Surface> Tops => tops;

        /// <summary>Replaces the room. Tables are also added to the tops.</summary>
        public static void Set(float floorY, float ceilingY, IEnumerable<Surface> wallFaces, IEnumerable<Surface> tableTops, IEnumerable<Surface> otherTops)
        {
            walls.Clear();
            tables.Clear();
            tops.Clear();
            walls.AddRange(wallFaces);
            tables.AddRange(tableTops);
            tops.AddRange(tables);
            tops.AddRange(otherTops);
            FloorY = floorY;
            CeilingY = ceilingY;
            Scanned = true;
            Version++;
        }

        public static void Clear()
        {
            if (!Scanned) return;
            walls.Clear();
            tables.Clear();
            tops.Clear();
            Scanned = false;
            Version++;
        }

        /// <summary>The floor height: scanned, or the stand-in.</summary>
        public static float Floor(Camera view) =>
            Scanned ? FloorY : view != null && view.transform.position.y < 0.9f ? view.transform.position.y - 1.5f : 0f;

        /// <summary>The ceiling height if the scan has one, or <paramref name="standIn"/>.</summary>
        public static float Ceiling(float standIn) => Scanned && CeilingY > FloorY + 1.6f ? CeilingY : standIn;

        /// <summary>
        /// The height things dropped at <paramref name="p"/> land on: the top of the furniture under it, or the floor
        /// (<paramref name="floorY"/>).
        /// </summary>
        public static float GroundBelow(Vector3 p, float floorY)
        {
            float ground = floorY;
            foreach (var top in tops)
                if (top.Center.y < p.y && top.Center.y > ground && top.Contains(p)) ground = top.Center.y;
            return ground;
        }

        /// <summary>The furniture top under <paramref name="p"/>, if there is one.</summary>
        public static bool TopBelow(Vector3 p, out Surface top)
        {
            top = default;
            bool found = false;
            foreach (var t in tops)
                if (t.Center.y < p.y && (!found || t.Center.y > top.Center.y) && t.Contains(p)) { top = t; found = true; }
            return found;
        }

        /// <summary>
        /// Where a ray first meets a scanned wall, within <paramref name="maxDistance"/> metres: the distance along
        /// the ray and the wall. Walls are hit from the room side only.
        /// </summary>
        public static bool RaycastWalls(Ray ray, float maxDistance, out float distance, out Surface wall)
        {
            distance = maxDistance;
            wall = default;
            bool found = false;
            foreach (var w in walls)
            {
                float facing = -Vector3.Dot(ray.direction, w.Normal);
                if (facing < 1e-3f) continue;
                float t = Vector3.Dot(ray.origin - w.Center, w.Normal) / facing;
                if (t <= 0f || t >= distance) continue;
                var l = w.Local(ray.GetPoint(t));
                if (Mathf.Abs(l.x) > w.Size.x * 0.5f + 0.05f || Mathf.Abs(l.y) > w.Size.y * 0.5f + 0.05f) continue;
                distance = t;
                wall = w;
                found = true;
            }
            return found;
        }

        /// <summary>
        /// The wall you are looking at: the first wall face a level ray from <paramref name="from"/> along
        /// <paramref name="direction"/> hits within <paramref name="maxDistance"/>. <paramref name="point"/> is
        /// where it hits, at the ray's height.
        /// </summary>
        public static bool WallAhead(Vector3 from, Vector3 direction, float maxDistance, out Surface wall, out Vector3 point)
        {
            wall = default;
            point = default;
            if (!Scanned) return false;
            direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            float best = maxDistance;
            bool found = false;
            foreach (var w in walls)
            {
                float facing = -Vector3.Dot(direction, w.Normal);
                if (facing < 0.2f) continue;
                float distance = Vector3.Dot(from - w.Center, w.Normal) / facing;
                if (distance < 0.2f || distance > best) continue;
                var hit = from + direction * distance;
                // A little slack past the wall's ends, since scanned corners rarely meet exactly.
                if (Mathf.Abs(w.Local(hit).x) > w.Size.x * 0.5f + 0.15f) continue;
                best = distance;
                wall = w;
                point = hit;
                found = true;
            }
            return found;
        }

        /// <summary>
        /// A table in front of you, close enough to reach: its top lies within <paramref name="reach"/> metres
        /// (level distance) and 70° of where you look, between knee height and a little below your eyes.
        /// <paramref name="point"/> is the spot on it nearest to where you look, kept <paramref name="margin"/> inside its edges.
        /// </summary>
        public static bool TableAhead(Vector3 head, Vector3 forward, float reach, float margin, out Surface table, out Vector3 point)
        {
            table = default;
            point = default;
            if (!Scanned) return false;
            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            float best = float.MaxValue;
            bool found = false;
            foreach (var t in tables)
            {
                if (t.Center.y < FloorY + 0.25f || t.Center.y > head.y - 0.2f) continue;
                // The spot on the table nearest to a point a comfortable arm's length in front of you.
                var aim = head + forward * 0.45f;
                aim.y = t.Center.y;
                var spot = t.Clamp(aim, margin);
                var flat = Vector3.ProjectOnPlane(spot - head, Vector3.up);
                if (flat.magnitude > reach || Vector3.Angle(flat, forward) > 70f) continue;
                float score = flat.magnitude + Vector3.Angle(flat, forward) / 90f;
                if (score >= best) continue;
                best = score;
                table = t;
                point = spot;
                found = true;
            }
            return found;
        }
    }
}
