using DevExpress.XtraBars;
using DevExpress.XtraBars.Ribbon;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using Library.SystemModels;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Server.Views
{
    public class GuildTerritoryInfoView : DevExpress.XtraBars.Ribbon.RibbonForm
    {
        private readonly GridControl _grid;
        private readonly GridView _view;
        private readonly RepositoryItemLookUpEdit _instanceLookUp;

        public GuildTerritoryInfoView()
        {
            Text = "Guild Territory Info";
            Size = new Size(900, 500);

            var ribbon = new RibbonControl { Dock = DockStyle.Top };
            var page = new RibbonPage("Home");
            var group = new RibbonPageGroup("Database");
            var save = new BarButtonItem { Caption = "Save Database" };
            save.ItemClick += (s, e) => SMain.Session.Save(true);
            var import = new BarButtonItem { Caption = "Import" };
            import.ItemClick += (s, e) => JsonImporter.Import<GuildTerritoryInfo>();
            var export = new BarButtonItem { Caption = "Export" };
            export.ItemClick += (s, e) => JsonExporter.Export<GuildTerritoryInfo>(_view);
            group.ItemLinks.Add(save);
            group.ItemLinks.Add(import);
            group.ItemLinks.Add(export);
            page.Groups.Add(group);
            ribbon.Pages.Add(page);
            ribbon.Items.Add(save);
            ribbon.Items.Add(import);
            ribbon.Items.Add(export);

            // Match NPCPage / MovementInfo Instance lookups (object reference, no ValueMember).
            _instanceLookUp = new RepositoryItemLookUpEdit
            {
                AutoHeight = false,
                BestFitMode = BestFitMode.BestFitResizePopup,
                DisplayMember = "Name",
                NullText = "[Instance is null]",
            };
            _instanceLookUp.Buttons.AddRange(new[] { new EditorButton(ButtonPredefines.Combo) });
            _instanceLookUp.Columns.AddRange(new[]
            {
                new LookUpColumnInfo("Index", "Index"),
                new LookUpColumnInfo("Name", "Name"),
                new LookUpColumnInfo("Type", "Type"),
            });
            _instanceLookUp.DataSource = SMain.Session.GetCollection<InstanceInfo>().Binding;

            _grid = new GridControl { Dock = DockStyle.Fill };
            _view = new GridView(_grid);
            _grid.MainView = _view;
            _grid.RepositoryItems.Add(_instanceLookUp);
            _grid.DataSource = SMain.Session.GetCollection<GuildTerritoryInfo>().Binding;

            _view.OptionsView.ShowGroupPanel = false;
            _view.OptionsView.NewItemRowPosition = NewItemRowPosition.Top;
            _view.OptionsView.ShowButtonMode = DevExpress.XtraGrid.Views.Base.ShowButtonModeEnum.ShowAlways;
            _view.Columns.Add(new GridColumn { FieldName = "Name", Caption = "Name", Visible = true, VisibleIndex = 0, Width = 140 });
            _view.Columns.Add(new GridColumn { FieldName = "Instance", Caption = "Instance", Visible = true, VisibleIndex = 1, Width = 200, ColumnEdit = _instanceLookUp });
            _view.Columns.Add(new GridColumn { FieldName = "RentCost", Caption = "Rent Cost", Visible = true, VisibleIndex = 2, Width = 100 });
            _view.Columns.Add(new GridColumn { FieldName = "RenewCost", Caption = "Renew Cost", Visible = true, VisibleIndex = 3, Width = 100 });
            _view.Columns.Add(new GridColumn { FieldName = "Duration", Caption = "Duration", Visible = true, VisibleIndex = 4, Width = 100 });
            _view.Columns.Add(new GridColumn { FieldName = "MinGuildLevel", Caption = "Min Guild Level", Visible = true, VisibleIndex = 5, Width = 100 });
            _view.Columns.Add(new GridColumn { FieldName = "Enabled", Caption = "Enabled", Visible = true, VisibleIndex = 6, Width = 70 });

            Controls.Add(_grid);
            Controls.Add(ribbon);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            SMain.SetUpView(_view);
        }
    }
}
