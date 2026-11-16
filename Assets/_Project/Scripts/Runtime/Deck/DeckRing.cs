using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// Where the cassettes stand round the turntable, and how it turns. Scroll is the place in the song list that faces
    /// the player (whole numbers put a cassette squarely at the front); each cassette's angle round the ring follows
    /// from how far it is from the front. When the cassettes fit round the rim they are spaced evenly; when there are
    /// more than fit, the ones at the front keep their full size and spacing so they stay readable, and the ones
    /// further round get smaller and closer together, the way a carousel crowds towards its back. Turning has
    /// inertia: a flick keeps it going, friction slows it, and a gentle spring settles one cassette at the front.
    /// </summary>
    public sealed class DeckRing
    {
        /// <summary>How spread out the full-size cassettes at the front are, in cassettes.</summary>
        const float Spread = 1.5f;
        /// <summary>The smallest a cassette at the back may get.</summary>
        const float SmallestScale = 0.35f;
        /// <summary>Above this speed (cassettes per second) the turntable coasts; below it, it settles.</summary>
        const float SettleSpeed = 2.4f;
        const float MaxSpeed = 9f;
        const float Friction = 2.3f;
        const float Stiffness = 55f;

        float backScale = 1f, total = 1f;
        bool even = true, pushed;
        float target = float.NaN;

        /// <summary>How many cassettes stand on the ring.</summary>
        public int Count { get; private set; }

        /// <summary>The place in the list at the front, in cassettes.</summary>
        public float Scroll;

        /// <summary>How fast it is turning, in cassettes per second (positive brings later songs to the front).</summary>
        public float Velocity;

        /// <summary>The angle (radians) between the front cassette and the next, where the player turns it.</summary>
        public float FrontPitch { get; private set; } = 1f;

        /// <summary>The cassette at the front.</summary>
        public int Front => Count == 0 ? -1 : Wrap(Mathf.RoundToInt(Scroll));

        /// <summary>True while it coasts or settles, false once a cassette sits still at the front.</summary>
        public bool Moving => Mathf.Abs(Velocity) > 0.01f || Mathf.Abs(Scroll - Mathf.Round(Scroll)) > 0.002f;

        /// <summary>
        /// Lays out <paramref name="count"/> cassettes on a rim <paramref name="circumference"/> long, each needing
        /// <paramref name="pitch"/> of it at full size.
        /// </summary>
        public void Layout(int count, float circumference, float pitch)
        {
            Count = Mathf.Max(0, count);
            float fit = circumference / Mathf.Max(pitch, 1e-4f);
            if (Count <= fit || Count < 3)
            {
                even = true;
                backScale = 1f;
                FrontPitch = Mathf.PI * 2f / Mathf.Max(1, Count);
                return;
            }
            // Full size at the front, shrinking with distance from it: scale(p) = back + (1 - back) / (1 + (p / Spread)^2),
            // with the back size chosen so that the sizes round the ring add up to what fits.
            even = false;
            float bump = 2f * Spread * Mathf.Atan(Count / (2f * Spread));
            backScale = Mathf.Clamp((fit - bump) / (Count - bump), SmallestScale, 1f);
            total = backScale * Count + (1f - backScale) * bump;
            FrontPitch = Mathf.PI * 2f / total;
        }

        public int Wrap(int i) => Count == 0 ? 0 : ((i % Count) + Count) % Count;

        /// <summary>How far cassette <paramref name="i"/> is from the front, in cassettes, between -Count/2 and Count/2.</summary>
        public float Offset(int i)
        {
            if (Count == 0) return 0f;
            float p = i - Scroll;
            return p - Count * Mathf.Floor(p / Count + 0.5f);
        }

        /// <summary>The angle round the ring (radians, positive to the player's right) of a cassette <paramref name="offset"/> from the front.</summary>
        public float Angle(float offset)
        {
            if (even) return offset * FrontPitch;
            return Mathf.PI * 2f * (backScale * offset + (1f - backScale) * Spread * Mathf.Atan(offset / Spread)) / total;
        }

        /// <summary>The size of a cassette <paramref name="offset"/> from the front, 1 at the front.</summary>
        public float Scale(float offset)
        {
            if (even) return 1f;
            float k = offset / Spread;
            return backScale + (1f - backScale) / (1f + k * k);
        }

        /// <summary>Keeps the same cassette at the front when the list changes and it is now number <paramref name="front"/>.</summary>
        public void Renumber(int front)
        {
            float fraction = Scroll - Mathf.Round(Scroll);
            Scroll = front + fraction;
            target = float.NaN;
        }

        /// <summary>Puts cassette <paramref name="i"/> at the front at once.</summary>
        public void Face(int i)
        {
            Scroll = i;
            Velocity = 0f;
            target = float.NaN;
        }

        /// <summary>Turned by a hand: the scroll follows it, and its speed is remembered for the coast afterwards.</summary>
        public void Drag(float scroll, float dt)
        {
            if (dt > 1e-5f)
            {
                float speed = Mathf.Clamp((scroll - Scroll) / dt, -MaxSpeed, MaxSpeed);
                Velocity = Mathf.Lerp(Velocity, speed, 1f - Mathf.Exp(-14f * dt));
            }
            Scroll = scroll;
            target = float.NaN;
        }

        /// <summary>Nudged towards a speed by a hand sweeping across it, as if by friction.</summary>
        public void Push(float speed, float dt)
        {
            Velocity = Mathf.Lerp(Velocity, Mathf.Clamp(speed, -MaxSpeed, MaxSpeed), 1f - Mathf.Exp(-9f * dt));
            target = float.NaN;
            pushed = true;
        }

        /// <summary>One frame of coasting and settling when no hand is turning it.</summary>
        public void Step(float dt)
        {
            if (Count == 0 || dt <= 0f) return;
            // While a hand sweeps it, it simply goes with the hand; it settles once the hand has gone.
            if (pushed)
            {
                pushed = false;
                Scroll += Velocity * dt;
                return;
            }
            if (float.IsNaN(target) && Mathf.Abs(Velocity) > SettleSpeed)
            {
                Velocity *= Mathf.Exp(-Friction * dt);
                Scroll += Velocity * dt;
                return;
            }
            // Settle on the cassette it would reach, with a spring a little short of critically damped.
            if (float.IsNaN(target)) target = Mathf.Round(Scroll + Velocity * 0.18f);
            float damping = 2f * Mathf.Sqrt(Stiffness) * 0.92f;
            int steps = Mathf.CeilToInt(dt / 0.011f);
            float h = dt / steps;
            for (int s = 0; s < steps; s++)
            {
                Velocity += (Stiffness * (target - Scroll) - damping * Velocity) * h;
                Scroll += Velocity * h;
            }
            if (Mathf.Abs(target - Scroll) < 0.0005f && Mathf.Abs(Velocity) < 0.01f)
            {
                Scroll = target;
                Velocity = 0f;
            }
            // Keep the numbers small; the ring wraps round anyway.
            if (Mathf.Abs(Scroll) > Count * 8)
            {
                float shift = Count * Mathf.Round(Scroll / Count);
                Scroll -= shift;
                target -= shift;
            }
        }
    }
}
