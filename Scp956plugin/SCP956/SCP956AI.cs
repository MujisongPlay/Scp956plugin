using CustomPlayerEffects;
using Exiled.API.Features;
using Interactables;
using Interactables.Interobjects;
using Interactables.Interobjects.DoorUtils;
using InventorySystem.Items;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerStatsSystem;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using InventorySystem;
using InventorySystem.Items.Pickups;
using InventorySystem.Items.Usables.Scp330;

namespace SCP956Plugin.SCP956
{
    public class SCP956AI : MonoBehaviour
    {
        void Start()
        {
            this.lerpRot = UnityEngine.Random.Range(0f, 360f);
            this.gameObject.transform.rotation = Quaternion.Euler(0f, lerpRot, 0f);
            this.gameObject.transform.position += new Vector3(0f, config.SchematicOffsetHeight, 0f);
            this.lerpPos = gameObject.transform.position;
            _spawnPos = this.gameObject.transform.position;
            rotateTime = config.TimeToRotateTowardTarget;
            moveTime = config.TimeToApproachTarget;
            chargeTime = config.TimeToCharge;
            coolTime = config.CooldownToFaceAnotherTarget;
        }

        void Update()
        {
            this.UpdateVisual();
            if (!this.Spawned)
            {
                return;
            }
            Timer += Time.deltaTime;
            foreach (ReferenceHub hub in ReferenceHub.AllHubs)
            {
                if (hub.isLocalPlayer)
                {
                    continue;
                }
                if (!PlayerCheck(hub))
                {
                    if (Targetable.Remove(hub))
                    {
                        if (Target == hub)
                        {
                            _sequenceTimer = 0f;
                        }
                        Targeted.Remove(hub);
                        TimerPerReferenceHub.Remove(hub);
                        TurnEffects(hub, false);
                    }
                }
                else
                {
                    if (!Targetable.Contains(hub))
                    {
                        Targetable.Add(hub);
                        TimerPerReferenceHub.Add(hub, Timer);
                        if (config.ResetTimerWhenTargetablePlayerObserved)
                        {
                            DestroyNow = false;
                            stopwatch = 0f;
                        }
                    }
                    else if (TimerPerReferenceHub.TryGetValue(hub, out float time) && Timer - time >= config.TimeScp330OwnerTargeting && !Targeted.Contains(hub))
                    {
                        Targeted.Add(hub);
                        TurnEffects(hub, true);
                    }
                    else if (config.TargetingAmbient != -1 && Player.TryGet(hub, out Player target))
                    {
                        Exiled.API.Extensions.MirrorExtensions.SendFakeTargetRpc(target, ReferenceHub.HostHub.networkIdentity, typeof(AmbientSoundPlayer), "RpcPlaySound", new object[]
                        {
                            config.TargetingAmbient
                        });
                    }
                }
            }
            if (Targeted.Count == 0)
            {
                return;
            }
            bool flag = Target == null;
            if (!flag && !Targeted.Contains(Target))
            {
                Target = null;
                TargetPos = Vector3.zero;
                _sequenceTimer = 0f;
                flag = true;
            }
            if (flag)
            {
                float MinDistance = float.MaxValue;
                foreach (ReferenceHub referenceHub in Targeted)
                {
                    float sqrMagnitude = (referenceHub.PlayerCameraReference.position - this.transform.position).sqrMagnitude;
                    if (sqrMagnitude <= MinDistance)
                    {
                        Target = referenceHub;
                        MinDistance = sqrMagnitude;
                    }
                }
                if (Target == null)
                {
                    return;
                }
                this._sequenceTimer = 0f;
                triggered = false;
                this._initialRot = this.lerpRot;
                this._initialPos = this.lerpPos;
            }
            if (Target.roleManager.CurrentRole is FpcStandardRoleBase)
            {
                TargetPos = (Target.roleManager.CurrentRole as FpcStandardRoleBase).FpcModule.Position;
            }
            else
            {
                TargetPos = Target.transform.position;
            }
            stopwatch = 0f;
            DestroyNow = false;
            this._sequenceTimer += Time.deltaTime;
            Vector3 normalized = (TargetPos - this.transform.position).normalized;
            float b = Vector3.Angle(normalized, Vector3.forward) * Mathf.Sign(Vector3.Dot(normalized, Vector3.right));
            this.lerpRot = Mathf.LerpAngle(this._initialRot, b, (this._sequenceTimer - coolTime) / (rotateTime - (coolTime + rotateTime / 6f)));
            if (this._sequenceTimer < rotateTime)
            {
                return;
            }
            Vector3 b2 = this.TargetPos - this.gameObject.transform.forward.NormalizeIgnoreY();
            if (Mathf.Abs(this._spawnPos.y - this.TargetPos.y) < 1.2f)
            {
                b2.y = this._spawnPos.y;
                this.IsFlying = false;
            }
            else
            {
                b2.y = this.TargetPos.y;
                this.IsFlying = true;
            }
            this.lerpPos = Vector3.Lerp(this._initialPos, b2, (this._sequenceTimer - rotateTime) / moveTime);
            if (Physics.Raycast(this.gameObject.transform.position, b2 - this._initialPos, out RaycastHit hit, 1f))
            {
                DestroyDisturb(hit);
            }
            if (this._sequenceTimer < chargeTime + moveTime + rotateTime - 0.2f)
            {
                return;
            }
            if (!triggered)
            {
                TurnEffects(Target, false);
                triggered = true;
            }
            if (this._sequenceTimer < chargeTime + moveTime + rotateTime)
            {
                return;
            }
            CreateCandies(normalized, Target);
            if (Player.TryGet(Target, out Player player))
            {
                player.PlayShieldBreakSound();
                player.PlayGunSound(config.DeathGunSoundSource, config.DeathGunSoundLoudness, config.DeathGunSoundClipNum);
            }
            this.Target.playerStats.DealDamage(new CustomReasonDamageHandler(config.DeathReason));
            this._sequenceTimer = 0f;
            Targetable.Remove(Target);
            Targeted.Remove(Target);
            TimerPerReferenceHub.Remove(Target);
            Target = null;
            triggered = false;
            return;
        }

