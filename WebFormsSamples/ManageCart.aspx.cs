using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Globalization;

//using AjaxControlToolkit;
//using Infragistics.Web.UI.LayoutControls;

using VNWEB40.Components;
using VNWEB40_GeneralUtilities;
using VNWEB40_DAL;
using VNWEB40_Shared;
using VNWEB40_BLL;

namespace VNWEB40 {
public partial class ManageCart : BasePage {
  // int iRowCount = 0;
  NumberFormatInfo _objPrimaryCurrencyFormat = null;
  decimal _dSubTotal = 0M;
  decimal _dTotal = 0M;
  decimal _dFreight = 0M;
  int _iTotalQuantity = 0;

  protected int TotalQuantity {
    get {
      return (_iTotalQuantity);
    }
  }

  protected decimal Freight {
    get {
      return (_dFreight);
    }
  }

  protected decimal Total {
    get {
      return (_dTotal);
    }
  }

  protected decimal SubTotal {
    get {
      return (_dSubTotal);
    }
  }

  protected int SelectedCartItemId {
    get {
      return ((int)(ViewState["_iSelectedCartItemId"] ?? 0));
    }
    set {
      ViewState["_iSelectedCartItemId"] = value;
    }
  }

  protected NumberFormatInfo PrimaryCurrencyFormat {
    get {
      return (_objPrimaryCurrencyFormat);
    }
  }

  protected string GetStatusText(int iStock, int iStockNL) {
    if (BLL.ProductStockOnHand(iStock, iStockNL, _objCurrentState.UserWarehouse) >= AppSettings.StockThreshold)
      return ("In Stock");
    else
      return ("1 -2 Weeks");
  }

  private void LoadCart() {
    List<GetCartItemsByCartId_Result> lstCartItems = null;
    //iRowCount = 0;

    try {
      using(vtnbsweb_dev2Entities entities = new vtnbsweb_dev2Entities()) {
        lstCartItems = entities.GetCartItemsByCartId(_objCurrentState.CartId).ToList<GetCartItemsByCartId_Result>();
      }

    } catch (System.Exception objEx) {
      throw new ApplicationException(string.Format("An exception occurred while loading the cart items: Cart Id {0}", _objCurrentState.CartId), objEx);
    }

    if (lstCartItems.Count < 1) {
      _divCartEmpty.Visible = true;
      _divTheCart.Visible = false;
      _divButtons_EmptyCart.Visible = true;
      //_divSummary.Visible = false;

    } else {
      _divCartEmpty.Visible = false;

      _divButtons_EmptyCart.Visible = false;
      //_divSummary.Visible = true;

      //bind results to grid

      _dgCart.ClearDataSource();

      _dgCart.DataSource = lstCartItems;
      _dgCart.DataBind();

      _divTheCart.Visible = true;

      if (_objCurrentState.FreightPct != null) {
        _dFreight = _dSubTotal * ((decimal) _objCurrentState.FreightPct) / 100M;
      }

      _dTotal = _dSubTotal + _dFreight;
    }
  }

  protected void Page_Load(object sender, EventArgs e) {
    _objPrimaryCurrencyFormat = new CultureInfo("en-US", false).NumberFormat;
    _objPrimaryCurrencyFormat.CurrencyDecimalDigits = 2;

    _lnkShop.HRef =
        AppHelpers.ConstructURLForStoreHomePage_Absolute(AppSettingsGeneral.StoreBaseUrl, AppSettings.PagesRequiringSSL);

    _lnkShopEmpty.HRef =
        AppHelpers.ConstructURLForStoreHomePage_Absolute(AppSettingsGeneral.StoreBaseUrl, AppSettings.PagesRequiringSSL);

    _lnkProceed.HRef = WebUtilities.ConstructTopLevelAbsoluteURL(
        AppConstants.PAGE_STORE_BILLSHIP,
        AppSettingsGeneral.StoreBaseUrl,
        AppSettings.PagesRequiringSSL);

    if (!IsPostBack) {
      _dlgDeleteConfirm.WindowState = Infragistics.Web.UI.LayoutControls.DialogWindowState.Hidden;

      LoadCart();

      //system activity
      if (AppSettings.AddSystemActivityYN)
        BLL.AddSystemActivity(
            AppSettingsGeneral.WebSiteId,
            _objCurrentState.UserId,
            DBConstants.SystemActivityTypes.CART,
            _sCurrentUrlFull,
            _objCurrentState.CartId,
            null,
            null,
            null,
            null,
            null,
            DateTime.Now);

      ////breadcrumb
      //try
      //{
      //    _objCurrentState.BreadCrumb.Add(
      //        new CrumbItem((string)AppSettings.PageTitles[WebUtilities.FixPageForCompare(AppConstants.PAGE_STORE_CART)],
      //            _sPageName,
      //            WebUtilities.ConstructTopLevelAbsoluteURL(_sPageName, AppSettingsGeneral.StoreBaseUrl, AppSettings.PagesRequiringSSL) + _sCurrentUrlQueryString,
      //            Convert.ToInt32(AppSettings.PageCrumbs[WebUtilities.FixPageForCompare(_sPageName)])));

      //}
      //catch (System.Exception objEx)
      //{
      //    VNAppExceptionHandler.LogException(
      //        new VNAppException(string.Format("An exception occurred while constructing breadcrumb for manage cart"), objEx),
      //        VNExceptionModeType.NOT_SILENT);

      //}
    }
  }

