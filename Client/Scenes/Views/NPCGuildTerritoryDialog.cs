using Client.Controls;
using Client.Envir;
using Client.UserModels;
using Library;
using Library.Network.ClientPackets;
using Library.SystemModels;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Client.Scenes.Views
{
    public sealed class NPCGuildTerritoryDialog : DXWindow
    {
        public DXLabel StatusLabel;
        public DXComboBox TerritoryBox;
        public DXTextBox SummonNameBox;
        public DXButton RentButton, RenewButton, EnterButton;
        public DXButton UpgradeButton, SummonOneButton, SummonAllButton, RecallButton;

        public override WindowType Type => WindowType.None;
        public override bool CustomSize => false;
        public override bool AutomaticVisibility => false;

        public NPCGuildTerritoryDialog()
        {
            TitleLabel.Text = "Guild Territory";
            Size = new Size(320, 280);
            HasFooter = false;

            StatusLabel = new DXLabel
            {
                Parent = this,
                Location = new Point(10, 40),
                Size = new Size(300, 55),
                AutoSize = false,
                DrawFormat = TextFormatFlags.WordBreak,
                Text = "Select a territory."
            };

            TerritoryBox = new DXComboBox
            {
                Parent = this,
                Location = new Point(10, 100),
                Size = new Size(290, DXComboBox.DefaultNormalHeight),
                DropDownHeight = 120
            };
            TerritoryBox.SelectedItemChanged += (o, e) => RefreshStatus();

            RentButton = new DXButton
            {
                Parent = this,
                Location = new Point(10, 128),
                Size = new Size(70, SmallButtonHeight),
                Label = { Text = "Rent" }
            };
            RentButton.MouseClick += (o, e) =>
            {
                if (TerritoryBox.SelectedItem is not GuildTerritoryInfo info) return;
                CEnvir.Enqueue(new GuildTerritoryRent { Index = info.Index });
            };

            RenewButton = new DXButton
            {
                Parent = this,
                Location = new Point(85, 128),
                Size = new Size(70, SmallButtonHeight),
                Label = { Text = "Renew" }
            };
            RenewButton.MouseClick += (o, e) =>
            {
                int index = TerritoryBox.SelectedItem is GuildTerritoryInfo info
                    ? info.Index
                    : GameScene.Game.GuildBox.GuildInfo?.TerritoryIndex ?? 0;
                CEnvir.Enqueue(new GuildTerritoryRenew { Index = index });
            };

            EnterButton = new DXButton
            {
                Parent = this,
                Location = new Point(160, 128),
                Size = new Size(70, SmallButtonHeight),
                Label = { Text = "Enter" }
            };
            EnterButton.MouseClick += (o, e) =>
            {
                int index = TerritoryBox.SelectedItem is GuildTerritoryInfo info
                    ? info.Index
                    : GameScene.Game.GuildBox.GuildInfo?.TerritoryIndex ?? 0;
                CEnvir.Enqueue(new GuildTerritoryEnter { Index = index });
            };

            UpgradeButton = new DXButton
            {
                Parent = this,
                Location = new Point(235, 128),
                Size = new Size(70, SmallButtonHeight),
                Label = { Text = "Upgrade" }
            };
            UpgradeButton.MouseClick += (o, e) => CEnvir.Enqueue(new GuildTerritoryUpgrade());

            SummonNameBox = new DXTextBox
            {
                Parent = this,
                Location = new Point(10, 160),
                Size = new Size(145, 18),
            };

            SummonOneButton = new DXButton
            {
                Parent = this,
                Location = new Point(160, 158),
                Size = new Size(70, SmallButtonHeight),
                Label = { Text = "Summon" }
            };
            SummonOneButton.MouseClick += (o, e) =>
            {
                CEnvir.Enqueue(new GuildTerritorySummon { MemberName = SummonNameBox.TextBox.Text?.Trim() });
            };

            SummonAllButton = new DXButton
            {
                Parent = this,
                Location = new Point(235, 158),
                Size = new Size(70, SmallButtonHeight),
                Label = { Text = "All" }
            };
            SummonAllButton.MouseClick += (o, e) =>
            {
                CEnvir.Enqueue(new GuildTerritorySummon { MemberName = string.Empty });
            };

            RecallButton = new DXButton
            {
                Parent = this,
                Location = new Point(10, 190),
                Size = new Size(295, SmallButtonHeight),
                Label = { Text = "Recall Self (Rank 3+)" }
            };
            RecallButton.MouseClick += (o, e) => CEnvir.Enqueue(new GuildTerritoryRecall());
        }

        public void RefreshList()
        {
            foreach (DXControl control in TerritoryBox.ListBox.Controls.ToList())
            {
                if (control is DXListBoxItem)
                    control.Dispose();
            }

            if (Globals.GuildTerritoryInfoList?.Binding == null) return;

            GuildTerritoryInfo select = null;
            int current = GameScene.Game.GuildBox.GuildInfo?.TerritoryIndex ?? 0;

            foreach (GuildTerritoryInfo info in Globals.GuildTerritoryInfoList.Binding.Where(x => x.Enabled))
            {
                new DXListBoxItem
                {
                    Parent = TerritoryBox.ListBox,
                    Label = { Text = $"{info.Name} (Rent {info.RentCost:#,##0})" },
                    Item = info
                };

                if (info.Index == current)
                    select = info;
            }

            TerritoryBox.ListBox.SelectItem(select ?? Globals.GuildTerritoryInfoList.Binding.FirstOrDefault(x => x.Enabled));
            RefreshStatus();
        }

        public void RefreshStatus()
        {
            ClientGuildInfo guild = GameScene.Game.GuildBox.GuildInfo;
            if (guild == null)
            {
                StatusLabel.Text = "You must be in a guild.";
                SetButtons(false, false, false, false, false, false, false);
                return;
            }

            bool canManage = (guild.Permission & GuildPermission.Leader) == GuildPermission.Leader ||
                             (guild.Permission & GuildPermission.ManageTerritory) == GuildPermission.ManageTerritory;

            if (guild.TerritoryIndex > 0 && guild.TerritoryRemaining > TimeSpan.Zero)
            {
                int rank = guild.TerritoryRank;
                string perk = GuildTerritoryInfo.GetRankPerkDescription(rank);
                StatusLabel.Text =
                    $"Lease: {guild.TerritoryName}\n" +
                    $"Remaining: {FormatRemaining(guild.TerritoryRemaining)}\n" +
                    $"Rank {rank}: {perk}";

                bool canUpgrade = canManage && rank < GuildTerritoryInfo.MaxRank;
                SetButtons(
                    rent: false,
                    renew: canManage,
                    enter: true,
                    upgrade: canUpgrade,
                    summonOne: canManage && rank >= 1,
                    summonAll: canManage && rank >= 2,
                    recall: rank >= 3);
            }
            else
            {
                StatusLabel.Text = "No active territory lease.";
                SetButtons(
                    rent: canManage && TerritoryBox.SelectedItem is GuildTerritoryInfo,
                    renew: false,
                    enter: false,
                    upgrade: false,
                    summonOne: false,
                    summonAll: false,
                    recall: false);
            }
        }

        private void SetButtons(bool rent, bool renew, bool enter, bool upgrade, bool summonOne, bool summonAll, bool recall)
        {
            RentButton.Enabled = rent;
            RenewButton.Enabled = renew;
            EnterButton.Enabled = enter;
            UpgradeButton.Enabled = upgrade;
            SummonOneButton.Enabled = summonOne;
            SummonAllButton.Enabled = summonAll;
            SummonNameBox.Editable = summonOne;
            RecallButton.Enabled = recall;
        }

        private static string FormatRemaining(TimeSpan span)
        {
            if (span.TotalDays >= 1)
                return $"{(int)span.TotalDays}d {span.Hours}h";
            if (span.TotalHours >= 1)
                return $"{(int)span.TotalHours}h {span.Minutes}m";
            return $"{span.Minutes}m";
        }
    }
}
