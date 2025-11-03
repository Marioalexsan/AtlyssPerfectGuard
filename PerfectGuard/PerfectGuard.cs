using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Marioalexsan.PerfectGuard.Patches;
using Mirror;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Marioalexsan.PerfectGuard;

[BepInPlugin(ModInfo.GUID, ModInfo.NAME, ModInfo.VERSION)]
public class PerfectGuard : BaseUnityPlugin
{
    internal static new ManualLogSource Logger { get; private set; } = null!;
    private readonly Harmony _harmony = new(ModInfo.GUID);

    public static ConfigEntry<KeyCode> WindowKey { get; private set; } = null!;
    public static ConfigEntry<bool> EnablePerfectGuard { get; private set; } = null!;
    public static ConfigEntry<bool> EnableMessageRateLimiting { get; private set; } = null!;
    public static ConfigEntry<bool> EnableItemCleanup { get; private set; } = null!;
    public static ConfigEntry<bool> EnableAudioSpamProtection { get; private set; } = null!;
    public static ConfigEntry<bool> EnableDetailedLogging { get; private set; } = null!;
    public static ConfigEntry<int> MaxItemsThreshold { get; private set; } = null!;

    private Rect _windowRect;
    private bool _windowShown;

    private TimeSpan _abuseDetectorCleanupAccumulator;
    private TimeSpan _miscAccumulator;

    public PerfectGuard()
    {
        Logger = base.Logger;

        WindowKey = Config.Bind("General", nameof(WindowKey), KeyCode.F9, "Key to open the configuration window.");

        EnablePerfectGuard = Config.Bind("General", nameof(EnablePerfectGuard), true, "Enable or disable all features of this mod.");
        EnableDetailedLogging = Config.Bind("General", "DetailedLogging", true, "Log detailed warnings for detected malicious activities.");

        EnableMessageRateLimiting = Config.Bind("Protections", nameof(EnableMessageRateLimiting), true, "Enables rate limits for network spam, such as visual effects.");
        EnableItemCleanup = Config.Bind("Protections", nameof(EnableItemCleanup), true, "Enable cleanup for excessive item drops.");
        EnableAudioSpamProtection = Config.Bind("Protections", nameof(EnableAudioSpamProtection), true, "Enables audio rate limits to prevent spam.");
        
        MaxItemsThreshold = Config.Bind("Tuning", nameof(MaxItemsThreshold), 400, "The maximum number of items allowed before the items are forcefully cleaned up.");
    }

    public void Awake()
    {
        _harmony.PatchAll();
        Logger.LogMessage($"{ModInfo.NAME} v{ModInfo.VERSION} has loaded!");
    }

    public void Update()
    {
        if (Input.GetKeyDown(WindowKey.Value))
            _windowShown = !_windowShown;

        // Run the spike check EVERY FRAME for instant reaction to prevent freezing.
        if (EnablePerfectGuard.Value && EnableItemCleanup.Value && NetworkClient.isConnected)
        {
            CheckForObjectSpikes();
        }

        _abuseDetectorCleanupAccumulator += TimeSpan.FromSeconds(Time.deltaTime);
        if (_abuseDetectorCleanupAccumulator >= TimeSpan.FromSeconds(60))
        {
            _abuseDetectorCleanupAccumulator = TimeSpan.Zero;
            AbuseDetector.RunActorCleanup();
        }

        _miscAccumulator += TimeSpan.FromSeconds(Time.deltaTime);
        if (_miscAccumulator >= TimeSpan.FromSeconds(2))
        {
            _abuseDetectorCleanupAccumulator = TimeSpan.Zero;

            PreventAudioSpam.CleanupDeadAudioSources();
        }
    }

    public void OnGUI()
    {
        if (!_windowShown) return;
        _windowRect = GUILayout.Window(GUIUtility.GetControlID(FocusType.Passive), 
            new Rect(Screen.width * 0.1f, Screen.height * 0.1f, Screen.width * 0.3f, Screen.height * 0.5f), 
            DrawWindow, $"{ModInfo.NAME} v{ModInfo.VERSION}");
    }

    private void DrawWindow(int windowID)
    {
        EnablePerfectGuard.Value = GUILayout.Toggle(EnablePerfectGuard.Value, "Enable All Protections (Master Switch)");
        GUILayout.Space(10);
        EnableMessageRateLimiting.Value = GUILayout.Toggle(EnableMessageRateLimiting.Value, "Global RPC Spam Shield");
        EnableItemCleanup.Value = GUILayout.Toggle(EnableItemCleanup.Value, "Item Drop Spike Protection");
        EnableAudioSpamProtection.Value = GUILayout.Toggle(EnableAudioSpamProtection.Value, "Audio Spam Protection");
        GUILayout.Space(10);
        EnableDetailedLogging.Value = GUILayout.Toggle(EnableDetailedLogging.Value, "Enable Detailed Logging");
        
        GUILayout.FlexibleSpace();
        GUI.DragWindow();
    }


    private void CheckForObjectSpikes()
    {
        if (NetItemObjectTracker.Items.Count > MaxItemsThreshold.Value)
        {
            Logger.LogWarning($"Current item count ({NetItemObjectTracker.Items.Count}) is above the allowed limit ({MaxItemsThreshold.Value})! Cleaning up excessive items...");
            
            CleanItems();
        }
    }
    
    private static void CleanItems()
    {
        var items = NetItemObjectTracker.Items;

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];

            if (!item || !item.gameObject)
                continue;

            // Disable immediately to stop visual/physical effects in the same frame.
            // We dont want the player to be dead immediately lol.
            if (item.TryGetComponent<Renderer>(out var renderer))
                renderer.enabled = false;

            if (item.TryGetComponent<Collider>(out var collider))
                collider.enabled = false;

            Destroy(items[i].gameObject);
        }
    }
}