  //protected void _dgCart_RowAdding(object source, Infragistics.Web.UI.GridControls.RowAddingEventArgs e)
  //{

  //}

  protected void _dgCart_RowInitialize(object source, Infragistics.Web.UI.GridControls.RowEventArgs e) {
    //iRowCount++;

    GetCartItemsByCartId_Result objCartItem = (GetCartItemsByCartId_Result) e.Row.DataItem;

    decimal dRowSubTotal = (decimal)(objCartItem.Price * objCartItem.Quantity);

    _dSubTotal += dRowSubTotal;

    Infragistics.Web.UI.EditorControls.WebNumericEditor ctlQuantity =
        (Infragistics.Web.UI.EditorControls.WebNumericEditor) e.Row.Items.FindItemByKey("Quantity").FindControl("_txtQuantity");

    ctlQuantity.MaxValue = AppSettings.MaxQuantity;
    ctlQuantity.MaxLength = AppSettings.MaxQuantityLength;
    ctlQuantity.ValueInt = objCartItem.Quantity;
    ctlQuantity.NullValue = 1;

    _iTotalQuantity += objCartItem.Quantity;

    //AjaxControlToolkit.ConfirmButtonExtender ctlConfirm =
    //    (AjaxControlToolkit.ConfirmButtonExtender)e.Row.Items.FindItemByKey("RemoveButtonCol").FindControl("ConfirmButtonExtender2");

    //ctlConfirm.TargetControlID = "_btnRemove";

    //AjaxControlToolkit.ModalPopupExtender ctlPopup =
    //    (AjaxControlToolkit.ModalPopupExtender)e.Row.Items.FindItemByKey("RemoveButtonCol").FindControl("ModalPopupExtender1");

    //ctlPopup.TargetControlID = "_btnRemove";
  }

  protected void _dgCart_Command(object sender, Infragistics.Web.UI.GridControls.HandleCommandEventArgs e) {
    //check to see if still signed in?

    try {
      int iRowNum = Convert.ToInt32(e.CommandArgument);
      SelectedCartItemId = (int) _dgCart.Rows[iRowNum].DataKey[0];

      if (e.CommandName == "RemoveItem") {
        _dlgDeleteConfirm.WindowState = Infragistics.Web.UI.LayoutControls.DialogWindowState.Normal;
      }

    } catch (System.Exception objEx) {
      VNAppExceptionHandler.LogException(new VNAppException(objEx), VNExceptionModeType.NOT_SILENT);
    }
  }

  protected void _lnkUpdate_Click(object sender, EventArgs e) {
    int iCartItemId = 0;
    Infragistics.Web.UI.EditorControls.WebNumericEditor ctlQuantity = null;
    int iNewQuantity = 0;
    int iNumUpdatedRows = 0;

    try {
      foreach (Infragistics.Web.UI.GridControls.GridRecord objRow in _dgCart.Rows) {
        iCartItemId = (int)(objRow.DataKey.GetValue(0));

        ctlQuantity = (Infragistics.Web.UI.EditorControls.WebNumericEditor) objRow.Items.FindItemByKey("Quantity").FindControl("_txtQuantity");

        iNewQuantity = ctlQuantity.ValueInt;

        using(vtnbsweb_dev2Entities objEntities = new vtnbsweb_dev2Entities()) {
          iNumUpdatedRows =
              (int) objEntities.UpdateCartItemQuantityByCartItemId(
                                   iCartItemId,
                                   iNewQuantity)
                  .First();
        }

        if (iNumUpdatedRows < 1)
          throw new ApplicationException(string.Format("The cart item quantity was not updated. CartItemId {0}, NewQuantity {1}", iCartItemId, iNewQuantity));
      }

    } catch (System.Exception objEx) {
      VNAppExceptionHandler.LogException(
          new VNAppException(string.Format("An exception occurred while updating cart item quantities: CartItemId {0}, NewQuantity {1}", iCartItemId, iNewQuantity), objEx),
          VNExceptionModeType.NOT_SILENT);
    }

    //refresh the grid
    LoadCart();
  }

  protected void _btnDeleteYes_Click(object sender, EventArgs e) {
    int iNumDeletedRows = 0;

    using(vtnbsweb_dev2Entities objEntities = new vtnbsweb_dev2Entities()) {
      iNumDeletedRows = (int) objEntities.DeleteCartItemByCartItemId(SelectedCartItemId).First();
    }

    if (iNumDeletedRows != 1)
      throw new ApplicationException(string.Format("The cart item was not deleted. CartItemId {0}", SelectedCartItemId));

    //system activity
    if (AppSettings.AddSystemActivityYN)
      BLL.AddSystemActivity(
          AppSettingsGeneral.WebSiteId,
          _objCurrentState.UserId,
          DBConstants.SystemActivityTypes.REM_LIST,
          Request.Url.AbsoluteUri,
          SelectedCartItemId,
          null,
          null,
          null,
          null,
          null,
          DateTime.Now);

    //Refresh grid and other stuff
    LoadCart();

    _dlgDeleteConfirm.WindowState = Infragistics.Web.UI.LayoutControls.DialogWindowState.Hidden;
  }

  protected void _btnDeleteNo_Click(object sender, EventArgs e) {
    //Refresh grid and other stuff
    LoadCart();

    _dlgDeleteConfirm.WindowState = Infragistics.Web.UI.LayoutControls.DialogWindowState.Hidden;
  }
}

}