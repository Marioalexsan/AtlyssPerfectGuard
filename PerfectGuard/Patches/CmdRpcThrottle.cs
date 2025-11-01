using HarmonyLib;
using Mirror;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Marioalexsan.PerfectGuard.Patches;

[HarmonyPatch]
static class CmdRpcThrottle
{
    struct ThrottleData
    {
        public int Limit;
        public string MethodName;
        public ushort FunctionHash;
    }

    private const int UnthrottledLimit = 1000;
    private const int DefaultLimit = 40;

    private static readonly Regex NameRegex = new Regex("::([^\\(]*)\\(");

    private static readonly Dictionary<ushort, ThrottleData> CustomRateLimits = [];

    static CmdRpcThrottle()
    {
        // Unthrottled RPCs / CMDs
        AddThrottle("System.Void PlayerVisual::Rpc_AltMovementBool(System.Boolean)", UnthrottledLimit);
        AddThrottle("System.Void PlayerVisual::Rpc_CrossFadeAnim(System.String:System.Single:System.Int32)", UnthrottledLimit);
        AddThrottle("System.Void Mirror.NetworkTransformUnreliable::CmdClientToServerSync(System.Nullable`1<UnityEngine.Vector3>,System.Nullable`1<UnityEngine.Quaternion>,System.Nullable`1<UnityEngine.Vector3>)", UnthrottledLimit);
        AddThrottle("System.Void Mirror.NetworkTransformUnreliable::RpcServerToClientSync(System.Nullable`1<UnityEngine.Vector3>,System.Nullable`1<UnityEngine.Quaternion>,System.Nullable`1<UnityEngine.Vector3>)", UnthrottledLimit);

        // Teleports
        AddThrottle("System.Void PlayerVisual::Rpc_PlayTeleportEffect()", 4);
        AddThrottle("System.Void PlayerVisual::Cmd_PlayTeleportEffect()", 4);

        // Jump Attacks
        AddThrottle("System.Void PlayerVisual::Rpc_VanitySparkleEffect()", 4);
        AddThrottle("System.Void PlayerVisual::Cmd_VanitySparkleEffect()", 4);

        // Pook Smoke
        AddThrottle("System.Void PlayerVisual::Rpc_JumpAttackEffect()", 4);
        AddThrottle("System.Void PlayerVisual::Cmd_JumpAttackEffect()", 4);

        // Item drops
        AddThrottle("System.Void PlayerInventory::Cmd_DropItem(ItemData,System.Int32)", 5);
    }

    private static void AddThrottle(string functionName, int rateLimit)
    {
        var hash = Hash(functionName);
        var nameMatch = NameRegex.Match(functionName);

        CustomRateLimits.Add(hash, new()
        {
            Limit = rateLimit,
            MethodName = nameMatch.Success ? nameMatch.Groups[1].Value : functionName,
            FunctionHash = hash
        });
    }

    private static ushort Hash(string functionName) => (ushort)(functionName.GetStableHashCode() & 0xFFFF);

    private static readonly ConcurrentDictionary<ushort, AbuseDetector> Detectors = [];

    private static AbuseDetector CreateDetector(ushort functionHash)
    {
        return new AbuseDetector(CustomRateLimits.GetValueOrDefault(functionHash, new()
        {
            FunctionHash = functionHash,
            Limit = DefaultLimit,
            MethodName = ""
        }).Limit);
    }

    static bool CheckRateLimits(uint netId, ushort functionHash, byte componentIndex, bool isRpc)
    {
        if (!PerfectGuard.EnablePerfectGuard.Value || !PerfectGuard.EnableMessageRateLimiting.Value)
            return true;

        NetworkIdentity identity;

        if (isRpc)
        {
            if (!NetworkClient.active || !NetworkClient.spawned.TryGetValue(netId, out identity))
                return true;
        }
        else
        {
            if (!NetworkServer.active || !NetworkServer.spawned.TryGetValue(netId, out identity))
                return true;
        }

        if (!(0 <= componentIndex && componentIndex < identity.NetworkBehaviours.Length))
            return true;

        var behaviour = identity.NetworkBehaviours[componentIndex];

        var detector = Detectors.GetOrAdd(functionHash, CreateDetector);

        bool isNormal = detector.TrackEventAndCheckBehaviour(behaviour);

        if (!isNormal)
        {
            var customName = CustomRateLimits.TryGetValue(functionHash, out var throttleData) ? throttleData.MethodName : "<Unnamed>";

            PerfectGuard.Logger.LogWarning($"RPC / CMD SPAM DETECTED! Blocking excessive calls (name: {customName}, hash: {functionHash}) from sender: {behaviour.name}.");
        }

        return isNormal;
    }

    [HarmonyPatch(typeof(NetworkClient), nameof(NetworkClient.OnRPCMessage))]
    [HarmonyPrefix]
    static bool RpcThrottle(ref RpcMessage message)
        => CheckRateLimits(message.netId, message.functionHash, message.componentIndex, true);

    [HarmonyPatch(typeof(NetworkServer), nameof(NetworkServer.OnCommandMessage))]
    static bool CmdThrottle(ref CommandMessage message)
        => CheckRateLimits(message.netId, message.functionHash, message.componentIndex, false);
}
