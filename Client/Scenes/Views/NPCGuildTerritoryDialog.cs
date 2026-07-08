using Client.Controls;
using Client.Envir;
using Client.UserModels;
using Library;
using Library.Network.ClientPackets;
using Library.SystemModels;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Client.Scenes.Views
{
    public sealed class NPCGuildTerritoryDialog : DXWindow
    {
        public DXLabel StatusLabel;
        public DXComboBox TerritoryBox;
        public DXButton RentButton, RenewButton, EnterButton;

        public override WindowType Type => WindowType.None;
        public override bool CustomSize => false;
        public override bool AutomaticVisibility => false;

        public NPCGuildTerritoryDialog()
        {
            TitleLabel.Text = "Guild Territory";
            Size = new Size(280, 180);
            HasFooter = false;

            StatusLabel = new DXLabel
            {
                Parent = this,
                Location = new Point(10, 40),
                Size = new Size(260, 40),
                AutoSize = false,
                DrawFormat = TextFormatFlags.WordBreak,
                Text = "Select a territory."
            };

            TerritoryBox = new DXComboBox
            {
                Parent = this,
                Location = new Point(10, 90),
                Size = new Size(250, DXComboBox.DefaultNormalHeight),
                DropDownHeight = 120
            };
            TerritoryBox.SelectedItemChanged += (o, e) => RefreshStatus();

            RentButton = new DXButton
            {
                Parent = this,
                Location = new Point(10, 120),
                Size = new Size(80, SmallButtonHeight),
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
                Location = new Point(100, 120),
                Size = new Size(80, SmallButtonHeight),
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
                Location = new Point(190, 120),
                Size = new Size(80, SmallButtonHeight),
                Label = { Text = "Enter" }
            };
            EnterButton.MouseClick += (o, e) =>
            {
                int index = TerritoryBox.SelectedItem is GuildTerritoryInfo info
                    ? info.Index
                    : GameScene.Game.GuildBox.GuildInfo?.TerritoryIndex ?? 0;
                CEnvir.Enqueue(new GuildTerritoryEnter { Index = index });
            };
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
                RentButton.Enabled = RenewButton.Enabled = EnterButton.Enabled = false;
                return;
            }

            bool canManage = (guild.Permission & GuildPermission.Leader) == GuildPermission.Leader ||
                             (guild.Permission & GuildPermission.ManageTerritory) == GuildPermission.ManageTerritory;

            if (guild.TerritoryIndex > 0 && guild.TerritoryRemaining > System.TimeSpan.Zero)
            {
                StatusLabel.Text = $"Lease: {guild.TerritoryName}\nRemaining: {FormatRemaining(guild.TerritoryRemaining)}";
                EnterButton.Enabled = true;
                RenewButton.Enabled = canManage;
                RentButton.Enabled = false;
            }
            else
            {
                StatusLabel.Text = "No active territory lease.";
                EnterButton.Enabled = false;
                RenewButton.Enabled = false;
                RentButton.Enabled = canManage && TerritoryBox.SelectedItem is GuildTerritoryInfo;
            }
        }

        private static string FormatRemaining(System.TimeSpan span)
        {
            if (span.TotalDays >= 1)
                return $"{(int)span.TotalDays}d {span.Hours}h";
            if (span.TotalHours >= 1)
                return $"{(int)span.TotalHours}h {span.Minutes}m";
            return $"{span.Minutes}m";
        }
    }
}
