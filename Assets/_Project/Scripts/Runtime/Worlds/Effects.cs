using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>Visual building blocks shared by the worlds.</summary>
    static class Effects
    {
        public static readonly Color Pink = new Color(1f, 0.37f, 0.68f);
        public static readonly Color Orange = new Color(1f, 0.7f, 0.42f);
        public static readonly Color Gold = new Color(1f, 0.83f, 0.42f);
        public static readonly Color Mint = new Color(0.49f, 0.95f, 0.79f);
        public static readonly Color Blue = new Color(0.5f, 0.72f, 1f);
        public static readonly Color Violet = new Color(0.79f, 0.61f, 1f);

        /// <summary>A collider-free quad for glows and rings.</summary>
        public static Renderer MakeQuad(string name, Transform parent, Material material)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            DebugShapes.RemoveCollider(quad);
            quad.transform.SetParent(parent, false);
            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        /// <summary>Turns a quad to face the viewer.</summary>
        public static void Face(Transform quad, Camera view)
        {
            if (view == null) return;
            var away = quad.position - view.transform.position;
            if (away.sqrMagnitude > 1e-8f) quad.rotation = Quaternion.LookRotation(away, view.transform.up);
        }

        /// <summary>The viewer's level forward direction.</summary>
        public static Vector3 FlatForward(Camera view)
        {
            var f = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up);
            return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
        }

        /// <summary>
        /// The floor height. With floor-level tracking the floor is at 0; if the head looks too low for that
        /// (eye-level tracking), assume the floor is 1.5 m below the eyes.
        /// </summary>
        public static float FloorY(Camera view) => view != null && view.transform.position.y < 0.9f ? view.transform.position.y - 1.5f : 0f;

        /// <summary>Nudges a hand's visible root by a world-space offset (fake resistance), or resets it.</summary>
        public static void OffsetHand(HandRig.Hand hand, Vector3 worldOffset)
        {
            var root = hand.VisualRoot;
            if (root == null || root.parent == null) return;
            root.localPosition = root.parent.InverseTransformVector(worldOffset);
        }
        /// <summary>
        /// A world-space particle system for small glowing motes that fade in, drift and shrink away. Nothing emits
        /// on its own: callers use Emit with a position, colour, size and lifetime per particle.
        /// </summary>
        public static ParticleSystem MakeSparkSystem(string name, Transform parent, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 800;
            main.startSpeed = 0f;
            main.gravityModifier = -0.012f;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.enabled = false;

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f)));

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.03f;
            noise.frequency = 1.2f;
            noise.scrollSpeed = 0.4f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            if (Application.isPlaying) ps.Play();
            return ps;
        }
    }
}