        bool triggered = false;

        private bool PlayerCheck(ReferenceHub hub)
        {
            Vector3 position = this.gameObject.transform.position;
            Vector3 position2 = (hub.roleManager.CurrentRole as FpcStandardRoleBase).FpcModule.Position;
            float num = config.TargetMaximumDistance * config.TargetMaximumDistance;
            if (!config.TargetableFaction.Contains(hub.roleManager.CurrentRole.Team.GetFaction()) || config.WhitelistRoles.Contains(hub.roleManager.CurrentRole.RoleTypeId) || (!config.CanTargetScp268 && hub.playerEffectsController.GetEffect<Invisible>().IsEnabled))
            {
                return false;
            }
            if (hub.inventory.UserInventory.Items.Any((KeyValuePair<ushort, ItemBase> x) => x.Value.ItemTypeId == ItemType.SCP330) || config.TargetEveryone)
            {
                return (position - position2).sqrMagnitude <= num && this.CheckVisibility(position, position2) && hub.roleManager.CurrentRole.RoleTypeId != RoleTypeId.Spectator;
            }
            return false;
        }

        public void CreateCandies(Vector3 _velocity, ReferenceHub ply)
        {
            _velocity = (_velocity * 3f + Vector3.up) * 9f;
            Scp330Bag scp330Bag;
            if (!InventoryItemLoader.TryGetItem<Scp330Bag>(ItemType.SCP330, out scp330Bag))
            {
                return;
            }
            int num = (UnityEngine.Random.value < config.SpecialMethodPercentage * 0.05f && config.UseSpecialCandyPinkSpawnMethod) ? 1 : 0;
            for (int i = 0; i < SCP956Plugin.Instance.Config.CandyDropCount; i++)
            {
                PickupSyncInfo psi = new PickupSyncInfo(ItemType.SCP330, ply.transform.position, Quaternion.identity, scp330Bag.Weight, 0);
                Scp330Pickup scp330Pickup = ply.inventory.ServerCreatePickup(scp330Bag, psi, true) as Scp330Pickup;
                if (!(scp330Pickup == null))
                {
                    CandyKindID candyKindID = (num-- > 0) ? CandyKindID.Pink : TryGet();
                    scp330Pickup.StoredCandies.Add(candyKindID);
                    scp330Pickup.NetworkExposedCandy = candyKindID;
                    scp330Pickup.RigidBody.velocity = _velocity;
                }
            }
        }

