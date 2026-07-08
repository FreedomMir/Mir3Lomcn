using System.Drawing;
using System.Windows.Forms;
using Client.Controls;
using Client.Scenes.Automation;
using Client.UserModels;
using Library;

namespace Client.Scenes.Views;

public sealed class AutoPlayDialog : DXWindow
{
	private const int ClientWidth = 300;

	private const int ClientHeight = 360;

	private DXTabControl TabControl;

	private DXTab CombatTab;

	private DXTab ItemTab;

	private DXTab TownTab;

	public DXNumberTextBox MaxDeathBox;

	public DXCheckBox KiteCheck;

	public DXCheckBox ReturnDeathCheck;

	public DXCheckBox LootCheck;

	public DXCheckBox HarvestCheck;

	public DXCheckBox EquipTorchCheck;

	public DXCheckBox EquipPoisonCheck;

	public DXCheckBox EquipTalismanCheck;

	public DXCheckBox TownBagFullCheck;

	public DXCheckBox TownHealthCheck;

	public DXCheckBox TownManaCheck;

	public DXCheckBox TownPoisonCheck;

	public DXCheckBox TownTalismanCheck;

	public DXCheckBox TownBrokenCheck;

	public DXCheckBox SellCheck;

	public DXComboBox SellModeBox;

	public DXCheckBox RepairCheck;

	public DXCheckBox SpecialRepairCheck;

	public DXCheckBox BuyCheck;

	private bool _updating;

	public override WindowType Type => WindowType.AutoPlayBox;

	public override bool CustomSize => false;

	public override bool AutomaticVisibility => true;

	public AutoPlayDialog()
	{
		base.TitleLabel.Text = "Auto Play Settings";
		base.HasFooter = false;
		SetClientSize(new Size(ClientWidth, ClientHeight));
		TabControl = new DXTabControl
		{
			Parent = this,
			Location = base.ClientArea.Location,
			Size = base.ClientArea.Size
		};
		CombatTab = NewTab("Combat");
		ItemTab = NewTab("Item");
		TownTab = NewTab("Town");
		TabControl.SelectedTab = CombatTab;
		int num = 8;
		KiteCheck = AddCheck(CombatTab, "Kite Monsters (Wizard / Taoist)", num);
		num += 24;
		ReturnDeathCheck = AddCheck(CombatTab, "Return on Death", num);
		num += 24;
		MaxDeathBox = AddNumber(CombatTab, "Max Death Returns", num, 0, 20);
		num += 24;
		num = 8;
		LootCheck = AddCheck(ItemTab, "Can Pick Up", num);
		num += 24;
		HarvestCheck = AddCheck(ItemTab, "Auto Harvest", num);
		num += 24;
		EquipTorchCheck = AddCheck(ItemTab, "Auto Torch", num);
		num += 24;
		EquipPoisonCheck = AddCheck(ItemTab, "Auto Poison", num);
		num += 24;
		EquipTalismanCheck = AddCheck(ItemTab, "Auto Talisman", num);
		num += 24;
		num = 8;
		num = AddSubHeader(TownTab, "Go To Town When", num);
		TownBagFullCheck = AddCheck(TownTab, "Bag Full / Overweight", num);
		num += 24;
		TownHealthCheck = AddCheck(TownTab, "No Health Potions", num);
		num += 24;
		TownManaCheck = AddCheck(TownTab, "No Mana Potions", num);
		num += 24;
		TownPoisonCheck = AddCheck(TownTab, "No Poisons", num);
		num += 24;
		TownTalismanCheck = AddCheck(TownTab, "No Talismans", num);
		num += 24;
		TownBrokenCheck = AddCheck(TownTab, "Equipment Broken", num);
		num += 24;
		num += 8;
		num = AddSubHeader(TownTab, "Town Actions", num);
		SellCheck = AddCheck(TownTab, "Can Auto Sell", num);
		num += 24;
		SellModeBox = AddSellMode(TownTab, "Sell Mode", num);
		num += 24;
		RepairCheck = AddCheck(TownTab, "Can Auto Repair", num);
		num += 24;
		SpecialRepairCheck = AddCheck(TownTab, "Should Special Repair", num);
		num += 24;
		BuyCheck = AddCheck(TownTab, "Can Auto Buy", num);
		num += 24;
	}

	private DXTab NewTab(string text)
	{
		DXTab dXTab = new DXTab();
		dXTab.Parent = TabControl;
		dXTab.Border = true;
		dXTab.TabButton.Label.Text = text;
		return dXTab;
	}

	private int AddSubHeader(DXControl parent, string text, int y)
	{
		new DXLabel
		{
			Parent = parent,
			Text = text,
			ForeColour = Color.FromArgb(198, 166, 99),
			Location = new Point(8, y + 2)
		};
		return y + 24;
	}

	private DXCheckBox AddCheck(DXControl parent, string text, int y)
	{
		DXCheckBox dXCheckBox = new DXCheckBox();
		dXCheckBox.Parent = parent;
		dXCheckBox.Location = new Point(14, y + 4);
		dXCheckBox.Label.AutoSize = false;
		dXCheckBox.Label.Size = new Size(210, 18);
		dXCheckBox.Label.DrawFormat |= TextFormatFlags.VerticalCenter;
		dXCheckBox.Label.Text = text;
		dXCheckBox.CheckedChanged += delegate
		{
			SendUpdate();
		};
		return dXCheckBox;
	}

