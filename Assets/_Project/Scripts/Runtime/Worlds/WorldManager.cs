using System;
using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// Keeps the worlds that are on: one, or a few at once when they use different parts of the room and your body
    /// (<see cref="World.Spaces"/>). Choosing a world in the menu (<see cref="Choose"/>) turns it on beside the worlds
    /// that are on, and turns off any it clashes with, or, past <see cref="MaxWorlds"/>, the one chosen longest ago.
    /// Choosing a world that is on turns it off, though one always stays on. Of the worlds that are on, one shows the
    /// sung words and one dresses the hands (see <see cref="ChooseOwners"/>); every other hook reaches them all. The
    /// music keeps playing across changes.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class WorldManager : MonoBehaviour
    {
        /// <summary>What choosing a world does.</summary>
        public enum Choice
        {
            /// <summary>Nothing: there is no such world.</summary>
            None,
            /// <summary>It turns on beside the worlds that are on.</summary>
            Add,
            /// <summary>It turns on in place of the worlds it clashes with, or of the one chosen longest ago.</summary>
            Swap,
            /// <summary>It is on, and turns off.</summary>
            TurnOff,
            /// <summary>It is the only world on, so it stays on.</summary>
            StayOn
        }

        [SerializeField] World[] worlds = Array.Empty<World>();
        [SerializeField] int startWorld;
        [Tooltip("The most worlds that can be on at once; each one costs frame time. Choosing one more turns off the one chosen longest ago.")]
        [SerializeField, Range(1, 4)] int maxWorlds = 3;

        readonly List<int> active = new List<int>();
        readonly List<int> leaving = new List<int>();

        public static WorldManager Instance { get; private set; }

        public IReadOnlyList<World> Worlds => worlds;

        /// <summary>The worlds that are on, by index, in the order they were chosen: the last is <see cref="Current"/>.</summary>
        public IReadOnlyList<int> Active => active;

        /// <summary>The world chosen last of those that are on, or -1.</summary>
        public int Current => active.Count > 0 ? active[active.Count - 1] : -1;

        /// <summary>The most worlds that can be on at once.</summary>
        public int MaxWorlds => Mathf.Max(1, maxWorlds);

        /// <summary>Goes up each time the worlds that are on change.</summary>
        public int Version { get; private set; }

        /// <summary>The world that shows the sung words, or -1.</summary>
        public int WordsWorld { get; private set; } = -1;

        /// <summary>The world that dresses the hands, or -1.</summary>
        public int HandsWorld { get; private set; } = -1;

        /// <summary>A world was turned on (its index): by the menu, the demo tour or at the start.</summary>
        public event Action<int> WorldStarted;

        public bool IsOn(int index) => active.Contains(index);

        void Awake()
        {
            Instance = this;
            foreach (var world in worlds)
            {
                if (world != null) world.gameObject.SetActive(false);
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start() => Switch(startWorld);

        /// <summary>Turns world <paramref name="index"/> on alone, every other world off: the start, the demo tour, previews.</summary>
        public void Switch(int index)
        {
            if (!Valid(index) || (active.Count == 1 && active[0] == index)) return;
            leaving.Clear();
            foreach (int a in active)
                if (a != index) leaving.Add(a);
            Apply(leaving, active.Contains(index) ? -1 : index);
        }

        /// <summary>
        /// Chooses world <paramref name="index"/> as the menu does (see <see cref="Plan"/>): it turns on, and the worlds
        /// it clashes with turn off; or, when it is on, it turns off unless it is the only one. Returns what it did.
        /// </summary>
        public Choice Choose(int index)
        {
            var choice = Plan(index, leaving);
            if (choice == Choice.TurnOff)
            {
                leaving.Clear();
                leaving.Add(index);
                Apply(leaving, -1);
            }
            else if (choice == Choice.Add || choice == Choice.Swap) Apply(leaving, index);
            return choice;
        }

        /// <summary>
        /// What choosing world <paramref name="index"/> would do. For an Add or a Swap, <paramref name="off"/> gets the
        /// worlds it would turn off, in the order they were chosen: those that clash with it, and then, if that still
        /// leaves too many on, those chosen longest ago.
        /// </summary>
        public Choice Plan(int index, List<int> off)
        {
            off?.Clear();
            if (!Valid(index)) return Choice.None;
            if (active.Contains(index)) return active.Count > 1 ? Choice.TurnOff : Choice.StayOn;
            var chosen = worlds[index];
            int staying = 0;
            foreach (int a in active)
                if (chosen.CombinesWith(worlds[a])) staying++;
            int crowded = Mathf.Max(0, staying + 1 - MaxWorlds);
            bool swap = false;
            foreach (int a in active)
            {
                bool leaves = !chosen.CombinesWith(worlds[a]);
                if (!leaves && crowded > 0)
                {
                    leaves = true;
                    crowded--;
                }
                if (!leaves) continue;
                swap = true;
                off?.Add(a);
            }
            return swap ? Choice.Swap : Choice.Add;
        }

        bool Valid(int index) => index >= 0 && index < worlds.Length && worlds[index] != null;

        /// <summary>
        /// Turns the worlds in <paramref name="off"/> off and world <paramref name="on"/> (if any) on. A world that throws
        /// as it turns on or off is logged, and the change still goes through.
        /// </summary>
        void Apply(List<int> off, int on)
        {
            foreach (int index in off)
            {
                active.Remove(index);
                var world = worlds[index];
                // Off first: while playing that runs its OnExit (OnDisable), and whatever happens there, it stops.
                world.gameObject.SetActive(false);
                try
                {
                    world.Exit();
                }
                catch (Exception e)
                {
                    Debug.LogException(e, world);
                }
                // A world that is off is on its own again (the editor previews step worlds one at a time).
                world.SetOwners(true, true);
            }
            if (on >= 0) active.Add(on);
            ChooseOwners();
            if (on >= 0)
            {
                // Its owners are set first, so it starts dressing the hands, or not, from its first frame.
                worlds[on].gameObject.SetActive(true);
                try
                {
                    worlds[on].Enter();
                }
                catch (Exception e)
                {
                    Debug.LogException(e, worlds[on]);
                }
            }
            Version++;
            if (on >= 0) WorldStarted?.Invoke(on);
        }

        /// <summary>
        /// Picks which of the worlds that are on shows the sung words and which dresses the hands, and tells each of
        /// them. The words go to the lyric world, else to the one chosen last that shows words, else to the one chosen
        /// last. The hands go to the world you play with them most: the one that uses the Hands space; else one whose
        /// pinches, fists or things within reach your hands work (its feedback on the hands, such as Note rain's
        /// filling palm or Sand table's under-light, belongs to what they are doing); else the one chosen last.
        /// </summary>
        void ChooseOwners()
        {
            const World.Space Played = World.Space.Pinch | World.Space.Fists | World.Space.InFront;
            int words = -1, hands = -1, played = -1;
            // The newest first, so the one chosen last wins unless an older one is a lyric world or plays more with the
            // hands. (Two lyric worlds, or two that use the hands, are never on together.)
            for (int i = active.Count - 1; i >= 0; i--)
            {
                int index = active[i];
                var world = worlds[index];
                if (world.IsLyric || (words < 0 && world.ShowsWords)) words = index;
                if (hands < 0 && (world.Spaces & World.Space.Hands) != 0) hands = index;
                if (played < 0 && (world.Spaces & Played) != 0) played = index;
            }
            if (words < 0) words = Current;
            if (hands < 0) hands = played >= 0 ? played : Current;
            WordsWorld = words;
            HandsWorld = hands;
            foreach (int index in active) worlds[index].SetOwners(index == words, index == hands);
        }
    }
}