        CandyKindID TryGet()
        {
            float total = config.CandyPercentageWeightPerCandy.Values.Sum();
            if (total == 0)
            {
                return CandyKindID.Red;
            }
            total = UnityEngine.Random.Range(0f, total);
            foreach (KeyValuePair<CandyKindID, float> pair in config.CandyPercentageWeightPerCandy)
            {
                if ((total -= pair.Value) <= 0f)
                {
                    return pair.Key;
                }
            }
            return CandyKindID.Red;
        }

        void TurnEffects(ReferenceHub hub, bool Enable)
        {
            if (Handlers.SchematicHandler.aIs.Any((SCP956AI x) => x.Targeted.Contains(hub)) && !Enable)
            {
                return;
            }
            if (config.FreezeTarget)
            {
                if (Enable)
                {
                    hub.playerEffectsController.EnableEffect<Ensnared>();
                }
                else
                {
                    hub.playerEffectsController.DisableEffect<Ensnared>();
                }
            }
            if (config.SetTargetInvincible)
            {
                hub.characterClassManager.GodMode = Enable;
            }
        }

        private bool CheckVisibility(Vector3 checkPos, Vector3 humanPos)
        {
            return !Physics.Linecast(humanPos, checkPos, out RaycastHit hit, WallMask) || hit.collider.transform.root.gameObject == this.gameObject;
        }

        void UpdateVisual()
        {
            stopwatch += Time.deltaTime;
            if (Spawned)
            {
                if (this.IsFlying)
                {
                    lerpPos.y += Mathf.Sin(Time.timeSinceLevelLoad * 3.14f) * 0.15f;
                }
                if ((this.transform.position - lerpPos).sqrMagnitude > 9f)
                {
                    this.transform.position = this.lerpPos;
                }
                this.transform.SetPositionAndRotation(Vector3.Lerp(this.transform.position, lerpPos, Time.deltaTime * 8f), Quaternion.Lerp(this.transform.rotation, Quaternion.Euler(Vector3.up * this.lerpRot), Time.deltaTime * 10f));
                this.lerpPos = this.transform.position;
                this.lerpRot = this.transform.rotation.eulerAngles.y;
                if (stopwatch >= (DestroyNow ? config.DespawnFailedTryAgainTime : config.DespawnTime))
                {
                    if (config.DoNotDespawnWhileBeingWatched && IsWatched())
                    {
                        DestroyNow = true;
                        stopwatch = 0f;
                    }
                    else
                    {
                        DestroyNow = false;
                        this.gameObject.transform.position = new Vector3(0f, -300f, 0f);
                        stopwatch = 0f;
                        DoorList.Remove(CurrentDoor);
                        CurrentDoor = null;
                        Timer = 0f;
                        TimerPerReferenceHub.Clear();
                        Targetable.Clear();
                        Targeted.Clear();
                        Target = null;
                        Spawned = false;
                        TargetPos = Vector3.zero;
                    }
                }
            }
            else
            {
                if (stopwatch >= config.SpawnTimer && config.SpawnableZone.Length != 0)
                {
                    Vector3 pos;
                    DoorVariant door;
                    stopwatch = 0f;
                    if (!TryGetSpawnPos(out pos, out door))
                    {
                        return;
                    }
                    _spawnPos = pos;
                    IsFlying = false;
                    this.gameObject.transform.position = pos;
                    CurrentDoor = door;
                    this.lerpRot = UnityEngine.Random.Range(0f, 360f);
                    this.gameObject.transform.rotation = Quaternion.Euler(0f, this.lerpRot, 0f);
                    DoorList.Add(door);
                    Spawned = true;
                }
            }
            return;
        }

