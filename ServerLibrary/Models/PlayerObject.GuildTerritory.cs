using Library;
using Library.SystemModels;
using Server.DBModels;
using Server.Envir;
using System;
using System.Linq;
using C = Library.Network.ClientPackets;
using S = Library.Network.ServerPackets;

namespace Server.Models
{
    public partial class PlayerObject
    {
        public void GuildTerritoryRent(int index)
        {
            var result = new S.GuildTerritoryResult();

            if (Character.Account.GuildMember == null)
            {
                result.Message = "You must be in a guild.";
                Enqueue(result);
                return;
            }

            GuildInfo guild = Character.Account.GuildMember.Guild;
            if (guild.StarterGuild)
            {
                result.Message = "Starter Guild cannot rent a territory.";
                Enqueue(result);
                return;
            }

            if (!CanManageTerritory())
            {
                result.Message = "You do not have permission to manage guild territory.";
                Enqueue(result);
                return;
            }

            GuildTerritoryInfo info = SEnvir.GuildTerritoryInfoList.Binding.FirstOrDefault(x => x.Index == index);
            if (info == null || !info.Enabled || info.Instance == null)
            {
                result.Message = "Territory is not available.";
                Enqueue(result);
                return;
            }

            if (info.Instance.Type != InstanceType.Guild)
            {
                result.Message = "Territory instance is misconfigured (must be Guild type).";
                Enqueue(result);
                return;
            }

            if (guild.GuildLevel < info.MinGuildLevel)
            {
                result.Message = $"Guild must be level {info.MinGuildLevel} to rent this territory.";
                Enqueue(result);
                return;
            }

            ClearExpiredTerritory(guild);

            if (guild.Territory != null && guild.TerritoryExpiry > SEnvir.Now)
            {
                result.Message = guild.Territory == info
                    ? "Your guild already rents this territory. Use Renew to extend it."
                    : $"Your guild already rents {guild.Territory.Name}.";
                Enqueue(result);
                return;
            }

            if (guild.GuildFunds < info.RentCost)
            {
                result.Message = $"Not enough guild funds. Need {info.RentCost:#,##0}.";
                Enqueue(result);
                return;
            }

            long cost = info.RentCost;
            guild.GuildFunds -= cost;
            guild.DailyGrowth -= cost;
            guild.Territory = info;
            guild.TerritoryExpiry = SEnvir.Now.Add(info.Duration);
            guild.TerritoryRank = 0;

            BroadcastGuildFunds(-cost);
            BroadcastGuildUpdate(guild);
            RefreshTerritoryBuffs(guild);

            FillTerritoryResult(result, guild, true, $"Rented {info.Name} until {guild.TerritoryExpiry:u} UTC.");
            Enqueue(result);
            Connection.ReceiveChat(result.Message, MessageType.System);
        }

        public void GuildTerritoryRenew(int index)
        {
            var result = new S.GuildTerritoryResult();

            if (Character.Account.GuildMember == null)
            {
                result.Message = "You must be in a guild.";
                Enqueue(result);
                return;
            }

            GuildInfo guild = Character.Account.GuildMember.Guild;
            if (!CanManageTerritory())
            {
                result.Message = "You do not have permission to manage guild territory.";
                Enqueue(result);
                return;
            }

            ClearExpiredTerritory(guild);

            GuildTerritoryInfo info = index > 0
                ? SEnvir.GuildTerritoryInfoList.Binding.FirstOrDefault(x => x.Index == index)
                : guild.Territory;

            if (info == null || !info.Enabled)
            {
                result.Message = "Territory is not available.";
                Enqueue(result);
                return;
            }

            if (guild.Territory != info || guild.TerritoryExpiry <= SEnvir.Now)
            {
                result.Message = "Your guild does not have an active lease on this territory.";
                Enqueue(result);
                return;
            }

            long cost = info.GetRenewCost(guild.TerritoryRank);
            if (guild.GuildFunds < cost)
            {
                result.Message = $"Not enough guild funds. Need {cost:#,##0}.";
                Enqueue(result);
                return;
            }

            guild.GuildFunds -= cost;
            guild.DailyGrowth -= cost;
            guild.TerritoryExpiry = guild.TerritoryExpiry.Add(info.Duration);

            BroadcastGuildFunds(-cost);
            BroadcastGuildUpdate(guild);

            string discountNote = guild.TerritoryRank >= 4
                ? $" (rank {guild.TerritoryRank} discount applied)"
                : string.Empty;
            FillTerritoryResult(result, guild, true, $"Renewed {info.Name} until {guild.TerritoryExpiry:u} UTC.{discountNote}");
            Enqueue(result);
            Connection.ReceiveChat(result.Message, MessageType.System);
        }

