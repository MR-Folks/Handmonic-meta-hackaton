using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The cassette deck's four models: Cassette, Radio, Turntable and Table. Each comes from Resources/CassetteDeck
    /// once it has been made in Blender and exported there; until then it is built here from simple shapes with the
    /// same sizes, pivots and part names, so the deck works the same either way. Every part gets the deck's material
    /// for its slot name (Resources/CassetteDeck/Materials/&lt;Slot&gt;.mat), whichever way it was made, and the
    /// placeholder meshes are made once and shared by every copy.
    /// </summary>
    public static class DeckParts
    {
        public const string Folder = "CassetteDeck/";
        public const string MaterialFolder = Folder + "Materials/";
        public const string Cassette = "Cassette", Radio = "Radio", Turntable = "Turntable", Table = "Table";

        /// <summary>The material slots the models use, plus the deck's own effects (ghost, shadow, rim, glow).</summary>
        public static readonly string[] Slots =
        {
            "Shell", "Label", "Window", "Reel", "Tape", "RadioBody", "RadioTrim", "Glass", "Speaker", "Button", "Light",
            "Turntable", "TurntableBase", "TableTop", "TableLegs", "Ghost", "Shadow", "Rim", "DeckGlow", "Plate"
        };

        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static readonly Dictionary<string, GameObject> models = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

        /// <summary>The deck's material for a slot, or null if there is none.</summary>
        public static Material Material(string slot)
        {
            if (string.IsNullOrEmpty(slot)) return null;
            if (materials.TryGetValue(slot, out var m) && m != null) return m;
            m = Resources.Load<Material>(MaterialFolder + slot);
            materials[slot] = m;
            return m;
        }

        /// <summary>The exported model in Resources/CassetteDeck, or null while there is none yet.</summary>
        public static GameObject Model(string name)
        {
            if (models.TryGetValue(name, out var model) && model != null) return model;
            model = Resources.Load<GameObject>(Folder + name);
            models[name] = model;
            return model;
        }

        /// <summary>Forgets loaded models and materials, so a newly exported model is picked up (editor checks).</summary>
        public static void Forget()
        {
            models.Clear();
            materials.Clear();
        }

        /// <summary>A copy of a model, from its export or else from the placeholder, with the deck's materials.</summary>
        public static GameObject Make(string name, Transform parent)
        {
            var model = Model(name);
            GameObject go;
            if (model != null)
            {
                go = Object.Instantiate(model, parent, false);
                // An imported model may bring an animator or colliders; the deck needs neither.
                foreach (var animator in go.GetComponentsInChildren<Animator>(true)) Discard(animator);
                foreach (var collider in go.GetComponentsInChildren<Collider>(true)) Discard(collider);
            }
            else
            {
                go = name switch
                {
                    Cassette => MakeCassette(),
                    Radio => MakeRadio(),
                    Turntable => MakeTurntable(),
                    _ => MakeTable()
                };
                go.transform.SetParent(parent, false);
            }
            go.name = name;
            AssignMaterials(go);
            return go;
        }

        /// <summary>Gives every renderer the deck's material for each of its slots, matching by the slot's name.</summary>
        static void AssignMaterials(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    var mapped = Material(SlotName(shared[i] != null ? shared[i].name : null));
                    if (mapped != null) shared[i] = mapped;
                }
                r.sharedMaterials = shared;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
        }

        /// <summary>A material's slot: its name without Unity's " (Instance)" or Blender's ".001".</summary>
        public static string SlotName(string materialName)
        {
            if (string.IsNullOrEmpty(materialName)) return null;
            var name = materialName.Replace(" (Instance)", "").Trim();
            int dot = name.LastIndexOf('.');
            if (dot > 0 && dot < name.Length - 1 && char.IsDigit(name[dot + 1])) name = name.Substring(0, dot);
            foreach (var slot in Slots)
                if (string.Equals(slot, name, System.StringComparison.OrdinalIgnoreCase)) return slot;
            return name;
        }

        /// <summary>Finds a part anywhere under <paramref name="root"/> by name.</summary>
        public static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = Find(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>The bounds of a part's mesh in <paramref name="space"/>'s local space (empty bounds if it has none).</summary>
        public static Bounds LocalBounds(Transform space, Transform part)
        {
            var filter = part != null ? part.GetComponent<MeshFilter>() : null;
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null) return new Bounds(part != null ? space.InverseTransformPoint(part.position) : Vector3.zero, Vector3.zero);
            var b = mesh.bounds;
            var toSpace = space.worldToLocalMatrix * part.localToWorldMatrix;
            var result = new Bounds(toSpace.MultiplyPoint3x4(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                result.Encapsulate(toSpace.MultiplyPoint3x4(corner));
            }
            return result;
        }

        /// <summary>The bounds of every mesh under <paramref name="root"/>, in its local space.</summary>
        public static Bounds ModelBounds(Transform root)
        {
            bool any = false;
            var result = new Bounds();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var b = LocalBounds(root, filter.transform);
                if (!any) result = b;
                else result.Encapsulate(b);
                any = true;
            }
            return result;
        }

        static void Discard(Object o)
        {
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }

        // ---- The placeholders: the sizes, pivots and part names of the Blender models ----

        static GameObject Part(string name, Transform parent, Mesh mesh, params string[] slots)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (mesh == null) return go;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            var list = new Material[slots.Length];
            // Named like an imported model's materials, so AssignMaterials maps them the same way.
            for (int i = 0; i < slots.Length; i++) list[i] = Material(slots[i]) ?? new Material(Shader.Find("MusicInYourHands/CassetteDeck")) { name = slots[i] };
            r.sharedMaterials = list;
            return go;
        }

        static Mesh Shared(string key, System.Func<Mesh> make)
        {
            if (meshes.TryGetValue(key, out var mesh) && mesh != null) return mesh;
            mesh = make();
            mesh.name = key;
            meshes[key] = mesh;
            return mesh;
        }

        /// <summary>A cassette: 10 x 6.4 x 1.2 cm, pivot at its centre, label and window on its front (-Z).</summary>
        static GameObject MakeCassette()
        {
            var root = new GameObject(Cassette).transform;
            Part("Shell", root, Shared("Cassette Shell", () =>
            {
                var s = new ShapeBuilder();
                s.Box(new Vector3(-0.05f, -0.032f, -0.006f), new Vector3(0.05f, 0.032f, 0.006f), 0.0025f);
                // The raised strip along the bottom edge where the tape meets the player's heads.
                s.Box(new Vector3(-0.033f, -0.032f, -0.0068f), new Vector3(0.033f, -0.022f, -0.0055f), 0.001f);
                return s.Build();
            }), "Shell");
            Part("Label", root, Shared("Cassette Label", () =>
            {
                var s = new ShapeBuilder();
                s.Face(new Vector3(0f, 0.0185f, -0.00615f), Vector3.back, 0.045f, 0.0105f);
                return s.Build();
            }), "Label");
            Part("Tape", root, Shared("Cassette Tape", () =>
            {
                var s = new ShapeBuilder();
                s.Cylinder(new Vector3(-0.021f, -0.0075f, -0.0062f), 0.0125f, 0.0004f, 2, 28);
                s.Cylinder(new Vector3(0.021f, -0.0075f, -0.0062f), 0.0105f, 0.0004f, 2, 28);
                return s.Build();
            }), "Tape");
            var reel = Shared("Cassette Reel", () =>
            {
                var s = new ShapeBuilder();
                s.Cylinder(Vector3.zero, 0.0078f, 0.0005f, 2, 24);
                return s.Build();
            });
            Part("ReelLeft", root, reel, "Reel").transform.localPosition = new Vector3(-0.021f, -0.0075f, -0.00655f);
            Part("ReelRight", root, reel, "Reel").transform.localPosition = new Vector3(0.021f, -0.0075f, -0.00655f);
            Part("Window", root, Shared("Cassette Window", () =>
            {
                var s = new ShapeBuilder();
                s.Face(new Vector3(0f, -0.0075f, -0.0071f), Vector3.back, 0.0275f, 0.0105f);
                return s.Build();
            }), "Window");
            return root.gameObject;
        }

        /// <summary>
        /// A radio: 42 x 24.5 x 12 cm, pivot at its bottom centre, front (-Z) to the player: two speakers, a cassette
        /// door between them hinged at its bottom edge, three keys on top and a light above the door.
        /// </summary>
        static GameObject MakeRadio()
        {
            var root = new GameObject(Radio).transform;
            const float w = 0.21f, h = 0.205f, d = 0.06f;
            // The cassette well behind the door.
            const float wx = 0.068f, wy0 = 0.055f, wy1 = 0.15f, wz = -0.03f;
            Part("Body", root, Shared("Radio Body", () =>
            {
                var s = new ShapeBuilder();
                s.Submesh(0);
                s.Box(new Vector3(-w, 0.012f, wz), new Vector3(w, h, d), 0.012f);
                s.Box(new Vector3(-w, 0.012f, -d), new Vector3(-wx, h, wz + 0.004f), 0.008f);
                s.Box(new Vector3(wx, 0.012f, -d), new Vector3(w, h, wz + 0.004f), 0.008f);
                s.Box(new Vector3(-wx, 0.012f, -d), new Vector3(wx, wy0, wz + 0.004f), 0.002f);
                s.Box(new Vector3(-wx, wy1, -d), new Vector3(wx, h, wz + 0.004f), 0.002f);
                s.Submesh(1);
                // Chrome: the carrying handle, a foot strip along the bottom, and a frame round the well.
                s.Box(new Vector3(-0.16f, h, -0.009f), new Vector3(-0.145f, 0.238f, 0.009f), 0.002f);
                s.Box(new Vector3(0.145f, h, -0.009f), new Vector3(0.16f, 0.238f, 0.009f), 0.002f);
                s.Box(new Vector3(-0.168f, 0.232f, -0.011f), new Vector3(0.168f, 0.245f, 0.011f), 0.005f);
                s.Box(new Vector3(-w + 0.006f, 0f, -d + 0.006f), new Vector3(w - 0.006f, 0.012f, d - 0.006f), 0.003f);
                s.Box(new Vector3(-wx - 0.006f, wy0 - 0.006f, -d - 0.002f), new Vector3(wx + 0.006f, wy0, -d + 0.002f), 0.001f);
                s.Box(new Vector3(-wx - 0.006f, wy1, -d - 0.002f), new Vector3(wx + 0.006f, wy1 + 0.006f, -d + 0.002f), 0.001f);
                s.Box(new Vector3(-wx - 0.006f, wy0, -d - 0.002f), new Vector3(-wx, wy1, -d + 0.002f), 0.001f);
                s.Box(new Vector3(wx, wy0, -d - 0.002f), new Vector3(wx + 0.006f, wy1, -d + 0.002f), 0.001f);
                return s.Build();
            }), "RadioBody", "RadioTrim");

            var speaker = Shared("Radio Speaker", () =>
            {
                var s = new ShapeBuilder();
                s.Cylinder(new Vector3(0f, 0f, -0.002f), 0.062f, 0.006f, 2, 48);
                return s.Build();
            });
            Part("SpeakerLeft", root, speaker, "Speaker").transform.localPosition = new Vector3(-0.139f, 0.105f, -d);
            Part("SpeakerRight", root, speaker, "Speaker").transform.localPosition = new Vector3(0.139f, 0.105f, -d);

            // The door: pivot on its bottom hinge edge; it tilts open about its X axis, top towards the player.
            var door = Part("Door", root, Shared("Radio Door", () =>
            {
                var s = new ShapeBuilder();
                const float dh = wy1 - wy0, t = 0.006f;
                s.Submesh(0);
                s.Box(new Vector3(-wx, 0f, -t), new Vector3(wx, 0.011f, 0f), 0.001f);
                s.Box(new Vector3(-wx, dh - 0.009f, -t), new Vector3(wx, dh, 0f), 0.001f);
                s.Box(new Vector3(-wx, 0.011f, -t), new Vector3(-wx + 0.008f, dh - 0.009f, 0f), 0.001f);
                s.Box(new Vector3(wx - 0.008f, 0.011f, -t), new Vector3(wx, dh - 0.009f, 0f), 0.001f);
                // A finger grip along the door's top.
                s.Box(new Vector3(-0.025f, dh - 0.004f, -t - 0.004f), new Vector3(0.025f, dh, -t), 0.001f);
                s.Submesh(1);
                s.Box(new Vector3(-wx + 0.008f, 0.011f, -t * 0.6f), new Vector3(wx - 0.008f, dh - 0.009f, -t * 0.3f), 0f);
                return s.Build();
            }), "RadioTrim", "Glass");
            door.transform.localPosition = new Vector3(0f, wy0, -d - 0.0005f);

            var slot = new GameObject("Slot").transform;
            slot.SetParent(root, false);
            slot.localPosition = new Vector3(0f, wy0 + 0.046f, -0.043f);

            var key = Shared("Radio Key", () =>
            {
                var s = new ShapeBuilder();
                s.Box(new Vector3(-0.017f, 0f, -0.013f), new Vector3(0.017f, 0.014f, 0.013f), 0.003f);
                return s.Build();
            });
            Part("PlayButton", root, key, "Button").transform.localPosition = new Vector3(-0.046f, h - 0.004f, -0.03f);
            Part("StopButton", root, key, "Button").transform.localPosition = new Vector3(0f, h - 0.004f, -0.03f);
            Part("EjectButton", root, key, "Button").transform.localPosition = new Vector3(0.046f, h - 0.004f, -0.03f);

            Part("Light", root, Shared("Radio Light", () =>
            {
                var s = new ShapeBuilder();
                s.Box(new Vector3(-0.022f, -0.0035f, -0.003f), new Vector3(0.022f, 0.0035f, 0.001f), 0.0015f);
                return s.Build();
            }), "Light").transform.localPosition = new Vector3(0f, 0.176f, -d);
            return root.gameObject;
        }

        /// <summary>A turntable: radius 26 cm, pivot at its base centre, the platter turning on top of the base.</summary>
        static GameObject MakeTurntable()
        {
            var root = new GameObject(Turntable).transform;
            Part("Base", root, Shared("Turntable Base", () =>
            {
                var s = new ShapeBuilder();
                s.Cylinder(new Vector3(0f, 0.007f, 0f), 0.26f, 0.014f, 1, 72);
                return s.Build();
            }), "TurntableBase");
            Part("Platter", root, Shared("Turntable Platter", () =>
            {
                var s = new ShapeBuilder();
                s.Cylinder(new Vector3(0f, 0.022f, 0f), 0.25f, 0.016f, 1, 96);
                // A small spindle in the middle.
                s.Cylinder(new Vector3(0f, 0.036f, 0f), 0.028f, 0.012f, 1, 32);
                return s.Build();
            }), "Turntable");
            var rim = new GameObject("RimHeight").transform;
            rim.SetParent(root, false);
            rim.localPosition = new Vector3(0f, 0.03f, -0.23f);
            return root.gameObject;
        }

        /// <summary>A table: top 90 x 55 cm at exactly 68 cm, pivot at the floor under its centre.</summary>
        static GameObject MakeTable()
        {
            var root = new GameObject(Table).transform;
            Part("Top", root, Shared("Table Top", () =>
            {
                var s = new ShapeBuilder();
                s.Box(new Vector3(-0.45f, 0.65f, -0.275f), new Vector3(0.45f, 0.68f, 0.275f), 0.008f);
                return s.Build();
            }), "TableTop");
            Part("Legs", root, Shared("Table Legs", () =>
            {
                var s = new ShapeBuilder();
                foreach (var x in new[] { -0.39f, 0.39f })
                    foreach (var z in new[] { -0.22f, 0.22f })
                        s.Cylinder(new Vector3(x, 0.325f, z), 0.016f, 0.65f, 1, 16);
                // A frame under the top.
                s.Box(new Vector3(-0.4f, 0.6f, -0.232f), new Vector3(0.4f, 0.65f, -0.214f), 0.002f);
                s.Box(new Vector3(-0.4f, 0.6f, 0.214f), new Vector3(0.4f, 0.65f, 0.232f), 0.002f);
                s.Box(new Vector3(-0.402f, 0.6f, -0.232f), new Vector3(-0.384f, 0.65f, 0.232f), 0.002f);
                s.Box(new Vector3(0.384f, 0.6f, -0.232f), new Vector3(0.402f, 0.65f, 0.232f), 0.002f);
                return s.Build();
            }), "TableLegs");
            var top = new GameObject("TopSurface").transform;
            top.SetParent(root, false);
            top.localPosition = new Vector3(0f, 0.68f, 0f);
            return root.gameObject;
        }

        /// <summary>
        /// A round push button for the table (the deck's own, not one of the Blender models): a dark collar with a
        /// cap that sinks when pressed. Its pivot is at its base; the cap is the part named "Cap".
        /// </summary>
        public static GameObject MakePuck(string name, Transform parent, float radius)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            Part("Collar", root, Shared($"Puck Collar {radius:0.000}", () =>
            {
                var s = new ShapeBuilder();
                s.Cylinder(new Vector3(0f, 0.003f, 0f), radius + 0.005f, 0.006f, 1, 40);
                return s.Build();
            }), "RadioTrim");
            Part("Cap", root, Shared($"Puck Cap {radius:0.000}", () =>
            {
                var s = new ShapeBuilder();
                s.Cylinder(new Vector3(0f, 0.008f, 0f), radius, 0.012f, 1, 40);
                return s.Build();
            }), "Button");
            AssignMaterials(root.gameObject);
            return root.gameObject;
        }
    }

    /// <summary>Builds simple meshes (boxes with bevelled edges, cylinders) out of flat-shaded faces, in submeshes.</summary>
    public sealed class ShapeBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<List<int>> submeshes = new List<List<int>> { new List<int>() };
        int current;

        public void Submesh(int index)
        {
            while (submeshes.Count <= index) submeshes.Add(new List<int>());
            current = index;
        }

        /// <summary>A flat rectangle facing <paramref name="normal"/>, its UVs 0-1 from its left to right and bottom to top.</summary>
        public void Face(Vector3 center, Vector3 normal, float halfWidth, float halfHeight)
        {
            var up = Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up;
            var right = Vector3.Cross(up, -normal).normalized;
            up = Vector3.Cross(-normal, right).normalized;
            Quad(center - right * halfWidth - up * halfHeight, center - right * halfWidth + up * halfHeight,
                center + right * halfWidth + up * halfHeight, center + right * halfWidth - up * halfHeight, normal);
        }

        /// <summary>A box from <paramref name="min"/> to <paramref name="max"/>, its edges cut at 45° by <paramref name="bevel"/>.</summary>
        public void Box(Vector3 min, Vector3 max, float bevel)
        {
            var c = (min + max) * 0.5f;
            var e = (max - min) * 0.5f;
            float b = Mathf.Min(bevel, Mathf.Min(e.x, Mathf.Min(e.y, e.z)) * 0.9f);
            // The six faces, shrunk by the bevel.
            for (int axis = 0; axis < 3; axis++)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    var n = Vector3.zero;
                    n[axis] = sign;
                    var center = c + n * e[axis];
                    int a1 = (axis + 1) % 3, a2 = (axis + 2) % 3;
                    var u = Vector3.zero; u[a1] = e[a1] - b;
                    var v = Vector3.zero; v[a2] = e[a2] - b;
                    Quad(center - u - v, center - u + v, center + u + v, center + u - v, n);
                }
            if (b <= 0f) return;
            // The twelve bevelled edges.
            for (int axis = 0; axis < 3; axis++)
            {
                int a1 = (axis + 1) % 3, a2 = (axis + 2) % 3;
                for (int s1 = -1; s1 <= 1; s1 += 2)
                    for (int s2 = -1; s2 <= 1; s2 += 2)
                    {
                        var along = Vector3.zero; along[axis] = e[axis] - b;
                        var p1 = c; p1[a1] += s1 * e[a1]; p1[a2] += s2 * (e[a2] - b);
                        var p2 = c; p2[a1] += s1 * (e[a1] - b); p2[a2] += s2 * e[a2];
                        var n = Vector3.zero; n[a1] = s1; n[a2] = s2;
                        Quad(p1 - along, p1 + along, p2 + along, p2 - along, n.normalized);
                    }
            }
            // The eight corners.
            for (int i = 0; i < 8; i++)
            {
                var s = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
                var px = c + Vector3.Scale(s, new Vector3(e.x, e.y - b, e.z - b));
                var py = c + Vector3.Scale(s, new Vector3(e.x - b, e.y, e.z - b));
                var pz = c + Vector3.Scale(s, new Vector3(e.x - b, e.y - b, e.z));
                Triangle(px, py, pz, s.normalized);
            }
        }

        /// <summary>A cylinder along an axis (0 x, 1 y, 2 z), smooth round its side, flat at its ends.</summary>
        public void Cylinder(Vector3 center, float radius, float length, int axis, int segments)
        {
            int a1 = (axis + 1) % 3, a2 = (axis + 2) % 3;
            var half = Vector3.zero; half[axis] = length * 0.5f;
            var up = Vector3.zero; up[axis] = 1f;
            for (int i = 0; i < segments; i++)
            {
                float t0 = i / (float)segments * Mathf.PI * 2f, t1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                var d0 = Vector3.zero; d0[a1] = Mathf.Cos(t0); d0[a2] = Mathf.Sin(t0);
                var d1 = Vector3.zero; d1[a1] = Mathf.Cos(t1); d1[a2] = Mathf.Sin(t1);
                var q0 = center + d0 * radius;
                var q1 = center + d1 * radius;
                // Side, with normals pointing straight out so it shades round.
                int v = vertices.Count;
                Add(q0 - half, d0, new Vector2(i / (float)segments, 0f));
                Add(q0 + half, d0, new Vector2(i / (float)segments, 1f));
                Add(q1 + half, d1, new Vector2((i + 1) / (float)segments, 1f));
                Add(q1 - half, d1, new Vector2((i + 1) / (float)segments, 0f));
                Wind(v, v + 1, v + 2, (d0 + d1).normalized);
                Wind(v, v + 2, v + 3, (d0 + d1).normalized);
                // Ends.
                Triangle(center + half, q0 + half, q1 + half, up);
                Triangle(center - half, q1 - half, q0 - half, -up);
            }
        }

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            int v = vertices.Count;
            Add(a, normal, new Vector2(0f, 0f));
            Add(b, normal, new Vector2(0f, 1f));
            Add(c, normal, new Vector2(1f, 1f));
            Add(d, normal, new Vector2(1f, 0f));
            Wind(v, v + 1, v + 2, normal);
            Wind(v, v + 2, v + 3, normal);
        }

        void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
        {
            int v = vertices.Count;
            Add(a, normal, new Vector2(0.5f, 0.5f));
            Add(b, normal, new Vector2(0.5f, 0.5f));
            Add(c, normal, new Vector2(0.5f, 0.5f));
            Wind(v, v + 1, v + 2, normal);
        }

        void Add(Vector3 p, Vector3 n, Vector2 uv)
        {
            vertices.Add(p);
            normals.Add(n);
            uvs.Add(uv);
        }

        /// <summary>Adds a triangle wound so it faces <paramref name="normal"/> (Unity draws clockwise faces).</summary>
        void Wind(int a, int b, int c, Vector3 normal)
        {
            var list = submeshes[current];
            var face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            list.Add(a);
            if (Vector3.Dot(face, normal) >= 0f)
            {
                list.Add(b);
                list.Add(c);
            }
            else
            {
                list.Add(c);
                list.Add(b);
            }
        }

        public Mesh Build()
        {
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = submeshes.Count;
            for (int i = 0; i < submeshes.Count; i++) mesh.SetTriangles(submeshes[i], i);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
