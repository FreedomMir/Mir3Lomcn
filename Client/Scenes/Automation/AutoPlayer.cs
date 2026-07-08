using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Client.Envir;
using Client.Models;
using Client.Scenes;
using Client.Scenes.Views;
using Client.Scenes.Views;
using Library;
using Library.Network.ClientPackets;
using Library.Network.ServerPackets;
using Library.SystemModels;

namespace Client.Scenes.Automation;

public sealed class AutoPlayer : IDisposable
{
	private sealed class LootMemory
	{
		public int Attempts;

		public DateTime NextAttempt;

		public DateTime BlacklistedUntil;
	}

	private sealed class HarvestMemory
	{
		public int Swings;

		public DateTime BlacklistedUntil;
	}

	private sealed class TravelPlan
	{
		public Step Destination;

		public List<TravelPathSegment>? Segments;
	}

	private enum TravelPurpose
	{
		Manual,
		ReturnToHunt,
		TownRun,
		SafeZoneStop
	}

	private sealed record PendingManualTravel(Step Destination, bool ThroughDoor);

	private readonly record struct TrackedPet(MonsterFlag Flag, DateTime SeenTime);

	private enum EntryKind
	{
		Buff,
		Stance,
		Empower,
		Summon,
		HealSelf,
		PoisonUpkeep,
		AoE,
		Single
	}

	private sealed class RotationEntry
	{
		public MagicType Magic;

		public EntryKind Kind;

		public Element Element;

		public BuffType Buff;

		public int MinTargets;

		public bool CountTargetsFromCaster;

		public int MinDistance;

		public int Priority;

		public bool RequireUndead;

		public bool UndeadBonus;

		public Func<MonsterObject, UserObject, bool>? Condition;
	}

	private sealed class ThreatSource
	{
		public MonsterObject Monster;

		public int Weight;

		public int ViewRange;

		public bool EngagedOnMe;
	}

	private enum AutoTownState
	{
		None,
		TeleportingToTown,
		Travelling,
		MovingToNPC,
		AwaitingResult,
		Settling
	}

	private sealed class TownWorkItem
	{
		public AutoTownAction Action;

		public TownNPCRef NPC;
	}

	private const int TargetScanMs = 200;

	private const int CasterStandoff = 6;

	private const int KiteDangerRange = 2;

	private const int CombatLootRange = 4;

	private const int UnreachableBlacklistMs = 10000;

	private const int DestinationClearRange = 12;

	private const int DoorChokeRange = 8;

	private const int DoorChokeCount = 2;

	public int HuntSearchRange = 18;

	private MonsterObject? _target;

	private DateTime _nextTargetScan;

	private Point _combatPathTarget = Point.Empty;

	private readonly Dictionary<uint, DateTime> _unreachableTargets = new Dictionary<uint, DateTime>();

	public const int ManualSuppressionMs = 2500;

	[CompilerGenerated]
	private AutoIntention _003CIntention_003Ek__BackingField;

	public ClientAutoPlaySettings Settings = new ClientAutoPlaySettings();

	private DateTime _suppressUntil;

	private bool _mapChanged;

	private bool _deathPending;

	private int _deathReturnsUsed;

	private Step? _deathLocation;

	public bool DebugMessages;

	private const int EquipCooldownMs = 800;

	private DateTime _nextEquipTime;

	private ItemInfo? _lastPoisonInfo;

	private ItemInfo? _lastAmuletInfo;

	private const int LootScanMs = 300;

	private const int LootRetryMs = 750;

	private const int LootBlacklistMs = 30000;

	private const int MaxLootFailures = 4;

	private const int PathingBlacklistMs = 10000;

	private readonly Dictionary<uint, LootMemory> _lootMemory = new Dictionary<uint, LootMemory>();

	private ItemObject? _lootTarget;

	private Point _lootPathTarget;

	private DateTime _nextLootScan;

	private DateTime _nextHarvestScan;

	private const int LootSkipLogRange = 6;

	private readonly Dictionary<uint, DateTime> _lootSkipLogged = new Dictionary<uint, DateTime>();

	private const int HarvestSwingCap = 5;

	private const int HarvestBlacklistMs = 30000;

	private readonly Dictionary<uint, HarvestMemory> _harvestMemory = new Dictionary<uint, HarvestMemory>();

	private MonsterObject? _harvestTarget;

	private Point _harvestPathTarget;

	private const int StuckRepathMs = 1500;

	private const int StuckAbandonMs = 5000;

	private const int DoorWaitMs = 4000;

	private const int MaxRepathFailures = 3;

	private const int MaxForcedReplans = 3;

	private List<Point>? _path;

	private int _pathIndex;

	private List<TravelPathSegment>? _segments;

	private int _segmentIndex;

	private Step? _travelDestination;

	private TravelPurpose _travelPurpose;

	private Step? _huntAnchor;

	private CancellationTokenSource? _planCancel;

	private volatile TravelPlan? _pendingPlan;

	private bool _planning;

	private Point _roamTarget = Point.Empty;

	private Point _lastProgressLocation;

	private DateTime _lastProgressTime;

	private DateTime _doorWaitUntil;

	private int _repathFailures;

	private int _forcedReplans;

	private volatile PendingManualTravel? _pendingManualTravel;

	public const int DefaultHealThreshold = 60;

	private const int TaoistSummonPetLimit = 2;

	private const int TaoistAutoBuffRadius = 3;

	private static readonly TimeSpan TrackedPetLifetime = TimeSpan.FromMinutes(5L);

	private static readonly TimeSpan PendingSummonLifetime = TimeSpan.FromSeconds(30L);

	private readonly Dictionary<uint, TrackedPet> _trackedPets = new Dictionary<uint, TrackedPet>();

	private readonly Dictionary<MonsterFlag, DateTime> _pendingSummons = new Dictionary<MonsterFlag, DateTime>();

	private static readonly Dictionary<MirClass, List<RotationEntry>> Rotations;

	private const int BaseMonsterThreat = 100;

	private const int ThreatFalloffBuffer = 4;

	private const int AggroDecayMs = 8000;

	public int MaxThreatToEngage = 450;

	public int RoamThreatThreshold = 350;

	public int LootThreatThreshold = 250;

	private readonly Dictionary<uint, DateTime> _struckBy = new Dictionary<uint, DateTime>();

	private readonly List<ThreatSource> _threatSources = new List<ThreatSource>();

	private const int TownCheckMs = 2000;

	private const int TownResultTimeoutMs = 5000;

	private const int TownSettleMs = 2000;

	private const int TownSendPaceMs = 750;

	private const int TownTeleportStepThreshold = 50;

	private const int TownTeleportTimeoutMs = 10000;

	private AutoTownState _townState;

	private DateTime _nextTownCheck;

	private DateTime _townTimeout;

	private DateTime _nextTownSend;

	private readonly Queue<TownWorkItem> _townWork = new Queue<TownWorkItem>();

	private TownWorkItem? _currentWork;

	private volatile AutoTownResult? _townResult;

	private Step? _townPoint;

	private string? _safeStopReason;

	private bool _leavingForTown;

	private List<AutoTownAction>? _townWorkActions;

	private bool _townTeleportSuppressed;

	private readonly Dictionary<AutoTownAction, string> _townFailureMessages = new Dictionary<AutoTownAction, string>();

	private readonly HashSet<AutoTownAction> _townSkippedActions = new HashSet<AutoTownAction>();

	private readonly List<(ItemInfo Info, long Needed)> _townInitialRestockNeeds = new List<(ItemInfo, long)>();

	public MonsterObject? Target => _target;

	private bool IsMeleeClass
	{
		get
		{
			MirClass mirClass = User.Class;
			if (mirClass == MirClass.Warrior || mirClass == MirClass.Assassin)
			{
				return true;
			}
			return false;
		}
	}

	public GameScene Scene { get; }

	private UserObject User => Scene.User;

	private MapControl Map => Scene.MapControl;

	private int CurrentMapIndex => Map.MapInfo?.Index ?? (-1);

	public bool Enabled { get; private set; }

	public AutoIntention Intention
	{
		[CompilerGenerated]
		get
		{
			return _003CIntention_003Ek__BackingField;
		}
		private set
		{
			if (_003CIntention_003Ek__BackingField != value)
			{
				_003CIntention_003Ek__BackingField = value;
				this.StateChanged?.Invoke(this, EventArgs.Empty);
			}
		}
	}

	public string StateText { get; private set; } = string.Empty;

	private bool AutoEquipTorch => Settings.AutoEquipTorch;

	private bool AutoEquipPoison => Settings.AutoEquipPoison;

	private bool AutoEquipTalisman => Settings.AutoEquipTalisman;

	private bool UsesPoison
	{
		get
		{
			MirClass mirClass = User.Class;
			if (mirClass - 2 > MirClass.Wizard)
			{
				return _lastPoisonInfo != null;
			}
			return true;
		}
	}

	private bool UsesTalisman
	{
		get
		{
			if (User.Class != MirClass.Taoist)
			{
				return _lastAmuletInfo != null;
			}
			return true;
		}
	}

	private bool LootEnabled => Settings.LootEnabled;

	private bool HarvestEnabled
	{
		get
		{
			if (LootEnabled)
			{
				return Settings.HarvestEnabled;
			}
			return false;
		}
	}

	public IReadOnlyList<Point>? Path => _path;

	public int PathIndex => _pathIndex;

	public IReadOnlyList<TravelPathSegment>? Segments => _segments;

	private bool KiteEnabled => Settings.KiteEnabled;

	public int EngagedCount { get; private set; }

	private int HPPercent
	{
		get
		{
			if (User.Stats[Stat.Health] <= 0)
			{
				return 100;
			}
			return User.CurrentHP * 100 / User.Stats[Stat.Health];
		}
	}

	private int TownNPCRange => Math.Clamp(Scene.TownActionRange - 4, 2, 15);

	public event EventHandler? StateChanged;

	public event EventHandler? PathChanged;

	private bool ProcessCombat()
	{
		bool flag = Intention == AutoIntention.Travelling && MustFightWhileTravelling();
		bool defensiveOnly = (Intention != AutoIntention.Hunting || _leavingForTown) && !flag;
		if (_target != null && (_target.Dead || !Map.Objects.Contains(_target)))
		{
			_target = null;
			_combatPathTarget = Point.Empty;
			ClearWalkPath();
		}
		if (Intention == AutoIntention.Travelling)
		{
			if (_target == null && !flag)
			{
				return TryTravelSelfHeal();
			}
			if (_target != null)
			{
				ResetProgress(forceTimeReset: true);
			}
		}
		if (CEnvir.Now >= _nextTargetScan && (_target == null || !IsCommittedToTarget(_target) || TargetTier(_target) < 2))
		{
			_nextTargetScan = CEnvir.Now.AddMilliseconds(200.0);
			MonsterObject monsterObject = FindTarget(defensiveOnly, flag);
			if (monsterObject != null && _target == null && TargetTier(monsterObject) == 0 && (FindLoot() != null || FindHarvestTarget() != null))
			{
				monsterObject = null;
			}
			if (monsterObject != null && monsterObject != _target && ShouldSwitchTarget(monsterObject))
			{
				_target = monsterObject;
				_combatPathTarget = Point.Empty;
				ClearWalkPath();
				DebugLog($"Target: {monsterObject.MonsterInfo.MonsterName} (tier {TargetTier(monsterObject)})");
			}
		}
		if (_target == null)
		{
			return false;
		}
		SetState("Fighting " + _target.MonsterInfo.MonsterName);
		bool flag2 = MonsterObject.IsMagicImmuneAI(_target.MonsterInfo.AI);
		if (TryRotation(_target, !flag2))
		{
			return true;
		}
		int num = Functions.Distance(User.CurrentLocation, _target.CurrentLocation);
		if (num == 0)
		{
			TryStepAwayFromOverlappedTarget(_target);
			return true;
		}
		if (IsMeleeClass || flag2 || !HasUsableOffense())
		{
			if (num == 1 && TryMeleeAttack(_target))
			{
				return true;
			}
			if (num > 1 && ShouldDetourForLoot())
			{
				_combatPathTarget = Point.Empty;
				return false;
			}
			return ApproachTarget(1);
		}
		if (KiteEnabled && TryKite(_target))
		{
			return true;
		}
		if (num == 1 && TryMeleeAttack(_target))
		{
			return true;
		}
		if (num <= 6)
		{
			if (TryCombatLootStep(_target))
			{
				return true;
			}
			if (ShouldDetourForLoot())
			{
				_combatPathTarget = Point.Empty;
				return false;
			}
			return true;
		}
		if (ShouldDetourForLoot())
		{
			_combatPathTarget = Point.Empty;
			return false;
		}
		return ApproachTarget(6);
	}

	private bool ShouldDetourForLoot()
	{
		if (!LootEnabled || Intention == AutoIntention.Travelling)
		{
			return false;
		}
		if (CountAdjacentEnemies() > 0)
		{
			return false;
		}
		int num = 4;
		if (_target != null)
		{
			num = Math.Max(num, Functions.Distance(User.CurrentLocation, _target.CurrentLocation) - 1);
		}
		ItemObject itemObject = FindNearbyLoot(User.CurrentLocation, num);
		if (itemObject != null && GetThreatAt(itemObject.CurrentLocation, _target) <= LootThreatThreshold)
		{
			return true;
		}
		MonsterObject monsterObject = FindHarvestTarget();
		if (monsterObject != null)
		{
			return Functions.Distance(User.CurrentLocation, monsterObject.CurrentLocation) <= num;
		}
		return false;
	}

	private bool TryCombatLootStep(MonsterObject target)
	{
		if (!LootEnabled)
		{
			return false;
		}
		if ((!Config.SmoothMove && !Scene.MoveFrame) || User.Horse != HorseType.None)
		{
			return false;
		}
		if (CEnvir.Now < User.NextActionTime || User.ActionQueue.Count > 0)
		{
			return false;
		}
		ItemObject itemObject = FindNearbyLoot(User.CurrentLocation, 4);
		if (itemObject == null)
		{
			return false;
		}
		if (Functions.Distance(User.CurrentLocation, itemObject.CurrentLocation) <= User.Stats[Stat.PickUpRadius])
		{
			return false;
		}
		MirDirection dir = Functions.DirectionFromPoint(User.CurrentLocation, itemObject.CurrentLocation);
		for (int i = 0; i < 3; i++)
		{
			MirDirection direction = Functions.ShiftDirection(dir, (((i & 1) == 0) ? 1 : (-1)) * ((i + 1) / 2));
			Point point = Functions.Move(User.CurrentLocation, direction);
			if (point.X >= 0 && point.Y >= 0 && point.X < Map.Width && point.Y < Map.Height && !Map.Cells[point.X, point.Y].Blocking() && Functions.Distance(point, target.CurrentLocation) <= 10)
			{
				User.AttemptAction(new ObjectAction(MirAction.Moving, direction, point, 1, MagicType.None));
				return true;
			}
		}
		return false;
	}

	private bool ApproachTarget(int desired)
	{
		if (_target == null)
		{
			return false;
		}
		if (Functions.Distance(User.CurrentLocation, _target.CurrentLocation) <= desired)
		{
			return true;
		}
		if (_path == null || Functions.Distance(_combatPathTarget, _target.CurrentLocation) > 2)
		{
			_combatPathTarget = _target.CurrentLocation;
			if (!StartPath(_combatPathTarget, desired, 2000, HuntSearchRange * 2))
			{
				_unreachableTargets[_target.ObjectID] = CEnvir.Now.AddMilliseconds(10000.0);
				_nextTargetScan = DateTime.MinValue;
				_target = null;
				_combatPathTarget = Point.Empty;
				return false;
			}
		}
		FollowPath();
		return true;
	}

	private bool TryStepAwayFromOverlappedTarget(MonsterObject target)
	{
		if ((!Config.SmoothMove && !Scene.MoveFrame) || User.Horse != HorseType.None)
		{
			return false;
		}
		if (CEnvir.Now < User.NextActionTime || User.ActionQueue.Count > 0)
		{
			return false;
		}
		if ((User.Poison & PoisonType.WraithGrip) == PoisonType.WraithGrip)
		{
			return false;
		}
		Point currentLocation = User.CurrentLocation;
		Point? point = null;
		int num = int.MaxValue;
		for (int i = 0; i < 8; i++)
		{
			MirDirection direction = (MirDirection)i;
			Point point2 = Functions.Move(currentLocation, direction);
			if (point2.X >= 0 && point2.Y >= 0 && point2.X < Map.Width && point2.Y < Map.Height && !Map.Cells[point2.X, point2.Y].Blocking())
			{
				int threatAt = GetThreatAt(point2, target);
				if (threatAt < num)
				{
					num = threatAt;
					point = point2;
				}
			}
		}
		if (!point.HasValue)
		{
			return false;
		}
		_combatPathTarget = Point.Empty;
		ClearWalkPath();
		MirDirection direction2 = Functions.DirectionFromPoint(currentLocation, point.Value);
		User.AttemptAction(new ObjectAction(MirAction.Moving, direction2, point.Value, 1, MagicType.None));
		return true;
	}