        public void GuildTerritoryEnter(int index)
        {
            var result = new S.GuildTerritoryResult();

            if (Character.Account.GuildMember == null)
            {
                result.Message = "You must be in a guild.";
                Enqueue(result);
                return;
            }

            if (CurrentMap.Instance != null)
            {
                result.Message = "Leave your current instance first.";
                Enqueue(result);
                return;
            }

            GuildInfo guild = Character.Account.GuildMember.Guild;
            ClearExpiredTerritory(guild);

            GuildTerritoryInfo info = index > 0
                ? SEnvir.GuildTerritoryInfoList.Binding.FirstOrDefault(x => x.Index == index)
                : guild.Territory;

            if (info == null || info.Instance == null)
            {
                result.Message = "Territory is not available.";
                Enqueue(result);
                return;
            }

            if (guild.Territory != info || guild.TerritoryExpiry <= SEnvir.Now)
            {
                result.Message = "Your guild does not have an active territory lease.";
                Enqueue(result);
                return;
            }

            if (!TryEnterTerritory(this, guild, info, out string fail))
            {
                result.Message = fail;
                Enqueue(result);
                return;
            }

            FillTerritoryResult(result, guild, true, $"Entered {info.Name}.");
            Enqueue(result);
        }

        public void GuildTerritoryUpgrade()
        {
            var result = new S.GuildTerritoryResult();

            if (Character.Account.GuildMember == null)
            {
                result.Message = "You must be in a guild.";
                Enqueue(result);
                return;
            }

            GuildInfo guild = Character.Account.GuildMember.Guild;
            if (!CanManageTerritory())
            {
                result.Message = "You do not have permission to manage guild territory.";
                Enqueue(result);
                return;
            }

            ClearExpiredTerritory(guild);

            if (guild.Territory == null || guild.TerritoryExpiry <= SEnvir.Now)
            {
                result.Message = "Your guild does not have an active territory lease.";
                Enqueue(result);
                return;
            }

            if (guild.TerritoryRank >= GuildTerritoryInfo.MaxRank)
            {
                result.Message = "Territory is already at maximum rank.";
                Enqueue(result);
                return;
            }

            int nextRank = guild.TerritoryRank + 1;
            long cost = guild.Territory.GetRankUpgradeCost(nextRank);
            if (cost <= 0)
            {
                result.Message = "Rank upgrade is not configured.";
                Enqueue(result);
                return;
            }

            if (guild.GuildFunds < cost)
            {
                result.Message = $"Not enough guild funds. Need {cost:#,##0} for rank {nextRank}.";
                Enqueue(result);
                return;
            }

            guild.GuildFunds -= cost;
            guild.DailyGrowth -= cost;
            guild.TerritoryRank = nextRank;

            BroadcastGuildFunds(-cost);
            BroadcastGuildUpdate(guild);
            RefreshTerritoryBuffs(guild);

            string perk = GuildTerritoryInfo.GetRankPerkDescription(nextRank);
            FillTerritoryResult(result, guild, true, $"Territory upgraded to rank {nextRank}: {perk}.");
            Enqueue(result);
            Connection.ReceiveChat(result.Message, MessageType.System);
        }

        public void GuildTerritorySummon(string memberName)
        {
            var result = new S.GuildTerritoryResult();

            if (Character.Account.GuildMember == null)
            {
                result.Message = "You must be in a guild.";
                Enqueue(result);
                return;
            }

            GuildInfo guild = Character.Account.GuildMember.Guild;
            if (!CanManageTerritory())
            {
                result.Message = "You do not have permission to manage guild territory.";
                Enqueue(result);
                return;
            }

            ClearExpiredTerritory(guild);

            if (guild.Territory == null || guild.TerritoryExpiry <= SEnvir.Now)
            {
                result.Message = "Your guild does not have an active territory lease.";
                Enqueue(result);
                return;
            }

            bool summonAll = string.IsNullOrWhiteSpace(memberName);
            if (summonAll)
            {
                if (guild.TerritoryRank < 2)
                {
                    result.Message = "Rank 2 required to summon the whole guild.";
                    Enqueue(result);
                    return;
                }
            }
            else if (guild.TerritoryRank < 1)
            {
                result.Message = "Rank 1 required to summon a member.";
                Enqueue(result);
                return;
            }

            // Leader must already be inside the territory so members join the same instance slot.
            if (CurrentMap?.Instance == null || CurrentMap.Instance != guild.Territory.Instance)
            {
                result.Message = "Enter the territory first, then summon members to your location.";
                Enqueue(result);
                return;
            }

            int summoned = 0;

            if (summonAll)
            {
                foreach (GuildMemberInfo member in guild.Members)
                {
                    PlayerObject player = member.Account.Connection?.Player;
                    if (player == null || player == this) continue;
                    if (player.Dead) continue;
                    if (player.CurrentMap?.Instance == CurrentMap.Instance &&
                        player.CurrentMap.InstanceSequence == CurrentMap.InstanceSequence)
                        continue;

                    if (SendTerritoryRecallRequest(player))
                        summoned++;
                }

                FillTerritoryResult(result, guild, summoned > 0,
                    summoned > 0 ? $"Sent territory recall to {summoned} guild member(s)." : "No eligible members to recall.");
            }
            else
            {
                PlayerObject target = guild.Members
                    .Select(m => m.Account.Connection?.Player)
                    .FirstOrDefault(p => p != null && string.Equals(p.Name, memberName, StringComparison.OrdinalIgnoreCase));

                if (target == null)
                {
                    result.Message = $"Online member '{memberName}' was not found.";
                    Enqueue(result);
                    return;
                }

                if (target == this)
                {
                    result.Message = "You are already here.";
                    Enqueue(result);
                    return;
                }

                if (target.Dead)
                {
                    result.Message = $"{target.Name} is dead and cannot be summoned.";
                    Enqueue(result);
                    return;
                }

                if (!SendTerritoryRecallRequest(target))
                {
                    result.Message = $"Failed to recall {target.Name}.";
                    Enqueue(result);
                    return;
                }

                FillTerritoryResult(result, guild, true, $"Sent territory recall to {target.Name}.");
            }

            Enqueue(result);
            if (result.Success)
                Connection.ReceiveChat(result.Message, MessageType.System);
        }

