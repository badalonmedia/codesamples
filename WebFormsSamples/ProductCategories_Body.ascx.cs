namespace VNWeb.UserControls {
  using System;
  using System.Data;
  using System.Drawing;
  using System.Web;
  using System.Web.UI.WebControls;
  using System.Web.UI.HtmlControls;
  using System.Collections;
  using System.Configuration;

  using VNWeb.Components;
  using VNWeb.Components.Data;

  /// <summary>
  ///		Summary description for ProductCategories_Body.
  /// </summary>
  public class ProductCategories_Body : SuperControl {
    private bool m_bShowMoreLink = false;
    private SearchEngineType m_objSearchEngine = null;

    public SearchEngineType SearchEngine {
      get {
        return (m_objSearchEngine);
      }
      set {
        m_objSearchEngine = value;
      }
    }

    //protected ProductCategories_Inner m_ctlInnerTest;

    public bool ShowMoreLink {
      get {
        return (m_bShowMoreLink);
      }
      set {
        m_bShowMoreLink = value;
      }
    }

    private void Page_Load(object sender, System.EventArgs e) {
      //string sCatAppend = " ===>";

      try {
        if (this.Visible) {
          InitControl();

          CategoryCollectionType collCat = new CategoryCollectionType();
          IEnumerator objEnum;
          ProductCategories_Inner ctlInner;

          collCat.MaxItems = AppConstants.CATEGORY_TREE_MAX_CATS;  //Convert.ToInt32(ConfigurationSettings.AppSettings[AppConstants.CONFIG_UI_CATEGORY_TREE_MAX_CATS]);

          //collCat.Load(CategoryModeType.LEFT);		//Load category info for only 1 top level categories
          collCat.Load(true);

          //TEST
          //m_ctlInnerTest.ParentCategory = (CategoryType)collCat.TheData[0];

          //12/4/05: Adding a 2nd pass to handle the Current Category, i.e. to put it first.

          objEnum = collCat.TheData.GetEnumerator();
          int iCtlCount = 0;
          CategoryType objCat = null;
          bool bAddCat = false;

          while (objEnum.MoveNext()) {
            objCat = (CategoryType) objEnum.Current;

            if (m_objSearchEngine != null)  //Will be null on Home page for example.
            {
              if (objCat.CategoryId == m_objSearchEngine.Criteria.CategoryId)
                bAddCat = true;
            }

            //objCat.CategoryName = objCat.CategoryName.Replace(sCatAppend, string.Empty);	//A little cheesy

            if (bAddCat) {
              ctlInner = (ProductCategories_Inner) LoadControl("ProductCategories_Inner.ASCX");
              ctlInner.ID = "m_ctlCategoryInner_" + iCtlCount.ToString();  //Does not help
              ctlInner.EnableViewState = true;
              ctlInner.ParentCategory = objCat;

              ctlInner.SearchEngine = m_objSearchEngine;

              ctlInner.ShowSepLine = (iCtlCount > 0);

              ctlInner.ShowNumCategoryProducts = false;  //12/4/05: Added

              Controls.Add(ctlInner);
              iCtlCount++;

              bAddCat = false;
            }
          }

          //Don't add "Show More" link

          //Now do second (original) pass to handle rest of categories.

          //objEnum = collCat.TheData.GetEnumerator();

          objEnum.Reset();
          bAddCat = false;

          while (objEnum.MoveNext()) {
            objCat = (CategoryType) objEnum.Current;

            if (m_objSearchEngine == null)
              bAddCat = true;
            else if (objCat.CategoryId != m_objSearchEngine.Criteria.CategoryId)
              bAddCat = true;

            if (bAddCat) {
              ctlInner = (ProductCategories_Inner) LoadControl("ProductCategories_Inner.ASCX");
              ctlInner.ID = "m_ctlCategoryInner_" + iCtlCount.ToString();  //Does not help
              ctlInner.EnableViewState = true;
              ctlInner.ParentCategory = objCat;

              ctlInner.SearchEngine = m_objSearchEngine;

              ctlInner.ShowSepLine = (iCtlCount > 0);

              Controls.Add(ctlInner);
              iCtlCount++;

              bAddCat = false;
            }
          }

          //this.SaveViewState();

          //Now check to see if <More...> link needs to be displayed.
          if (collCat.MaxItems < collCat.TheData.ActualItems)
            m_bShowMoreLink = true;

          collCat.Dispose();
          collCat = null;
        }

      } catch (Exception objEx) {
        VTNBSAppExceptionHandler.LogException(new VTNBSAppException(objEx), ExceptionModeType.NOT_SILENT);
      }
    }

#region Web Form Designer generated code
    override protected void OnInit(EventArgs e) {
      //
      // CODEGEN: This call is required by the ASP.NET Web Form Designer.
      //
      InitializeComponent();
      base.OnInit(e);
    }

    /// <summary>
    ///		Required method for Designer support - do not modify
    ///		the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent() {
      this.Load += new System.EventHandler(this.Page_Load);
    }
#endregion
  }
}