	private bool TryKite(MonsterObject target)
	{
		MonsterObject monsterObject = null;
		int num = int.MaxValue;
		foreach (ThreatSource threatSource in _threatSources)
		{
			if (threatSource.EngagedOnMe)
			{
				int num2 = Functions.Distance(User.CurrentLocation, threatSource.Monster.CurrentLocation);
				if (num2 < num)
				{
					num = num2;
					monsterObject = threatSource.Monster;
				}
			}
		}
		if (monsterObject == null || num > 2)
		{
			return false;
		}
		if ((!Config.SmoothMove && !Scene.MoveFrame) || User.Horse != HorseType.None)
		{
			return false;
		}
		if (CEnvir.Now < User.NextActionTime || User.ActionQueue.Count > 0)
		{
			return false;
		}
		Point currentLocation = User.CurrentLocation;
		int range = Math.Max(0, User.Stats[Stat.PickUpRadius]);
		Point? point = null;
		int num3 = num * 4;
		for (int i = 0; i < 8; i++)
		{
			MirDirection direction = (MirDirection)i;
			Point point2 = Functions.Move(currentLocation, direction);
			if (point2.X < 0 || point2.Y < 0 || point2.X >= Map.Width || point2.Y >= Map.Height || Map.Cells[point2.X, point2.Y].Blocking() || Functions.Distance(point2, target.CurrentLocation) > 10)
			{
				continue;
			}
			int num4 = Functions.Distance(point2, monsterObject.CurrentLocation);
			if (num4 > num)
			{
				int num5 = num4 * 4 + ((FindNearbyLoot(point2, range) != null) ? 5 : 0);
				if (num5 > num3)
				{
					num3 = num5;
					point = point2;
				}
			}
		}
		if (!point.HasValue)
		{
			return false;
		}
		MirDirection direction2 = Functions.DirectionFromPoint(currentLocation, point.Value);
		User.AttemptAction(new ObjectAction(MirAction.Moving, direction2, point.Value, 1, MagicType.None));
		return true;
	}

	private bool MustFightWhileTravelling()
	{
		if (_travelDestination.HasValue && CurrentMapIndex == _travelDestination.Value.MapIndex && Functions.Distance(User.CurrentLocation, _travelDestination.Value.Location) <= 12)
		{
			return true;
		}
		if (CountAdjacentEnemies() > 0 && (_repathFailures > 0 || _forcedReplans > 0 || _path == null || CEnvir.Now > _lastProgressTime.AddMilliseconds(1500.0)))
		{
			return true;
		}
		TravelPathSegment travelPathSegment = ((_segments != null && _segmentIndex < _segments.Count) ? _segments[_segmentIndex] : null);
		if (travelPathSegment?.ExitMovement != null)
		{
			int num = Functions.Distance(User.CurrentLocation, travelPathSegment.Objective);
			if (num <= 8)
			{
				int num2 = 0;
				bool flag = false;
				foreach (ThreatSource threatSource in _threatSources)
				{
					int num3 = Functions.Distance(threatSource.Monster.CurrentLocation, travelPathSegment.Objective);
					if (num3 <= 3 || (num3 < num && Functions.Distance(threatSource.Monster.CurrentLocation, User.CurrentLocation) <= 3))
					{
						num2++;
						if (threatSource.Monster.MonsterInfo.IsBoss)
						{
							flag = true;
						}
					}
				}
				if (flag || num2 >= 2)
				{
					return true;
				}
			}
		}
		return false;
	}

	private int TargetTier(MonsterObject monster)
	{
		if (monster.TargetID == User.ObjectID || _struckBy.ContainsKey(monster.ObjectID))
		{
			return 2;
		}
		return IsTargetingProtectedTarget(monster) ? 1 : 0;
	}

	private bool ShouldSwitchTarget(MonsterObject found)
	{
		if (_target == null)
		{
			return true;
		}
		int num = TargetTier(found);
		int num2 = TargetTier(_target);
		if (num > num2)
		{
			return true;
		}
		if (num < num2)
		{
			return false;
		}
		if (IsCommittedToTarget(_target))
		{
			return false;
		}
		return Functions.Distance(User.CurrentLocation, found.CurrentLocation) < Functions.Distance(User.CurrentLocation, _target.CurrentLocation);
	}

	private bool IsCommittedToTarget(MonsterObject target)
	{
		int num = Functions.Distance(User.CurrentLocation, target.CurrentLocation);
		bool flag = MonsterObject.IsMagicImmuneAI(target.MonsterInfo.AI);
		if (!(IsMeleeClass || flag) && HasUsableOffense())
		{
			return num <= 6;
		}
		return num <= 1;
	}

	private MonsterObject? FindTarget(bool defensiveOnly, bool ignoreThreatGate = false)
	{
		MonsterObject result = null;
		int num = int.MinValue;
		foreach (MapObject @object in Map.Objects)
		{
			if (!(@object is MonsterObject monsterObject) || !IsValidTarget(monsterObject, defensiveOnly, ignoreThreatGate))
			{
				continue;
			}
			int num2 = Functions.Distance(User.CurrentLocation, monsterObject.CurrentLocation);
			if (num2 <= HuntSearchRange)
			{
				int num3 = TargetTier(monsterObject) * 1000 - num2 * 10;
				if (num3 > num)
				{
					num = num3;
					result = monsterObject;
				}
			}
		}
		return result;
	}

	private bool IsValidTarget(MonsterObject monster, bool defensiveOnly, bool ignoreThreatGate = false)
	{
		if (monster.Dead || monster.MonsterInfo == null)
		{
			return false;
		}
		if (!string.IsNullOrEmpty(monster.PetOwner))
		{
			return false;
		}
		if (_unreachableTargets.TryGetValue(monster.ObjectID, out var value))
		{
			if (CEnvir.Now < value)
			{
				return false;
			}
			_unreachableTargets.Remove(monster.ObjectID);
		}
		if (monster.TargetID == User.ObjectID || _struckBy.ContainsKey(monster.ObjectID))
		{
			return true;
		}
		if (IsTargetingProtectedTarget(monster))
		{
			return true;
		}
		if (defensiveOnly)
		{
			return false;
		}
		if (monster.MonsterInfo.AI < 0)
		{
			return false;
		}
		if (!ignoreThreatGate && GetThreatAt(monster.CurrentLocation, monster) > MaxThreatToEngage)
		{
			return false;
		}
		return true;
	}

	private bool IsTargetingProtectedTarget(MonsterObject monster)
	{
		return IsProtectedTarget(monster.TargetID);
	}

	private bool IsProtectedTarget(uint objectID)
	{
		if (objectID == 0)
		{
			return false;
		}
		if (objectID == User.ObjectID)
		{
			return true;
		}
		if (Scene.Partner != null && Scene.Partner.ObjectID == objectID)
		{
			return true;
		}
		List<ClientPlayerInfo> list = Scene.GroupBox?.Members;
		if (list != null)
		{
			foreach (ClientPlayerInfo item in list)
			{
				if (item.ObjectID == objectID)
				{
					return true;
				}
			}
		}
		foreach (MapObject @object in Map.Objects)
		{
			if (@object.ObjectID == objectID)
			{
				return @object is MonsterObject pet && IsProtectedPet(pet);
			}
		}
		return false;
	}

