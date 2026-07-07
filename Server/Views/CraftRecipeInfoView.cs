using DevExpress.XtraBars;
using Library.SystemModels;
using System;

namespace Server.Views
{
    public partial class CraftRecipeInfoView : DevExpress.XtraBars.Ribbon.RibbonForm
    {
        public CraftRecipeInfoView()
        {
            InitializeComponent();

            CraftRecipeInfoGridControl.DataSource = SMain.Session.GetCollection<CraftRecipeInfo>().Binding;

            ResultItemLookUpEdit.DataSource = SMain.Session.GetCollection<ItemInfo>().Binding;
            IngredientItemLookUpEdit.DataSource = SMain.Session.GetCollection<ItemInfo>().Binding;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            SMain.SetUpView(CraftRecipeInfoGridView);
            SMain.SetUpView(CraftRecipeIngredientInfoGridView);
        }

        private void SaveDatabaseButton_ItemClick(object sender, ItemClickEventArgs e)
        {
            SMain.Session.Save(true);
        }

        private void ImportButton_ItemClick(object sender, ItemClickEventArgs e)
        {
            JsonImporter.Import<CraftRecipeInfo>();
        }

        private void ExportButton_ItemClick(object sender, ItemClickEventArgs e)
        {
            JsonExporter.Export<CraftRecipeInfo>(CraftRecipeInfoGridView);
        }
    }
}
