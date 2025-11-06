using Mirror;

namespace Marioalexsan.PerfectGuard;

internal class AbuseDetectorTokenBucket
{
    public struct RateLimitData
    {
        public bool IsRateLimited;
        public DateTime PreviousRateLimitAt;
    }

    struct TrackedData
    {
        public DateTime LastEventTime;
        public DateTime LastRateLimitedTime;
        public double Tokens;
    }

    public static List<AbuseDetectorTokenBucket> AllDetectors { get; } = [];

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

    public AbuseDetectorTokenBucket(double rateLimit) : this(rateLimit, rateLimit * 4) { }

    public AbuseDetectorTokenBucket(double rateLimit, double burstLimit)
    {
        RateLimit = rateLimit;
        BurstLimit = Math.Max(burstLimit, rateLimit);

        // Sanity check
        if (burstLimit <= rateLimit)
            Logging.LogWarning("A token bucket configuration is incorrect!");

        AllDetectors.Add(this);
    }

    public double RateLimit { get; }
    public double BurstLimit { get; }

    private readonly Dictionary<NetworkBehaviour, TrackedData> PlayerData = [];

    /// <summary>
    /// Tracks event and returns true if the rate limit hasn't been reached yet.
    /// </summary>
    /// <param name="player">The player to track event for</param>
    /// <param name="lastRateLimitTime">Additional details about the current state of the player</param>
    /// <returns>True if current behaviour should be considered as normal, false otherwise</returns>
    public RateLimitData TrackEvent(NetworkBehaviour player)
    {
        const double TokenCost = 1.0;

        var currentTime = DateTime.Now;

        if (!PlayerData.TryGetValue(player, out var playerData))
        {
            PlayerData[player] = playerData = new TrackedData()
            {
                LastEventTime = currentTime,
                LastRateLimitedTime = DateTime.UnixEpoch,
                Tokens = BurstLimit
            };
        }

        // Refill tokens based on time elapsed
        var secondsSinceLastEvent = Math.Max((currentTime - playerData.LastEventTime).TotalSeconds, 0);
        playerData.LastEventTime = currentTime;

        playerData.Tokens = Math.Clamp(playerData.Tokens + RateLimit * secondsSinceLastEvent, 0, BurstLimit);

        bool isRateLimited = true;
        var previousRateLimitedTime = playerData.LastRateLimitedTime;

        if (playerData.Tokens >= TokenCost)
        {
            playerData.Tokens -= TokenCost;
            isRateLimited = false;
        }
        else
        {
            playerData.LastRateLimitedTime = currentTime;
        }

        PlayerData[player] = playerData;
        return new()
        {
            IsRateLimited = isRateLimited,
            PreviousRateLimitAt = previousRateLimitedTime
        };
    }
}
