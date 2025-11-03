using BepInEx.Logging;
using HarmonyLib;
using Mirror;
using Mirror.RemoteCalls;
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

    private const int Unlimited = 16000;
    private const int DefaultLimit = VeryHighLimit;
    private const int UnspecifiedDefaultLimit = MediumLimit;

    private const int VeryHighLimit = 400;
    private const int HighLimit = 100;
    private const int MediumLimit = 40;
    private const int LowLimit = 10;
    private const int VeryLowLimit = 10;

    private static readonly Regex NameRegex = new Regex("::([^\\(]*)\\(");

    private static readonly Dictionary<ushort, ThrottleData> CustomRateLimits = [];

    static CmdRpcThrottle()
    {
        // Limit CMD rates for CMDs that shouldn't be called often by conforming clients
        // Any Throttle without an explicit limit will use DefaultLimit
        // Any method that's not present in here will be lightly throttled with UnspecifiedDefaultLimit
        // Due to how the Detector works, these limits are tracked per NetId

        // For RPCs, be more careful since these come from the server and are rate limited by the client
        // In some cases, higher volume calls are to be expected

        // Mirror
        Throttle("System.Void Mirror.NetworkTransformUnreliable::CmdClientToServerSync(System.Nullable`1<UnityEngine.Vector3>,System.Nullable`1<UnityEngine.Quaternion>,System.Nullable`1<UnityEngine.Vector3>)", Unlimited);
        Throttle("System.Void Mirror.NetworkTransformUnreliable::RpcServerToClientSync(System.Nullable`1<UnityEngine.Vector3>,System.Nullable`1<UnityEngine.Quaternion>,System.Nullable`1<UnityEngine.Vector3>)", Unlimited);

        // BreakableObject
        Throttle("System.Void BreakableObject::Rpc_Break(UnityEngine.Vector3)", LowLimit);

        // ChatBehaviour
        Throttle("System.Void ChatBehaviour::Cmd_SetChatChannel(System.String)", LowLimit);
        Throttle("System.Void ChatBehaviour::Cmd_JoinChatRoom(System.String)", LowLimit);
        Throttle("System.Void ChatBehaviour::Cmd_ToggleChatBubble(System.Boolean)", LowLimit);
        Throttle("System.Void ChatBehaviour::Cmd_SendChatMessage(System.String,ChatBehaviour/ChatChannel)", LowLimit); // Even this value is too generous
        Throttle("System.Void ChatBehaviour::Rpc_RecieveChatMessage(System.String,System.Boolean,ChatBehaviour/ChatChannel)", HighLimit); // Includes all chat comms
        Throttle("System.Void ChatBehaviour::Target_RecieveMessage(System.String)", LowLimit);
        Throttle("System.Void ChatBehaviour::Target_GameLogicMessage(System.String)", HighLimit); // Item pickup / exp / etc. messages
        Throttle("System.Void ChatBehaviour::Target_RecieveTriggerMessage(System.String)", LowLimit); // Status text in the center of the screen

        // Creep
        Throttle("System.Void Creep::Rpc_PlayCreepJumpEffect()", LowLimit);
        Throttle("System.Void Creep::Rpc_PlayAggroIcon(System.Int32)", LowLimit);
        Throttle("System.Void Creep::Rpc_OnCreepSpawn()", LowLimit);
        Throttle("System.Void Creep::Rpc_PlayCreepHurtEffect()", MediumLimit); // Could be called often if the creep is attacked by lots of people
        Throttle("System.Void Creep::Rpc_InitCreepDeathParams(System.Boolean)", LowLimit);
        Throttle("System.Void Creep::Rpc_TintRender(UnityEngine.Color)", MediumLimit);
        Throttle("System.Void Creep::Rpc_InitSkillChargeEffect(System.String,System.Single)", LowLimit);
        Throttle("System.Void Creep::Rpc_InitSkillCastEffect(System.String)", LowLimit);
        Throttle("System.Void Creep::Rpc_CrossFadeAnim(System.String,System.Single,System.Int32)", Unlimited);
        Throttle("System.Void Creep::Rpc_CrossFadeAnim_Timed(System.String,System.Single,System.Int32,System.Single)", Unlimited);
        Throttle("System.Void Creep::Rpc_InitUnspawnEffect(UnityEngine.Vector3,System.Single)", LowLimit);

        // CreepSpawner
        Throttle("System.Void CreepSpawner::Rpc_InitSpecialSpawnEffect(UnityEngine.Vector3)", VeryLowLimit);

        // Net_ItemObject
        Throttle("System.Void Net_ItemObject::Rpc_NetItemObjectPickup(Mirror.NetworkIdentity)", MediumLimit);
        Throttle("System.Void Net_ItemObject::Rpc_SpawnPuffcloud()", MediumLimit);

        // NetNPC
        Throttle("System.Void NetNPC::Rpc_CrossFade_Anim(System.String,System.Single,System.Int32)", Unlimited);

        // Player
        Throttle("System.Void Player::Cmd_SetLatency(System.Int32)");
        Throttle("System.Void Player::Cmd_InitAfkCondition(System.Boolean)");
        Throttle("System.Void Player::Cmd_SetInactive()");
        Throttle("System.Void Player::Cmd_Respawn()");
        Throttle("System.Void Player::Cmd_ReturnToRecalledInstance()");
        Throttle("System.Void Player::Cmd_SceneTransport(System.String,System.String,ZoneDifficulty)");
        Throttle("System.Void Player::Cmd_InviteToParty(Player)");
        Throttle("System.Void Player::Cmd_LeaveParty()");
        Throttle("System.Void Player::Cmd_SetPartyInviteCondition(PartyInviteStatus)");
        Throttle("System.Void Player::Target_ResetCameraPositioning()");

        // PlayerCasting
        Throttle("System.Void PlayerCasting::Cmd_QuerySkillStructs(SkillStruct[])");
        Throttle("System.Void PlayerCasting::Cmd_InitSkill(System.String)");
        Throttle("System.Void PlayerCasting::Cmd_SpawnEarlySkillObj()");
        Throttle("System.Void PlayerCasting::Cmd_InitCastInterrupt()");
        Throttle("System.Void PlayerCasting::Cmd_SetReviveEntity(StatusEntity)");
        Throttle("System.Void PlayerCasting::Cmd_CastInit()");
        Throttle("System.Void PlayerCasting::Rpc_QuerySkillStructs(SkillStruct[])");
        Throttle("System.Void PlayerCasting::Rpc_InitSkill(System.String)");
        Throttle("System.Void PlayerCasting::Rpc_SpawnEarlySkillObj()");
        Throttle("System.Void PlayerCasting::Rpc_InterruptCast()");
        Throttle("System.Void PlayerCasting::Rpc_CastSkill(System.String)");
        Throttle("System.Void PlayerCasting::Target_InitSkillLibrary()");

        // PlayerCombat
        Throttle("System.Void PlayerCombat::Cmd_ResetHitboxes()");
        Throttle("System.Void PlayerCombat::Cmd_QuickSwapWeapon()");
        Throttle("System.Void PlayerCombat::Cmd_LockWeapon()");
        Throttle("System.Void PlayerCombat::Cmd_SheatheWeapon(WeaponSheatheCondition)");
        Throttle("System.Void PlayerCombat::Cmd_InterruptCombat(System.Boolean)");
        Throttle("System.Void PlayerCombat::Cmd_ApplyBlockCondition(System.Boolean)");
        Throttle("System.Void PlayerCombat::Cmd_WeaponChargeDisplay(System.Boolean)");
        Throttle("System.Void PlayerCombat::Rpc_InterruptCombat(System.Boolean)");
        Throttle("System.Void PlayerCombat::Rpc_WeaponChargeDisplay(System.Boolean)");
        Throttle("System.Void PlayerCombat::Target_CancelBlock()");

        // PlayerEquipment
        Throttle("System.Void PlayerEquipment::Cmd_SyncEquipStruct(EquipSyncStruct,EquipSfx)");
        Throttle("System.Void PlayerEquipment::Cmd_SyncVanityStruct(EquipSyncStruct)");
        Throttle("System.Void PlayerEquipment::Cmd_RemoveVanity(System.String)");
        Throttle("System.Void PlayerEquipment::Rpc_EquipSfx(EquipSfx)");

        // PlayerInteract
        Throttle("System.Void PlayerInteract::Cmd_SetPushblockParent(PushBlock)");
        Throttle("System.Void PlayerInteract::Cmd_SetInteraction(Mirror.NetworkIdentity)");
        Throttle("System.Void PlayerInteract::Cmd_RemoveInteraction(Mirror.NetworkIdentity)");
        Throttle("System.Void PlayerInteract::Cmd_InteractWithTrigger(Mirror.NetworkIdentity)");
        Throttle("System.Void PlayerInteract::Cmd_InteractWithPortal(Portal,ZoneDifficulty)");
        Throttle("System.Void PlayerInteract::Cmd_OpenChest(ItemDropEntity_Chest)");
        Throttle("System.Void PlayerInteract::Cmd_InteractNetItemObject(Net_ItemObject,Mirror.NetworkIdentity)");
        Throttle("System.Void PlayerInteract::Rpc_OpenChest(ItemDropEntity_Chest)");

        // PlayerInventory
        Throttle("System.Void PlayerInventory::Cmd_AddCurrency(System.Int32)");
        Throttle("System.Void PlayerInventory::Cmd_SubtractCurrency(System.Int32)");
        Throttle("System.Void PlayerInventory::Cmd_DropCurrency(System.Int32)");
        Throttle("System.Void PlayerInventory::Cmd_PurchaseBuybackItem(NetNPC,System.Int32,System.String)");
        Throttle("System.Void PlayerInventory::Cmd_SellItem(Mirror.NetworkIdentity,ItemData,System.Int32)");
        Throttle("System.Void PlayerInventory::Cmd_InitBuyEffect()");
        Throttle("System.Void PlayerInventory::Cmd_DropItem(ItemData,System.Int32)");
        Throttle("System.Void PlayerInventory::Cmd_UseConsumable(ItemData)");
        Throttle("System.Void PlayerInventory::Rpc_PlayBuyEffect()");
        Throttle("System.Void PlayerInventory::Rpc_PlaySellEffect()");
        Throttle("System.Void PlayerInventory::Rpc_Init_ConsumableObject(Player,System.String)");
        Throttle("System.Void PlayerInventory::Target_RemoveItem(ItemData,System.Int32)");
        Throttle("System.Void PlayerInventory::Target_AddItem(ItemData)");

        // PlayerMove
        Throttle("System.Void PlayerMove::Target_SetRotation(UnityEngine.Quaternion)");
        Throttle("System.Void PlayerMove::Target_LockMovement(MovementLockType,System.Single)");
        Throttle("System.Void PlayerMove::Target_LockLookRotationMidair()");
        Throttle("System.Void PlayerMove::Target_InitJump(System.Single,System.Single,System.Single,UnityEngine.Vector3,System.Single)");

        // PlayerPvp
        Throttle("System.Void PlayerPvp::Cmd_FlagPvp(System.Boolean)");

        // PlayerQuesting
        Throttle("System.Void PlayerQuesting::Cmd_QueryServerQuestData(System.String[],System.String[])");
        Throttle("System.Void PlayerQuesting::Cmd_InitServersideQuestRewards(System.String)");
        Throttle("System.Void PlayerQuesting::Target_Query_CreepKillProgress(System.String)");
        Throttle("System.Void PlayerQuesting::Target_QueryQuestTriggerProgress(System.String)");

        // PlayerStats
        Throttle("System.Void PlayerStats::Cmd_ApplyAttributePoints(System.Int32[],System.Int32[])");
        Throttle("System.Void PlayerStats::Cmd_GainProfessionExp(ResourceEntity,System.Int32)");
        Throttle("System.Void PlayerStats::Cmd_RequestClass(System.String)");
        Throttle("System.Void PlayerStats::Cmd_RequestClassTier(System.Int32)");
        Throttle("System.Void PlayerStats::Rpc_LevelUpEffect()");
        Throttle("System.Void PlayerStats::Rpc_ProfessionLevelUpEffect()");
        Throttle("System.Void PlayerStats::Target_ResetSkillPoints()");
        Throttle("System.Void PlayerStats::Target_DisplayExpFloatText(System.Int32)");

        // PlayerTargeting
        Throttle("System.Void PlayerTargeting::Cmd_TargetSyncCreep(Mirror.NetworkIdentity)");

        // PlayerVisual
        Throttle("System.Void PlayerVisual::Cmd_SendNew_PlayerAppearanceStruct(PlayerAppearanceStruct)");
        Throttle("System.Void PlayerVisual::Cmd_PlayTeleportEffect()", VeryLowLimit);
        Throttle("System.Void PlayerVisual::Cmd_VanitySparkleEffect()", VeryLowLimit);
        Throttle("System.Void PlayerVisual::Cmd_PoofSmokeEffect()", VeryLowLimit);
        Throttle("System.Void PlayerVisual::Cmd_JumpAttackEffect()", VeryLowLimit);
        Throttle("System.Void PlayerVisual::Cmd_CrossFadeAnim(System.String,System.Single,System.Int32)");
        Throttle("System.Void PlayerVisual::Cmd_ShowItemEmote(System.String)");
        Throttle("System.Void PlayerVisual::Cmd_ChangeClimbAnimationSpeed(System.Single)");
        Throttle("System.Void PlayerVisual::Cmd_AltMovementAnimBool(System.Boolean)");
        Throttle("System.Void PlayerVisual::Cmd_ToggleArmorRender(System.Int32,EquipCellTab)");
        Throttle("System.Void PlayerVisual::Cmd_HideWeapon(System.Single)");
        Throttle("System.Void PlayerVisual::Cmd_ResetSpinPlayerModel()");
        Throttle("System.Void PlayerVisual::Rpc_PlayTeleportEffect()", VeryLowLimit);
        Throttle("System.Void PlayerVisual::Rpc_VanitySparkleEffect()", VeryLowLimit);
        Throttle("System.Void PlayerVisual::Rpc_PoofSmokeEffect()", VeryLowLimit);
        Throttle("System.Void PlayerVisual::Rpc_JumpAttackEffect()", VeryLowLimit);
        Throttle("System.Void PlayerVisual::Rpc_CrossFadeAnim(System.String,System.Single,System.Int32)");
        Throttle("System.Void PlayerVisual::IncludeRpc_Crossfade(System.String,System.Single,System.Int32)");
        Throttle("System.Void PlayerVisual::Rpc_ShowItemEmote(System.String)", VeryLowLimit);
        Throttle("System.Void PlayerVisual::Rpc_SetPlaybackSpeed(System.Single)");
        Throttle("System.Void PlayerVisual::Rpc_AltMovementBool(System.Boolean)");
        Throttle("System.Void PlayerVisual::Rpc_HideWeapon(System.Single)");
        Throttle("System.Void PlayerVisual::Rpc_RandomSpinPlayerModel(System.Single)");
        Throttle("System.Void PlayerVisual::Rpc_ResetSpinPlayerModel()");
        Throttle("System.Void PlayerVisual::Rpc_PreventRotationLerp(System.Single)");

        // PushBlock
        Throttle("System.Void PushBlock::Cmd_RemoveParentPlayer()");
        Throttle("System.Void PushBlock::Request_PushBlockAction(UnityEngine.Vector3,System.Int32,System.Boolean)");

        // QuestTrigger
        Throttle("System.Void QuestTrigger::Rpc_InitEffect()");

        // StatusEntity
        Throttle("System.Void StatusEntity::Cmd_AddCondition(System.Int32,System.Int32,System.Int32)");
        Throttle("System.Void StatusEntity::Cmd_ReplenishAll()");
        Throttle("System.Void StatusEntity::Cmd_RevivePlayer(StatusEntity)");
        Throttle("System.Void StatusEntity::Cmd_SubtractMana(System.Int32)");
        Throttle("System.Void StatusEntity::Cmd_SubtractStamina(System.Int32)");
        Throttle("System.Void StatusEntity::Cmd_TakeDamage(System.Int32,System.Single,System.Boolean,System.Boolean,DamageWeight,UnityEngine.Vector3,UnityEngine.Vector3)");
        Throttle("System.Void StatusEntity::Rpc_AngelaTearEffect()");
        Throttle("System.Void StatusEntity::Rpc_DisplayHitEffect(DamageWeight,System.Int32,UnityEngine.Vector3)");
        Throttle("System.Void StatusEntity::Rpc_DisplayBlockHitEffect(StatusEntity,System.Int32,System.Boolean,System.Boolean,UnityEngine.Vector3)");
        Throttle("System.Void StatusEntity::Rpc_DisplayAbsorbHitEffect()");
        Throttle("System.Void StatusEntity::Rpc_DisplayCritHitEffect(StatusEntity,UnityEngine.Vector3)");
        Throttle("System.Void StatusEntity::Rpc_DisplayMissHitEffect(StatusEntity)");

        // StatusEntityGUI
        Throttle("System.Void StatusEntityGUI::Target_Display_FloatingNumber(StatusEntity,FloatTextColor,System.Int32,System.Int32,System.Int32)");
    }

    private static void Throttle(string functionName) => Throttle(functionName, DefaultLimit);

    private static void Throttle(string functionName, int rateLimit)
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
        if (!CustomRateLimits.TryGetValue(functionHash, out var data))
        {
            CustomRateLimits[functionHash] = data = new()
            {
                FunctionHash = functionHash,
                Limit = UnspecifiedDefaultLimit,
                MethodName = ""
            };
            PerfectGuard.Logger.LogWarning($"Got an unconfigured RPC function ({GetMethodName(functionHash)})! Will throttle using default limit for unspecified methods.");
        }

        return new AbuseDetector(data.Limit);
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

        var previousSuspicion = detector.Suspicion;
        detector.TrackEvent(behaviour);
        var nextSuspicion = detector.Suspicion;

        if (previousSuspicion == SuspicionLevel.Normal && nextSuspicion != SuspicionLevel.Normal)
        {
            PerfectGuard.Logger.LogWarning($"RPC / CMD SPAM DETECTED! Blocking excessive calls to {GetMethodName(functionHash)} from sender: {behaviour.name}.");
        }

        return nextSuspicion == SuspicionLevel.Normal;
    }

    static string GetMethodName(ushort functionHash)
    {
        if (CustomRateLimits.TryGetValue(functionHash, out var throttleData))
            return throttleData.MethodName;

        if (RemoteProcedureCalls.remoteCallDelegates.TryGetValue(functionHash, out var invoker))
        {
            var fullName = invoker.function.Method.Name;
            var nameMatch = NameRegex.Match(invoker.function.Method.Name);
            return nameMatch.Success ? nameMatch.Groups[1].Value : fullName;
        }

        return $"<hash {functionHash}>";
    }

    [HarmonyPatch(typeof(NetworkClient), nameof(NetworkClient.OnRPCMessage))]
    [HarmonyPrefix]
    static bool RpcThrottle(ref RpcMessage message)
        => CheckRateLimits(message.netId, message.functionHash, message.componentIndex, true);

    [HarmonyPatch(typeof(NetworkServer), nameof(NetworkServer.OnCommandMessage))]
    static bool CmdThrottle(ref CommandMessage message)
        => CheckRateLimits(message.netId, message.functionHash, message.componentIndex, false);
}