        public void GuildTerritoryRecall()
        {
            var result = new S.GuildTerritoryResult();

            if (Character.Account.GuildMember == null)
            {
                result.Message = "You must be in a guild.";
                Enqueue(result);
                return;
            }

            if (Dead)
            {
                result.Message = "You cannot recall while dead.";
                Enqueue(result);
                return;
            }

            if (CurrentMap.Instance != null)
            {
                result.Message = "Leave your current instance first.";
                Enqueue(result);
                return;
            }

            GuildInfo guild = Character.Account.GuildMember.Guild;
            ClearExpiredTerritory(guild);

            if (guild.Territory == null || guild.TerritoryExpiry <= SEnvir.Now)
            {
                result.Message = "Your guild does not have an active territory lease.";
                Enqueue(result);
                return;
            }

            if (guild.TerritoryRank < 3)
            {
                result.Message = "Rank 3 required for self-recall to territory.";
                Enqueue(result);
                return;
            }

            if (!TryEnterTerritory(this, guild, guild.Territory, out string fail))
            {
                result.Message = fail;
                Enqueue(result);
                return;
            }

            FillTerritoryResult(result, guild, true, $"Recalled to {guild.Territory.Name}.");
            Enqueue(result);
        }

        public void GuildTerritoryAcceptRecall(string summonerName)
        {
            if (Dead)
            {
                Connection.ReceiveChat("You cannot recall while dead.", MessageType.System);
                return;
            }

            if (Character.Account.GuildMember == null)
            {
                Connection.ReceiveChat("You must be in a guild.", MessageType.System);
                return;
            }

            GuildInfo guild = Character.Account.GuildMember.Guild;
            ClearExpiredTerritory(guild);

            if (guild.Territory == null || guild.TerritoryExpiry <= SEnvir.Now)
            {
                Connection.ReceiveChat("Your guild does not have an active territory lease.", MessageType.System);
                return;
            }

            if (CurrentMap?.Instance != null && CurrentMap.Instance != guild.Territory.Instance)
            {
                Connection.ReceiveChat("Leave your current instance first.", MessageType.System);
                return;
            }

            PlayerObject summoner = guild.Members
                .Select(m => m.Account.Connection?.Player)
                .FirstOrDefault(p => p != null && string.Equals(p.Name, summonerName, StringComparison.OrdinalIgnoreCase));

            if (summoner == null)
            {
                Connection.ReceiveChat("The summon request is no longer valid.", MessageType.System);
                return;
            }

            if (summoner.CurrentMap?.Instance == null || summoner.CurrentMap.Instance != guild.Territory.Instance)
            {
                Connection.ReceiveChat("The summoner is no longer in the territory.", MessageType.System);
                return;
            }

            if (!summoner.TeleportMemberToMyTerritory(this))
            {
                Connection.ReceiveChat("Unable to join the territory.", MessageType.System);
                return;
            }

            var result = new S.GuildTerritoryResult();
            FillTerritoryResult(result, guild, true, $"Entered {guild.Territory.Name}.");
            Enqueue(result);
        }

