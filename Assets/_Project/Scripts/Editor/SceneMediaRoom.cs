using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MusicInYourHands.EditorTools
{
    /// <summary>
    /// The room the store and demo media are shot in (see SceneMedia): a lit, furnished living room at dusk standing
    /// in for the player's own room, which the headset shows through passthrough. Wood floor and rug, painted walls,
    /// a window onto a dusk sky, a sofa, a bookshelf, a floor lamp, plants and framed art. Its walls, ceiling and
    /// furniture tops are handed to the worlds as the room scan, the way MRUK gives them a scanned room on the headset.
    /// With <c>scanned</c>, it is the smaller room of the scanned screenshots (SceneShots.Furnish), with the same
    /// table, sofa and shelf where those shots expect them; otherwise a larger room with an open floor.
    /// The head camera (about 108 degrees wide) sees the side walls only in their back half, so that is where the
    /// window and the furniture along them go.
    /// </summary>
    public static partial class EditorChecks
    {
        static Shader litShader;

        static void BuildMediaRoom(bool scanned)
        {
            litShader = Shader.Find("Universal Render Pipeline/Lit");
            float side = scanned ? 1.7f : 1.9f, back = scanned ? 1.5f : 2.6f, behind = scanned ? -1.5f : -1.6f;
            float height = scanned ? 2.5f : 2.7f;
            float width = side * 2f, depth = back - behind, midZ = (back + behind) * 0.5f;
            var root = new GameObject("Media Room").transform;
            var toRoomFromBack = Vector3.back;
            var toRoomFromLeft = Vector3.right;
            var toRoomFromRight = Vector3.left;

            // Shell: wood floor, warm side walls, a deep teal back wall, an off-white ceiling, skirting boards.
            var wood = Mat(Color.white, 0.35f, WoodTexture(), new Vector2(width / 1.2f, depth / 1.2f));
            Quad(root, "Floor", new Vector3(0f, 0f, midZ), Quaternion.Euler(90f, 0f, 0f), new Vector2(width, depth), wood);
            var sideWall = Mat(new Color(0.66f, 0.58f, 0.49f), 0.1f);
            var backWall = Mat(new Color(0.17f, 0.33f, 0.36f), 0.1f);
            Quad(root, "Back Wall", new Vector3(0f, height * 0.5f, back), Facing(toRoomFromBack), new Vector2(width, height), backWall);
            Quad(root, "Left Wall", new Vector3(-side, height * 0.5f, midZ), Facing(toRoomFromLeft), new Vector2(depth, height), sideWall);
            Quad(root, "Right Wall", new Vector3(side, height * 0.5f, midZ), Facing(toRoomFromRight), new Vector2(depth, height), sideWall);
            Quad(root, "Behind Wall", new Vector3(0f, height * 0.5f, behind), Facing(Vector3.forward), new Vector2(width, height), sideWall);
            Quad(root, "Ceiling", new Vector3(0f, height, midZ), Quaternion.Euler(-90f, 0f, 0f), new Vector2(width, depth), Mat(new Color(0.86f, 0.83f, 0.78f), 0.05f));
            var skirting = Mat(new Color(0.9f, 0.88f, 0.84f), 0.3f);
            Box(root, "Skirting Back", new Vector3(0f, 0.05f, back - 0.01f), new Vector3(width, 0.1f, 0.02f), skirting);
            Box(root, "Skirting Left", new Vector3(-side + 0.01f, 0.05f, midZ), new Vector3(0.02f, 0.1f, depth), skirting);
            Box(root, "Skirting Right", new Vector3(side - 0.01f, 0.05f, midZ), new Vector3(0.02f, 0.1f, depth), skirting);
            Cylinder(root, "Ceiling Light", new Vector3(0f, height - 0.02f, midZ + 0.6f), new Vector3(0.45f, 0.02f, 0.45f),
                Mat(new Color(1f, 0.95f, 0.85f), 0.2f, emission: new Color(1f, 0.9f, 0.75f) * 0.9f));

            var fabric = Mat(new Color(0.2f, 0.27f, 0.42f), 0.1f);
            var cushion = Mat(new Color(0.82f, 0.6f, 0.36f), 0.1f);
            var darkWood = Mat(new Color(0.3f, 0.19f, 0.12f), 0.35f);
            var tops = new List<Room.Surface>();
            var tables = new List<Room.Surface>();
            if (scanned)
            {
                // The scanned screenshots' furniture, where their hand poses expect it; the window on the back wall,
                // since little of the side walls shows in this smaller room.
                var table = new Vector3(0f, 0.75f, 0.55f);
                Box(root, "Table Top", table + Vector3.down * 0.02f, new Vector3(1.2f, 0.04f, 0.7f), darkWood);
                foreach (var (x, z) in new[] { (-0.55f, 0.25f), (0.55f, 0.25f), (-0.55f, 0.85f), (0.55f, 0.85f) })
                    Box(root, "Table Leg", new Vector3(x, 0.365f, z), new Vector3(0.05f, 0.73f, 0.05f), darkWood);
                tables.Add(new Room.Surface(table, Vector3.up, Vector3.right, new Vector2(1.2f, 0.7f)));
                Rug(root, new Vector3(0f, 0.004f, 0.55f), new Vector2(1.9f, 1.3f));
                Sofa(root, new Vector3(1.25f, 0f, 0.4f), 1.5f, fabric, cushion);
                tops.Add(new Room.Surface(new Vector3(1.25f, 0.45f, 0.4f), Vector3.up, Vector3.right, new Vector2(0.8f, 1.5f)));
                Shelf(root, new Vector3(-1.35f, 0f, 1.3f), new Vector3(0.6f, 0.95f, 0.35f), 2, darkWood);
                tops.Add(new Room.Surface(new Vector3(-1.35f, 0.95f, 1.3f), Vector3.up, Vector3.right, new Vector2(0.6f, 0.35f)));
                Window(root, new Vector3(-1.0f, 1.6f, back), toRoomFromBack, new Vector2(0.9f, 0.95f));
                Art(root, new Vector3(0.45f, 1.62f, back), toRoomFromBack, new Vector2(0.7f, 0.5f), 29);
                Lamp(root, new Vector3(-1.45f, 0f, 0.85f));
                Plant(root, new Vector3(0.7f, 0f, 1.3f));
            }
            else
            {
                // An open floor in front. Along the back wall, left to right: a plant, a lamp, a bookshelf, a low
                // cabinet with art above it. The window on the left wall, the sofa along the right.
                Rug(root, new Vector3(0f, 0.004f, 1.4f), new Vector2(2.1f, 1.5f));
                Plant(root, new Vector3(-side + 0.3f, 0f, back - 0.28f));
                Lamp(root, new Vector3(-1.15f, 0f, back - 0.3f));
                Shelf(root, new Vector3(-0.55f, 0f, back - 0.18f), new Vector3(0.7f, 1.9f, 0.35f), 4, darkWood);
                tops.Add(new Room.Surface(new Vector3(-0.55f, 1.9f, back - 0.18f), Vector3.up, Vector3.right, new Vector2(0.7f, 0.35f)));
                Box(root, "Cabinet", new Vector3(0.45f, 0.25f, back - 0.21f), new Vector3(1.0f, 0.5f, 0.42f), darkWood);
                tops.Add(new Room.Surface(new Vector3(0.45f, 0.5f, back - 0.21f), Vector3.up, Vector3.right, new Vector2(1.0f, 0.42f)));
                Plant(root, new Vector3(0.75f, 0.5f, back - 0.21f), 0.45f);
                Art(root, new Vector3(0.4f, 1.5f, back), toRoomFromBack, new Vector2(0.8f, 0.55f), 11);
                Art(root, new Vector3(1.3f, 1.62f, back), toRoomFromBack, new Vector2(0.36f, 0.46f), 29);
                Window(root, new Vector3(-side, 1.55f, 1.95f), toRoomFromLeft, new Vector2(1.0f, 1.1f));
                Sofa(root, new Vector3(side - 0.45f, 0f, 1.6f), 1.3f, fabric, cushion);
                tops.Add(new Room.Surface(new Vector3(side - 0.45f, 0.45f, 1.6f), Vector3.up, Vector3.right, new Vector2(0.8f, 1.3f)));
            }

            // Dusk: a cool fill through the window, warm lamplight and a ceiling light, a soft ambient.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.3f, 0.28f, 0.33f);
            var dusk = new GameObject("Dusk Light").AddComponent<Light>();
            dusk.type = LightType.Directional;
            dusk.color = new Color(0.85f, 0.75f, 0.95f);
            dusk.intensity = 0.45f;
            // No shadows: the worlds' bubbles, fish and particles would cast dark smudges on the walls.
            dusk.shadows = LightShadows.None;
            dusk.transform.rotation = Quaternion.Euler(35f, 70f, 0f);
            var ceiling = new GameObject("Room Light").AddComponent<Light>();
            ceiling.type = LightType.Point;
            ceiling.color = new Color(1f, 0.88f, 0.72f);
            ceiling.intensity = 1.6f;
            ceiling.range = 6f;
            ceiling.transform.position = new Vector3(0f, height - 0.3f, midZ + 0.6f);

            Room.Set(0f, height,
                new[]
                {
                    new Room.Surface(new Vector3(0f, height * 0.5f, back), Vector3.back, Vector3.right, new Vector2(width, height)),
                    new Room.Surface(new Vector3(-side, height * 0.5f, midZ), Vector3.right, Vector3.back, new Vector2(depth, height)),
                    new Room.Surface(new Vector3(side, height * 0.5f, midZ), Vector3.left, Vector3.forward, new Vector2(depth, height)),
                    new Room.Surface(new Vector3(0f, height * 0.5f, behind), Vector3.forward, Vector3.left, new Vector2(width, height))
                },
                tables, tops);
        }

        // A quad's visible face points along -forward, so to face along n it looks along -n.
        static Quaternion Facing(Vector3 intoRoom) => Quaternion.LookRotation(-intoRoom, Vector3.up);

        static void Window(Transform root, Vector3 wallPoint, Vector3 intoRoom, Vector2 size)
        {
            var turn = Facing(intoRoom);
            var right = turn * Vector3.right;
            Quad(root, "Window Sky", wallPoint + intoRoom * 0.005f, turn, size, Mat(Color.black, 0f, SkyTexture(), Vector2.one, emissionMap: true));
            var frame = Mat(new Color(0.93f, 0.92f, 0.9f), 0.4f);
            var at = wallPoint + intoRoom * 0.025f;
            float hw = size.x * 0.5f, hh = size.y * 0.5f;
            Box(root, "Window Top", at + Vector3.up * (hh + 0.025f), new Vector3(size.x + 0.1f, 0.05f, 0.05f), frame, turn);
            Box(root, "Window Bottom", at - Vector3.up * (hh + 0.025f), new Vector3(size.x + 0.1f, 0.05f, 0.05f), frame, turn);
            Box(root, "Window Side", at + right * (hw + 0.025f), new Vector3(0.05f, size.y, 0.05f), frame, turn);
            Box(root, "Window Side", at - right * (hw + 0.025f), new Vector3(0.05f, size.y, 0.05f), frame, turn);
            Box(root, "Window Mullion", at, new Vector3(0.03f, size.y, 0.04f), frame, turn);
            Box(root, "Window Sill", wallPoint + intoRoom * 0.07f - Vector3.up * (hh + 0.06f), new Vector3(size.x + 0.16f, 0.03f, 0.14f), frame, turn);
        }

        static void Art(Transform root, Vector3 wallPoint, Vector3 intoRoom, Vector2 size, int seed)
        {
            var turn = Facing(intoRoom);
            Box(root, "Art Frame", wallPoint + intoRoom * 0.0125f, new Vector3(size.x + 0.06f, size.y + 0.06f, 0.025f), Mat(new Color(0.1f, 0.09f, 0.08f), 0.4f), turn);
            Quad(root, "Art", wallPoint + intoRoom * 0.027f, turn, size, Mat(Color.white, 0.1f, ArtTexture(seed), Vector2.one));
        }

        static void Sofa(Transform root, Vector3 floorCentre, float length, Material fabric, Material cushion)
        {
            // Along the right-hand wall, facing into the room (towards -x).
            Box(root, "Sofa Seat", floorCentre + new Vector3(0f, 0.22f, 0f), new Vector3(0.8f, 0.44f, length), fabric);
            Box(root, "Sofa Back", floorCentre + new Vector3(0.32f, 0.55f, 0f), new Vector3(0.18f, 0.6f, length), fabric);
            Box(root, "Sofa Arm", floorCentre + new Vector3(0f, 0.32f, -length * 0.5f - 0.08f), new Vector3(0.8f, 0.64f, 0.16f), fabric);
            Box(root, "Sofa Arm", floorCentre + new Vector3(0f, 0.32f, length * 0.5f + 0.08f), new Vector3(0.8f, 0.64f, 0.16f), fabric);
            // Two soft pillows leaning on the back.
            foreach (var z in new[] { -0.25f * length, 0.25f * length })
            {
                var pillow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pillow.name = "Pillow";
                Object.DestroyImmediate(pillow.GetComponent<Collider>());
                pillow.transform.SetParent(root, false);
                pillow.transform.SetPositionAndRotation(floorCentre + new Vector3(0.17f, 0.62f, z), Quaternion.Euler(0f, 0f, 14f));
                pillow.transform.localScale = new Vector3(0.13f, 0.34f, 0.4f);
                pillow.GetComponent<Renderer>().sharedMaterial = cushion;
            }
        }

        static void Shelf(Transform root, Vector3 floorCentre, Vector3 size, int levels, Material wood)
        {
            // Sides, top and boards (the shelf runs along x), filled with books of many colours.
            const float thick = 0.03f;
            Box(root, "Shelf Top", floorCentre + new Vector3(0f, size.y - thick * 0.5f, 0f), new Vector3(size.x, thick, size.z), wood);
            Box(root, "Shelf Side", floorCentre + new Vector3(-size.x * 0.5f, size.y * 0.5f, 0f), new Vector3(thick, size.y, size.z), wood);
            Box(root, "Shelf Side", floorCentre + new Vector3(size.x * 0.5f, size.y * 0.5f, 0f), new Vector3(thick, size.y, size.z), wood);
            Box(root, "Shelf Back", floorCentre + new Vector3(0f, size.y * 0.5f, size.z * 0.5f - 0.01f), new Vector3(size.x, size.y, 0.02f), wood);
            var random = new System.Random(7);
            var bookColors = new[]
            {
                new Color(0.7f, 0.2f, 0.2f), new Color(0.2f, 0.4f, 0.6f), new Color(0.85f, 0.7f, 0.3f), new Color(0.3f, 0.55f, 0.4f),
                new Color(0.55f, 0.35f, 0.6f), new Color(0.9f, 0.85f, 0.75f), new Color(0.2f, 0.2f, 0.25f)
            };
            var mats = new Material[bookColors.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = Mat(bookColors[i], 0.25f);
            float step = size.y / levels;
            for (int level = 0; level < levels; level++)
            {
                float y = level * step;
                Box(root, "Shelf Board", floorCentre + new Vector3(0f, y + thick * 0.5f, 0f), new Vector3(size.x, thick, size.z), wood);
                float at = -size.x * 0.5f + 0.04f;
                while (at < size.x * 0.5f - 0.07f)
                {
                    float w = 0.025f + (float)random.NextDouble() * 0.03f;
                    float h = Mathf.Min(step - 0.06f, 0.16f + (float)random.NextDouble() * 0.1f);
                    Box(root, "Book", floorCentre + new Vector3(at + w * 0.5f, y + thick + h * 0.5f, 0f), new Vector3(w, h, size.z * 0.8f), mats[random.Next(mats.Length)]);
                    at += w + 0.004f;
                    if (random.NextDouble() < 0.12) at += 0.08f;
                }
            }
        }

        static void Lamp(Transform root, Vector3 floor)
        {
            var metal = Mat(new Color(0.12f, 0.12f, 0.13f), 0.6f);
            Cylinder(root, "Lamp Base", floor + new Vector3(0f, 0.015f, 0f), new Vector3(0.28f, 0.015f, 0.28f), metal);
            Cylinder(root, "Lamp Pole", floor + new Vector3(0f, 0.8f, 0f), new Vector3(0.025f, 0.8f, 0.025f), metal);
            Cylinder(root, "Lamp Shade", floor + new Vector3(0f, 1.62f, 0f), new Vector3(0.38f, 0.16f, 0.38f),
                Mat(new Color(1f, 0.9f, 0.75f), 0.1f, emission: new Color(1f, 0.72f, 0.42f) * 1.3f));
            var light = new GameObject("Lamp Light").AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.72f, 0.45f);
            light.intensity = 2.4f;
            light.range = 3.5f;
            light.transform.position = floor + new Vector3(0f, 1.55f, 0f);
        }

        static void Plant(Transform root, Vector3 floor, float scale = 1f)
        {
            var pot = Mat(new Color(0.78f, 0.42f, 0.28f), 0.3f);
            var leaf = Mat(new Color(0.22f, 0.5f, 0.28f), 0.35f);
            Cylinder(root, "Pot", floor + new Vector3(0f, 0.18f, 0f) * scale, new Vector3(0.32f, 0.18f, 0.32f) * scale, pot);
            var random = new System.Random(3);
            for (int i = 0; i < 9; i++)
            {
                float a = i * 2.4f, r = 0.08f + 0.06f * (float)random.NextDouble();
                var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ball.name = "Leaves";
                Object.DestroyImmediate(ball.GetComponent<Collider>());
                ball.transform.SetParent(root, false);
                ball.transform.position = floor + new Vector3(Mathf.Cos(a) * r, 0.5f + 0.35f * (float)random.NextDouble(), Mathf.Sin(a) * r) * scale;
                ball.transform.localScale = new Vector3(0.24f, 0.32f, 0.24f) * scale;
                ball.transform.rotation = Quaternion.Euler(20f * i, a * 50f, 15f);
                ball.GetComponent<Renderer>().sharedMaterial = leaf;
            }
        }

        static void Rug(Transform root, Vector3 centre, Vector2 size) =>
            Quad(root, "Rug", centre, Quaternion.Euler(90f, 0f, 0f), size, Mat(Color.white, 0.05f, RugTexture(), Vector2.one));

        // Shapes and materials.

        static GameObject Quad(Transform root, string name, Vector3 position, Quaternion rotation, Vector2 size, Material material)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(root, false);
            quad.transform.SetPositionAndRotation(position, rotation);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            quad.GetComponent<Renderer>().sharedMaterial = material;
            return quad;
        }

        static GameObject Box(Transform root, string name, Vector3 centre, Vector3 size, Material material, Quaternion? rotation = null)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.transform.SetParent(root, false);
            box.transform.SetPositionAndRotation(centre, rotation ?? Quaternion.identity);
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }


        static GameObject Cylinder(Transform root, string name, Vector3 centre, Vector3 scale, Material material)
        {
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            Object.DestroyImmediate(cylinder.GetComponent<Collider>());
            cylinder.transform.SetParent(root, false);
            cylinder.transform.position = centre;
            cylinder.transform.localScale = scale;
            cylinder.GetComponent<Renderer>().sharedMaterial = material;
            return cylinder;
        }

        static Material Mat(Color color, float smoothness, Texture2D texture = null, Vector2 tiling = default,
            Color? emission = null, bool emissionMap = false)
        {
            var m = new Material(litShader);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            if (texture != null)
            {
                m.SetTexture("_BaseMap", texture);
                m.SetTextureScale("_BaseMap", tiling == default ? Vector2.one : tiling);
            }
            if (emission.HasValue || emissionMap)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                m.SetColor("_EmissionColor", emission ?? Color.white);
                if (emissionMap && texture != null) m.SetTexture("_EmissionMap", texture);
            }
            return m;
        }

        // Textures, drawn here so the room needs no assets.

        static Texture2D WoodTexture()
        {
            const int size = 512, planks = 8;
            var t = new Texture2D(size, size, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 8 };
            var random = new System.Random(5);
            var tones = new float[planks];
            var seams = new float[planks];
            for (int i = 0; i < planks; i++) { tones[i] = (float)random.NextDouble(); seams[i] = (float)random.NextDouble(); }
            var light = new Color(0.62f, 0.42f, 0.26f);
            var dark = new Color(0.44f, 0.28f, 0.16f);
            for (int y = 0; y < size; y++)
            {
                int plank = y * planks / size;
                float inPlank = (y * planks % size) / (float)size;
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float grain = Mathf.PerlinNoise(u * 3f + plank * 7f, y * 0.35f) * 0.6f + Mathf.Sin(u * 40f + Mathf.PerlinNoise(u * 2f, plank) * 6f) * 0.08f;
                    var c = Color.Lerp(dark, light, Mathf.Clamp01(tones[plank] * 0.6f + grain * 0.5f));
                    bool seam = inPlank < 0.035f || Mathf.Repeat(u - seams[plank], 1f) < 0.004f;
                    t.SetPixel(x, y, seam ? c * 0.55f : c);
                }
            }
            t.Apply();
            return t;
        }

        static Texture2D SkyTexture()
        {
            const int size = 256;
            var t = new Texture2D(size, size, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp };
            var random = new System.Random(9);
            var stars = new HashSet<int>();
            for (int i = 0; i < 40; i++) stars.Add(random.Next(size) + size * (size / 2 + random.Next(size / 2)));
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)size;
                var sky = v < 0.35f ? Color.Lerp(new Color(1f, 0.58f, 0.32f), new Color(0.85f, 0.4f, 0.5f), v / 0.35f)
                    : Color.Lerp(new Color(0.85f, 0.4f, 0.5f), new Color(0.16f, 0.14f, 0.38f), (v - 0.35f) / 0.65f);
                for (int x = 0; x < size; x++)
                {
                    float hills = 0.12f + 0.1f * Mathf.PerlinNoise(x * 0.02f, 3f) + 0.05f * Mathf.PerlinNoise(x * 0.08f, 7f);
                    var c = v < hills ? new Color(0.08f, 0.07f, 0.12f) : sky;
                    if (stars.Contains(x + size * y)) c = Color.white;
                    t.SetPixel(x, y, c);
                }
            }
            t.Apply();
            return t;
        }

        static Texture2D RugTexture()
        {
            const int size = 256;
            var t = new Texture2D(size, size, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Clamp };
            var field = new Color(0.5f, 0.16f, 0.2f);
            var border = new Color(0.9f, 0.82f, 0.66f);
            var ink = new Color(0.16f, 0.24f, 0.36f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                    Color c = edge < 0.05f ? border : edge < 0.08f ? ink : field;
                    float d = Mathf.Abs(u - 0.5f) + Mathf.Abs(v - 0.5f);
                    if (edge >= 0.08f && Mathf.Repeat(d * 10f, 1f) < 0.12f) c = border * 0.9f;
                    if (edge >= 0.08f && d < 0.12f) c = ink;
                    t.SetPixel(x, y, c * (0.92f + 0.08f * Mathf.PerlinNoise(x * 0.3f, y * 0.3f)));
                }
            t.Apply();
            return t;
        }

        static Texture2D ArtTexture(int seed)
        {
            const int size = 256;
            var t = new Texture2D(size, size, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Clamp };
            var random = new System.Random(seed);
            var palette = new[] { new Color(0.95f, 0.55f, 0.35f), new Color(0.3f, 0.6f, 0.75f), new Color(0.95f, 0.8f, 0.4f), new Color(0.6f, 0.35f, 0.6f) };
            var ground = new Color(0.93f, 0.9f, 0.84f);
            var blobs = new List<(Vector2 at, float r, Color c)>();
            for (int i = 0; i < 4; i++)
                blobs.Add((new Vector2((float)random.NextDouble(), (float)random.NextDouble()), 0.15f + 0.2f * (float)random.NextDouble(), palette[i]));
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x / (float)size, y / (float)size);
                    var c = ground;
                    foreach (var (at, r, colour) in blobs)
                        if (Vector2.Distance(p, at) < r) c = Color.Lerp(c, colour, 0.85f);
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }
    }
}
