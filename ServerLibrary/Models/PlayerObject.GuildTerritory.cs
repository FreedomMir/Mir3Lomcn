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

            if ((Character.Account.GuildMember.Permission & GuildPermission.ManageTerritory) != GuildPermission.ManageTerritory &&
                (Character.Account.GuildMember.Permission & GuildPermission.Leader) != GuildPermission.Leader)
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

            BroadcastGuildFunds(-cost);
            BroadcastGuildUpdate(guild);

            result.Success = true;
            result.Message = $"Rented {info.Name} until {guild.TerritoryExpiry:u} UTC.";
            result.TerritoryIndex = info.Index;
            result.TerritoryName = info.Name;
            result.TerritoryRemaining = guild.TerritoryExpiry - SEnvir.Now;
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
            if ((Character.Account.GuildMember.Permission & GuildPermission.ManageTerritory) != GuildPermission.ManageTerritory &&
                (Character.Account.GuildMember.Permission & GuildPermission.Leader) != GuildPermission.Leader)
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

            if (guild.GuildFunds < info.RenewCost)
            {
                result.Message = $"Not enough guild funds. Need {info.RenewCost:#,##0}.";
                Enqueue(result);
                return;
            }

            long cost = info.RenewCost;
            guild.GuildFunds -= cost;
            guild.DailyGrowth -= cost;
            guild.TerritoryExpiry = guild.TerritoryExpiry.Add(info.Duration);

            BroadcastGuildFunds(-cost);
            BroadcastGuildUpdate(guild);

            result.Success = true;
            result.Message = $"Renewed {info.Name} until {guild.TerritoryExpiry:u} UTC.";
            result.TerritoryIndex = info.Index;
            result.TerritoryName = info.Name;
            result.TerritoryRemaining = guild.TerritoryExpiry - SEnvir.Now;
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

            var (sequence, joinResult) = GetInstance(info.Instance, dungeonFinder: false);
            if (joinResult != InstanceResult.Success || !sequence.HasValue)
            {
                SendInstanceMessage(info.Instance, joinResult);
                result.Message = "Unable to enter territory.";
                Enqueue(result);
                return;
            }

            if (!Teleport(info.Instance.ConnectRegion, info.Instance, sequence.Value))
            {
                result.Message = "Failed to teleport to territory.";
                Enqueue(result);
                return;
            }

            result.Success = true;
            result.Message = $"Entered {info.Name}.";
            result.TerritoryIndex = info.Index;
            result.TerritoryName = info.Name;
            result.TerritoryRemaining = guild.TerritoryExpiry - SEnvir.Now;
            Enqueue(result);
        }

        private static void ClearExpiredTerritory(GuildInfo guild)
        {
            if (guild == null) return;
            if (guild.Territory == null) return;
            if (guild.TerritoryExpiry > SEnvir.Now) return;

            guild.Territory = null;
            guild.TerritoryExpiry = DateTime.MinValue;
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