        public void ApplyTerritoryBuff()
        {
            BuffRemove(BuffType.GuildTerritory);

            GuildInfo guild = Character.Account.GuildMember?.Guild;
            if (guild?.Territory == null || guild.TerritoryExpiry <= SEnvir.Now) return;
            if (guild.TerritoryRank < 5) return;

            Stats stats = new Stats
            {
                [Stat.ExperienceRate] = 5,
                [Stat.DropRate] = 5,
                [Stat.GoldRate] = 5,
            };

            BuffAdd(BuffType.GuildTerritory, TimeSpan.MaxValue, stats, false, false, TimeSpan.Zero);
        }

        private bool CanManageTerritory()
        {
            GuildMemberInfo member = Character.Account.GuildMember;
            if (member == null) return false;

            return (member.Permission & GuildPermission.Leader) == GuildPermission.Leader ||
                   (member.Permission & GuildPermission.ManageTerritory) == GuildPermission.ManageTerritory;
        }

        private bool SendTerritoryRecallRequest(PlayerObject player)
        {
            if (player == null || CurrentMap?.Instance == null) return false;
            if (player.Dead) return false;
            if (player.CurrentMap?.Instance == CurrentMap.Instance &&
                player.CurrentMap.InstanceSequence == CurrentMap.InstanceSequence)
                return false;
            if (player.CurrentMap?.Instance != null && player.CurrentMap.Instance != CurrentMap.Instance)
                return false;

            GuildInfo guild = Character.Account.GuildMember?.Guild;
            string territoryName = guild?.Territory?.Name ?? "Guild Territory";

            player.Enqueue(new S.GuildTerritoryRecallRequest
            {
                SummonerName = Name,
                TerritoryName = territoryName,
                ObserverPacket = false,
            });
            return true;
        }

        private bool TeleportMemberToMyTerritory(PlayerObject player)
        {
            if (player == null || CurrentMap?.Instance == null) return false;
            if (player.CurrentMap?.Instance != null && player.CurrentMap.Instance != CurrentMap.Instance)
                return false;

            if (!player.Teleport(CurrentMap, CurrentMap.GetRandomLocation(CurrentLocation, 8)))
                return false;

            player.ApplyTerritoryBuff();
            return true;
        }

        private static bool TryEnterTerritory(PlayerObject player, GuildInfo guild, GuildTerritoryInfo info, out string failMessage)
        {
            failMessage = null;

            var (sequence, joinResult) = player.GetInstance(info.Instance, dungeonFinder: false);
            if (joinResult != InstanceResult.Success || !sequence.HasValue)
            {
                player.SendInstanceMessage(info.Instance, joinResult);
                failMessage = "Unable to enter territory.";
                return false;
            }

            if (!player.Teleport(info.Instance.ConnectRegion, info.Instance, sequence.Value))
            {
                failMessage = "Failed to teleport to territory.";
                return false;
            }

            player.ApplyTerritoryBuff();
            return true;
        }

        private static void ClearExpiredTerritory(GuildInfo guild)
        {
            if (guild == null) return;
            if (guild.Territory == null) return;
            if (guild.TerritoryExpiry > SEnvir.Now) return;

            guild.Territory = null;
            guild.TerritoryExpiry = DateTime.MinValue;
            guild.TerritoryRank = 0;
            RefreshTerritoryBuffs(guild);
        }

        private static void RefreshTerritoryBuffs(GuildInfo guild)
        {
            if (guild?.Members == null) return;

            foreach (GuildMemberInfo member in guild.Members)
                member.Account.Connection?.Player?.ApplyTerritoryBuff();
        }

        private static void FillTerritoryResult(S.GuildTerritoryResult result, GuildInfo guild, bool success, string message)
        {
            result.Success = success;
            result.Message = message;
            result.TerritoryIndex = guild.Territory?.Index ?? 0;
            result.TerritoryName = guild.Territory?.Name;
            result.TerritoryRemaining = guild.Territory != null && guild.TerritoryExpiry > SEnvir.Now
                ? guild.TerritoryExpiry - SEnvir.Now
                : TimeSpan.Zero;
            result.TerritoryRank = guild.Territory != null && guild.TerritoryExpiry > SEnvir.Now
                ? guild.TerritoryRank
                : 0;
        }

        private void BroadcastGuildFunds(long change)
        {
            GuildInfo guild = Character.Account.GuildMember?.Guild;
            if (guild == null) return;

            foreach (GuildMemberInfo member in guild.Members)
                member.Account.Connection?.Player?.Enqueue(new S.GuildFundsChanged { Change = change, ObserverPacket = false });
        }

        private void BroadcastGuildUpdate(GuildInfo guild)
        {
            if (guild == null) return;

            S.GuildUpdate update = guild.GetUpdatePacket();
            foreach (GuildMemberInfo member in guild.Members)
                member.Account.Connection?.Player?.Enqueue(update);
        }
    }
}
