using System;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// Where the cassettes stand round the turntable, and how it turns. Scroll is the place in the song list that faces
    /// the player (whole numbers put a cassette squarely at the front); each cassette's angle round the ring follows
    /// from how far it is from the front. When the cassettes fit round the rim they are spaced evenly; when there are
    /// more than fit, the ones at the front keep their full size and spacing so they stay readable, and the ones
    /// further round get smaller and closer together, the way a carousel crowds towards its back.
    /// It turns like a heavy, well-oiled turntable. A pinch on its edge holds it (StartGrip, Grip): it follows the hand
    /// closely, smoothed so tracking jitter never jerks it, and keeps the hand's speed when let go (Release). An open
    /// hand brushes it (Brush): its speed is pulled softly towards the hand's, so a quick slide flicks it into a spin and
    /// a slow one nudges it, and a hand resting in it brakes it. Spinning freely it glides on with little friction,
    /// its friction stretched or shortened by a few percent so that it comes to rest on a cassette, and once slow it
    /// settles there with a soft spring. A pinch on a spinning edge slips at first and brakes it to the hand's speed
    /// before it holds, so catching it is a quick, smooth brake rather than a jolt.
    /// </summary>
    public sealed class DeckRing
    {
        /// <summary>How the turntable feels under the hands. The world menu keeps it, so it can be tuned in the scene.</summary>
        [Serializable]
        public sealed class Tuning
        {
            [Tooltip("How quickly a free spin loses speed, per second. Lower glides longer: with the stop friction, a strong flick glides about four seconds.")]
            [SerializeField] float friction = 0.35f;
            [Tooltip("A steady slowing on top of the friction (cassettes per second, each second), so even a slow spin comes to rest.")]
            [SerializeField] float stopFriction = 0.45f;
            [Tooltip("The fastest it spins, in cassettes per second, however hard the flick.")]
            [SerializeField] float topSpeed = 6f;
            [Tooltip("How strongly an open hand moving across the cassettes pulls the turntable's speed towards its own, per second. Higher follows the hand more closely.")]
            [SerializeField] float softCoupling = 5f;
            [Tooltip("How quickly a hand put back into a spinning turntable, or a pinch on its edge, brakes it, per second.")]
            [SerializeField] float catchBraking = 9f;
            [Tooltip("How long a pinched edge takes to catch up with the hand (s): enough to smooth away tracking jitter, little enough to feel held.")]
            [SerializeField] float gripSmoothing = 0.05f;
            [Tooltip("Below this speed (cassettes per second) a spin stops gliding and settles onto the cassette it is reaching.")]
            [SerializeField] float settleSpeed = 1f;

            public float Friction => Mathf.Max(friction, 0.01f);
            public float StopFriction => Mathf.Max(stopFriction, 0.01f);
            public float SettleSpeed => Mathf.Max(settleSpeed, 0.1f);
            public float TopSpeed => Mathf.Max(topSpeed, SettleSpeed + 0.5f);
            public float SoftCoupling => Mathf.Max(softCoupling, 0f);
            public float CatchBraking => Mathf.Max(catchBraking, 0.5f);
            public float GripSmoothing => Mathf.Max(gripSmoothing, 0.005f);
        }

        /// <summary>How spread out the full-size cassettes at the front are, in cassettes.</summary>
        const float Spread = 1.5f;
        /// <summary>The smallest a cassette at the back may get.</summary>
        const float SmallestScale = 0.35f;
        /// <summary>The settling spring's stiffness, and its damping as a share of critical (a touch under, for a soft landing).</summary>
        const float Stiffness = 40f, DampingRatio = 0.92f;
        /// <summary>A pinch on a spinning edge stops slipping and holds once the turntable is within this speed of the hand (cassettes per second).</summary>
        const float HoldSpeed = 0.25f;
        /// <summary>How far a glide's friction may be stretched or shortened to stop it on a cassette (factors).</summary>
        const float SteerLeast = 0.6f, SteerMost = 1.6f;
        /// <summary>How long the held hand's speed is averaged over for the spin it gives when let go (s).</summary>
        const float GripSpeedSmoothing = 0.04f;

        readonly Tuning feel;
        float backScale = 1f, total = 1f;
        bool even = true;
        float target = float.NaN;
        // What the hands did since the last step: a moving hand carried it along, or a resting hand braked it.
        bool driven, braked;
        // Caught (a cassette taken from it while it spins): it brakes until it is slow enough to settle.
        bool catching;
        // The grip: where the scroll was when the pinch took hold, the hand's travel since then smoothed in two steps,
        // the hand's speed, how far the turntable slipped under the fingers while it was braked, and whether it now holds.
        bool gripped, holding;
        float gripOrigin, gripRough, gripSmooth, gripSpeed, slip;

        public DeckRing(Tuning feel)
        {
            this.feel = feel ?? new Tuning();
        }

        /// <summary>How the turntable feels under the hands.</summary>
        public Tuning Feel => feel;

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

        /// <summary>True while it turns, glides or settles, false once a cassette sits still at the front.</summary>
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
            float shift = front + fraction - Scroll;
            Scroll += shift;
            gripOrigin += shift;
            target = float.NaN;
        }

        /// <summary>Puts cassette <paramref name="i"/> at the front at once, still (a pinch holding the edge keeps hold).</summary>
        public void Face(int i)
        {
            gripOrigin += i - Scroll;
            Scroll = i;
            Velocity = 0f;
            target = float.NaN;
            catching = driven = braked = false;
        }

        // ---- The hard area: a pinch on the edge ----

        /// <summary>
        /// A pinch takes hold of the edge. A still turntable is held at once; a spinning one slips under the fingers at
        /// first and is braked to the hand's speed (catch braking), then held where it got to.
        /// </summary>
        public void StartGrip()
        {
            gripped = true;
            gripOrigin = Scroll;
            gripRough = gripSmooth = gripSpeed = slip = 0f;
            holding = Mathf.Abs(Velocity) < HoldSpeed;
            if (holding) Velocity = 0f;
            target = float.NaN;
            catching = false;
        }

        /// <summary>
        /// The held edge has been turned <paramref name="travel"/> cassettes by the hand since the pinch took hold
        /// (positive brings later songs to the front). Smoothed in two gentle steps, so the jitter of hand tracking is
        /// lost and the hand's motion kept; its speed is remembered for the spin when the pinch opens.
        /// </summary>
        public void Grip(float travel, float dt)
        {
            if (!gripped || dt <= 1e-5f) return;
            float k = 1f - Mathf.Exp(-dt / (feel.GripSmoothing * 0.5f));
            float before = gripSmooth;
            gripRough += (travel - gripRough) * k;
            gripSmooth += (gripRough - gripSmooth) * k;
            gripSpeed = Mathf.Lerp(gripSpeed, (gripSmooth - before) / dt, 1f - Mathf.Exp(-dt / GripSpeedSmoothing));
        }

        /// <summary>
        /// The pinch opens: the turntable spins on at the hand's speed (no faster than the top speed). With
        /// <paramref name="keepSpeed"/> false (the hand was lost, say) it just settles.
        /// </summary>
        public void Release(bool keepSpeed)
        {
            if (!gripped) return;
            gripped = false;
            float speed = holding ? gripSpeed : Velocity;
            Velocity = keepSpeed ? Mathf.Clamp(speed, -feel.TopSpeed, feel.TopSpeed) : 0f;
            target = float.NaN;
        }

        // ---- The soft area: an open hand over the cassettes or the platter ----

        /// <summary>
        /// An open hand in the soft area pulls the turntable's speed towards <paramref name="speed"/> (cassettes per
        /// second) at <paramref name="rate"/> per second, like a finger on a spinning record. A <paramref name="moving"/>
        /// hand carries it along, with no friction and no settling while it does; a resting one only brakes it, and lets
        /// it settle once it is slow.
        /// </summary>
        public void Brush(float speed, float rate, bool moving, float dt)
        {
            if (gripped || dt <= 0f || Count == 0) return;
            // A resting hand doesn't fight the settling spring.
            if (!moving && !float.IsNaN(target)) return;
            Velocity = speed + (Velocity - speed) * Mathf.Exp(-rate * dt);
            if (moving)
            {
                driven = true;
                catching = false;
                target = float.NaN;
            }
            else braked = true;
        }

        /// <summary>Caught while spinning (a cassette taken from it): it brakes quickly, then settles.</summary>
        public void Catch()
        {
            if (gripped || Mathf.Abs(Velocity) <= feel.SettleSpeed) return;
            catching = true;
            target = float.NaN;
        }

        // ---- Each frame ----

        /// <summary>One frame of turning: held, carried by a hand, gliding, or settling onto a cassette.</summary>
        public void Step(float dt)
        {
            bool wasDriven = driven, wasBraked = braked;
            driven = braked = false;
            if (Count == 0 || dt <= 0f) return;
            if (gripped)
            {
                if (holding)
                {
                    Scroll = gripOrigin + gripSmooth + slip;
                    Velocity = gripSpeed;
                }
                else
                {
                    // Slipping under the fingers until braked to the hand's speed, then held where it got to.
                    Velocity = gripSpeed + (Velocity - gripSpeed) * Mathf.Exp(-feel.CatchBraking * dt);
                    Scroll += Velocity * dt;
                    slip = Scroll - gripOrigin - gripSmooth;
                    holding = Mathf.Abs(Velocity - gripSpeed) < HoldSpeed;
                }
                KeepSmall();
                return;
            }

            Velocity = Mathf.Clamp(Velocity, -feel.TopSpeed, feel.TopSpeed);
            bool braking = wasBraked || catching;
            if (catching)
            {
                Velocity *= Mathf.Exp(-feel.CatchBraking * dt);
                if (Mathf.Abs(Velocity) <= feel.SettleSpeed) catching = false;
            }
            // Carried by a hand or braked by one, it just goes at the speed the hand left it; free, it glides.
            if (wasDriven) Scroll += Velocity * dt;
            else if (float.IsNaN(target) && Mathf.Abs(Velocity) > feel.SettleSpeed)
            {
                if (braking) Scroll += Velocity * dt;
                else Glide(dt);
            }
            // Caught, it settles on the cassette nearest where the brake would stop it, rather than rolling on to the next.
            else Settle(dt, braking ? 1f / feel.CatchBraking : SettleLead);
            KeepSmall();
        }

        /// <summary>
        /// A free spin: a little friction that grows with speed and a little that doesn't, so a strong flick glides for
        /// seconds and a slow spin still stops. The friction is stretched or shortened (by a few percent on a long glide)
        /// so that the spin will end with a cassette at the front, and the settling spring has almost nothing to correct.
        /// </summary>
        void Glide(float dt)
        {
            float u = Mathf.Abs(Velocity), sign = Mathf.Sign(Velocity);
            float mu = feel.Friction, c = feel.StopFriction, slow = feel.SettleSpeed;
            float glide = GlideDistance(u, slow, mu, c);
            float lead = slow * SettleLead;
            // Where it would come to rest as it is, the cassette nearest that, and the glide that would end there.
            float rest = Scroll + sign * (glide + lead);
            float wanted = sign * (Mathf.Round(rest) - Scroll) - lead;
            float steer = wanted > 1e-4f ? Mathf.Clamp(glide / wanted, SteerLeast, SteerMost) : SteerMost;
            float a = mu * steer, b = c * steer;
            // Exact for this friction over the frame: du/dt = -(a u + b).
            float next = (u + b / a) * Mathf.Exp(-a * dt) - b / a;
            float moved;
            if (next > 0f) moved = (u - next - b * dt) / a;
            else
            {
                float stopTime = Mathf.Log((u + b / a) / (b / a)) / a;
                moved = (u - b * stopTime) / a;
                next = 0f;
            }
            Scroll += sign * moved;
            Velocity = sign * next;
        }

        /// <summary>How far a spin at <paramref name="u"/> glides before it is down to <paramref name="slow"/>, under friction <paramref name="mu"/> and <paramref name="c"/>.</summary>
        static float GlideDistance(float u, float slow, float mu, float c)
        {
            if (u <= slow) return 0f;
            return ((u - slow) - c / mu * Mathf.Log((mu * u + c) / (mu * slow + c))) / mu;
        }

        /// <summary>How far ahead of the scroll, per unit of speed, the settling spring aims, so that it takes over without a jolt.</summary>
        static float SettleLead => 2f * DampingRatio / Mathf.Sqrt(Stiffness);

        /// <summary>
        /// Settles on the cassette it is reaching, <paramref name="lead"/> seconds of its speed ahead, with a spring a
        /// little short of critically damped.
        /// </summary>
        void Settle(float dt, float lead)
        {
            if (float.IsNaN(target)) target = Mathf.Round(Scroll + Velocity * lead);
            float damping = 2f * Mathf.Sqrt(Stiffness) * DampingRatio;
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
        }

        /// <summary>Keeps the numbers small; the ring wraps round anyway.</summary>
        void KeepSmall()
        {
            if (Mathf.Abs(Scroll) <= Count * 8) return;
            float shift = Count * Mathf.Round(Scroll / Count);
            Scroll -= shift;
            target -= shift;
            gripOrigin -= shift;
        }
    }
}