        public bool TryGetSpawnPos(out Vector3 pos, out DoorVariant doo)
        {
            pos = Vector3.zero;
            doo = null;
            List<DoorVariant> list = new List<DoorVariant> { };
            foreach (DoorVariant door in DoorVariant.AllDoors)
            {
                if (door is BreakableDoor && door.Rooms != null && door.Rooms.Length != 0 && config.SpawnableZone.Contains(door.Rooms[0].Zone))
                {
                    if (config.DoNotOverlapSpawnPosition && DoorList.Contains(door))
                    {
                        continue;
                    }
                    list.Add(door);
                    continue;
                }
            }
            IL_01:
            if (list.Count == 0)
            {
                return false;
            }
            int index = UnityEngine.Random.Range(0, list.Count - 1);
            doo = list[index];
            list.Remove(doo);
            Transform transform = doo.transform;
            pos = transform.position + 0.75f * (UnityEngine.Random.Range(0, 1) * 2f - 1f) * transform.forward + Vector3.up * config.SchematicOffsetHeight;
            if (Physics.SphereCast(pos, 0.5f, transform.right * (UnityEngine.Random.Range(0, 1) * 2f - 1f), out RaycastHit hit, 10f, FpcStateProcessor.Mask) && hit.distance >= 0.3f)
            {
                Vector3 vector = hit.point + hit.normal * 0.5f;
                pos = new Vector3(vector.x, transform.position.y + config.SchematicOffsetHeight, vector.z);
                Vector3 checkPos = pos;
                if (config.LogsItslocation)
                {
                    ServerConsole.AddLog("SCP-956 Spawned in: " + MapGeneration.RoomIdUtils.RoomAtPosition(pos).name);
                }
                this.lerpPos = pos;
                return true;
            }
            goto IL_01;
        }

        void DestroyDisturb(RaycastHit hit)
        {
            IDamageableDoor damageableDoor;
            if (config.DestroyWindowWhileApproaching && hit.collider.TryGetComponent<BreakableWindow>(out BreakableWindow breakableWindow))
            {
                breakableWindow.Damage(500f, null, Vector3.zero);
            }
            if (config.DestryDoorWhileApproaching && hit.collider.TryGetComponent<InteractableCollider>(out InteractableCollider interactableCollider) && (damageableDoor = (interactableCollider.Target as IDamageableDoor)) != null)
            {
                damageableDoor.ServerDamage(500f, DoorDamageType.ServerCommand);
            }
            if (ReferenceHub.TryGetHub(hit.transform.root.gameObject, out ReferenceHub hub) && config.KillableDisturbingRoles.Contains(hub.roleManager.CurrentRole.RoleTypeId) && hub != Target)
            {
                if (!config.OnlyKillDisturbingWhileOwnScp330 || PlayerCheck(hub))
                {
                    hub.playerStats.DealDamage(new CustomReasonDamageHandler(config.DeathReasonDIsturbingWay));
                }
            }
        }

        public void SpecialConstruction()
        {
            Spawned = false;
        }

        bool IsWatched()
        {
            ReferenceHub[] hubs = ReferenceHub.AllHubs.ToArray<ReferenceHub>();
            foreach (ReferenceHub hub in hubs)
            {
                if (CheckVisibility(this.gameObject.transform.position, hub.PlayerCameraReference.position))
                {
                    return true;
                }
            }
            return false;
        }

        public float stopwatch = 0f;

        float Timer;

        public bool Spawned = true;

        public bool DestroyNow = false;

        Config config = SCP956Plugin.Instance.Config;

        static LayerMask _mask = 0;

        public static LayerMask WallMask
        {
            get
            {
                if (SCP956AI._mask == 0)
                {
                    _mask = FpcStateProcessor.Mask;
                    if (SCP956Plugin.Instance.Config.CanTargetThroughWindow)
                    {
                        _mask ^= 1 << LayerMask.NameToLayer("Glass");
                    }
                    if (SCP956Plugin.Instance.Config.CanTargetThroughDoor)
                    {
                        _mask ^= 1 << LayerMask.NameToLayer("Door");
                    }
                }
                return SCP956AI._mask;
            }
        }

        public static List<DoorVariant> DoorList = new List<DoorVariant> { };

        public List<ReferenceHub> Targeted = new List<ReferenceHub> { };

        public bool IsFlying = false;

        public ReferenceHub Target = null;

        public DoorVariant CurrentDoor;

        public List<ReferenceHub> Targetable = new List<ReferenceHub> { };

        private float _sequenceTimer;

        public Vector3 _spawnPos;

        private float _initialRot;

        private Vector3 _initialPos;

        private Vector3 TargetPos;

        public Dictionary<ReferenceHub, float> TimerPerReferenceHub = new Dictionary<ReferenceHub, float> { };

        private float rotateTime;

        private float moveTime;

        private float chargeTime;

        private float coolTime;

        Vector3 lerpPos;

        float lerpRot;
    }
}