	private DXComboBox AddSellMode(DXControl parent, string text, int y)
	{
		new DXLabel
		{
			Parent = parent,
			Text = text,
			Location = new Point(14, y + 4)
		};

		DXComboBox box = new DXComboBox
		{
			Parent = parent,
			Location = new Point(110, y + 2),
			Size = new Size(166, DXComboBox.DefaultNormalHeight),
			DropDownHeight = 90
		};

		new DXListBoxItem
		{
			Parent = box.ListBox,
			Label = { Text = "Never" },
			Item = AutoSellMode.Never
		};
		new DXListBoxItem
		{
			Parent = box.ListBox,
			Label = { Text = "Normal Only" },
			Item = AutoSellMode.NormalOnly
		};
		new DXListBoxItem
		{
			Parent = box.ListBox,
			Label = { Text = "All Unlocked" },
			Item = AutoSellMode.All
		};

		box.ListBox.SelectItem(AutoSellMode.NormalOnly);
		box.SelectedItemChanged += (o, e) => SendUpdate();
		return box;
	}

	private DXNumberTextBox AddNumber(DXControl parent, string text, int y, int min, int max)
	{
		new DXLabel
		{
			Parent = parent,
			Text = text,
			Location = new Point(14, y + 4)
		};
		DXNumberTextBox dXNumberTextBox = new DXNumberTextBox();
		dXNumberTextBox.Parent = parent;
		dXNumberTextBox.Size = new Size(56, 20);
		dXNumberTextBox.MinValue = min;
		dXNumberTextBox.MaxValue = max;
		dXNumberTextBox.DrawTexture = true;
		dXNumberTextBox.BackColour = Color.FromArgb(30, 30, 30);
		dXNumberTextBox.ForeColour = Color.White;
		dXNumberTextBox.Location = new Point(220, y + 2);
		dXNumberTextBox.ValueChanged += delegate
		{
			SendUpdate();
		};
		return dXNumberTextBox;
	}

	public override void OnIsVisibleChanged(bool oValue, bool nValue)
	{
		base.OnIsVisibleChanged(oValue, nValue);
		if (base.IsVisible)
		{
			UpdateValues();
		}
	}

	public void UpdateValues()
	{
		ClientAutoPlaySettings clientAutoPlaySettings = GameScene.Game.AutoPlay?.Settings;
		if (clientAutoPlaySettings != null)
		{
			_updating = true;
			KiteCheck.Checked = clientAutoPlaySettings.KiteEnabled;
			ReturnDeathCheck.Checked = clientAutoPlaySettings.ReturnOnDeath;
			MaxDeathBox.Value = clientAutoPlaySettings.MaxDeathReturns;
			LootCheck.Checked = clientAutoPlaySettings.LootEnabled;
			HarvestCheck.Checked = clientAutoPlaySettings.HarvestEnabled;
			EquipTorchCheck.Checked = clientAutoPlaySettings.AutoEquipTorch;
			EquipPoisonCheck.Checked = clientAutoPlaySettings.AutoEquipPoison;
			EquipTalismanCheck.Checked = clientAutoPlaySettings.AutoEquipTalisman;
			TownBagFullCheck.Checked = clientAutoPlaySettings.TownOnBagFull;
			TownHealthCheck.Checked = clientAutoPlaySettings.TownOnNoHealthPotion;
			TownManaCheck.Checked = clientAutoPlaySettings.TownOnNoManaPotion;
			TownPoisonCheck.Checked = clientAutoPlaySettings.TownOnNoPoison;
			TownTalismanCheck.Checked = clientAutoPlaySettings.TownOnNoTalisman;
			TownBrokenCheck.Checked = clientAutoPlaySettings.TownOnEquipmentBroken;
			SellCheck.Checked = clientAutoPlaySettings.CanAutoSell;
			SellModeBox?.ListBox?.SelectItem(clientAutoPlaySettings.SellMode);
			RepairCheck.Checked = clientAutoPlaySettings.CanAutoRepair;
			SpecialRepairCheck.Checked = clientAutoPlaySettings.ShouldSpecialRepair;
			BuyCheck.Checked = clientAutoPlaySettings.CanAutoBuy;
			_updating = false;
		}
	}

	private void SendUpdate()
	{
		if (!_updating)
		{
			AutoPlayer autoPlay = GameScene.Game.AutoPlay;
			if (autoPlay != null)
			{
				ClientAutoPlaySettings settings = autoPlay.Settings;
				settings.KiteEnabled = KiteCheck.Checked;
				settings.ReturnOnDeath = ReturnDeathCheck.Checked;
				settings.MaxDeathReturns = (int)MaxDeathBox.Value;
				settings.LootEnabled = LootCheck.Checked;
				settings.HarvestEnabled = HarvestCheck.Checked;
				settings.AutoEquipTorch = EquipTorchCheck.Checked;
				settings.AutoEquipPoison = EquipPoisonCheck.Checked;
				settings.AutoEquipTalisman = EquipTalismanCheck.Checked;
				settings.TownOnBagFull = TownBagFullCheck.Checked;
				settings.TownOnNoHealthPotion = TownHealthCheck.Checked;
				settings.TownOnNoManaPotion = TownManaCheck.Checked;
				settings.TownOnNoPoison = TownPoisonCheck.Checked;
				settings.TownOnNoTalisman = TownTalismanCheck.Checked;
				settings.TownOnEquipmentBroken = TownBrokenCheck.Checked;
				settings.CanAutoSell = SellCheck.Checked;
				if (SellModeBox?.SelectedItem is AutoSellMode mode)
					settings.SellMode = mode;
				settings.CanAutoRepair = RepairCheck.Checked;
				settings.ShouldSpecialRepair = SpecialRepairCheck.Checked;
				settings.CanAutoBuy = BuyCheck.Checked;
				autoPlay.SendSettingsUpdate();
			}
		}
	}
}
