namespace MistForest.Core.GameRefactored
{
    /// <summary>
    /// Counts how many owner turns an ability stays blocked. Every ability ticks its cooldown at
    /// the owner's turn start, so one implementation replaces the per-role skip flags that used to
    /// compensate for round-end bookkeeping.
    /// </summary>
    public readonly struct Cooldown
    {
        private Cooldown(int remaining, bool startedThisTurn)
        {
            Remaining = remaining;
            StartedThisTurn = startedThisTurn;
        }

        /// <summary>Owner turns still blocked. Zero means the ability is ready.</summary>
        public int Remaining { get; }

        /// <summary>Whether the cooldown began after the current turn had already started.</summary>
        private bool StartedThisTurn { get; }

        public bool IsReady => Remaining == 0;

        /// <summary>A ready cooldown.</summary>
        public static Cooldown None => default;

        /// <summary>A cooldown that still blocks the given number of owner turns.</summary>
        public static Cooldown New(int remaining) => new(remaining, false);

        /// <summary>
        /// Blocks the given number of owner turns starting with the next one. The current turn has
        /// already begun, so the first tick is skipped and the count survives until then.
        /// </summary>
        public Cooldown Use(int turns) => new(turns, true);

        /// <summary>Advances one owner turn.</summary>
        public Cooldown Tick()
            => StartedThisTurn ? new(Remaining, false) : new(Remaining > 0 ? Remaining - 1 : 0, false);
    }
}
