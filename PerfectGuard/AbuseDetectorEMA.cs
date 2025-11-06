using Mirror;

namespace Marioalexsan.PerfectGuard;

public enum SuspicionLevel
{
    Normal,
    Suspicious,
    Confirmed
}

internal class AbuseDetectorEMA
{
    public static List<AbuseDetectorEMA> AllDetectors { get; } = [];

    public static void RunActorCleanup()
    {
        for (int i = 0; i < AllDetectors.Count; i++)
        {
            var playerData = AllDetectors[i].PlayerData;

            foreach (var pair in playerData.ToArray())
            {
                if (!pair.Key)
                    playerData.Remove(pair.Key);
            }
        }
    }

    public AbuseDetectorEMA(double suspicionRate) : this(suspicionRate, suspicionRate * 4) { }

    public AbuseDetectorEMA(double suspicionRate, double confirmRate)
    {
        // Enforce that confirmRate >= suspicionRate
        SupicionRate = suspicionRate;
        ConfirmRate = Math.Max(confirmRate, suspicionRate);

        SuspicionTimeBetweenEvents = TimeSpan.FromSeconds(1 / SupicionRate);
        ConfirmTimeBetweenEvents = TimeSpan.FromSeconds(1 / ConfirmRate);

        AllDetectors.Add(this);
    }

    public double SupicionRate { get; }
    public double ConfirmRate { get; }
    public double Factor { get; } = 0.3;

    public TimeSpan SuspicionTimeBetweenEvents { get; }
    public TimeSpan ConfirmTimeBetweenEvents { get; }

    public SuspicionLevel Suspicion { get; private set; } = SuspicionLevel.Normal;

    public event EventHandler<NetworkBehaviour>? OnSuspicionRaised;
    public event EventHandler<NetworkBehaviour>? OnConfirmRaised;

    struct TrackedData
    {
        public DateTime TimeSinceLastEvent;
        public TimeSpan TimeBetweenEventsEMA;
        public SuspicionLevel Suspicion;
    }

    private readonly Dictionary<NetworkBehaviour, TrackedData> PlayerData = [];

    /// <summary>
    /// Tracks event and returns true if the current player behaviour seems normal.
    /// </summary>
    /// <param name="player">The player to track event for</param>
    /// <returns>True if current behaviour should be considered as normal, false otherwise</returns>
    public void TrackEvent(NetworkBehaviour player)
    {
        var currentTime = DateTime.Now;

        if (!PlayerData.TryGetValue(player, out var playerData))
        {
            PlayerData[player] = new TrackedData()
            {
                TimeSinceLastEvent = currentTime,
                TimeBetweenEventsEMA = SuspicionTimeBetweenEvents * 2,
                Suspicion = SuspicionLevel.Normal
            };
            return;
        }

        var timeSinceLastEvent = currentTime - playerData.TimeSinceLastEvent;
        var currentEMA = Factor * timeSinceLastEvent + (1 - Factor) * playerData.TimeBetweenEventsEMA;

        playerData.TimeSinceLastEvent = currentTime;
        playerData.TimeBetweenEventsEMA = currentEMA;

        var newSuspicionLevel = SuspicionLevel.Normal;

        if (currentEMA <= ConfirmTimeBetweenEvents)
        {
            newSuspicionLevel = SuspicionLevel.Confirmed;

            if (newSuspicionLevel > playerData.Suspicion)
                OnConfirmRaised?.Invoke(this, player);
        }

        else if (currentEMA <= SuspicionTimeBetweenEvents)
        {
            newSuspicionLevel = SuspicionLevel.Suspicious;

            if (newSuspicionLevel > playerData.Suspicion)
                OnSuspicionRaised?.Invoke(this, player);
        }

        PlayerData[player] = playerData with { Suspicion = newSuspicionLevel };
        Suspicion = newSuspicionLevel;
    }
}
