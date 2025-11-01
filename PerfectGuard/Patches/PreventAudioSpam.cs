using HarmonyLib;
using System;
using UnityEngine;

namespace Marioalexsan.PerfectGuard.Patches;

[HarmonyPatch]
internal static class PreventAudioSpam
{
    private const float AudioSpamCooldownSeconds = 0.1f;

    private static readonly Dictionary<AudioSource, float> AudioCooldowns = [];
    private static readonly List<AudioSource> DeadAudioSources = [];

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AudioSource), nameof(AudioSource.Play), [])]
    public static bool AudioSource_Play_Prefix(AudioSource __instance)
    {
        return CheckAudioCooldown(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AudioSource), nameof(AudioSource.PlayOneShot), [typeof(AudioClip), typeof(float)])]
    public static bool AudioSource_PlayOneShot_Prefix(AudioSource __instance)
    {
        return CheckAudioCooldown(__instance);
    }

    internal static bool CheckAudioCooldown(AudioSource instance)
    {
        if (!PerfectGuard.EnablePerfectGuard.Value || !PerfectGuard.EnableAudioSpamProtection.Value || instance == null)
            return true;

        if (!instance.gameObject.activeInHierarchy)
            return false;

        if (AudioCooldowns.TryGetValue(instance, out float cooldownEndTime) && Time.time < cooldownEndTime)
            return false;

        AudioCooldowns[instance] = Time.time + AudioSpamCooldownSeconds;
        return true;
    }

    internal static void CleanupDeadAudioSources()
    {
        DeadAudioSources.Clear();
        DeadAudioSources.AddRange(AudioCooldowns.Keys.Where(audioSource => audioSource == null));

        foreach (var deadSource in DeadAudioSources) 
            AudioCooldowns.Remove(deadSource);
    }
}