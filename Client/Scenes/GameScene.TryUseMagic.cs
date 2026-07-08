using Client.Controls;
using Client.Envir;
using Client.Models;
using Client.Scenes.Views;
using Library;
using Library.SystemModels;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using C = Library.Network.ClientPackets;

namespace Client.Scenes
{
    public sealed partial class GameScene
    {
	public bool TryUseMagic(ClientUserMagic magic, MapObject explicitTarget = null, Point? explicitLocation = null)
	{
		if (Observer || User == null || User.Horse != HorseType.None)
		{
			return false;
		}
		if (magic?.Info == null || User.Level < magic.Info.NeedLevel1)
		{
			return false;
		}
		if (User.Buffs == null)
		{
			return false;
		}
		MapObject mapObject = explicitTarget ?? MouseObject;
		Point point = explicitLocation ?? MapControl.MapLocation;
		switch (magic.Info.Magic)
		{
		case MagicType.Swordsmanship:
		case MagicType.SpiritSword:
		case MagicType.WillowDance:
		case MagicType.VineTreeDance:
			return false;
		case MagicType.Thrusting:
			if (CEnvir.Now < ToggleTime)
			{
				return false;
			}
			ToggleTime = CEnvir.Now.AddSeconds(1.0);
			CEnvir.Enqueue(new C.MagicToggle
			{
				Magic = magic.Info.Magic,
				CanUse = !User.CanThrusting
			});
			return true;
		case MagicType.HalfMoon:
			if (CEnvir.Now < ToggleTime)
			{
				return false;
			}
			ToggleTime = CEnvir.Now.AddSeconds(1.0);
			CEnvir.Enqueue(new C.MagicToggle
			{
				Magic = magic.Info.Magic,
				CanUse = !User.CanHalfMoon
			});
			return true;
		case MagicType.DestructiveSurge:
			if (CEnvir.Now < ToggleTime)
			{
				return false;
			}
			ToggleTime = CEnvir.Now.AddSeconds(1.0);
			CEnvir.Enqueue(new C.MagicToggle
			{
				Magic = magic.Info.Magic,
				CanUse = !User.CanDestructiveSurge
			});
			return true;
		case MagicType.FlamingSword:
		case MagicType.DragonRise:
		case MagicType.BladeStorm:
		case MagicType.DemonicRecovery:
			if (CEnvir.Now < magic.NextCast || magic.Cost > User.CurrentMP)
			{
				return false;
			}
			magic.NextCast = CEnvir.Now.AddSeconds(0.5);
			CEnvir.Enqueue(new C.MagicToggle
			{
				Magic = magic.Info.Magic
			});
			return true;
		case MagicType.FlameSplash:
			if (CEnvir.Now < ToggleTime)
			{
				return false;
			}
			ToggleTime = CEnvir.Now.AddSeconds(1.0);
			CEnvir.Enqueue(new C.MagicToggle
			{
				Magic = magic.Info.Magic,
				CanUse = !User.CanFlameSplash
			});
			return true;
		case MagicType.FullBloom:
		case MagicType.WhiteLotus:
		case MagicType.RedLotus:
		case MagicType.SweetBrier:
			if (CEnvir.Now < ToggleTime || CEnvir.Now < magic.NextCast)
			{
				return false;
			}
			if (User.AttackMagic != magic.Info.Magic)
			{
				ReceiveChat(magic.Info.Name + " is now Ready.", MessageType.Hint);
				int val = 1500 - MapObject.User.Stats[Stat.AttackSpeed] * 47;
				val = Math.Max(800, val);
				ToggleTime = CEnvir.Now + TimeSpan.FromMilliseconds(val + 200);
				User.AttackMagic = magic.Info.Magic;
			}
			return true;
		case MagicType.Endurance:
			if (CEnvir.Now < magic.NextCast || magic.Cost > User.CurrentMP)
			{
				return false;
			}
			magic.NextCast = CEnvir.Now.AddSeconds(0.5);
			CEnvir.Enqueue(new C.MagicToggle
			{
				Magic = magic.Info.Magic
			});
			return true;
		case MagicType.Karma:
			if (CEnvir.Now < ToggleTime || CEnvir.Now < magic.NextCast || User.Buffs.All((ClientBuffInfo x) => x.Type != BuffType.Cloak))
			{
				return false;
			}
			if (User.AttackMagic != magic.Info.Magic)
			{
				ReceiveChat(magic.Info.Name + " is now Ready.", MessageType.Hint);
				ToggleTime = CEnvir.Now + TimeSpan.FromMilliseconds(500L);
				User.AttackMagic = magic.Info.Magic;
			}
			return true;
		default:
		{
			if (CEnvir.Now < User.NextMagicTime || User.Dead || User.Buffs.Any((ClientBuffInfo x) => x.Type == BuffType.DragonRepulse || x.Type == BuffType.FrostBite) || (User.Poison & PoisonType.Paralysis) == PoisonType.Paralysis || (User.Poison & PoisonType.Silenced) == PoisonType.Silenced)
			{
				return false;
			}
			if (CEnvir.Now < magic.NextCast)
			{
				if (CEnvir.Now >= OutputTime)
				{
					OutputTime = CEnvir.Now.AddSeconds(1.0);
					ReceiveChat("Unable to cast " + magic.Info.Name + ", it is still on Cooldown.", MessageType.Hint);
				}
				return false;
			}
			switch (magic.Info.Magic)
			{
			case MagicType.Cloak:
				if (User.VisibleBuffs.ContainsKey(BuffType.Cloak))
				{
					break;
				}
				if (CEnvir.Now < User.CombatTime.AddSeconds(10.0))
				{
					if (CEnvir.Now >= OutputTime)
					{
						OutputTime = CEnvir.Now.AddSeconds(1.0);
						ReceiveChat("Unable to cast " + magic.Info.Name + " whilst in combat", MessageType.Hint);
					}
					return false;
				}
				if (User.Stats[Stat.Health] * magic.Cost / 1000 >= User.CurrentHP || User.CurrentHP < User.Stats[Stat.Health] / 10)
				{
					if (CEnvir.Now >= OutputTime)
					{
						OutputTime = CEnvir.Now.AddSeconds(1.0);
						ReceiveChat("Unable to cast " + magic.Info.Name + ", You do not have enough Health.", MessageType.Hint);
					}
					return false;
				}
				break;
			case MagicType.DarkConversion:
				if (!User.VisibleBuffs.ContainsKey(BuffType.DarkConversion) && magic.Cost > User.CurrentMP)
				{
					if (CEnvir.Now >= OutputTime)
					{
						OutputTime = CEnvir.Now.AddSeconds(1.0);
						ReceiveChat("Unable to cast " + magic.Info.Name + ", You do not have enough Mana.", MessageType.Hint);
					}
					return false;
				}
				break;
			case MagicType.DragonRepulse:
				if (User.Stats[Stat.Health] * magic.Cost / 1000 >= User.CurrentHP || User.CurrentHP < User.Stats[Stat.Health] / 10)
				{
					if (CEnvir.Now >= OutputTime)
					{
						OutputTime = CEnvir.Now.AddSeconds(1.0);
						ReceiveChat("Unable to cast " + magic.Info.Name + ", You do not have enough Health.", MessageType.Hint);
					}
					return false;
				}
				if (User.Stats[Stat.Mana] * magic.Cost / 1000 >= User.CurrentMP || User.CurrentMP < User.Stats[Stat.Mana] / 10)
				{
					if (CEnvir.Now >= OutputTime)
					{
						OutputTime = CEnvir.Now.AddSeconds(1.0);
						ReceiveChat("Unable to cast " + magic.Info.Name + ", You do not have enough Mana.", MessageType.Hint);
					}
					return false;
				}
				break;
			default:
				if (magic.Cost > User.CurrentMP)
				{
					if (CEnvir.Now >= OutputTime)
					{
						OutputTime = CEnvir.Now.AddSeconds(1.0);
						ReceiveChat("Unable to cast " + magic.Info.Name + ", You do not have enough Mana.", MessageType.Hint);
					}
					return false;
				}
				break;
			}
			MapObject mapObject2 = null;
			MirDirection direction = ((explicitTarget != null && explicitTarget != User) ? Functions.DirectionFromPoint(User.CurrentLocation, explicitTarget.CurrentLocation) : ((explicitLocation.HasValue && explicitLocation.Value != User.CurrentLocation) ? Functions.DirectionFromPoint(User.CurrentLocation, explicitLocation.Value) : ((explicitTarget == null && !explicitLocation.HasValue) ? MapControl.MouseDirection() : User.Direction)));
			switch (magic.Info.Magic)
			{
			case MagicType.ShoulderDash:
				if (CEnvir.Now < User.ServerTime)
				{
					return false;
				}
				if ((User.Poison & PoisonType.WraithGrip) == PoisonType.WraithGrip)
				{
					return false;
				}
				User.ServerTime = CEnvir.Now.AddSeconds(5.0);
				User.NextMagicTime = CEnvir.Now + Globals.MagicDelay;
				CEnvir.Enqueue(new C.Magic
				{
					Direction = direction,
					Action = MirAction.Spell,
					Type = magic.Info.Magic
				});
				return true;
			case MagicType.DanceOfSwallow:
				if (CEnvir.Now < User.ServerTime)
				{
					return false;
				}
				if (CanAttackTarget(mapObject))
				{
					mapObject2 = mapObject;
				}
				if (mapObject2 == null)
				{
					return false;
				}
				if (!Functions.InRange(mapObject2.CurrentLocation, User.CurrentLocation, 10))
				{
					if (CEnvir.Now < OutputTime)
					{
						return false;
					}
					OutputTime = CEnvir.Now.AddSeconds(1.0);
					ReceiveChat("Unable to cast " + magic.Info.Name + ", Your target is too far.", MessageType.Hint);
					return false;
				}
				User.ServerTime = CEnvir.Now.AddSeconds(5.0);
				User.NextMagicTime = CEnvir.Now + Globals.MagicDelay;
				MapObject.TargetObject = mapObject2;
				MapObject.MagicObject = mapObject2;
				CEnvir.Enqueue(new C.Magic
				{
					Action = MirAction.Spell,
					Type = magic.Info.Magic,
					Target = mapObject2.ObjectID
				});
				return true;
			case MagicType.FireBall:
			case MagicType.LightningBall:
			case MagicType.IceBolt:
			case MagicType.GustBlast:
			case MagicType.ElectricShock:
			case MagicType.AdamantineFireBall:
			case MagicType.ThunderBolt:
			case MagicType.IceBlades:
			case MagicType.Cyclone:
			case MagicType.ExpelUndead:
			case MagicType.PoisonDust:
			case MagicType.ExplosiveTalisman:
			case MagicType.EvilSlayer:
			case MagicType.GreaterEvilSlayer:
			case MagicType.ImprovedExplosiveTalisman:
			case MagicType.Infection:
				if (CanAttackTarget(MagicObject))
				{
					mapObject2 = MagicObject;
				}
				if (CanAttackTarget(mapObject))
				{
					mapObject2 = mapObject;
					if (mapObject.Race == ObjectType.Monster && ((MonsterObject)mapObject).MonsterInfo.AI >= 0)
					{
						MapObject.MagicObject = mapObject2;
					}
					else
					{
						MapObject.MagicObject = null;
					}
				}
				break;
			case MagicType.WraithGrip:
			case MagicType.HellFire:
			case MagicType.Abyss:
				if (CanAttackTarget(mapObject))
				{
					mapObject2 = mapObject;
				}
				break;
			case MagicType.Interchange:
			case MagicType.Beckon:
				if (CanAttackTarget(mapObject))
				{
					mapObject2 = mapObject;
				}
				break;
			case MagicType.Heal:
			case MagicType.Purification:
				mapObject2 = mapObject ?? User;
				break;
			case MagicType.CelestialLight:
				if (User.Buffs != null && User.Buffs.Any((ClientBuffInfo x) => x.Type == BuffType.CelestialLight))
				{
					return false;
				}
				break;
			case MagicType.Resurrection:
				if (mapObject == null || !mapObject.Dead || mapObject.Race != ObjectType.Player)
				{
					return false;
				}
				mapObject2 = mapObject;
				break;
			case MagicType.Defiance:
				direction = MirDirection.Down;
				break;
			case MagicType.Might:
				direction = MirDirection.Down;
				break;
			case MagicType.ReflectDamage:
				if (User.Buffs.Any((ClientBuffInfo x) => x.Type == BuffType.ReflectDamage))
				{
					return false;
				}
				direction = MirDirection.Down;
				break;
			case MagicType.Fetter:
				direction = MirDirection.Down;
				break;
			case MagicType.MagicShield:
				if (User.Buffs.Any((ClientBuffInfo x) => x.Type == BuffType.MagicShield))
				{
					return false;
				}
				break;
			case MagicType.FrostBite:
				if (User.Buffs.Any((ClientBuffInfo x) => x.Type == BuffType.FrostBite))
				{
					return false;
				}
				break;
			case MagicType.SwiftBlade:
			case MagicType.FireWall:
			case MagicType.GeoManipulation:
			case MagicType.FireStorm:
			case MagicType.LightningWave:
			case MagicType.IceStorm:
			case MagicType.DragonTornado:
			case MagicType.ChainLightning:
			case MagicType.MeteorShower:
			case MagicType.Tempest:
			case MagicType.Asteroid:
			case MagicType.MagicResistance:
			case MagicType.MassInvisibility:
			case MagicType.Resilience:
			case MagicType.TrapOctagon:
			case MagicType.ElementalSuperiority:
			case MagicType.MassHeal:
			case MagicType.BloodLust:
			case MagicType.Transparency:
			case MagicType.LifeSteal:
				if (!Functions.InRange(point, User.CurrentLocation, 10))
				{
					if (CEnvir.Now < OutputTime)
					{
						return false;
					}
					OutputTime = CEnvir.Now.AddSeconds(1.0);
					ReceiveChat("Unable to cast " + magic.Info.Name + ", Your target is too far.", MessageType.Hint);
					return false;
				}
				break;
			default:
				return false;
			case MagicType.MassBeckon:
			case MagicType.SeismicSlam:
			case MagicType.Repulsion:
			case MagicType.Teleportation:
			case MagicType.ScortchedEarth:
			case MagicType.LightningBeam:
			case MagicType.FrozenEarth:
			case MagicType.BlowEarth:
			case MagicType.GreaterFrozenEarth:
			case MagicType.Renounce:
			case MagicType.JudgementOfHeaven:
			case MagicType.ThunderStrike:
			case MagicType.MirrorImage:
			case MagicType.Invisibility:
			case MagicType.ThunderKick:
			case MagicType.SummonSkeleton:
			case MagicType.SummonShinsu:
			case MagicType.SummonJinSkeleton:
			case MagicType.StrengthOfFaith:
			case MagicType.SummonDemonicCreature:
			case MagicType.DemonExplosion:
			case MagicType.PoisonousCloud:
			case MagicType.Cloak:
			case MagicType.SummonPuppet:
			case MagicType.TheNewBeginning:
			case MagicType.DarkConversion:
			case MagicType.DragonRepulse:
			case MagicType.FlashOfLight:
			case MagicType.Evasion:
			case MagicType.RagingWind:
				break;
			}
			if (mapObject2 != null && !Functions.InRange(mapObject2.CurrentLocation, User.CurrentLocation, 10))
			{
				if (CEnvir.Now < OutputTime)
				{
					return false;
				}
				OutputTime = CEnvir.Now.AddSeconds(1.0);
				ReceiveChat("Unable to cast " + magic.Info.Name + ", Your target is too far.", MessageType.Hint);
				return false;
			}
			if (mapObject2 != null && mapObject2 != User)
			{
				direction = Functions.DirectionFromPoint(User.CurrentLocation, mapObject2.CurrentLocation);
			}
			uint item = mapObject2?.ObjectID ?? 0;
			Point item2;
			switch (magic.Info.Magic)
			{
			case MagicType.PoisonDust:
			case MagicType.ExplosiveTalisman:
			case MagicType.EvilSlayer:
			case MagicType.GreaterEvilSlayer:
			case MagicType.Purification:
			case MagicType.ImprovedExplosiveTalisman:
				item2 = point;
				break;
			default:
				item2 = mapObject2?.CurrentLocation ?? point;
				break;
			}
			if (mapObject != null && mapObject.Race == ObjectType.Monster)
			{
				FocusObject = (MonsterObject)mapObject;
			}
			// Extra layout must match UseMagic / MapObject.SetFrame Spell handling:
			// [0] MagicType, [1] target ids, [2] locations, [3] MagicCast, [4] AttackElement
			User.MagicAction = new ObjectAction(MirAction.Spell, direction, MapObject.User.CurrentLocation, magic.Info.Magic, new List<uint> { item }, new List<Point> { item2 }, false, Element.None);
			return true;
		}
		}
	}
    }
}
