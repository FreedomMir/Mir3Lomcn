namespace Server.Views
{
    partial class CraftRecipeInfoView
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            DevExpress.XtraGrid.GridLevelNode gridLevelNode1 = new DevExpress.XtraGrid.GridLevelNode();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CraftRecipeInfoView));
            CraftRecipeIngredientInfoGridView = new DevExpress.XtraGrid.Views.Grid.GridView();
            gridColumnIngredientItem = new DevExpress.XtraGrid.Columns.GridColumn();
            IngredientItemLookUpEdit = new DevExpress.XtraEditors.Repository.RepositoryItemLookUpEdit();
            gridColumnIngredientAmount = new DevExpress.XtraGrid.Columns.GridColumn();
            CraftRecipeInfoGridControl = new DevExpress.XtraGrid.GridControl();
            CraftRecipeInfoGridView = new DevExpress.XtraGrid.Views.Grid.GridView();
            gridColumnIndex = new DevExpress.XtraGrid.Columns.GridColumn();
            gridColumnDescription = new DevExpress.XtraGrid.Columns.GridColumn();
            gridColumnResultItem = new DevExpress.XtraGrid.Columns.GridColumn();
            ResultItemLookUpEdit = new DevExpress.XtraEditors.Repository.RepositoryItemLookUpEdit();
            gridColumnResultAmount = new DevExpress.XtraGrid.Columns.GridColumn();
            gridColumnGoldCost = new DevExpress.XtraGrid.Columns.GridColumn();
            gridColumnSuccessChance = new DevExpress.XtraGrid.Columns.GridColumn();
            gridColumnRequiredLevel = new DevExpress.XtraGrid.Columns.GridColumn();
            gridColumnRequiredClass = new DevExpress.XtraGrid.Columns.GridColumn();
            gridColumnCategory = new DevExpress.XtraGrid.Columns.GridColumn();
            gridColumnEnabled = new DevExpress.XtraGrid.Columns.GridColumn();
            ribbon = new DevExpress.XtraBars.Ribbon.RibbonControl();
            SaveDatabaseButton = new DevExpress.XtraBars.BarButtonItem();
            ImportButton = new DevExpress.XtraBars.BarButtonItem();
            ExportButton = new DevExpress.XtraBars.BarButtonItem();
            ribbonPage1 = new DevExpress.XtraBars.Ribbon.RibbonPage();
            ribbonPageGroup1 = new DevExpress.XtraBars.Ribbon.RibbonPageGroup();
            JsonImportExport = new DevExpress.XtraBars.Ribbon.RibbonPageGroup();
            ((System.ComponentModel.ISupportInitialize)CraftRecipeIngredientInfoGridView).BeginInit();
            ((System.ComponentModel.ISupportInitialize)IngredientItemLookUpEdit).BeginInit();
            ((System.ComponentModel.ISupportInitialize)CraftRecipeInfoGridControl).BeginInit();
            ((System.ComponentModel.ISupportInitialize)CraftRecipeInfoGridView).BeginInit();
            ((System.ComponentModel.ISupportInitialize)ResultItemLookUpEdit).BeginInit();
            ((System.ComponentModel.ISupportInitialize)ribbon).BeginInit();
            SuspendLayout();
            // 
            // CraftRecipeIngredientInfoGridView
            // 
            CraftRecipeIngredientInfoGridView.Columns.AddRange(new DevExpress.XtraGrid.Columns.GridColumn[] { gridColumnIngredientItem, gridColumnIngredientAmount });
            CraftRecipeIngredientInfoGridView.GridControl = CraftRecipeInfoGridControl;
            CraftRecipeIngredientInfoGridView.Name = "CraftRecipeIngredientInfoGridView";
            CraftRecipeIngredientInfoGridView.OptionsView.EnableAppearanceEvenRow = true;
            CraftRecipeIngredientInfoGridView.OptionsView.EnableAppearanceOddRow = true;
            CraftRecipeIngredientInfoGridView.OptionsView.NewItemRowPosition = DevExpress.XtraGrid.Views.Grid.NewItemRowPosition.Top;
            CraftRecipeIngredientInfoGridView.OptionsView.ShowButtonMode = DevExpress.XtraGrid.Views.Base.ShowButtonModeEnum.ShowAlways;
            CraftRecipeIngredientInfoGridView.OptionsView.ShowGroupPanel = false;
            // 
            // gridColumnIngredientItem
            // 
            gridColumnIngredientItem.Caption = "Item";
            gridColumnIngredientItem.ColumnEdit = IngredientItemLookUpEdit;
            gridColumnIngredientItem.FieldName = "Item";
            gridColumnIngredientItem.Name = "gridColumnIngredientItem";
            gridColumnIngredientItem.Visible = true;
            gridColumnIngredientItem.VisibleIndex = 0;
            // 
            // IngredientItemLookUpEdit
            // 
            IngredientItemLookUpEdit.AutoHeight = false;
            IngredientItemLookUpEdit.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] { new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo) });
            IngredientItemLookUpEdit.Columns.AddRange(new DevExpress.XtraEditors.Controls.LookUpColumnInfo[] { new DevExpress.XtraEditors.Controls.LookUpColumnInfo("Index", "Index"), new DevExpress.XtraEditors.Controls.LookUpColumnInfo("ItemName", "Item Name"), new DevExpress.XtraEditors.Controls.LookUpColumnInfo("ItemType", "Item Type") });
            IngredientItemLookUpEdit.DisplayMember = "ItemName";
            IngredientItemLookUpEdit.Name = "IngredientItemLookUpEdit";
            IngredientItemLookUpEdit.NullText = "[Item is null]";
            // 
            // gridColumnIngredientAmount
            // 
            gridColumnIngredientAmount.Caption = "Amount";
            gridColumnIngredientAmount.FieldName = "Amount";
            gridColumnIngredientAmount.Name = "gridColumnIngredientAmount";
            gridColumnIngredientAmount.Visible = true;
            gridColumnIngredientAmount.VisibleIndex = 1;
            // 
            // CraftRecipeInfoGridControl
            // 
            CraftRecipeInfoGridControl.Dock = System.Windows.Forms.DockStyle.Fill;
            gridLevelNode1.LevelTemplate = CraftRecipeIngredientInfoGridView;
            gridLevelNode1.RelationName = "Ingredients";
            CraftRecipeInfoGridControl.LevelTree.Nodes.AddRange(new DevExpress.XtraGrid.GridLevelNode[] { gridLevelNode1 });
            CraftRecipeInfoGridControl.Location = new System.Drawing.Point(0, 144);
            CraftRecipeInfoGridControl.MainView = CraftRecipeInfoGridView;
            CraftRecipeInfoGridControl.MenuManager = ribbon;
            CraftRecipeInfoGridControl.Name = "CraftRecipeInfoGridControl";
            CraftRecipeInfoGridControl.RepositoryItems.AddRange(new DevExpress.XtraEditors.Repository.RepositoryItem[] { ResultItemLookUpEdit, IngredientItemLookUpEdit });
            CraftRecipeInfoGridControl.Size = new System.Drawing.Size(900, 376);
            CraftRecipeInfoGridControl.TabIndex = 1;
            CraftRecipeInfoGridControl.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] { CraftRecipeInfoGridView, CraftRecipeIngredientInfoGridView });
            // 
            // CraftRecipeInfoGridView
            // 
            CraftRecipeInfoGridView.Columns.AddRange(new DevExpress.XtraGrid.Columns.GridColumn[] { gridColumnIndex, gridColumnDescription, gridColumnResultItem, gridColumnResultAmount, gridColumnGoldCost, gridColumnSuccessChance, gridColumnRequiredLevel, gridColumnRequiredClass, gridColumnCategory, gridColumnEnabled });
            CraftRecipeInfoGridView.GridControl = CraftRecipeInfoGridControl;
            CraftRecipeInfoGridView.Name = "CraftRecipeInfoGridView";
            CraftRecipeInfoGridView.OptionsDetail.AllowExpandEmptyDetails = true;
            CraftRecipeInfoGridView.OptionsView.NewItemRowPosition = DevExpress.XtraGrid.Views.Grid.NewItemRowPosition.Top;
            CraftRecipeInfoGridView.OptionsView.ShowButtonMode = DevExpress.XtraGrid.Views.Base.ShowButtonModeEnum.ShowAlways;
            CraftRecipeInfoGridView.OptionsView.ShowGroupPanel = false;
            // 
            // gridColumnIndex
            // 
            gridColumnIndex.FieldName = "Index";
            gridColumnIndex.Name = "gridColumnIndex";
            gridColumnIndex.Visible = true;
            gridColumnIndex.VisibleIndex = 0;
            // 
            // gridColumnDescription
            // 
            gridColumnDescription.FieldName = "Description";
            gridColumnDescription.Name = "gridColumnDescription";
            gridColumnDescription.Visible = true;
            gridColumnDescription.VisibleIndex = 1;
            // 
            // gridColumnResultItem
            // 
            gridColumnResultItem.Caption = "Result Item";
            gridColumnResultItem.ColumnEdit = ResultItemLookUpEdit;
            gridColumnResultItem.FieldName = "ResultItem";
            gridColumnResultItem.Name = "gridColumnResultItem";
            gridColumnResultItem.Visible = true;
            gridColumnResultItem.VisibleIndex = 2;
            // 
            // ResultItemLookUpEdit
            // 
            ResultItemLookUpEdit.AutoHeight = false;
            ResultItemLookUpEdit.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] { new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo) });
            ResultItemLookUpEdit.Columns.AddRange(new DevExpress.XtraEditors.Controls.LookUpColumnInfo[] { new DevExpress.XtraEditors.Controls.LookUpColumnInfo("Index", "Index"), new DevExpress.XtraEditors.Controls.LookUpColumnInfo("ItemName", "Item Name"), new DevExpress.XtraEditors.Controls.LookUpColumnInfo("ItemType", "Item Type") });
            ResultItemLookUpEdit.DisplayMember = "ItemName";
            ResultItemLookUpEdit.Name = "ResultItemLookUpEdit";
            ResultItemLookUpEdit.NullText = "[Item is null]";
            // 
            // gridColumnResultAmount
            // 
            gridColumnResultAmount.Caption = "Result Amount";
            gridColumnResultAmount.FieldName = "ResultAmount";
            gridColumnResultAmount.Name = "gridColumnResultAmount";
            gridColumnResultAmount.Visible = true;
            gridColumnResultAmount.VisibleIndex = 3;
            // 
            // gridColumnGoldCost
            // 
            gridColumnGoldCost.Caption = "Gold Cost";
            gridColumnGoldCost.FieldName = "GoldCost";
            gridColumnGoldCost.Name = "gridColumnGoldCost";
            gridColumnGoldCost.Visible = true;
            gridColumnGoldCost.VisibleIndex = 4;
            // 
            // gridColumnSuccessChance
            // 
            gridColumnSuccessChance.Caption = "Success %";
            gridColumnSuccessChance.FieldName = "SuccessChance";
            gridColumnSuccessChance.Name = "gridColumnSuccessChance";
            gridColumnSuccessChance.Visible = true;
            gridColumnSuccessChance.VisibleIndex = 5;
            // 
            // gridColumnRequiredLevel
            // 
            gridColumnRequiredLevel.Caption = "Required Level";
            gridColumnRequiredLevel.FieldName = "RequiredLevel";
            gridColumnRequiredLevel.Name = "gridColumnRequiredLevel";
            gridColumnRequiredLevel.Visible = true;
            gridColumnRequiredLevel.VisibleIndex = 6;
            // 
            // gridColumnRequiredClass
            // 
            gridColumnRequiredClass.Caption = "Required Class";
            gridColumnRequiredClass.FieldName = "RequiredClass";
            gridColumnRequiredClass.Name = "gridColumnRequiredClass";
            gridColumnRequiredClass.Visible = true;
            gridColumnRequiredClass.VisibleIndex = 7;
            // 
            // gridColumnCategory
            // 
            gridColumnCategory.FieldName = "Category";
            gridColumnCategory.Name = "gridColumnCategory";
            gridColumnCategory.Visible = true;
            gridColumnCategory.VisibleIndex = 8;
            // 
            // gridColumnEnabled
            // 
            gridColumnEnabled.FieldName = "Enabled";
            gridColumnEnabled.Name = "gridColumnEnabled";
            gridColumnEnabled.Visible = true;
            gridColumnEnabled.VisibleIndex = 9;
            // 
            // ribbon
            // 
            ribbon.ExpandCollapseItem.Id = 0;
            ribbon.Items.AddRange(new DevExpress.XtraBars.BarItem[] { ribbon.ExpandCollapseItem, ribbon.SearchEditItem, SaveDatabaseButton, ImportButton, ExportButton });
            ribbon.Location = new System.Drawing.Point(0, 0);
            ribbon.MaxItemId = 4;
            ribbon.Name = "ribbon";
            ribbon.Pages.AddRange(new DevExpress.XtraBars.Ribbon.RibbonPage[] { ribbonPage1 });
            ribbon.Size = new System.Drawing.Size(900, 144);
            // 
            // SaveDatabaseButton
            // 
            SaveDatabaseButton.Caption = "Save Database";
            SaveDatabaseButton.Id = 1;
            SaveDatabaseButton.ImageOptions.Image = (System.Drawing.Image)resources.GetObject("SaveDatabaseButton.ImageOptions.Image");
            SaveDatabaseButton.ImageOptions.LargeImage = (System.Drawing.Image)resources.GetObject("SaveDatabaseButton.ImageOptions.LargeImage");
            SaveDatabaseButton.LargeWidth = 60;
            SaveDatabaseButton.Name = "SaveDatabaseButton";
            SaveDatabaseButton.ItemClick += SaveDatabaseButton_ItemClick;
            // 
            // ImportButton
            // 
            ImportButton.Caption = "Import";
            ImportButton.Id = 2;
            ImportButton.ImageOptions.Image = (System.Drawing.Image)resources.GetObject("ImportButton.ImageOptions.Image");
            ImportButton.ImageOptions.LargeImage = (System.Drawing.Image)resources.GetObject("ImportButton.ImageOptions.LargeImage");
            ImportButton.Name = "ImportButton";
            ImportButton.ItemClick += ImportButton_ItemClick;
            // 
            // ExportButton
            // 
            ExportButton.Caption = "Export";
            ExportButton.Id = 3;
            ExportButton.ImageOptions.Image = (System.Drawing.Image)resources.GetObject("ExportButton.ImageOptions.Image");
            ExportButton.ImageOptions.LargeImage = (System.Drawing.Image)resources.GetObject("ExportButton.ImageOptions.LargeImage");
            ExportButton.Name = "ExportButton";
            ExportButton.ItemClick += ExportButton_ItemClick;
            // 
            // ribbonPage1
            // 
            ribbonPage1.Groups.AddRange(new DevExpress.XtraBars.Ribbon.RibbonPageGroup[] { ribbonPageGroup1, JsonImportExport });
            ribbonPage1.Name = "ribbonPage1";
            ribbonPage1.Text = "Home";
            // 
            // ribbonPageGroup1
            // 
            ribbonPageGroup1.AllowTextClipping = false;
            ribbonPageGroup1.CaptionButtonVisible = DevExpress.Utils.DefaultBoolean.False;
            ribbonPageGroup1.ItemLinks.Add(SaveDatabaseButton);
            ribbonPageGroup1.Name = "ribbonPageGroup1";
            ribbonPageGroup1.Text = "Saving";
            // 
            // JsonImportExport
            // 
            JsonImportExport.ItemLinks.Add(ImportButton);
            JsonImportExport.ItemLinks.Add(ExportButton);
            JsonImportExport.Name = "JsonImportExport";
            JsonImportExport.Text = "Json";
            // 
            // CraftRecipeInfoView
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(900, 520);
            Controls.Add(CraftRecipeInfoGridControl);
            Controls.Add(ribbon);
            Name = "CraftRecipeInfoView";
            Ribbon = ribbon;
            Text = "Craft Recipe Info";
            ((System.ComponentModel.ISupportInitialize)CraftRecipeIngredientInfoGridView).EndInit();
            ((System.ComponentModel.ISupportInitialize)IngredientItemLookUpEdit).EndInit();
            ((System.ComponentModel.ISupportInitialize)CraftRecipeInfoGridControl).EndInit();
            ((System.ComponentModel.ISupportInitialize)CraftRecipeInfoGridView).EndInit();
            ((System.ComponentModel.ISupportInitialize)ResultItemLookUpEdit).EndInit();
            ((System.ComponentModel.ISupportInitialize)ribbon).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private DevExpress.XtraBars.Ribbon.RibbonControl ribbon;
        private DevExpress.XtraBars.Ribbon.RibbonPage ribbonPage1;
        private DevExpress.XtraBars.Ribbon.RibbonPageGroup ribbonPageGroup1;
        private DevExpress.XtraBars.BarButtonItem SaveDatabaseButton;
        private DevExpress.XtraGrid.GridControl CraftRecipeInfoGridControl;
        private DevExpress.XtraGrid.Views.Grid.GridView CraftRecipeInfoGridView;
        private DevExpress.XtraBars.BarButtonItem ImportButton;
        private DevExpress.XtraBars.BarButtonItem ExportButton;
        private DevExpress.XtraBars.Ribbon.RibbonPageGroup JsonImportExport;
        private DevExpress.XtraGrid.Views.Grid.GridView CraftRecipeIngredientInfoGridView;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumnIngredientItem;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumnIngredientAmount;
        private DevExpress.XtraEditors.Repository.RepositoryItemLookUpEdit IngredientItemLookUpEdit;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumnIndex;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumnDescription;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumnResultItem;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumnResultAmount;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumnGoldCost;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumnSuccessChance;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumnRequiredLevel;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumnRequiredClass;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumnCategory;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumnEnabled;
        private DevExpress.XtraEditors.Repository.RepositoryItemLookUpEdit ResultItemLookUpEdit;
    }
}