	private bool IsProtectedPet(MonsterObject pet)
	{
		if (string.IsNullOrEmpty(pet.PetOwner))
		{
			return false;
		}
		if (string.Equals(pet.PetOwner, User.Name, StringComparison.Ordinal))
		{
			return true;
		}
		if (Scene.Partner != null && string.Equals(pet.PetOwner, Scene.Partner.Name, StringComparison.Ordinal))
		{
			return true;
		}
		List<ClientPlayerInfo> list = Scene.GroupBox?.Members;
		if (list == null)
		{
			return false;
		}
		foreach (ClientPlayerInfo item in list)
		{
			if (string.Equals(pet.PetOwner, item.Name, StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}

	private bool TryMeleeAttack(MonsterObject target)
	{
		if (CEnvir.Now < User.AttackTime || User.Horse != HorseType.None)
		{
			return false;
		}
		User.AttemptAction(new ObjectAction(MirAction.Attack, Functions.DirectionFromPoint(User.CurrentLocation, target.CurrentLocation), User.CurrentLocation, 0, MagicType.None, Element.None));
		return true;
	}

	private int CountEnemiesNear(Point location, int radius)
	{
		int num = 0;
		foreach (ThreatSource threatSource in _threatSources)
		{
			if (Functions.Distance(threatSource.Monster.CurrentLocation, location) <= radius)
			{
				num++;
			}
		}
		return num;
	}

	private int CountAdjacentEnemies()
	{
		return CountEnemiesNear(User.CurrentLocation, 1);
	}

	public void SendSettingsUpdate()
	{
		CEnvir.Enqueue(new AutoPlaySettingsChanged
		{
			Settings = Settings
		});
	}

	public AutoPlayer(GameScene scene)
	{
		Scene = scene;
		scene.MapControl.MapInfoChanged += MapControl_MapInfoChanged;
	}

	private void MapControl_MapInfoChanged(object? sender, EventArgs e)
	{
		_mapChanged = true;
	}

	public void Toggle()
	{
		if (Enabled)
		{
			Stop("Stopped.");
		}
		else
		{
			Start();
		}
	}

	public void Start()
	{
		try
		{
			if (Enabled || Scene == null || Scene.Observer || User == null || User.Dead || Map == null)
			{
				return;
			}
			if (!Scene.AllowAutoPlay)
			{
				Scene.ReceiveChat("[AutoPlay] Auto Play is disabled on this server.", MessageType.Hint);
				return;
			}
			MapInfo mapInfo = Map.MapInfo;
			if (mapInfo != null && mapInfo.DisableAutoPlay)
			{
				Scene.ReceiveChat("[AutoPlay] Auto Play is not allowed on this map.", MessageType.Hint);
				return;
			}
			Enabled = true;
			_suppressUntil = DateTime.MinValue;
			_deathReturnsUsed = 0;
			_deathPending = false;
			Intention = ((!_travelDestination.HasValue) ? AutoIntention.Hunting : AutoIntention.Travelling);
			ResetProgress(forceTimeReset: true);
			Scene.ReceiveChat("[AutoPlay] Started.", MessageType.Hint);
			if (Intention == AutoIntention.Hunting)
			{
				CaptureHuntAnchor();
				if (User.InSafeZone && TryStartLocalTownRun())
				{
					Scene.ReceiveChat("[AutoPlay] Starting with town actions.", MessageType.Hint);
				}
			}
			this.StateChanged?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception ex)
		{
			Enabled = false;
			Intention = AutoIntention.None;
			CEnvir.SaveException(ex);
			Scene?.ReceiveChat("[AutoPlay] Failed to start: " + ex.GetType().Name, MessageType.System);
		}
	}

	public void Stop(string? reason)
	{
		if (Enabled)
		{
			Enabled = false;
			Intention = AutoIntention.None;
			CancelTravel();
			ClearPath();
			ResetTown();
			_target = null;
			_unreachableTargets.Clear();
			_travelDestination = null;
			_huntAnchor = null;
			_safeStopReason = null;
			_deathPending = false;
			_deathLocation = null;
			SetState(string.Empty);
			if (!string.IsNullOrEmpty(reason))
			{
				Scene.ReceiveChat("[AutoPlay] " + reason, MessageType.Hint);
			}
			this.StateChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	public void DebugLog(string message)
	{
		if (DebugMessages)
		{
			Scene.ReceiveChat("[AutoPlay] " + message, MessageType.Hint);
		}
	}

	public void NotifyManualInput()
	{
		if (Enabled)
		{
			_suppressUntil = CEnvir.Now.AddMilliseconds(2500.0);
			ClearPath();
		}
	}

	public void Process()
	{
		try
		{
			ConsumePendingManualTravel();
			if (!Enabled || !Preamble())
			{
				return;
			}
			if (CEnvir.Now < _suppressUntil)
			{
				SetState("Paused - manual input");
				return;
			}
			if (ProcessAutoTown())
			{
				return;
			}
			ProcessAutoEquip();
			GrabFreeLoot();
			if (!ProcessCombat() && !ProcessLoot() && !ProcessHarvest())
			{
				switch (Intention)
				{
				case AutoIntention.Travelling:
					ProcessTravel();
					break;
				case AutoIntention.Hunting:
					ProcessHunt();
					break;
				}
			}
		}
		catch (Exception ex)
		{
			CEnvir.SaveException(ex);
			Stop("Error - Auto Play stopped: " + ex.GetType().Name);
		}
	}

	private bool Preamble()
	{
		if (Scene.Observer || User == null)
		{
			Stop(null);
			return false;
		}
		if (User.Dead)
		{
			if (!Settings.ReturnOnDeath)
			{
				Stop("Character died.");
				return false;
			}
			if (!_deathPending)
			{
				_deathPending = true;
				_deathLocation = new Step(CurrentMapIndex, User.CurrentLocation);
				_target = null;
				ResetTown();
				CancelTravel();
				ClearPath();
			}
			SetState("Dead - waiting to revive");
			return false;
		}
		if (_deathPending)
		{
			if (User.ActionQueue.Count > 0)
			{
				SetState("Reviving...");
				return false;
			}
			_deathPending = false;
			if (_deathReturnsUsed >= Settings.MaxDeathReturns)
			{
				Stop($"Died {_deathReturnsUsed} time(s) - death-return limit reached.");
				return false;
			}
			_deathReturnsUsed++;
			Scene.ReceiveChat($"[AutoPlay] Revived - returning to the hunt ({_deathReturnsUsed}/{Settings.MaxDeathReturns}).", MessageType.Hint);
			if (_deathLocation.HasValue)
			{
				_huntAnchor = _deathLocation;
				if (!TryStartLocalTownRun())
				{
					SetTravelDestination(_deathLocation.Value, huntOnArrival: true);
				}
			}
		}
		MapInfo mapInfo = Map.MapInfo;
		if (mapInfo != null && mapInfo.DisableAutoPlay)
		{
			Stop("Auto Play is not allowed on this map.");
			return false;
		}
		if ((User.Poison & PoisonType.Paralysis) == PoisonType.Paralysis)
		{
			return false;
		}
		if (User.Buffs.Any((ClientBuffInfo x) => x.Type == BuffType.DragonRepulse || x.Type == BuffType.FrostBite))
		{
			return false;
		}
		if (_mapChanged)
		{
			_mapChanged = false;
			OnMapChanged();
		}
		RefreshThreat();
		return true;
	}

	private void SetState(string text)
	{
		if (!(StateText == text))
		{
			StateText = text;
			this.StateChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	private void OnPathChanged()
	{
		this.PathChanged?.Invoke(this, EventArgs.Empty);
	}

	public void Dispose()
	{
		Enabled = false;
		CancelTravel();
		if (Scene.MapControl != null)
		{
			Scene.MapControl.MapInfoChanged -= MapControl_MapInfoChanged;
		}
		this.StateChanged = null;
		this.PathChanged = null;
	}

	private void ProcessAutoEquip()
	{
		ClientUserItem clientUserItem = Scene.Equipment[10];
		if (clientUserItem?.Info != null)
		{
			_lastPoisonInfo = clientUserItem.Info;
		}
		ClientUserItem clientUserItem2 = Scene.Equipment[11];
		ItemInfo itemInfo = clientUserItem2?.Info;
		if (itemInfo != null && itemInfo.ItemType == ItemType.Amulet)
		{
			_lastAmuletInfo = clientUserItem2.Info;
		}
		if (!(CEnvir.Now < _nextEquipTime) && (!AutoEquipTorch || Scene.Equipment[3] != null || !TryEquip(ItemType.Torch, EquipmentSlot.Torch, null)) && (!AutoEquipPoison || !UsesPoison || clientUserItem != null || !TryEquip(ItemType.Poison, EquipmentSlot.Poison, _lastPoisonInfo)) && AutoEquipTalisman && UsesTalisman && clientUserItem2 == null)
		{
			TryEquip(ItemType.Amulet, EquipmentSlot.Amulet, _lastAmuletInfo);
		}
	}

	private bool TryEquip(ItemType type, EquipmentSlot slot, ItemInfo? prefer)
	{
		int num = -1;
		for (int i = 0; i < Scene.Inventory.Length; i++)
		{
			ClientUserItem clientUserItem = Scene.Inventory[i];
			if (clientUserItem?.Info != null && clientUserItem.Info.ItemType == type)
			{
				if (num < 0)
				{
					num = i;
				}
				if (prefer != null && clientUserItem.Info == prefer)
				{
					num = i;
					break;
				}
			}
		}
		if (num < 0)
		{
			return false;
		}
		CEnvir.Enqueue(new Library.Network.ClientPackets.ItemMove
		{
			FromGrid = GridType.Inventory,
			FromSlot = num,
			ToGrid = GridType.Equipment,
			ToSlot = (int)slot,
			MergeItem = false
		});
		_nextEquipTime = CEnvir.Now.AddMilliseconds(800.0);
		DebugLog($"Equipping {Scene.Inventory[num].Info.ItemName} -> {slot}");
		return true;
	}

	private bool ProcessLoot()
	{
		if (!LootEnabled)
		{
			return false;
		}
		if (_lootTarget != null && !Map.Objects.Contains(_lootTarget))
		{
			_lootTarget = null;
		}
		if (_lootTarget == null || CEnvir.Now >= _nextLootScan)
		{
			_nextLootScan = CEnvir.Now.AddMilliseconds(300.0);
			_lootTarget = FindLoot();
			if (_lootTarget != null)
			{
				MonsterObject monsterObject = FindHarvestTarget();
				if (monsterObject != null && Functions.Distance(User.CurrentLocation, monsterObject.CurrentLocation) < Functions.Distance(User.CurrentLocation, _lootTarget.CurrentLocation))
				{
					_lootTarget = null;
				}
			}
		}
		if (_lootTarget != null && Intention == AutoIntention.Travelling && Functions.Distance(User.CurrentLocation, _lootTarget.CurrentLocation) > 4)
		{
			_lootTarget = null;
		}
		if (_lootTarget == null)
		{
			return false;
		}
		SetState("Looting " + _lootTarget.Name);
		int num = Math.Max(0, User.Stats[Stat.PickUpRadius]);
		if (Functions.Distance(User.CurrentLocation, _lootTarget.CurrentLocation) <= num)
		{
			RefreshTravelProgressForSideWork();
			if (CEnvir.Now <= Scene.PickUpTime)
			{
				return true;
			}
			if (!_lootMemory.TryGetValue(_lootTarget.ObjectID, out LootMemory value))
			{
				value = (_lootMemory[_lootTarget.ObjectID] = new LootMemory());
			}
			if (CEnvir.Now < value.NextAttempt)
			{
				return true;
			}
			if (++value.Attempts >= 4)
			{
				value.BlacklistedUntil = CEnvir.Now.AddMilliseconds(30000.0);
				value.Attempts = 0;
				_lootTarget = null;
				return true;
			}
			value.NextAttempt = CEnvir.Now.AddMilliseconds(750.0);
			CEnvir.Enqueue(new PickUp());
			Scene.PickUpTime = CEnvir.Now.AddMilliseconds(250.0);
			return true;
		}
		if (_lootPathTarget != _lootTarget.CurrentLocation)
		{
			_lootPathTarget = _lootTarget.CurrentLocation;
			_path = null;
			_pathIndex = 0;
		}
		if (FollowPath())
		{
			return true;
		}
		if (!StartPath(_lootTarget.CurrentLocation, num, 2000, HuntSearchRange * 2))
		{
			if (!_lootMemory.TryGetValue(_lootTarget.ObjectID, out LootMemory value2))
			{
				value2 = (_lootMemory[_lootTarget.ObjectID] = new LootMemory());
			}
			value2.BlacklistedUntil = CEnvir.Now.AddMilliseconds(10000.0);
			_lootTarget = null;
			_lootPathTarget = Point.Empty;
			return false;
		}
		return true;
	}

	private bool IsLootWanted(ItemObject item)
	{
		if (item.Item?.Info == null)
		{
			return false;
		}
		switch ((IsGoldItem(item.Item.Info)) ? PickUpRule.Always : (Scene.GetDropFilterRule(item.Item.Info)?.Rule ?? PickUpRule.Always))
		{
		case PickUpRule.Never:
			return false;
		case PickUpRule.WhenBagSpace:
		{
			Stats addedStats = item.Item.AddedStats;
			if (addedStats == null || addedStats.Count <= 0)
			{
				return false;
			}
			break;
		}
		}
		return CanCarry(item);
	}

	private void GrabFreeLoot()
	{
		if (!LootEnabled || CEnvir.Now <= Scene.PickUpTime)
		{
			return;
		}
		int num = Math.Max(0, User.Stats[Stat.PickUpRadius]);
		foreach (MapObject @object in Map.Objects)
		{
			if (@object is ItemObject itemObject && Functions.Distance(User.CurrentLocation, itemObject.CurrentLocation) <= num && (!_lootMemory.TryGetValue(itemObject.ObjectID, out LootMemory value) || !(CEnvir.Now < value.BlacklistedUntil)) && IsLootWanted(itemObject))
			{
				RefreshTravelProgressForSideWork();
				CEnvir.Enqueue(new PickUp());
				Scene.PickUpTime = CEnvir.Now.AddMilliseconds(250.0);
				break;
			}
		}
	}

	private ItemObject? FindNearbyLoot(Point from, int range)
	{
		ItemObject result = null;
		int num = range + 1;
		foreach (MapObject @object in Map.Objects)
		{
			if (@object is ItemObject itemObject)
			{
				int num2 = Functions.Distance(from, itemObject.CurrentLocation);
				if (num2 < num && (!_lootMemory.TryGetValue(itemObject.ObjectID, out LootMemory value) || !(CEnvir.Now < value.BlacklistedUntil)) && IsLootWanted(itemObject))
				{
					num = num2;
					result = itemObject;
				}
			}
		}
		return result;
	}

	private ItemObject? FindLoot()
	{
		ItemObject result = null;
		int num = int.MinValue;
		foreach (MapObject @object in Map.Objects)
		{
			if (!(@object is ItemObject itemObject))
			{
				continue;
			}
			int num2 = Functions.Distance(User.CurrentLocation, itemObject.CurrentLocation);
			if (num2 > HuntSearchRange)
			{
				continue;
			}
			if (_lootMemory.TryGetValue(itemObject.ObjectID, out LootMemory value) && CEnvir.Now < value.BlacklistedUntil)
			{
				LogLootSkip(itemObject, num2, "blacklisted (pickup kept failing - not ours?)");
				continue;
			}
			if (!IsLootWanted(itemObject))
			{
				LogLootSkip(itemObject, num2, LootSkipReason(itemObject));
				continue;
			}
			if (GetThreatAt(itemObject.CurrentLocation, _target) > LootThreatThreshold)
			{
				LogLootSkip(itemObject, num2, "spot too dangerous right now");
				continue;
			}
			int num3 = -num2 * 50;
			if (num2 <= 1)
			{
				num3 += 1500;
			}
			if (IsGoldItem(itemObject.Item.Info))
			{
				num3 += 300;
			}
			if (itemObject.Item.Info.Rarity != Rarity.Common)
			{
				num3 += 500;
			}
			if (num3 > num)
			{
				num = num3;
				result = itemObject;
			}
		}
		return result;
	}

	private void LogLootSkip(ItemObject item, int distance, string reason)
	{
		if (DebugMessages && distance <= 6 && (!_lootSkipLogged.TryGetValue(item.ObjectID, out var value) || !(CEnvir.Now < value)))
		{
			_lootSkipLogged[item.ObjectID] = CEnvir.Now.AddSeconds(15.0);
			DebugLog($"Skipping {item.Name} ({distance} away): {reason}.");
		}
	}

	private string LootSkipReason(ItemObject item)
	{
		if (item.Item?.Info == null)
		{
			return "no item data";
		}
		if (!IsGoldItem(item.Item.Info))
		{
			switch (Scene.GetDropFilterRule(item.Item.Info)?.Rule ?? PickUpRule.Always)
			{
			case PickUpRule.Never:
				return "drop filter says Never";
			case PickUpRule.WhenBagSpace:
			{
				Stats addedStats = item.Item.AddedStats;
				if (addedStats == null || addedStats.Count <= 0)
				{
					return "drop filter says Only Special";
				}
				break;
			}
			}
			if (User.BagWeight + item.Item.Info.Weight > User.Stats[Stat.BagWeight])
			{
				return "too heavy to carry";
			}
		}
		if (!CanCarry(item))
		{
			return "no bag space";
		}
		return "unknown";
	}

	private bool CanCarry(ItemObject item)
	{
		ItemInfo info = item.Item.Info;
		if (info == null)
		{
			return false;
		}
		if (IsGoldItem(info))
		{
			return true;
		}
		if (User.BagWeight + info.Weight > User.Stats[Stat.BagWeight])
		{
			return false;
		}
		ClientUserItem[] inventory = Scene.Inventory;
		foreach (ClientUserItem clientUserItem in inventory)
		{
			if (clientUserItem == null)
			{
				return true;
			}
			if (clientUserItem.Info == info && info.StackSize > 1 && clientUserItem.Count + item.Item.Count <= info.StackSize)
			{
				return true;
			}
		}
		return false;
	}

	private bool ProcessHarvest()
	{
		if (!HarvestEnabled)
		{
			_harvestTarget = null;
			return false;
		}
		if (_harvestTarget != null && (!_harvestTarget.HarvestLoot || !Map.Objects.Contains(_harvestTarget)))
		{
			if (_harvestTarget != null && !_harvestTarget.HarvestLoot)
			{
				DebugLog("Harvest complete: " + _harvestTarget.Name);
			}
			_harvestTarget = null;
		}
		if (_harvestTarget == null || CEnvir.Now >= _nextHarvestScan)
		{
			_nextHarvestScan = CEnvir.Now.AddMilliseconds(300.0);
			MonsterObject monsterObject = FindHarvestTarget();
			if (monsterObject != _harvestTarget)
			{
				_harvestTarget = monsterObject;
				if (_harvestTarget != null)
				{
					DebugLog("Harvesting: " + _harvestTarget.Name);
				}
			}
		}
		if (_harvestTarget != null && Intention == AutoIntention.Travelling && Functions.Distance(User.CurrentLocation, _harvestTarget.CurrentLocation) > 4)
		{
			_harvestTarget = null;
		}
		if (_harvestTarget == null)
		{
			return false;
		}
		SetState("Harvesting " + _harvestTarget.MonsterInfo.MonsterName);
		if (Functions.Distance(User.CurrentLocation, _harvestTarget.CurrentLocation) > 1)
		{
			if (_harvestPathTarget != _harvestTarget.CurrentLocation)
			{
				_harvestPathTarget = _harvestTarget.CurrentLocation;
				_path = null;
				_pathIndex = 0;
			}
			if (FollowPath())
			{
				return true;
			}
			if (!StartPath(_harvestTarget.CurrentLocation, 1, 2000, HuntSearchRange * 2))
			{
				if (!_harvestMemory.TryGetValue(_harvestTarget.ObjectID, out HarvestMemory value))
				{
					value = (_harvestMemory[_harvestTarget.ObjectID] = new HarvestMemory());
				}
				value.BlacklistedUntil = CEnvir.Now.AddMilliseconds(10000.0);
				_harvestTarget = null;
				_harvestPathTarget = Point.Empty;
				return false;
			}
			return true;
		}
		if (User.Horse != HorseType.None)
		{
			return true;
		}
		RefreshTravelProgressForSideWork();
		if (CEnvir.Now < User.NextActionTime || User.ActionQueue.Count > 0)
		{
			return true;
		}
		if (!_harvestMemory.TryGetValue(_harvestTarget.ObjectID, out HarvestMemory value2))
		{
			value2 = (_harvestMemory[_harvestTarget.ObjectID] = new HarvestMemory());
		}
		if (++value2.Swings > 5)
		{
			BlacklistHarvest(_harvestTarget);
			_harvestTarget = null;
			return true;
		}
		User.AttemptAction(new ObjectAction(MirAction.Harvest, Functions.DirectionFromPoint(User.CurrentLocation, _harvestTarget.CurrentLocation), User.CurrentLocation));
		return true;
	}

	private MonsterObject? FindHarvestTarget()
	{
		if (!HarvestEnabled)
		{
			return null;
		}
		MonsterObject result = null;
		int num = int.MaxValue;
		foreach (MapObject @object in Map.Objects)
		{
			if (@object is MonsterObject { MonsterInfo: not null, Dead: not false, HarvestLoot: not false, Skeleton: false } monsterObject && MonsterObject.IsHarvestAI(monsterObject.MonsterInfo.AI))
			{
				int num2 = Functions.Distance(User.CurrentLocation, monsterObject.CurrentLocation);
				if (num2 <= HuntSearchRange && num2 < num && (!_harvestMemory.TryGetValue(monsterObject.ObjectID, out HarvestMemory value) || !(CEnvir.Now < value.BlacklistedUntil)) && GetThreatAt(monsterObject.CurrentLocation, _target) <= LootThreatThreshold)
				{
					num = num2;
					result = monsterObject;
				}
			}
		}
		return result;
	}

	private void BlacklistHarvest(MonsterObject monster)
	{
		if (!_harvestMemory.TryGetValue(monster.ObjectID, out HarvestMemory value))
		{
			value = (_harvestMemory[monster.ObjectID] = new HarvestMemory());
		}
		value.Swings = 0;
		value.BlacklistedUntil = CEnvir.Now.AddMilliseconds(30000.0);
	}

	private void RefreshTravelProgressForSideWork()
	{
		if (Intention == AutoIntention.Travelling)
		{
			ResetProgress(forceTimeReset: true);
		}
	}

	public void SetTravelDestination(Step destination, bool huntOnArrival)
	{
		BeginTravel(destination, huntOnArrival ? TravelPurpose.ReturnToHunt : TravelPurpose.Manual);
	}

	public void TravelToMapCell(int mapIndex, Point cell)
	{
		Task.Run(async delegate
		{
			PendingManualTravel pending = new PendingManualTravel(new Step(mapIndex, cell), ThroughDoor: false);
			foreach (MovementInfo movement in Globals.MovementInfoList.Binding)
			{
				MapRegion sourceRegion = movement.SourceRegion;
				if (sourceRegion != null && sourceRegion.Map?.Index == mapIndex && movement.DestinationRegion?.Map != null)
				{
					Point[] array = await RouteGraph.GetRegionPointsAsync(movement.SourceRegion).ConfigureAwait(continueOnCapturedContext: false);
					if (array != null && array.Any((Point p) => Functions.Distance(p, cell) <= 1))
					{
						Point[] array2 = await RouteGraph.GetRegionPointsAsync(movement.DestinationRegion).ConfigureAwait(continueOnCapturedContext: false);
						if (array2 != null && array2.Length > 0)
						{
							pending = new PendingManualTravel(new Step(movement.DestinationRegion.Map.Index, array2[0]), ThroughDoor: true);
						}
						break;
					}
				}
			}
			_pendingManualTravel = pending;
		});
	}

	private void ConsumePendingManualTravel()
	{
		PendingManualTravel pending = _pendingManualTravel;
		if (pending == null)
		{
			return;
		}
		_pendingManualTravel = null;
		if (pending.ThroughDoor)
		{
			MapInfo mapInfo = Globals.MapInfoList.Binding.FirstOrDefault((MapInfo x) => x.Index == pending.Destination.MapIndex);
			Scene.ReceiveChat("[AutoPlay] That is a map exit - travelling through to " + (mapInfo?.Description ?? "the other side") + ".", MessageType.Hint);
		}
		SetTravelDestination(pending.Destination, huntOnArrival: false);
	}

	private void BeginTravel(Step destination, TravelPurpose purpose)
	{
		_travelDestination = destination;
		_travelPurpose = purpose;
		_forcedReplans = 0;
		ClearPath();
		CancelTravel();
		if (!Enabled)
		{
			Start();
		}
		Intention = AutoIntention.Travelling;
		ResetProgress(forceTimeReset: true);
	}

	public bool TryTravelToMap(string name)
	{
		MapInfo mapInfo = Globals.MapInfoList.Binding.FirstOrDefault((MapInfo x) => string.Equals(x.Description, name, StringComparison.OrdinalIgnoreCase)) ?? Globals.MapInfoList.Binding.FirstOrDefault((MapInfo x) => string.Equals(x.FileName, name, StringComparison.OrdinalIgnoreCase)) ?? Globals.MapInfoList.Binding.FirstOrDefault((MapInfo x) => x.Description != null && x.Description.Contains(name, StringComparison.OrdinalIgnoreCase));
		if (mapInfo == null)
		{
			return false;
		}
		MapData mapData = MapDataCache.TryGet(mapInfo.FileName);
		Point location = ((mapData != null) ? new Point(mapData.Width / 2, mapData.Height / 2) : new Point(50, 50));
		SetTravelDestination(new Step(mapInfo.Index, location), huntOnArrival: false);
		return true;
	}

	private void CaptureHuntAnchor()
	{
		if (User != null && CurrentMapIndex >= 0)
		{
			_huntAnchor = new Step(CurrentMapIndex, User.CurrentLocation);
		}
	}

	private void ClearPath()
	{
		_roamTarget = Point.Empty;
		if (_path != null || _segments != null)
		{
			_path = null;
			_pathIndex = 0;
			_segments = null;
			_segmentIndex = 0;
			OnPathChanged();
		}
	}

	private void ClearWalkPath()
	{
		if (_path != null || _pathIndex != 0)
		{
			_path = null;
			_pathIndex = 0;
			OnPathChanged();
		}
	}

	private void CancelTravel()
	{
		_planCancel?.Cancel();
		_planCancel = null;
		_pendingPlan = null;
		_planning = false;
		_doorWaitUntil = DateTime.MinValue;
	}

	private void ResetProgress(bool forceTimeReset = false)
	{
		Point point = User?.CurrentLocation ?? Point.Empty;
		if (forceTimeReset || _lastProgressLocation != point || point == Point.Empty)
		{
			_lastProgressLocation = point;
			_lastProgressTime = CEnvir.Now;
		}
		_repathFailures = 0;
	}

	private int FinalTravelStopDistance()
	{
		if (_travelPurpose != TravelPurpose.TownRun)
		{
			return 2;
		}
		return TownNPCRange;
	}

	private void OnMapChanged()
	{
		_path = null;
		_pathIndex = 0;
		_roamTarget = Point.Empty;
		_doorWaitUntil = DateTime.MinValue;
		_forcedReplans = 0;
		ResetProgress(forceTimeReset: true);
		if (Intention == AutoIntention.Travelling && _segments != null)
		{
			int num = _segments.FindIndex((TravelPathSegment x) => x.MapIndex == CurrentMapIndex);
			if (num >= 0)
			{
				_segmentIndex = num;
				OnPathChanged();
			}
			else
			{
				_segments = null;
				CancelTravel();
			}
		}
		else if (Intention == AutoIntention.Hunting)
		{
			CaptureHuntAnchor();
		}
	}

	private void ProcessTravel()
	{
		if (!_travelDestination.HasValue)
		{
			Intention = (_huntAnchor.HasValue ? AutoIntention.Hunting : AutoIntention.None);
			return;
		}
		Step value = _travelDestination.Value;
		if (_travelPurpose == TravelPurpose.SafeZoneStop && User.InSafeZone)
		{
			ArriveAtDestination();
			return;
		}
		int num = FinalTravelStopDistance();
		if (CurrentMapIndex == value.MapIndex && (_segments == null || _segmentIndex == _segments.Count - 1) && Functions.Distance(User.CurrentLocation, value.Location) <= num)
		{
			ArriveAtDestination();
			return;
		}
		if (_segments == null)
		{
			ProcessPlanning(value);
			return;
		}
		if (_segmentIndex >= _segments.Count || _segments[_segmentIndex].MapIndex != CurrentMapIndex)
		{
			_segments = null;
			CancelTravel();
			return;
		}
		TravelPathSegment travelPathSegment = _segments[_segmentIndex];
		bool flag = _segmentIndex == _segments.Count - 1;
		SetState(flag ? $"Travelling - final map ({_segmentIndex + 1}/{_segments.Count})" : $"Travelling - map {_segmentIndex + 1}/{_segments.Count}");
		if (!flag && User.CurrentLocation == travelPathSegment.Objective)
		{
			if (_doorWaitUntil == DateTime.MinValue)
			{
				_doorWaitUntil = CEnvir.Now.AddMilliseconds(4000.0);
			}
			else if (CEnvir.Now > _doorWaitUntil)
			{
				Stop("Map exit did not trigger - it may require an item or level.");
			}
			return;
		}
		_doorWaitUntil = DateTime.MinValue;
		if (FollowPath())
		{
			return;
		}
		if (StartPath(travelPathSegment.Objective, flag ? FinalTravelStopDistance() : 0, 40000))
		{
			_repathFailures = 0;
			_forcedReplans = 0;
		}
		else if (++_repathFailures >= 3)
		{
			_repathFailures = 0;
			if (++_forcedReplans >= 3)
			{
				Stop(flag ? "Could not reach the destination." : "Could not find a path to the map exit.");
				return;
			}
			DebugLog($"Travel: no path to {(flag ? "destination" : "exit")} at {travelPathSegment.Objective.X}, {travelPathSegment.Objective.Y} ({Functions.Distance(User.CurrentLocation, travelPathSegment.Objective)} cells away) - replanning the route.");
			_segments = null;
			CancelTravel();
		}
	}

	private void ProcessPlanning(Step destination)
	{
		TravelPlan pendingPlan = _pendingPlan;
		if (pendingPlan != null)
		{
			_pendingPlan = null;
			_planning = false;
			if (pendingPlan.Destination != destination)
			{
				return;
			}
			if (pendingPlan.Segments == null)
			{
				Stop("No route to the destination.");
				return;
			}
			_segments = pendingPlan.Segments;
			_segmentIndex = Math.Max(0, _segments.FindIndex((TravelPathSegment x) => x.MapIndex == CurrentMapIndex));
			ResetProgress(forceTimeReset: true);
			OnPathChanged();
		}
		else if (_planning)
		{
			SetState("Planning route...");
		}
		else
		{
			BeginPlan(destination);
		}
	}

	private void BeginPlan(Step destination)
	{
		_planning = true;
		SetState("Planning route...");
		CancellationTokenSource cancel = new CancellationTokenSource();
		_planCancel = cancel;
		Step start = new Step(CurrentMapIndex, User.CurrentLocation);
		MirDirection startDirection = User.Direction;
		MirClass userClass = User.Class;
		int userLevel = User.Level;
		int finalStopDistance = FinalTravelStopDistance();
		HashSet<ItemInfo> carriedItems = new HashSet<ItemInfo>(Scene.Inventory.Select((ClientUserItem x) => x?.Info).OfType<ItemInfo>());
		Task.Run(async delegate
		{
			TravelPlan pendingPlan;
			try
			{
				pendingPlan = await BuildPlanAsync(start, destination, startDirection, userClass, userLevel, carriedItems.Contains, finalStopDistance, cancel.Token).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (Exception ex)
			{
				CEnvir.SaveError(ex.ToString());
				pendingPlan = new TravelPlan
				{
					Destination = destination,
					Segments = null
				};
			}
			if (!cancel.IsCancellationRequested)
			{
				_pendingPlan = pendingPlan;
			}
		}, cancel.Token);
	}

	private static async Task<TravelPlan> BuildPlanAsync(Step start, Step destination, MirDirection startDirection, MirClass userClass, int userLevel, Func<ItemInfo, bool> hasItem, int finalStopDistance, CancellationToken token)
	{
		TravelPlan failed = new TravelPlan
		{
			Destination = destination,
			Segments = null
		};
		List<RouteHop> list = await RouteGraph.FindRouteAsync(start, destination, userClass, userLevel, hasItem).ConfigureAwait(continueOnCapturedContext: false);
		if (list == null)
		{
			return failed;
		}
		List<TravelPathSegment> segments = new List<TravelPathSegment>();
		Point position = start.Location;
		int mapIndex = start.MapIndex;
		MirDirection direction = startDirection;
		foreach (RouteHop hop in list)
		{
			token.ThrowIfCancellationRequested();
			TravelPathSegment segment = new TravelPathSegment
			{
				MapIndex = mapIndex,
				Objective = hop.SourcePoint,
				ExitMovement = hop.Movement
			};
			MapData mapData = await MapDataCache.GetAsync(GetFileName(mapIndex)).ConfigureAwait(continueOnCapturedContext: false);
			if (mapData == null)
			{
				return failed;
			}
			List<Point> list2 = PathFinder.FindPath(new StaticGrid(mapData), position, hop.SourcePoint, direction);
			if (list2 == null)
			{
				return failed;
			}
			segment.Points = list2;
			segments.Add(segment);
			position = hop.DestinationPoint;
			mapIndex = hop.Movement.DestinationRegion.Map.Index;
			direction = MirDirection.Up;
		}
		token.ThrowIfCancellationRequested();
		MapData mapData2 = await MapDataCache.GetAsync(GetFileName(mapIndex)).ConfigureAwait(continueOnCapturedContext: false);
		if (mapData2 == null)
		{
			return failed;
		}
		Point point = new Point(Math.Clamp(destination.Location.X, 0, mapData2.Width - 1), Math.Clamp(destination.Location.Y, 0, mapData2.Height - 1));
		if (mapData2.IsWall(point.X, point.Y))
		{
			Point? point2 = FindNearestWalkable(mapData2, point, 30);
			if (!point2.HasValue)
			{
				return failed;
			}
			point = point2.Value;
		}
		TravelPathSegment travelPathSegment = new TravelPathSegment
		{
			MapIndex = mapIndex,
			Objective = point
		};
		List<Point> list3 = PathFinder.FindPath(new StaticGrid(mapData2), position, point, direction, finalStopDistance);
		if (list3 == null)
		{
			return failed;
		}
		travelPathSegment.Points = list3;
		segments.Add(travelPathSegment);
		return new TravelPlan
		{
			Destination = destination,
			Segments = segments
		};
	}

	private static string? GetFileName(int mapIndex)
	{
		return Globals.MapInfoList.Binding.FirstOrDefault((MapInfo x) => x.Index == mapIndex)?.FileName;
	}

	private static Point? FindNearestWalkable(MapData data, Point target, int maxRadius)
	{
		for (int i = 1; i <= maxRadius; i++)
		{
			for (int j = target.X - i; j <= target.X + i; j++)
			{
				for (int k = target.Y - i; k <= target.Y + i; k++)
				{
					if (Math.Max(Math.Abs(j - target.X), Math.Abs(k - target.Y)) == i && !data.IsWall(j, k))
					{
						return new Point(j, k);
					}
				}
			}
		}
		return null;
	}

	private void ArriveAtDestination()
	{
		_travelDestination = null;
		CancelTravel();
		ClearPath();
		switch (_travelPurpose)
		{
		case TravelPurpose.ReturnToHunt:
			Intention = AutoIntention.Hunting;
			CaptureHuntAnchor();
			SetState("Hunting");
			break;
		case TravelPurpose.TownRun:
			Intention = AutoIntention.None;
			OnTownArrived();
			break;
		case TravelPurpose.SafeZoneStop:
			Stop(_safeStopReason ?? "Stopped in safe zone.");
			_safeStopReason = null;
			break;
		default:
			Stop("Arrived at destination.");
			break;
		}
	}

	private void ProcessHunt()
	{
		SetState("Roaming");
		if (!FollowPath())
		{
			TryStartRoam();
		}
	}

	private bool TryStartRoam()
	{
		if (Map.Width <= 0 || Map.Height <= 0)
		{
			return false;
		}
		if (_roamTarget != Point.Empty)
		{
			int num = Functions.Distance(User.CurrentLocation, _roamTarget);
			if (num > 10 && StartPath(_roamTarget, 0, 8000, num + 20))
			{
				return true;
			}
			_roamTarget = Point.Empty;
		}
		Point point = ((_huntAnchor?.MapIndex == CurrentMapIndex) ? _huntAnchor.Value.Location : User.CurrentLocation);
		int num2 = Math.Max(Map.Width, Map.Height);
		int num3 = point.X + Random.Shared.Next(-num2, num2 + 1);
		int num4 = point.Y + Random.Shared.Next(-num2, num2 + 1);
		if (num3 < 0 || num4 < 0 || num3 >= Map.Width || num4 >= Map.Height)
		{
			return false;
		}
		Point point2 = new Point(num3, num4);
		int num5 = Functions.Distance(User.CurrentLocation, point2);
		if (num5 < 10 || Map.Cells[num3, num4].Flag)
		{
			return false;
		}
		if (GetThreatAt(point2) > RoamThreatThreshold)
		{
			return false;
		}
		_roamTarget = point2;
		return StartPath(point2, 0, 8000, num5 + 20);
	}

	private bool StartPath(Point target, int stopDistance, int maxNodes = 20000, int boundary = int.MaxValue)
	{
		List<Point> list = PathFinder.FindLivePath(Map, User.CurrentLocation, target, User.Direction, stopDistance, maxNodes, boundary);
		if (list == null || list.Count == 0)
		{
			_path = null;
			_pathIndex = 0;
			return false;
		}
		_path = list;
		_pathIndex = 0;
		_repathFailures = 0;
		ResetProgress();
		OnPathChanged();
		return true;
	}

	private bool FollowPath()
	{
		if (_path == null)
		{
			return false;
		}
		Point currentLocation = User.CurrentLocation;
		if (currentLocation != _lastProgressLocation)
		{
			_lastProgressLocation = currentLocation;
			_lastProgressTime = CEnvir.Now;
		}
		else
		{
			if (CEnvir.Now > _lastProgressTime.AddMilliseconds(5000.0))
			{
				AbandonPath();
				return false;
			}
			if (CEnvir.Now > _lastProgressTime.AddMilliseconds(1500.0))
			{
				Repath();
				return _path != null;
			}
		}
		int pathIndex = _pathIndex;
		for (int num = Math.Min(_path.Count, _pathIndex + 4) - 1; num >= _pathIndex; num--)
		{
			if (!(_path[num] != currentLocation))
			{
				_pathIndex = num + 1;
				break;
			}
		}
		if (_pathIndex >= _path.Count)
		{
			_path = null;
			_pathIndex = 0;
			OnPathChanged();
			return false;
		}
		if (_pathIndex != pathIndex)
		{
			OnPathChanged();
		}
		if (!Scene.MoveFrame && !Config.SmoothMove)
		{
			return true;
		}
		if ((User.Poison & PoisonType.WraithGrip) == PoisonType.WraithGrip)
		{
			return true;
		}
		Point point = _path[_pathIndex];
		if (Functions.Distance(currentLocation, point) != 1)
		{
			Repath();
			return _path != null;
		}
		MirDirection direction = Functions.DirectionFromPoint(currentLocation, point);
		if (Functions.Move(currentLocation, direction) != point)
		{
			Repath();
			return _path != null;
		}
		int num2 = 1;
		if (Scene.CanRun && CEnvir.Now >= User.NextRunTime && User.BagWeight <= User.Stats[Stat.BagWeight] && User.WearWeight <= User.Stats[Stat.WearWeight])
		{
			num2 = ((User.Horse != HorseType.None) ? 3 : 2);
		}
		int i;
		for (i = 1; i < num2 && _pathIndex + i < _path.Count && _path[_pathIndex + i] == Functions.Move(currentLocation, direction, i + 1); i++)
		{
		}
		int num3 = 0;
		for (int j = 1; j <= i; j++)
		{
			Point point2 = Functions.Move(currentLocation, direction, j);
			if (point2.X < 0 || point2.Y < 0 || point2.X >= Map.Width || point2.Y >= Map.Height || Map.Cells[point2.X, point2.Y].Blocking())
			{
				break;
			}
			num3 = j;
		}
		if (num3 == 0)
		{
			Repath();
			return _path != null;
		}
		User.AttemptAction(new ObjectAction(MirAction.Moving, direction, Functions.Move(currentLocation, direction, num3), num3, MagicType.None));
		return true;
	}

	private void Repath()
	{
		(Point, int)? tuple = CurrentObjective();
		_path = null;
		_pathIndex = 0;
		ResetProgress();
		if (!tuple.HasValue)
		{
			OnPathChanged();
			return;
		}
		int maxNodes = ((Intention == AutoIntention.Travelling) ? 40000 : 20000);
		if (!StartPath(tuple.Value.Item1, tuple.Value.Item2, maxNodes) && ++_repathFailures >= 3)
		{
			AbandonPath();
		}
	}

	private void AbandonPath()
	{
		_path = null;
		_pathIndex = 0;
		_roamTarget = Point.Empty;
		ResetProgress(forceTimeReset: true);
		OnPathChanged();
		if (Intention == AutoIntention.Travelling)
		{
			Stop("Stuck while travelling - stopping.");
		}
	}

	private (Point Location, int StopDistance)? CurrentObjective()
	{
		switch (Intention)
		{
		case AutoIntention.Travelling:
			if (_segments != null && _segmentIndex < _segments.Count)
			{
				TravelPathSegment travelPathSegment = _segments[_segmentIndex];
				return (travelPathSegment.Objective, (travelPathSegment.ExitMovement == null) ? FinalTravelStopDistance() : 0);
			}
			break;
		case AutoIntention.Hunting:
			if (_roamTarget != Point.Empty)
			{
				return (_roamTarget, 0);
			}
			break;
		}
		return null;
	}

	private bool TryRotation(MonsterObject target, bool allowOffense = true)
	{
		if (User.Horse != HorseType.None)
		{
			return false;
		}
		if (!Rotations.TryGetValue(User.Class, out List<RotationEntry> value))
		{
			return false;
		}
		int num = CountAdjacentEnemies();
		int num2 = Functions.Distance(User.CurrentLocation, target.CurrentLocation);
		RotationEntry rotationEntry = null;
		int num3 = int.MinValue;
		foreach (RotationEntry entry in value)
		{
			ClientUserMagic magic = GetMagic(entry.Magic);
			if (magic?.Info == null || User.Level < magic.Info.NeedLevel1 || !magic.AutoPlayEnabled)
			{
				continue;
			}
			switch (entry.Kind)
			{
			case EntryKind.Buff:
				if (!Ready(magic))
				{
					break;
				}
				if (IsTaoistAreaBuff(entry.Magic) && entry.Buff != BuffType.None)
				{
					if (TryGetAutoBuffLocation(entry.Buff, out var location2) && Scene.TryUseMagic(magic, User, location2))
					{
						return true;
					}
				}
				else if ((entry.Buff == BuffType.None || !User.Buffs.Any((ClientBuffInfo x) => x.Type == entry.Buff)) && Scene.TryUseMagic(magic))
				{
					return true;
				}
				break;
			case EntryKind.Stance:
				if (GetStanceState(entry.Magic) != num >= entry.MinTargets && Scene.TryUseMagic(magic))
				{
					return true;
				}
				break;
			case EntryKind.Empower:
				if (User.AttackMagic == MagicType.None && (entry.Condition == null || entry.Condition(target, User)) && Ready(magic) && Scene.TryUseMagic(magic))
				{
					return true;
				}
				break;
			case EntryKind.Summon:
				if (ShouldAutoSummon(entry.Magic) && Ready(magic) && Scene.TryUseMagic(magic))
				{
					RememberPendingSummon(entry.Magic);
					return true;
				}
				break;
			case EntryKind.HealSelf:
			{
				int num5 = ((magic.AutoPlayThreshold > 0) ? magic.AutoPlayThreshold : 60);
				if (HPPercent <= num5 && Ready(magic) && Scene.TryUseMagic(magic, User))
				{
					return true;
				}
				break;
			}
			case EntryKind.PoisonUpkeep:
				if (allowOffense && target.Poison == PoisonType.None && Ready(magic) && InMagicRange(target) && Scene.TryUseMagic(magic, target, target.CurrentLocation))
				{
					return true;
				}
				break;
			case EntryKind.AoE:
			case EntryKind.Single:
			{
				if (!allowOffense)
				{
					break;
				}
				if (entry.Kind == EntryKind.AoE)
				{
					Point location = (entry.CountTargetsFromCaster ? User.CurrentLocation : target.CurrentLocation);
					if (CountEnemiesNear(location, 2) < entry.MinTargets)
					{
						break;
					}
				}
				if ((entry.RequireUndead && !target.MonsterInfo.Undead) || (entry.Condition != null && !entry.Condition(target, User)) || num2 < entry.MinDistance || !Ready(magic) || !InMagicRange(target))
				{
					break;
				}
				int num4 = entry.Priority + ((entry.Kind == EntryKind.AoE) ? 500 : 0);
				if (entry.Element != Element.None)
				{
					int resistanceValue = target.MonsterInfo.Stats.GetResistanceValue(entry.Element);
					num4 -= resistanceValue * 100;
					if (resistanceValue >= 5)
					{
						num4 -= 10000;
					}
				}
				if (entry.UndeadBonus && target.MonsterInfo.Undead)
				{
					num4 += 150;
				}
				if (num4 > num3)
				{
					num3 = num4;
					rotationEntry = entry;
				}
				break;
			}
			}
		}
		if (rotationEntry == null)
		{
			return false;
		}
		ClientUserMagic magic2 = GetMagic(rotationEntry.Magic);
		if (magic2 != null)
		{
			return Scene.TryUseMagic(magic2, target, target.CurrentLocation);
		}
		return false;
	}

	private bool HasUsableOffense()
	{
		if (!Rotations.TryGetValue(User.Class, out List<RotationEntry> value))
		{
			return false;
		}
		foreach (RotationEntry item in value)
		{
			if (item.Kind == EntryKind.AoE || item.Kind == EntryKind.Single)
			{
				ClientUserMagic magic = GetMagic(item.Magic);
				if (magic?.Info != null && User.Level >= magic.Info.NeedLevel1 && magic.AutoPlayEnabled && HasRequiredConsumables(magic.Info.Magic))
				{
					return true;
				}
			}
		}
		return false;
	}

	private bool TryTravelSelfHeal()
	{
		if (User.Horse != HorseType.None)
		{
			return false;
		}
		if (!Rotations.TryGetValue(User.Class, out List<RotationEntry> value))
		{
			return false;
		}
		foreach (RotationEntry item in value)
		{
			if (item.Kind != EntryKind.HealSelf)
			{
				continue;
			}
			ClientUserMagic magic = GetMagic(item.Magic);
			if (magic?.Info != null && User.Level >= magic.Info.NeedLevel1 && magic.AutoPlayEnabled)
			{
				int num = ((magic.AutoPlayThreshold > 0) ? magic.AutoPlayThreshold : 60);
				if (HPPercent <= num && Ready(magic) && Scene.TryUseMagic(magic, User))
				{
					return true;
				}
			}
		}
		return false;
	}

	private ClientUserMagic? GetMagic(MagicType type)
	{
		foreach (KeyValuePair<MagicInfo, ClientUserMagic> magic in User.Magics)
		{
			if (magic.Key.Magic == type)
			{
				return magic.Value;
			}
		}
		return null;
	}

	public static bool IsRotationMagic(MagicType type)
	{
		foreach (List<RotationEntry> value in Rotations.Values)
		{
			foreach (RotationEntry item in value)
			{
				if (item.Magic == type)
				{
					return true;
				}
			}
		}
		return false;
	}

	public static bool IsHealMagic(MagicType type)
	{
		foreach (List<RotationEntry> value in Rotations.Values)
		{
			foreach (RotationEntry item in value)
			{
				if (item.Magic == type && item.Kind == EntryKind.HealSelf)
				{
					return true;
				}
			}
		}
		return false;
	}

	private bool Ready(ClientUserMagic magic)
	{
		if (CEnvir.Now >= magic.NextCast && magic.Cost <= User.CurrentMP)
		{
			return HasRequiredConsumables(magic.Info.Magic);
		}
		return false;
	}

	private static bool IsTaoistAreaBuff(MagicType type)
	{
		if (type == MagicType.MagicResistance || type == MagicType.Resilience || type == MagicType.BloodLust)
		{
			return true;
		}
		return false;
	}

	private bool TryGetAutoBuffLocation(BuffType buff, out Point location)
	{
		location = User.CurrentLocation;
		List<MapObject> list = new List<MapObject>();
		if (!HasBuff(User, buff))
		{
			list.Add(User);
		}
		HashSet<string> autoBuffPetOwners = GetAutoBuffPetOwners();
		int num = 13;
		foreach (MapObject @object in Map.Objects)
		{
			if (@object == User || @object.Dead || !@object.Visible || HasBuff(@object, buff) || Functions.Distance(User.CurrentLocation, @object.CurrentLocation) > num)
			{
				continue;
			}
			MapObject mapObject = @object;
			if (!(mapObject is PlayerObject playerObject))
			{
				if (mapObject is MonsterObject { CompanionObject: null } monsterObject && autoBuffPetOwners.Contains(monsterObject.PetOwner))
				{
					list.Add(monsterObject);
				}
			}
			else if (Scene.IsAlly(playerObject.ObjectID))
			{
				list.Add(playerObject);
			}
		}
		if (list.Count == 0)
		{
			return false;
		}
		bool flag = list.Contains(User);
		double num2 = 0.0;
		double num3 = 0.0;
		foreach (MapObject item in list)
		{
			num2 += (double)item.CurrentLocation.X;
			num3 += (double)item.CurrentLocation.Y;
		}
		num2 /= (double)list.Count;
		num3 /= (double)list.Count;
		int num4 = 0;
		double num5 = double.MaxValue;
		int num6 = int.MaxValue;
		int num7 = Math.Max(0, User.CurrentLocation.X - 10);
		int num8 = Math.Min(Map.Width - 1, User.CurrentLocation.X + 10);
		int num9 = Math.Max(0, User.CurrentLocation.Y - 10);
		int num10 = Math.Min(Map.Height - 1, User.CurrentLocation.Y + 10);
		for (int i = num9; i <= num10; i++)
		{
			for (int j = num7; j <= num8; j++)
			{
				Point point = new Point(j, i);
				if (!Functions.InRange(point, User.CurrentLocation, 10) || (flag && !Functions.InRange(point, User.CurrentLocation, 3)))
				{
					continue;
				}
				int num11 = 0;
				foreach (MapObject item2 in list)
				{
					if (Functions.InRange(point, item2.CurrentLocation, 3))
					{
						num11++;
					}
				}
				if (num11 != 0)
				{
					double num12 = (double)point.X - num2;
					double num13 = (double)point.Y - num3;
					double num14 = num12 * num12 + num13 * num13;
					int num15 = Functions.Distance(point, User.CurrentLocation);
					if (num11 >= num4 && (num11 != num4 || !(num14 > num5)) && (num11 != num4 || num14 != num5 || num15 < num6))
					{
						num4 = num11;
						num5 = num14;
						num6 = num15;
						location = point;
					}
				}
			}
		}
		return num4 > 0;
	}

	private static bool HasBuff(MapObject ob, BuffType buff)
	{
		if (!(ob is UserObject userObject))
		{
			return ob.VisibleBuffs.ContainsKey(buff);
		}
		return userObject.Buffs.Any((ClientBuffInfo x) => x.Type == buff);
	}

	private HashSet<string> GetAutoBuffPetOwners()
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		if (!string.IsNullOrEmpty(User.Name))
		{
			hashSet.Add(User.Name);
		}
		if (!string.IsNullOrEmpty(Scene.Partner?.Name))
		{
			hashSet.Add(Scene.Partner.Name);
		}
		if (Scene.GroupBox?.Members != null)
		{
			foreach (ClientPlayerInfo member in Scene.GroupBox.Members)
			{
				if (!string.IsNullOrEmpty(member.Name))
				{
					hashSet.Add(member.Name);
				}
			}
		}
		if (Scene.GuildBox?.GuildInfo?.Members != null)
		{
			foreach (ClientGuildMemberInfo member2 in Scene.GuildBox.GuildInfo.Members)
			{
				if (!string.IsNullOrEmpty(member2.Name))
				{
					hashSet.Add(member2.Name);
				}
			}
		}
		return hashSet;
	}

	private bool HasRequiredConsumables(MagicType type)
	{
		ClientUserItem clientUserItem = Scene.Equipment[11];
		ClientUserItem clientUserItem2 = Scene.Equipment[10];
		int result2;
		int result3;
		int result7;
		int result5;
		int result8;
		int result4;
		int result6;
		int result;
		switch (type)
		{
		case MagicType.PoisonDust:
			if (clientUserItem2 != null)
			{
				ItemInfo? info2 = clientUserItem2.Info;
				if (info2 != null && info2.ItemType == ItemType.Poison)
				{
					result2 = ((clientUserItem2.Count >= 1) ? 1 : 0);
					goto IL_00ea;
				}
			}
			result2 = 0;
			goto IL_00ea;
		case MagicType.ExplosiveTalisman:
		case MagicType.MagicResistance:
		case MagicType.ElementalSuperiority:
		case MagicType.ImprovedExplosiveTalisman:
		case MagicType.SummonSkeleton:
			if (clientUserItem != null)
			{
				ItemInfo? info5 = clientUserItem.Info;
				if (info5 != null && info5.ItemType == ItemType.Amulet && clientUserItem.Info.Shape == 0)
				{
					result3 = ((clientUserItem.Count >= 1) ? 1 : 0);
					goto IL_0128;
				}
			}
			result3 = 0;
			goto IL_0128;
		case MagicType.Invisibility:
		case MagicType.MassInvisibility:
		case MagicType.Resilience:
		case MagicType.TrapOctagon:
		case MagicType.BloodLust:
		case MagicType.Purification:
		case MagicType.LifeSteal:
		case MagicType.SummonJinSkeleton:
			if (clientUserItem != null)
			{
				ItemInfo? info6 = clientUserItem.Info;
				if (info6 != null && info6.ItemType == ItemType.Amulet && clientUserItem.Info.Shape == 0)
				{
					result7 = ((clientUserItem.Count >= 2) ? 1 : 0);
					goto IL_0166;
				}
			}
			result7 = 0;
			goto IL_0166;
		case MagicType.SummonShinsu:
		case MagicType.StrengthOfFaith:
			if (clientUserItem != null)
			{
				ItemInfo? info3 = clientUserItem.Info;
				if (info3 != null && info3.ItemType == ItemType.Amulet && clientUserItem.Info.Shape == 0)
				{
					result5 = ((clientUserItem.Count >= 5) ? 1 : 0);
					goto IL_01a4;
				}
			}
			result5 = 0;
			goto IL_01a4;
		case MagicType.Transparency:
			if (clientUserItem != null)
			{
				ItemInfo? info7 = clientUserItem.Info;
				if (info7 != null && info7.ItemType == ItemType.Amulet && clientUserItem.Info.Shape == 0)
				{
					result8 = ((clientUserItem.Count >= 10) ? 1 : 0);
					goto IL_01e3;
				}
			}
			result8 = 0;
			goto IL_01e3;
		case MagicType.CelestialLight:
		case MagicType.DemonExplosion:
			if (clientUserItem != null)
			{
				ItemInfo? info8 = clientUserItem.Info;
				if (info8 != null && info8.ItemType == ItemType.Amulet && clientUserItem.Info.Shape == 0)
				{
					result4 = ((clientUserItem.Count >= 20) ? 1 : 0);
					goto IL_0222;
				}
			}
			result4 = 0;
			goto IL_0222;
		case MagicType.SummonDemonicCreature:
			if (clientUserItem != null)
			{
				ItemInfo? info4 = clientUserItem.Info;
				if (info4 != null && info4.ItemType == ItemType.Amulet && clientUserItem.Info.Shape == 0)
				{
					result6 = ((clientUserItem.Count >= 25) ? 1 : 0);
					goto IL_025e;
				}
			}
			result6 = 0;
			goto IL_025e;
		case MagicType.Resurrection:
			if (clientUserItem != null)
			{
				ItemInfo? info = clientUserItem.Info;
				if (info != null && info.ItemType == ItemType.Amulet && clientUserItem.Info.Shape == 1)
				{
					result = ((clientUserItem.Count >= 1) ? 1 : 0);
					goto IL_029a;
				}
			}
			result = 0;
			goto IL_029a;
		default:
			{
				return true;
			}
			IL_00ea:
			return (byte)result2 != 0;
			IL_0128:
			return (byte)result3 != 0;
			IL_0222:
			return (byte)result4 != 0;
			IL_029a:
			return (byte)result != 0;
			IL_0166:
			return (byte)result7 != 0;
			IL_01a4:
			return (byte)result5 != 0;
			IL_025e:
			return (byte)result6 != 0;
			IL_01e3:
			return (byte)result8 != 0;
		}
	}

	private bool InMagicRange(MapObject target)
	{
		return Functions.InRange(target.CurrentLocation, User.CurrentLocation, 10);
	}

	private bool GetStanceState(MagicType type)
	{
		return type switch
		{
			MagicType.Thrusting => User.CanThrusting, 
			MagicType.HalfMoon => User.CanHalfMoon, 
			MagicType.DestructiveSurge => User.CanDestructiveSurge, 
			MagicType.FlameSplash => User.CanFlameSplash, 
			_ => false, 
		};
	}

	public void ObserveMonster(MonsterObject monster)
	{
		if (User != null && monster.MonsterInfo != null)
		{
			if (!monster.Dead && string.Equals(monster.PetOwner, User.Name, StringComparison.Ordinal))
			{
				MonsterFlag flag = monster.MonsterInfo.Flag;
				_trackedPets[monster.ObjectID] = new TrackedPet(flag, CEnvir.Now);
				_pendingSummons.Remove(flag);
			}
			else
			{
				_trackedPets.Remove(monster.ObjectID);
			}
		}
	}

	public void ObserveObjectDied(uint objectID)
	{
		_trackedPets.Remove(objectID);
		MonsterObject? target = _target;
		if (target != null && target.ObjectID == objectID)
		{
			_target = null;
			_combatPathTarget = default(Point);
			ClearWalkPath();
		}
	}

	private bool ShouldAutoSummon(MagicType type)
	{
		if (!TryGetSummonFlag(type, out var flag))
		{
			return false;
		}
		RefreshTrackedPets();
		if (_pendingSummons.ContainsKey(flag))
		{
			return false;
		}
		if (_trackedPets.Values.Any((TrackedPet x) => x.Flag == flag))
		{
			return false;
		}
		return _trackedPets.Count + _pendingSummons.Count < 2;
	}

	private void RememberPendingSummon(MagicType type)
	{
		if (TryGetSummonFlag(type, out var flag))
		{
			_pendingSummons[flag] = CEnvir.Now;
		}
	}

	private void RefreshTrackedPets()
	{
		foreach (MapObject @object in Map.Objects)
		{
			if (@object is MonsterObject monster)
			{
				ObserveMonster(monster);
			}
		}
		foreach (KeyValuePair<uint, TrackedPet> item in _trackedPets.ToList())
		{
			if (!(CEnvir.Now - item.Value.SeenTime <= TrackedPetLifetime))
			{
				_trackedPets.Remove(item.Key);
			}
		}
		foreach (KeyValuePair<MonsterFlag, DateTime> item2 in _pendingSummons.ToList())
		{
			if (!(CEnvir.Now - item2.Value <= PendingSummonLifetime))
			{
				_pendingSummons.Remove(item2.Key);
			}
		}
	}

	private static bool TryGetSummonFlag(MagicType type, out MonsterFlag flag)
	{
		switch (type)
		{
		case MagicType.SummonDemonicCreature:
			flag = MonsterFlag.InfernalSoldier;
			return true;
		case MagicType.SummonJinSkeleton:
			flag = MonsterFlag.JinSkeleton;
			return true;
		case MagicType.SummonShinsu:
			flag = MonsterFlag.Shinsu;
			return true;
		case MagicType.SummonSkeleton:
			flag = MonsterFlag.Skeleton;
			return true;
		default:
			flag = MonsterFlag.None;
			return false;
		}
	}

	public void ObserveTarget(MonsterObject monster, uint targetId)
	{
	}

	public void ObserveStruck(uint attackerId)
	{
		if (Enabled && attackerId != 0)
		{
			_struckBy[attackerId] = CEnvir.Now;
		}
	}

	private void RefreshThreat()
	{
		_threatSources.Clear();
		EngagedCount = 0;
		if (_struckBy.Count > 0)
		{
			List<uint> list = null;
			foreach (KeyValuePair<uint, DateTime> item in _struckBy)
			{
				if (!(CEnvir.Now < item.Value.AddMilliseconds(8000.0)))
				{
					(list ?? (list = new List<uint>())).Add(item.Key);
				}
			}
			if (list != null)
			{
				foreach (uint item2 in list)
				{
					_struckBy.Remove(item2);
				}
			}
		}
		foreach (MapObject @object in Map.Objects)
		{
			if (!(@object is MonsterObject { Dead: false, MonsterInfo: not null } monsterObject) || !string.IsNullOrEmpty(monsterObject.PetOwner))
			{
				continue;
			}
			bool flag = monsterObject.TargetID == User.ObjectID || _struckBy.ContainsKey(monsterObject.ObjectID);
			bool flag2 = IsTargetingProtectedTarget(monsterObject);
			if ((monsterObject.MonsterInfo.AI >= 0 && !MonsterObject.IsPassiveAI(monsterObject.MonsterInfo.AI)) || flag || flag2)
			{
				if (flag)
				{
					EngagedCount++;
				}
				int value = 100 + (monsterObject.MonsterInfo.Level - User.Level) * 10;
				value = Math.Clamp(value, 50, 300);
				if (monsterObject.MonsterInfo.IsBoss)
				{
					value *= 2;
				}
				if (flag)
				{
					value *= 2;
				}
				_threatSources.Add(new ThreatSource
				{
					Monster = monsterObject,
					Weight = value,
					ViewRange = Math.Max(1, monsterObject.MonsterInfo.ViewRange),
					EngagedOnMe = flag
				});
			}
		}
	}

	public int GetThreatAt(Point location, MonsterObject? exclude = null)
	{
		int num = 0;
		foreach (ThreatSource threatSource in _threatSources)
		{
			if (threatSource.Monster != exclude)
			{
				int num2 = Functions.Distance(threatSource.Monster.CurrentLocation, location);
				int num3 = threatSource.ViewRange + 4;
				if (num2 <= num3)
				{
					num = ((num2 > threatSource.ViewRange) ? (num + threatSource.Weight * (num3 - num2) / 4) : (num + threatSource.Weight));
				}
			}
		}
		return num;
	}

	public void ObserveAutoTownResult(AutoTownResult p)
	{
		_townResult = p;
	}

	private void ResetTown()
	{
		_townState = AutoTownState.None;
		_townWork.Clear();
		_currentWork = null;
		_townResult = null;
		_leavingForTown = false;
		_townWorkActions = null;
		_townTeleportSuppressed = false;
		_townFailureMessages.Clear();
		_townSkippedActions.Clear();
		_townInitialRestockNeeds.Clear();
	}

	private bool ProcessAutoTown()
	{
		switch (_townState)
		{
		case AutoTownState.None:
			if (Intention != AutoIntention.Hunting && _travelPurpose != TravelPurpose.ReturnToHunt)
			{
				return false;
			}
			if (!_leavingForTown)
			{
				if (CEnvir.Now < _nextTownCheck)
				{
					return false;
				}
				_nextTownCheck = CEnvir.Now.AddMilliseconds(2000.0);
				string text = TownReason();
				if (text == null)
				{
					return false;
				}
				_leavingForTown = true;
				_target = null;
				Scene.ReceiveChat("[AutoPlay] Returning to town: " + text + ".", MessageType.Hint);
			}
			if (FindLoot() != null || (!IsBagFull() && FindHarvestTarget() != null))
			{
				SetState("Town - collecting nearby loot");
				return false;
			}
			if (!ShouldGoToTown())
			{
				_leavingForTown = false;
				return false;
			}
			TryStartTownRun();
			return false;
		case AutoTownState.TeleportingToTown:
			if (User.InSafeZone && CurrentMapIndex == Scene.BindPointMapIndex && User.ActionQueue.Count == 0)
			{
				List<AutoTownAction> work = _townWorkActions ?? GetTownWork();
				int needsCovered;
				List<TownWorkItem> list = BuildTownPlan(CurrentMapIndex, work, GetRestockNeeds(), out needsCovered);
				TrackSkippedTownActions(work, list);
				if (list.Count == 0)
				{
					GoToSafeZoneAndStop(BuildUnresolvedTownStopReason() ?? "Teleported to town but nothing here can service the trip.");
					return true;
				}
				foreach (TownWorkItem item in list)
				{
					_townWork.Enqueue(item);
				}
				DebugLog("Town: teleport landed - starting town work.");
				_townState = AutoTownState.MovingToNPC;
				StartNextTownWork();
				return true;
			}
			if (CEnvir.Now > _townTimeout)
			{
				DebugLog("Town: teleport did not land, walking to town.");
				_townTeleportSuppressed = true;
				List<AutoTownAction> work2 = _townWorkActions ?? GetTownWork();
				_townState = AutoTownState.None;
				if (!StartTownRun(work2))
				{
					GoToSafeZoneAndStop("Could not reach town.");
				}
			}
			return true;
		case AutoTownState.Travelling:
			return false;
		case AutoTownState.MovingToNPC:
			return ProcessMovingToNPC();
		case AutoTownState.AwaitingResult:
		{
			AutoTownResult townResult = _townResult;
			if (townResult != null)
			{
				_townResult = null;
				DebugLog($"Town: {townResult.Action} result - {(townResult.Success ? "OK" : ("FAILED (" + townResult.Message + ")"))}.");
				if (!townResult.Success)
				{
					_townFailureMessages[townResult.Action] = (string.IsNullOrWhiteSpace(townResult.Message) ? "The server rejected the request." : townResult.Message);
					if (!string.IsNullOrEmpty(townResult.Message))
					{
						Scene.ReceiveChat("[AutoPlay] " + townResult.Message, MessageType.Hint);
					}
				}
				StartNextTownWork();
			}
			else if (CEnvir.Now > _townTimeout)
			{
				DebugLog($"Town: {_currentWork?.Action} timed out waiting for the server.");
				if (_currentWork != null)
				{
					_townFailureMessages[_currentWork.Action] = "Timed out waiting for the server.";
				}
				StartNextTownWork();
			}
			return true;
		}
		case AutoTownState.Settling:
			if (CEnvir.Now < _townTimeout)
			{
				return true;
			}
			FinishTownRun();
			return true;
		default:
			return false;
		}
	}

	private bool ShouldGoToTown()
	{
		return TownReason() != null;
	}

	private string? TownReason()
	{
		List<string> list = new List<string>();
		if (Settings.TownOnBagFull && IsBagFull())
		{
			list.Add("bag full");
		}
		if (Settings.TownOnNoHealthPotion && NeedHealthPotion())
		{
			list.Add("no health potions");
		}
		if (Settings.TownOnNoManaPotion && NeedManaPotion())
		{
			list.Add("no mana potions");
		}
		if (Settings.TownOnNoPoison && NeedPoisonAmmo())
		{
			list.Add("no poison");
		}
		if (Settings.TownOnNoTalisman && NeedTalismanAmulet())
		{
			list.Add("no talismans");
		}
		if (Settings.TownOnEquipmentBroken && EquipmentBroken())
		{
			list.Add("equipment broken");
		}
		if (list.Count != 0)
		{
			return string.Join(", ", list);
		}
		return null;
	}

	private List<AutoTownAction> GetTownWork()
	{
		List<AutoTownAction> list = new List<AutoTownAction>();
		if (Settings.CanAutoSell && GetSellLinks().Count > 0)
		{
			list.Add(AutoTownAction.Sell);
		}
		if (Settings.CanAutoRepair && GetRepairLinks().Count > 0)
		{
			list.Add(AutoTownAction.Repair);
		}
		if (Settings.CanAutoBuy && GetRestockNeeds().Count > 0)
		{
			list.Add(AutoTownAction.Buy);
		}
		return list;
	}

	private bool IsBagFull()
	{
		if (Scene.Inventory.Count((ClientUserItem x) => x == null) < 2)
		{
			return true;
		}
		if (User.Stats[Stat.BagWeight] > 0)
		{
			return User.BagWeight * 100 / User.Stats[Stat.BagWeight] >= 90;
		}
		return false;
	}

	private bool NeedHealthPotion()
	{
		return IsPotionDry(healthPotion: true);
	}

	private bool NeedManaPotion()
	{
		return IsPotionDry(healthPotion: false);
	}

	private static bool IsGoldItem(ItemInfo info)
	{
		return info != null && Globals.GoldInfo != null && info.Index == Globals.GoldInfo.Index;
	}

	private bool IsPotionDry(bool healthPotion)
	{
		ClientAutoPotionLink[] links = Scene.AutoPotionBox?.Links;
		if (links == null) return false;

		bool configured = false;
		foreach (ClientAutoPotionLink link in links)
		{
			if (link == null || !link.Enabled || link.LinkInfoIndex < 0) continue;

			ItemInfo info = Globals.ItemInfoList.Binding.FirstOrDefault(x => x.Index == link.LinkInfoIndex);
			if (info == null) continue;

			bool wantsHealth = link.Health > 0;
			bool wantsMana = link.Mana > 0;
			if (healthPotion && !wantsHealth) continue;
			if (!healthPotion && !wantsMana) continue;

			configured = true;
			if (CountCarried(info) > 0) return false;
		}

		return configured;
	}

	private bool NeedPoisonAmmo()
	{
		if (UsesPoison && !HasEquippedType(ItemType.Poison, EquipmentSlot.Poison))
		{
			return !HasInventoryType(ItemType.Poison);
		}
		return false;
	}

	private bool NeedTalismanAmulet()
	{
		if (UsesTalisman && !HasEquippedType(ItemType.Amulet, EquipmentSlot.Amulet))
		{
			return !HasInventoryType(ItemType.Amulet);
		}
		return false;
	}

	private bool EquipmentBroken()
	{
		ClientUserItem[] equipment = Scene.Equipment;
		foreach (ClientUserItem clientUserItem in equipment)
		{
			if (clientUserItem?.Info != null && clientUserItem.Info.Durability > 0 && clientUserItem.MaxDurability > 0 && clientUserItem.CurrentDurability == 0)
			{
				return true;
			}
		}
		return false;
	}

	private bool HasInventoryType(ItemType type)
	{
		ClientUserItem[] inventory = Scene.Inventory;
		foreach (ClientUserItem clientUserItem in inventory)
		{
			if (clientUserItem?.Info != null && clientUserItem.Info.ItemType == type)
			{
				return true;
			}
		}
		return false;
	}

	private bool HasEquippedType(ItemType type, EquipmentSlot slot)
	{
		ClientUserItem clientUserItem = Scene.Equipment[(int)slot];
		if (clientUserItem?.Info != null)
		{
			return clientUserItem.Info.ItemType == type;
		}
		return false;
	}

	private List<(ItemInfo Info, long Needed)> GetRestockNeeds()
	{
		List<(ItemInfo, long)> list = new List<(ItemInfo, long)>();
		foreach (ClientQuickBuyTarget target in Scene.QuickBuyTargets)
		{
			if (target.TargetAmount <= 0)
			{
				continue;
			}
			ItemInfo info = Globals.ItemInfoList.Binding.FirstOrDefault((ItemInfo x) => x.Index == target.ItemInfoIndex);
			if (info != null && !list.Any<(ItemInfo, long)>(((ItemInfo Info, long Needed) x) => x.Info == info))
			{
				long num = target.TargetAmount - CountCarried(info);
				if (num > 0)
				{
					list.Add((info, num));
				}
			}
		}
		return list;
	}

	private long CountCarried(ItemInfo info)
	{
		long num = 0L;
		ClientUserItem[] inventory = Scene.Inventory;
		foreach (ClientUserItem clientUserItem in inventory)
		{
			if (clientUserItem?.Info == info)
			{
				num += clientUserItem.Count;
			}
		}
		if (Scene.Companion?.InventoryArray != null)
		{
			inventory = Scene.Companion.InventoryArray;
			foreach (ClientUserItem clientUserItem2 in inventory)
			{
				if (clientUserItem2?.Info == info)
				{
					num += clientUserItem2.Count;
				}
			}
		}
		return num;
	}

	private List<CellLinkInfo> GetSellLinks()
	{
		List<CellLinkInfo> list = new List<CellLinkInfo>();
		for (int i = 0; i < Scene.Inventory.Length; i++)
		{
			ClientUserItem clientUserItem = Scene.Inventory[i];
			if (clientUserItem != null && ShouldTownSell(clientUserItem))
			{
				list.Add(new CellLinkInfo
				{
					GridType = GridType.Inventory,
					Slot = i,
					Count = clientUserItem.Count
				});
			}
		}
		return list;
	}

	private bool ShouldTownSell(ClientUserItem item)
	{
		if (!Settings.CanAutoSell)
		{
			return false;
		}
		return NPCSellHelper.ShouldAutoSell(item);
	}

	private List<CellLinkInfo> GetRepairLinks()
	{
		List<CellLinkInfo> list = new List<CellLinkInfo>();
		for (int i = 0; i < Scene.Equipment.Length; i++)
		{
			ClientUserItem clientUserItem = Scene.Equipment[i];
			if (clientUserItem?.Info != null && clientUserItem.Info.Durability > 0 && clientUserItem.MaxDurability > 0 && clientUserItem.CurrentDurability < clientUserItem.MaxDurability)
			{
				list.Add(new CellLinkInfo
				{
					GridType = GridType.Equipment,
					Slot = i,
					Count = 1L
				});
			}
		}
		return list;
	}

	private void BeginTownDiagnostics(List<(ItemInfo Info, long Needed)> restockNeeds)
	{
		_townFailureMessages.Clear();
		_townSkippedActions.Clear();
		_townInitialRestockNeeds.Clear();
		_townInitialRestockNeeds.AddRange(restockNeeds);
	}

	private void TrackSkippedTownActions(List<AutoTownAction> work, List<TownWorkItem> plan)
	{
		foreach (AutoTownAction item in work.Where((AutoTownAction action) => plan.All((TownWorkItem x) => x.Action != action)))
		{
			_townSkippedActions.Add(item);
		}
	}

	private string? BuildUnresolvedTownStopReason()
	{
		List<string> list = new List<string>();
		if (Settings.TownOnBagFull && IsBagFull())
		{
			list.Add(DescribeBagBlocker());
		}
		if (Settings.TownOnNoHealthPotion && NeedHealthPotion())
		{
			list.Add(DescribeAutoPotionBlocker(healthPotion: true));
		}
		if (Settings.TownOnNoManaPotion && NeedManaPotion())
		{
			list.Add(DescribeAutoPotionBlocker(healthPotion: false));
		}
		if (Settings.TownOnNoPoison && NeedPoisonAmmo())
		{
			list.Add(DescribeKnownRestockBlocker("No poison remains", _lastPoisonInfo, "poison"));
		}
		if (Settings.TownOnNoTalisman && NeedTalismanAmulet())
		{
			list.Add(DescribeKnownRestockBlocker("No talismans remain", _lastAmuletInfo, "talisman"));
		}
		if (Settings.TownOnEquipmentBroken && EquipmentBroken())
		{
			list.Add(DescribeRepairBlocker());
		}
		if (list.Count != 0)
		{
			return string.Join(" ", list);
		}
		return null;
	}

	private string DescribeBagBlocker()
	{
		if (!Settings.CanAutoSell)
		{
			return "Bag still full/overweight; Auto Sell is disabled.";
		}
		if (!WasTownActionWanted(AutoTownAction.Sell) && GetSellLinks().Count == 0)
		{
			return "Bag still full/overweight; no eligible items are marked for Auto Sell.";
		}
		if (IsTownActionUnavailable(AutoTownAction.Sell))
		{
			return "Bag still full/overweight; home town has no sell merchant.";
		}
		if (TryGetTownFailure(AutoTownAction.Sell, out string message))
		{
			return "Bag still full/overweight; Auto Sell failed: " + EndSentence(message);
		}
		return "Bag still full/overweight; Auto Sell did not free enough bag space/weight.";
	}

	private string DescribeRepairBlocker()
	{
		if (!Settings.CanAutoRepair)
		{
			return "Equipment is still broken; Auto Repair is disabled.";
		}
		if (IsTownActionUnavailable(AutoTownAction.Repair))
		{
			return "Equipment is still broken; home town has no repair NPC.";
		}
		if (TryGetTownFailure(AutoTownAction.Repair, out string message))
		{
			return "Equipment is still broken; Auto Repair failed: " + EndSentence(message);
		}
		return "Equipment is still broken; Auto Repair failed or you do not have enough gold.";
	}

	private string DescribeAutoPotionBlocker(bool healthPotion)
	{
		string text = (healthPotion ? "No health potions remain" : "No mana potions remain");
		if (!Settings.CanAutoBuy)
		{
			return text + "; Auto Buy is disabled.";
		}
		List<(ItemInfo, long)> restockNeeds = GetRestockNeeds();
		if (restockNeeds.Count == 0)
		{
			return text + "; no Quick Buy target is configured or short for Auto Buy.";
		}
		return DescribeBuyRestockBlocker(text, restockNeeds.Select<(ItemInfo, long), ItemInfo>(((ItemInfo Info, long Needed) x) => x.Item1).ToList());
	}

	private string DescribeKnownRestockBlocker(string condition, ItemInfo? item, string itemKind)
	{
		if (!Settings.CanAutoBuy)
		{
			return condition + "; Auto Buy is disabled.";
		}
		if (item == null)
		{
			return condition + "; AutoPlay does not know which " + itemKind + " to restock yet.";
		}
		if (Scene.GetQuickBuyTargetAmount(item) <= 0)
		{
			return condition + "; no Quick Buy target is configured for " + item.ItemName + ".";
		}
		return DescribeBuyRestockBlocker(condition, new List<ItemInfo> { item });
	}

	private string DescribeBuyRestockBlocker(string condition, List<ItemInfo> items)
	{
		List<ItemInfo> list = DistinctItems(items);
		List<ItemInfo> list2 = list.Where((ItemInfo info) => !HomeTownStocks(info)).ToList();
		if (list2.Count > 0)
		{
			return condition + "; home town does not stock " + FormatItemList(list2) + " for Auto Buy.";
		}
		if (TryGetTownFailure(AutoTownAction.Buy, out string message))
		{
			return condition + "; Auto Buy failed: " + EndSentence(message);
		}
		List<ItemInfo> persistingRestockItems = GetPersistingRestockItems(list);
		string text = FormatItemList((persistingRestockItems.Count > 0) ? persistingRestockItems : list);
		return condition + "; Auto Buy did not restore " + text + ". You may not have enough gold, bag space, or weight.";
	}

	private List<ItemInfo> GetPersistingRestockItems(List<ItemInfo> candidates)
	{
		List<(ItemInfo, long)> restockNeeds = GetRestockNeeds();
		List<ItemInfo> list = new List<ItemInfo>();
		foreach (ItemInfo info in candidates)
		{
			if (!list.Contains(info) && restockNeeds.Any<(ItemInfo, long)>(((ItemInfo Info, long Needed) x) => x.Info == info) && (_townInitialRestockNeeds.Count <= 0 || _townInitialRestockNeeds.Any<(ItemInfo, long)>(((ItemInfo Info, long Needed) x) => x.Info == info)))
			{
				list.Add(info);
			}
		}
		return list;
	}

	private bool WasTownActionWanted(AutoTownAction action)
	{
		return _townWorkActions?.Contains(action) ?? false;
	}

	private bool IsTownActionUnavailable(AutoTownAction action)
	{
		if (_townSkippedActions.Contains(action))
		{
			return true;
		}
		if (action <= AutoTownAction.Repair)
		{
			return FindTownNPC(HomeTownMapIndex(), action) == null;
		}
		return false;
	}

	private bool TryGetTownFailure(AutoTownAction action, out string message)
	{
		if (_townFailureMessages.TryGetValue(action, out message) && !string.IsNullOrWhiteSpace(message))
		{
			return true;
		}
		message = string.Empty;
		return false;
	}

	private bool HomeTownStocks(ItemInfo info)
	{
		return TownNPCHelper.StocksItem(HomeTownMapIndex(), info);
	}

	private int HomeTownMapIndex()
	{
		if (Scene.BindPointMapIndex < 0)
		{
			return CurrentMapIndex;
		}
		return Scene.BindPointMapIndex;
	}

	private static List<ItemInfo> DistinctItems(IEnumerable<ItemInfo> items)
	{
		List<ItemInfo> list = new List<ItemInfo>();
		foreach (ItemInfo item in items)
		{
			if (item != null && !list.Contains(item))
			{
				list.Add(item);
			}
		}
		return list;
	}

	private static string FormatItemList(IEnumerable<ItemInfo> items)
	{
		List<string> list = (from x in items
			where x != null
			select x.ItemName into x
			where !string.IsNullOrWhiteSpace(x)
			select x).Distinct().ToList();
		return list.Count switch
		{
			0 => "the needed item", 
			1 => "'" + list[0] + "'", 
			2 => $"'{list[0]}' and '{list[1]}'", 
			3 => $"'{list[0]}', '{list[1]}', and '{list[2]}'", 
			_ => $"'{list[0]}', '{list[1]}', and {list.Count - 2} more", 
		};
	}

	private static string EndSentence(string message)
	{
		message = message.Trim();
		if (!message.EndsWith(".", StringComparison.Ordinal) && !message.EndsWith("!", StringComparison.Ordinal) && !message.EndsWith("?", StringComparison.Ordinal))
		{
			return message + ".";
		}
		return message;
	}

	private bool TryStartTownRun()
	{
		if (!ShouldGoToTown())
		{
			return false;
		}
		List<AutoTownAction> townWork = GetTownWork();
		if (townWork.Count == 0)
		{
			GoToSafeZoneAndStop(BuildUnresolvedTownStopReason() ?? "Cannot resolve the town condition (nothing to sell/repair/buy).");
			return true;
		}
		if (!StartTownRun(townWork))
		{
			GoToSafeZoneAndStop(BuildUnresolvedTownStopReason() ?? "The home town cannot service any of the needed work.");
		}
		return true;
	}

	private bool TryStartLocalTownRun()
	{
		List<AutoTownAction> townWork = GetTownWork();
		if (townWork.Count > 0)
		{
			return StartTownRun(townWork);
		}
		return false;
	}

	private bool StartTownRun(List<AutoTownAction> work)
	{
		_townWorkActions = work;
		int mapIndex = ((Scene.BindPointMapIndex >= 0) ? Scene.BindPointMapIndex : CurrentMapIndex);
		List<(ItemInfo, long)> restockNeeds = GetRestockNeeds();
		BeginTownDiagnostics(restockNeeds);
		if (DebugMessages)
		{
			DebugLog("Town work wanted: " + string.Join(", ", work) + ".");
			if (restockNeeds.Count > 0)
			{
				DebugLog("Restock needs: " + string.Join(", ", restockNeeds.Select<(ItemInfo, long), string>(((ItemInfo Info, long Needed) x) => $"{x.Info.ItemName} x{x.Needed}")) + ".");
			}
		}
		int needsCovered;
		List<TownWorkItem> list = BuildTownPlan(mapIndex, work, restockNeeds, out needsCovered);
		TrackSkippedTownActions(work, list);
		if (list.Count == 0)
		{
			return false;
		}
		List<AutoTownAction> list2 = _townSkippedActions.Where(work.Contains).ToList();
		if (list2.Count > 0)
		{
			Scene.ReceiveChat("[AutoPlay] Home town has no NPC for: " + string.Join(", ", list2) + " - skipping.", MessageType.Hint);
		}
		TownNPCRef nPC = list[0].NPC;
		Point? nPCLocation = GetNPCLocation(nPC);
		if (!nPCLocation.HasValue)
		{
			return false;
		}
		Step step = new Step(nPC.Map.Index, nPCLocation.Value);
		if (!_townTeleportSuppressed && ShouldTownTeleport(step) && Scene.BindPointMapIndex >= 0)
		{
			int num = FindTownTeleportScrollSlot();
			if (num >= 0)
			{
				if (!_huntAnchor.HasValue)
				{
					CaptureHuntAnchor();
				}
				Intention = AutoIntention.None;
				_townPoint = null;
				_townState = AutoTownState.TeleportingToTown;
				_townTimeout = CEnvir.Now.AddMilliseconds(10000.0);
				DebugLog("Town: far from town - using a Town Teleport scroll.");
				UseTownTeleportScroll(num);
				return true;
			}
		}
		foreach (TownWorkItem item in list)
		{
			_townWork.Enqueue(item);
		}
		if (!_huntAnchor.HasValue)
		{
			CaptureHuntAnchor();
		}
		_townPoint = step;
		DebugLog($"Town plan ({nPC.Map.Description}): {string.Join(" -> ", list.Select((TownWorkItem x) => x.Action))}.");
		if (CurrentMapIndex == step.MapIndex && Functions.Distance(User.CurrentLocation, step.Location) <= TownNPCRange)
		{
			DebugLog("Town: already in NPC range - starting town work.");
			Intention = AutoIntention.None;
			_townState = AutoTownState.MovingToNPC;
			StartNextTownWork();
			return true;
		}
		_townState = AutoTownState.Travelling;
		BeginTravel(_townPoint.Value, TravelPurpose.TownRun);
		return true;
	}

	private bool ShouldTownTeleport(Step townDest)
	{
		MapInfo mapInfo = Map.MapInfo;
		if (mapInfo == null || !mapInfo.AllowTT)
		{
			return false;
		}
		if (townDest.MapIndex != CurrentMapIndex)
		{
			return true;
		}
		return Functions.Distance(User.CurrentLocation, townDest.Location) > 50;
	}

	private int FindTownTeleportScrollSlot()
	{
		for (int i = 0; i < Scene.Inventory.Length; i++)
		{
			ClientUserItem clientUserItem = Scene.Inventory[i];
			if (clientUserItem?.Info != null && clientUserItem.Info.ItemType == ItemType.Consumable && clientUserItem.Info.Shape == 2)
			{
				return i;
			}
		}
		return -1;
	}

	private void UseTownTeleportScroll(int slot)
	{
		CEnvir.Enqueue(new ItemUse
		{
			Link = new CellLinkInfo
			{
				GridType = GridType.Inventory,
				Slot = slot,
				Count = 1L
			}
		});
		Scene.UseItemTime = CEnvir.Now.AddMilliseconds(1000.0);
	}

	private List<TownWorkItem> BuildTownPlan(int mapIndex, List<AutoTownAction> work, List<(ItemInfo Info, long Needed)> restockNeeds, out int needsCovered)
	{
		List<TownWorkItem> list = new List<TownWorkItem>();
		needsCovered = 0;
		foreach (AutoTownAction item in work)
		{
			if (item == AutoTownAction.Sell)
			{
				List<ItemType> remainingTypes = GetSellLinks()
					.Select(link => Scene.Inventory[link.Slot]?.Info?.ItemType)
					.Where(t => t.HasValue)
					.Select(t => t.Value)
					.Distinct()
					.ToList();

				while (remainingTypes.Count > 0)
				{
					TownNPCRef sellNpc = FindTownNPC(mapIndex, AutoTownAction.Sell, remainingTypes);
					if (sellNpc == null) break;

					list.Add(new TownWorkItem
					{
						Action = AutoTownAction.Sell,
						NPC = sellNpc
					});

					List<ItemType> covered = TownNPCHelper.GetAcceptedSellTypes(sellNpc.NPCInfo, remainingTypes);
					if (covered.Count == 0) break;

					foreach (ItemType type in covered)
						remainingTypes.Remove(type);
				}
				continue;
			}

			if (item != AutoTownAction.Buy)
			{
				TownNPCRef TownNPCRef = FindTownNPC(mapIndex, item);
				if (TownNPCRef != null)
				{
					list.Add(new TownWorkItem
					{
						Action = item,
						NPC = TownNPCRef
					});
				}
				continue;
			}
			List<ItemInfo> list2 = restockNeeds.Select<(ItemInfo, long), ItemInfo>(((ItemInfo Info, long Needed) x) => x.Item1).ToList();
			while (list2.Count > 0)
			{
				TownNPCRef bestNpc = null;
				List<ItemInfo> bestItems = null;
				int bestScore = int.MinValue;
				foreach (TownNPCRef respawn in TownNPCHelper.Enumerate(mapIndex))
				{
					List<ItemInfo> matches = list2.Where(info => TownNPCHelper.FindGood(respawn.NPCInfo, info) != null).ToList();
					if (matches.Count == 0) continue;

					int score = matches.Count * 1000 - NPCDistance(respawn);
					if (score > bestScore)
					{
						bestScore = score;
						bestNpc = respawn;
						bestItems = matches;
					}
				}
				if (bestNpc == null)
				{
					break;
				}
				list.Add(new TownWorkItem
				{
					Action = AutoTownAction.Buy,
					NPC = bestNpc
				});
				needsCovered += bestItems.Count;
				foreach (ItemInfo item2 in bestItems)
				{
					list2.Remove(item2);
				}
			}
		}
		return list;
	}

	private TownNPCRef FindTownNPC(int mapIndex, AutoTownAction action, IEnumerable<ItemType> preferredTypes = null)
	{
		return TownNPCHelper.FindTownNPC(mapIndex, action, preferredTypes);
	}

	private int NPCDistance(TownNPCRef respawn)
	{
		if (respawn.Map?.Index != CurrentMapIndex)
		{
			return 0;
		}
		Point? nPCLocation = GetNPCLocation(respawn);
		if (nPCLocation.HasValue)
		{
			return Functions.Distance(User.CurrentLocation, nPCLocation.Value);
		}
		return 0;
	}

	private static NPCGood FindGood(NPCInfo npc, ItemInfo info)
	{
		return TownNPCHelper.FindGood(npc, info);
	}

	private static Point? GetNPCLocation(TownNPCRef respawn)
	{
		if (respawn?.Map != null)
		{
			return new Point(respawn.X, respawn.Y);
		}
		return null;
	}

	private void OnTownArrived()
	{
		if (_townWork.Count == 0)
		{
			FinishTownRun();
			return;
		}
		_townState = AutoTownState.MovingToNPC;
		StartNextTownWork();
	}

	private void StartNextTownWork()
	{
		ClearPath();
		if (_townWork.Count == 0)
		{
			_currentWork = null;
			_townState = AutoTownState.Settling;
			_townTimeout = CEnvir.Now.AddMilliseconds(2000.0);
			SetState("Town - finishing up");
		}
		else
		{
			_currentWork = _townWork.Dequeue();
			_townState = AutoTownState.MovingToNPC;
		}
	}

	private bool ProcessMovingToNPC()
	{
		TownWorkItem currentWork = _currentWork;
		if (currentWork == null)
		{
			StartNextTownWork();
			return true;
		}
		SetState($"Town - {currentWork.Action}");
		if (!NPCObject.NPCs.TryGetValue(currentWork.NPC.NPCInfo, out NPCObject value))
		{
			Point? nPCLocation = GetNPCLocation(currentWork.NPC);
			if (!nPCLocation.HasValue || (!FollowPath() && !StartPath(nPCLocation.Value, 2)))
			{
				string value2 = ((!nPCLocation.HasValue) ? ("No location is known for NPC '" + currentWork.NPC.NPCInfo?.NPCName + "'.") : $"No path to NPC '{currentWork.NPC.NPCInfo?.NPCName}' at {nPCLocation.Value.X}, {nPCLocation.Value.Y}.");
				_townFailureMessages[currentWork.Action] = value2;
				DebugLog($"Town: {currentWork.Action} failed - {value2}");
				StartNextTownWork();
			}
			return true;
		}
		if (Functions.Distance(User.CurrentLocation, value.CurrentLocation) > TownNPCRange)
		{
			if (!FollowPath() && !StartPath(value.CurrentLocation, TownNPCRange))
			{
				string value3 = $"No path to {value.Name} within range {TownNPCRange}.";
				_townFailureMessages[currentWork.Action] = value3;
				DebugLog($"Town: {currentWork.Action} failed - {value3}");
				StartNextTownWork();
			}
			return true;
		}
		if (CEnvir.Now < _nextTownSend)
		{
			SetState($"Town - {currentWork.Action} (waiting)");
			return true;
		}
		SendTownWork(currentWork, value);
		return true;
	}

	private void SendTownWork(TownWorkItem work, NPCObject npcObject)
	{
		ClearPath();
		switch (work.Action)
		{
		case AutoTownAction.Sell:
		{
			List<CellLinkInfo> sellLinks = GetSellLinks()
				.Where(link =>
				{
					ClientUserItem inv = Scene.Inventory[link.Slot];
					ItemType? type = inv?.Info?.ItemType;
					return type.HasValue && TownNPCHelper.AcceptsSellType(work.NPC.NPCInfo, type.Value);
				})
				.ToList();
			if (sellLinks.Count == 0)
			{
				_townFailureMessages[AutoTownAction.Sell] = "No eligible items for this sell merchant.";
				DebugLog("Town: nothing to sell at this NPC, skipping.");
				StartNextTownWork();
				return;
			}
			DebugLog($"Town: selling {sellLinks.Count} item(s) at {npcObject.Name}.");
			CEnvir.Enqueue(new AutoSell
			{
				ObjectID = npcObject.ObjectID,
				Links = sellLinks
			});
			break;
		}
		case AutoTownAction.Repair:
		{
			List<CellLinkInfo> repairLinks = GetRepairLinks();
			if (repairLinks.Count == 0)
			{
				_townFailureMessages[AutoTownAction.Repair] = "No damaged equipped items are eligible for Auto Repair.";
				DebugLog("Town: nothing to repair, skipping.");
				StartNextTownWork();
				return;
			}
			DebugLog($"Town: repairing {repairLinks.Count} item(s) at {npcObject.Name} (special: {Settings.ShouldSpecialRepair}).");
			CEnvir.Enqueue(new AutoRepair
			{
				ObjectID = npcObject.ObjectID,
				Links = repairLinks,
				Special = Settings.ShouldSpecialRepair
			});
			break;
		}
		case AutoTownAction.Buy:
		{
			List<AutoBuyItem> list = new List<AutoBuyItem>();
			long num = MapObject.User?.Gold?.Amount ?? 0;
			List<(ItemInfo, long)> restockNeeds = GetRestockNeeds();
			foreach (var item3 in restockNeeds)
			{
				ItemInfo item = item3.Item1;
				long item2 = item3.Item2;
				NPCGood nPCGood = FindGood(work.NPC.NPCInfo, item);
				if (nPCGood?.Item != null)
				{
					long num2 = item2;
					if (nPCGood.Cost > 0)
					{
						num2 = Math.Min(num2, num / nPCGood.Cost);
					}
					if (num2 > 0)
					{
						num -= num2 * nPCGood.Cost;
						list.Add(new AutoBuyItem
						{
							Index = nPCGood.Index,
							Amount = num2
						});
					}
				}
			}
			if (list.Count == 0)
			{
				List<ItemInfo> list2 = (from x in restockNeeds
					where FindGood(work.NPC.NPCInfo, x.Item1) != null
					select x.Item1).ToList();
				if (list2.Count > 0)
				{
					_townFailureMessages[AutoTownAction.Buy] = "Not enough gold to buy " + FormatItemList(list2) + ".";
				}
				else if (restockNeeds.Count > 0)
				{
					_townFailureMessages[AutoTownAction.Buy] = "This vendor does not stock the remaining restock items.";
				}
				DebugLog("Town: nothing to buy at " + npcObject.Name + ", skipping.");
				StartNextTownWork();
				return;
			}
			DebugLog($"Town: buying {list.Count} item line(s) at {npcObject.Name}.");
			CEnvir.Enqueue(new AutoBuy
			{
				ObjectID = npcObject.ObjectID,
				Purchases = list
			});
			break;
		}
		}
		_nextTownSend = CEnvir.Now.AddMilliseconds(750.0);
		_townState = AutoTownState.AwaitingResult;
		_townTimeout = CEnvir.Now.AddMilliseconds(5000.0);
	}

	private void FinishTownRun()
	{
		string text = BuildUnresolvedTownStopReason();
		if (text != null)
		{
			GoToSafeZoneAndStop(text);
			return;
		}
		ResetTown();
		if (_huntAnchor.HasValue)
		{
			Scene.ReceiveChat("[AutoPlay] Town run complete - returning to hunt.", MessageType.Hint);
			BeginTravel(_huntAnchor.Value, TravelPurpose.ReturnToHunt);
		}
		else
		{
			Stop("Town run complete.");
		}
	}

	private void GoToSafeZoneAndStop(string reason)
	{
		ResetTown();
		if (User.InSafeZone)
		{
			Stop(reason + " Stopped in safe zone.");
			return;
		}
		if (Scene.BindPointMapIndex < 0)
		{
			Stop(reason + " Stopped (no bind point known).");
			return;
		}
		_safeStopReason = reason;
		Scene.ReceiveChat("[AutoPlay] " + reason + " Heading to a safe zone.", MessageType.Hint);
		BeginTravel(new Step(Scene.BindPointMapIndex, Scene.BindPointLocation), TravelPurpose.SafeZoneStop);
	}

	static AutoPlayer()
	{
		Dictionary<MirClass, List<RotationEntry>> dictionary = new Dictionary<MirClass, List<RotationEntry>>();
		int num = 10;
		List<RotationEntry> list = new List<RotationEntry>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<RotationEntry> span = CollectionsMarshal.AsSpan(list);
		span[0] = new RotationEntry
		{
			Magic = MagicType.Defiance,
			Kind = EntryKind.Buff,
			Buff = BuffType.Defiance
		};
		span[1] = new RotationEntry
		{
			Magic = MagicType.Might,
			Kind = EntryKind.Buff,
			Buff = BuffType.Might
		};
		span[2] = new RotationEntry
		{
			Magic = MagicType.ReflectDamage,
			Kind = EntryKind.Buff,
			Buff = BuffType.ReflectDamage
		};
		span[3] = new RotationEntry
		{
			Magic = MagicType.Endurance,
			Kind = EntryKind.Buff,
			Buff = BuffType.Endurance
		};
		span[4] = new RotationEntry
		{
			Magic = MagicType.HalfMoon,
			Kind = EntryKind.Stance,
			MinTargets = 2
		};
		span[5] = new RotationEntry
		{
			Magic = MagicType.DestructiveSurge,
			Kind = EntryKind.Stance,
			MinTargets = 3
		};
		span[6] = new RotationEntry
		{
			Magic = MagicType.BladeStorm,
			Kind = EntryKind.Empower
		};
		span[7] = new RotationEntry
		{
			Magic = MagicType.DragonRise,
			Kind = EntryKind.Empower
		};
		span[8] = new RotationEntry
		{
			Magic = MagicType.FlamingSword,
			Kind = EntryKind.Empower
		};
		span[9] = new RotationEntry
		{
			Magic = MagicType.SeismicSlam,
			Kind = EntryKind.AoE,
			MinTargets = 3,
			Priority = 100
		};
		dictionary[MirClass.Warrior] = list;
		num = 24;
		List<RotationEntry> list2 = new List<RotationEntry>(num);
		CollectionsMarshal.SetCount(list2, num);
		Span<RotationEntry> span2 = CollectionsMarshal.AsSpan(list2);
		span2[0] = new RotationEntry
		{
			Magic = MagicType.MagicShield,
			Kind = EntryKind.Buff,
			Buff = BuffType.MagicShield
		};
		span2[1] = new RotationEntry
		{
			Magic = MagicType.Renounce,
			Kind = EntryKind.Buff,
			Buff = BuffType.Renounce
		};
		span2[2] = new RotationEntry
		{
			Magic = MagicType.Asteroid,
			Kind = EntryKind.AoE,
			Element = Element.Fire,
			MinTargets = 3,
			Priority = 260
		};
		span2[3] = new RotationEntry
		{
			Magic = MagicType.Tempest,
			Kind = EntryKind.AoE,
			Element = Element.Wind,
			MinTargets = 3,
			Priority = 250
		};
		span2[4] = new RotationEntry
		{
			Magic = MagicType.MeteorShower,
			Kind = EntryKind.AoE,
			Element = Element.Fire,
			MinTargets = 3,
			Priority = 240
		};
		span2[5] = new RotationEntry
		{
			Magic = MagicType.ChainLightning,
			Kind = EntryKind.AoE,
			Element = Element.Lightning,
			MinTargets = 3,
			Priority = 230
		};
		span2[6] = new RotationEntry
		{
			Magic = MagicType.DragonTornado,
			Kind = EntryKind.AoE,
			Element = Element.Wind,
			MinTargets = 3,
			Priority = 220
		};
		span2[7] = new RotationEntry
		{
			Magic = MagicType.IceStorm,
			Kind = EntryKind.AoE,
			Element = Element.Ice,
			MinTargets = 3,
			Priority = 210
		};
		span2[8] = new RotationEntry
		{
			Magic = MagicType.LightningWave,
			Kind = EntryKind.AoE,
			Element = Element.Lightning,
			MinTargets = 3,
			Priority = 200
		};
		span2[9] = new RotationEntry
		{
			Magic = MagicType.FireStorm,
			Kind = EntryKind.AoE,
			Element = Element.Fire,
			MinTargets = 3,
			Priority = 190
		};
		span2[10] = new RotationEntry
		{
			Magic = MagicType.GreaterFrozenEarth,
			Kind = EntryKind.AoE,
			Element = Element.Ice,
			MinTargets = 3,
			Priority = 180
		};
		span2[11] = new RotationEntry
		{
			Magic = MagicType.LightningBeam,
			Kind = EntryKind.AoE,
			Element = Element.Lightning,
			MinTargets = 2,
			Priority = 170
		};
		span2[12] = new RotationEntry
		{
			Magic = MagicType.FrozenEarth,
			Kind = EntryKind.AoE,
			Element = Element.Ice,
			MinTargets = 2,
			Priority = 160
		};
		span2[13] = new RotationEntry
		{
			Magic = MagicType.BlowEarth,
			Kind = EntryKind.AoE,
			Element = Element.Wind,
			MinTargets = 2,
			Priority = 150
		};
		span2[14] = new RotationEntry
		{
			Magic = MagicType.ScortchedEarth,
			Kind = EntryKind.AoE,
			Element = Element.Fire,
			MinTargets = 2,
			Priority = 140
		};
		span2[15] = new RotationEntry
		{
			Magic = MagicType.ExpelUndead,
			Kind = EntryKind.Single,
			Element = Element.Holy,
			Priority = 130,
			RequireUndead = true,
			Condition = (MonsterObject t, UserObject u) => !t.MonsterInfo.IsBoss && t.MonsterInfo.Level < 70 && t.MonsterInfo.Level < u.Level - 1
		};
		span2[16] = new RotationEntry
		{
			Magic = MagicType.AdamantineFireBall,
			Kind = EntryKind.Single,
			Element = Element.Fire,
			Priority = 120
		};
		span2[17] = new RotationEntry
		{
			Magic = MagicType.ThunderBolt,
			Kind = EntryKind.Single,
			Element = Element.Lightning,
			Priority = 115
		};
		span2[18] = new RotationEntry
		{
			Magic = MagicType.IceBlades,
			Kind = EntryKind.Single,
			Element = Element.Ice,
			Priority = 110
		};
		span2[19] = new RotationEntry
		{
			Magic = MagicType.Cyclone,
			Kind = EntryKind.Single,
			Element = Element.Wind,
			Priority = 105
		};
		span2[20] = new RotationEntry
		{
			Magic = MagicType.FireBall,
			Kind = EntryKind.Single,
			Element = Element.Fire,
			Priority = 100
		};
		span2[21] = new RotationEntry
		{
			Magic = MagicType.LightningBall,
			Kind = EntryKind.Single,
			Element = Element.Lightning,
			Priority = 95
		};
		span2[22] = new RotationEntry
		{
			Magic = MagicType.IceBolt,
			Kind = EntryKind.Single,
			Element = Element.Ice,
			Priority = 90
		};
		span2[23] = new RotationEntry
		{
			Magic = MagicType.GustBlast,
			Kind = EntryKind.Single,
			Element = Element.Wind,
			Priority = 85
		};
		dictionary[MirClass.Wizard] = list2;
		num = 15;
		List<RotationEntry> list3 = new List<RotationEntry>(num);
		CollectionsMarshal.SetCount(list3, num);
		Span<RotationEntry> span3 = CollectionsMarshal.AsSpan(list3);
		span3[0] = new RotationEntry
		{
			Magic = MagicType.MagicResistance,
			Kind = EntryKind.Buff,
			Buff = BuffType.MagicResistance
		};
		span3[1] = new RotationEntry
		{
			Magic = MagicType.Resilience,
			Kind = EntryKind.Buff,
			Buff = BuffType.Resilience
		};
		span3[2] = new RotationEntry
		{
			Magic = MagicType.BloodLust,
			Kind = EntryKind.Buff,
			Buff = BuffType.BloodLust
		};
		span3[3] = new RotationEntry
		{
			Magic = MagicType.CelestialLight,
			Kind = EntryKind.Buff,
			Buff = BuffType.CelestialLight
		};
		span3[4] = new RotationEntry
		{
			Magic = MagicType.StrengthOfFaith,
			Kind = EntryKind.Buff,
			Buff = BuffType.StrengthOfFaith
		};
		span3[5] = new RotationEntry
		{
			Magic = MagicType.Heal,
			Kind = EntryKind.HealSelf
		};
		span3[6] = new RotationEntry
		{
			Magic = MagicType.SummonDemonicCreature,
			Kind = EntryKind.Summon,
			Priority = 40
		};
		span3[7] = new RotationEntry
		{
			Magic = MagicType.SummonJinSkeleton,
			Kind = EntryKind.Summon,
			Priority = 30
		};
		span3[8] = new RotationEntry
		{
			Magic = MagicType.SummonShinsu,
			Kind = EntryKind.Summon,
			Priority = 20
		};
		span3[9] = new RotationEntry
		{
			Magic = MagicType.SummonSkeleton,
			Kind = EntryKind.Summon,
			Priority = 10
		};
		span3[10] = new RotationEntry
		{
			Magic = MagicType.PoisonDust,
			Kind = EntryKind.PoisonUpkeep
		};
		span3[11] = new RotationEntry
		{
			Magic = MagicType.ImprovedExplosiveTalisman,
			Kind = EntryKind.Single,
			Element = Element.Dark,
			Priority = 120
		};
		span3[12] = new RotationEntry
		{
			Magic = MagicType.GreaterEvilSlayer,
			Kind = EntryKind.Single,
			Element = Element.Holy,
			Priority = 115,
			UndeadBonus = true
		};
		span3[13] = new RotationEntry
		{
			Magic = MagicType.ExplosiveTalisman,
			Kind = EntryKind.Single,
			Element = Element.Dark,
			Priority = 100
		};
		span3[14] = new RotationEntry
		{
			Magic = MagicType.EvilSlayer,
			Kind = EntryKind.Single,
			Element = Element.Holy,
			Priority = 95,
			UndeadBonus = true
		};
		dictionary[MirClass.Taoist] = list3;
		num = 9;
		List<RotationEntry> list4 = new List<RotationEntry>(num);
		CollectionsMarshal.SetCount(list4, num);
		Span<RotationEntry> span4 = CollectionsMarshal.AsSpan(list4);
		span4[0] = new RotationEntry
		{
			Magic = MagicType.Evasion,
			Kind = EntryKind.Buff,
			Buff = BuffType.Evasion
		};
		span4[1] = new RotationEntry
		{
			Magic = MagicType.RagingWind,
			Kind = EntryKind.Buff,
			Buff = BuffType.RagingWind
		};
		span4[2] = new RotationEntry
		{
			Magic = MagicType.FlameSplash,
			Kind = EntryKind.Stance,
			MinTargets = 2
		};
		span4[3] = new RotationEntry
		{
			Magic = MagicType.SweetBrier,
			Kind = EntryKind.Empower,
			Condition = (MonsterObject t, UserObject u) => u.Buffs.Any((ClientBuffInfo x) => x.Type == BuffType.RedLotus)
		};
		span4[4] = new RotationEntry
		{
			Magic = MagicType.RedLotus,
			Kind = EntryKind.Empower,
			Condition = (MonsterObject t, UserObject u) => u.Buffs.Any((ClientBuffInfo x) => x.Type == BuffType.WhiteLotus)
		};
		span4[5] = new RotationEntry
		{
			Magic = MagicType.WhiteLotus,
			Kind = EntryKind.Empower,
			Condition = (MonsterObject t, UserObject u) => u.Buffs.Any((ClientBuffInfo x) => x.Type == BuffType.FullBloom)
		};
		span4[6] = new RotationEntry
		{
			Magic = MagicType.FullBloom,
			Kind = EntryKind.Empower,
			Condition = (MonsterObject t, UserObject u) => !u.Buffs.Any(delegate(ClientBuffInfo x)
			{
				BuffType type = x.Type;
				return (uint)(type - 401) <= 2u;
			})
		};
		span4[7] = new RotationEntry
		{
			Magic = MagicType.PoisonousCloud,
			Kind = EntryKind.AoE,
			MinTargets = 3,
			CountTargetsFromCaster = true,
			Priority = 100
		};
		span4[8] = new RotationEntry
		{
			Magic = MagicType.DanceOfSwallow,
			Kind = EntryKind.Single,
			MinDistance = 2,
			Priority = 90
		};
		dictionary[MirClass.Assassin] = list4;
		Rotations = dictionary;
	}
}
