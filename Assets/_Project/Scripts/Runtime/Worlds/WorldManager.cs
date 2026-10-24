using System;
using System.Collections.Generic;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>Keeps exactly one world active. The music keeps playing across switches.</summary>
    [DefaultExecutionOrder(-40)]
    public class WorldManager : MonoBehaviour
    {
        [SerializeField] World[] worlds = Array.Empty<World>();
        [SerializeField] int startWorld;

        public static WorldManager Instance { get; private set; }

        public IReadOnlyList<World> Worlds => worlds;
        public int Current { get; private set; } = -1;

        public event Action<int> WorldChanged;

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

        public void Switch(int index)
        {
            if (index == Current || index < 0 || index >= worlds.Length || worlds[index] == null) return;
            if (Current >= 0) worlds[Current].gameObject.SetActive(false);
            Current = index;
            worlds[index].gameObject.SetActive(true);
            WorldChanged?.Invoke(index);
        }
    }
}
