using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Marioalexsan.PerfectGuard.Patches;
using Marioalexsan.PerfectGuard.SoftDependencies;
using Mirror;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Marioalexsan.PerfectGuard;

[BepInPlugin(ModInfo.GUID, ModInfo.NAME, ModInfo.VERSION)]
[BepInDependency(EasySettings.ModID, BepInDependency.DependencyFlags.SoftDependency)]
public class PerfectGuard : BaseUnityPlugin
{
    public static PerfectGuard Plugin => _plugin ?? throw new InvalidOperationException($"{nameof(PerfectGuard)} hasn't been initialized yet. Either wait until initialization, or check via ChainLoader instead.");
    private static PerfectGuard? _plugin;

    internal new ManualLogSource Logger { get; private set; }

    private readonly Harmony _harmony = new(ModInfo.GUID);

    public static ConfigEntry<bool> DetailedLogging { get; private set; } = null!;

    private static ConfigEntry<bool> EnablePerfectGuard = null!;
    private static ConfigEntry<bool> NetworkRateLimiting = null!;
    private static ConfigEntry<bool> ItemCleanup = null!;
    private static ConfigEntry<bool> AudioRateLimiting = null!;
    private static ConfigEntry<float> MaxItemsThreshold = null!;

    // Store cached values so that these are applied on EasySettings's apply only
    public static bool NetworkRateLimitingEnabled { get; private set; }
    public static bool AudioRateLimitingEnabled { get; private set; }
    public static bool ItemCleanupEnabled { get; private set; }

    private TimeSpan _abuseDetectorCleanupAccumulator;
    private TimeSpan _miscAccumulator;

    public PerfectGuard()
    {
        _plugin = this;
        Logger = base.Logger;

        EnablePerfectGuard = Config.Bind("General", nameof(EnablePerfectGuard), true, "Enable or disable all features of this mod.");
        DetailedLogging = Config.Bind("General", nameof(DetailedLogging), true, "Log detailed warnings for detected malicious activities.");

        NetworkRateLimiting = Config.Bind("Protections", nameof(NetworkRateLimiting), true, "Enables rate limits for network spam, such as visual effects and other abuse.");
        AudioRateLimiting = Config.Bind("Protections", nameof(AudioRateLimiting), true, "Enables audio rate limits to prevent spam.");
        ItemCleanup = Config.Bind("Protections", nameof(ItemCleanup), true, "Enable cleanup for excessive item drops.");
        
        MaxItemsThreshold = Config.Bind("Tuning", nameof(MaxItemsThreshold), 150f, new ConfigDescription("The maximum number of items allowed before the items are forcefully cleaned up.", new AcceptableValueRange<float>(50, 500)));

        UpdateEnableValues();
    }

    private static void UpdateEnableValues()
    {
        NetworkRateLimitingEnabled = EnablePerfectGuard.Value && NetworkRateLimiting.Value;
        AudioRateLimitingEnabled = EnablePerfectGuard.Value && AudioRateLimiting.Value;
        ItemCleanupEnabled = EnablePerfectGuard.Value && ItemCleanup.Value;
    }

    public void Awake()
    {
        _harmony.PatchAll();

        if (EasySettings.IsAvailable)
        {
            EasySettings.OnInitialized.AddListener(() =>
            {
                EasySettings.AddHeader($"{ModInfo.NAME}");
                EasySettings.AddToggle($"Enable PerfectGuard (recommended)", EnablePerfectGuard);
                EasySettings.AddToggle($"Detailed logging", DetailedLogging);
                EasySettings.AddToggle($"Network rate limits (recommended)", NetworkRateLimiting);
                EasySettings.AddToggle($"Audio rate limits", AudioRateLimiting);
                EasySettings.AddToggle($"Enable item cleanup", ItemCleanup);
                EasySettings.AddSlider($"Item cleanup max items", MaxItemsThreshold, true);
            });
            EasySettings.OnApplySettings.AddListener(() =>
            {
                try
                {
                    Config.Save();
                    UpdateEnableValues();
                }
                catch (Exception e)
                {
                    Logging.LogError($"PefectGuard crashed in OnApplySettings! Please report this error to the mod developer:");
                    Logging.LogError(e.ToString());
                }
            });
        }
    }

    public void Update()
    {
        CheckForObjectSpikes();
        CheckAbuseDetectorCleanup();
        CheckAudioCleanup();
    }

    private void CheckAbuseDetectorCleanup()
    {
        _abuseDetectorCleanupAccumulator += TimeSpan.FromSeconds(Time.deltaTime);
        if (_abuseDetectorCleanupAccumulator >= TimeSpan.FromSeconds(60))
        {
            _abuseDetectorCleanupAccumulator = TimeSpan.Zero;
            AbuseDetectorEMA.RunActorCleanup();
            AbuseDetectorTokenBucket.RunActorCleanup();
        }
    }

    private void CheckAudioCleanup()
    {
        _miscAccumulator += TimeSpan.FromSeconds(Time.deltaTime);
        if (_miscAccumulator >= TimeSpan.FromSeconds(2))
        {
            _abuseDetectorCleanupAccumulator = TimeSpan.Zero;

            PreventAudioSpam.CleanupDeadAudioSources();
        }
    }

    private void CheckForObjectSpikes()
    {
        if (ItemCleanupEnabled && NetworkServer.active && NetItemObjectTracker.Items.Count > MaxItemsThreshold.Value)
        {
            Logging.LogWarning($"Current item count is above the allowed limit ({MaxItemsThreshold.Value})! Cleaning up excessive items...", DetailedLogging);

            var items = NetItemObjectTracker.Items;
            var itemsToRemove = items.Count * 85 / 100; // Delete oldest X% of items

            for (int i = 0; i < itemsToRemove; i++)
            {
                var item = items[i];

                if (item.TryGetComponent<Renderer>(out var renderer))
                    renderer.enabled = false;

                if (item.TryGetComponent<Collider>(out var collider))
                    collider.enabled = false;

                if (item && item._itemObj)
                    item._itemObj.UnspawnObject();

                items.RemoveAt(i);
                i--;
                itemsToRemove--;
            }
        }
    }
}