using HarmonyLib;

namespace Marioalexsan.PerfectGuard.Patches;

[HarmonyPatch]
public class NetItemObjectTrackerInjector
{
    [HarmonyPatch(typeof(Net_ItemObject), nameof(Net_ItemObject.Awake))]
    [HarmonyPostfix] // Run this patch *after* the original Awake method has finished.
    static void InjectTracker(Net_ItemObject __instance)
    {
        if (!__instance.GetComponent<NetItemObjectTracker>())
            __instance.gameObject.AddComponent<NetItemObjectTracker>();
    }
}