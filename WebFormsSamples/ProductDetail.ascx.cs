namespace VNWeb.UserControls {
  using System;
  using System.Data;
  using System.Drawing;
  using System.Web;
  using System.Web.UI.WebControls;
  using System.Web.UI.HtmlControls;
  using System.Globalization;
  using System.Text;
  using System.Configuration;

  using VNWeb.Components;
  using VNWeb.Components.Data;

  /// <summary>
  ///		Summary description for ProductDetail.
  /// </summary>
  public class ProductDetail : SuperControl {
    protected System.Web.UI.HtmlControls.HtmlTableRow m_ctlRow_AdditionalInfo;
    protected System.Web.UI.WebControls.TextBox m_txtQuantity;
    protected System.Web.UI.WebControls.LinkButton m_btnAddToCart;
    protected System.Web.UI.HtmlControls.HtmlTable m_tblValidation;
    protected System.Web.UI.WebControls.Label m_lblValidation;
    protected System.Web.UI.WebControls.LinkButton m_lnkRemove;
    private ProductType m_objProduct = null;
    private SearchEngineType m_objSearchEngine = null;
    private NumberFormatInfo m_objPrimaryCurrencyFormat = null;
    //private NumberFormatInfo m_objAltCurrencyFormat = null;
    private bool m_bIsSignedIn_Local = false;
    private bool m_bIsGuest_Local = false;

    public override void InitControl() {
      base.InitControl();

      if (!IsPostBack) {
        m_btnAddToCart.Attributes.Add("onmouseout", "turn_off('UpdateCart')");
        m_btnAddToCart.Attributes.Add("onmouseover", "turn_over('UpdateCart')");
      }
    }

    public bool IsSignedIn_Local {
      get {
        return (m_bIsSignedIn_Local);
      }
    }

    public bool IsGuest_Local {
      get {
        return (m_bIsGuest_Local);
      }
    }

    public string GetAltPriceHTML(ProductType objProd, int iQuantity) {
      StringBuilder sResult = new StringBuilder(string.Empty);

      if (m_objWebUserState.CurrencyName != string.Empty)  //Can users have no alternate currency? Should not, if signed in.
      {
        sResult.Append("(");

        sResult.Append(string.Format(m_objWebUserState.FormatString, ProductType.GetAltPrice(objProd.Price, iQuantity, m_objWebUserState.CurrencyFactor)));

        sResult.Append(")");
      }

      return (sResult.ToString());
    }

    public string GetAltPriceHTML(ProductType objProd) {
      return (GetAltPriceHTML(objProd, 1));
    }

    public NumberFormatInfo PrimaryCurrencyFormat {
      get {
        return (m_objPrimaryCurrencyFormat);
      }
      set {
        m_objPrimaryCurrencyFormat = value;
      }
    }

    //		public NumberFormatInfo AltCurrencyFormat
    //		{
    //			get
    //			{
    //				return(m_objAltCurrencyFormat);
    //			}
    //			set
    //			{
    //				AltCurrencyFormat = value;
    //			}
    //
    //		}

    public SearchEngineType SearchEngine {
      get {
        return (m_objSearchEngine);
      }
      set {
        m_objSearchEngine = value;
      }
    }

    public ProductType Product {
      get {
        return (m_objProduct);
      }
      set {
        m_objProduct = value;
      }
    }

    private void Page_Load(object sender, System.EventArgs e) {
      try {
        if (this.Visible) {
          InitControl();

          m_bIsSignedIn_Local = m_objWebUserState.IsSignedIn();
          m_bIsGuest_Local = m_objWebUserState.IsGuest();

          m_objPrimaryCurrencyFormat = new CultureInfo("en-US", false).NumberFormat;
          m_objPrimaryCurrencyFormat.CurrencyDecimalDigits = 2;
          //m_objCurrencyFormat.CurrencyDecimalSeparator = ",";
          //m_objCurrencyFormat.CurrencySymbol = string.Empty;

          //m_objAltCurrencyFormat = new CultureInfo("en-US", false).NumberFormat;	//Not sure what to use here
          //m_objAltCurrencyFormat.CurrencyDecimalDigits = 2;
          //m_objAltCurrencyFormat.CurrencySymbol = string.Empty;

          if (!IsPostBack) {
            if (m_txtQuantity.Visible) {
              m_txtQuantity.Attributes["onKeyPress"] = "javascript:HandleEnterKey(event, '" + m_btnAddToCart.UniqueID + "');";  //Simulate Submit with Enter key

              if (CartMode == ShoppingCartModeType.UPDATE) {
                m_txtQuantity.Text = Quantity.ToString();

                //if (m_btnAddToCart.Visible)
                //	m_btnAddToCart.Text = "Update Cart";
              }
            }

            m_tblValidation.Visible = false;  //Hide validation

            m_ctlRow_AdditionalInfo.Visible = (m_objProduct.AdditionalInfo.Trim() != string.Empty);
          }
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
      this.m_btnAddToCart.Click += new System.EventHandler(this.m_btnAddToCart_Click);
      this.m_lnkRemove.Click += new System.EventHandler(this.m_lnkRemove_Click);
      this.Load += new System.EventHandler(this.Page_Load);
    }
#endregion

    private void m_btnAddToCart_Click(object sender, System.EventArgs e) {
      try {
        if (!m_objWebUserState.IsSignedIn())
          Response.Redirect(SignInURL, false);
        else {
          //This next line sets a flag so next time cart is loaded, it won't be pulled from Cache.
          //Rather than surgically put this line further down, when I know the cart update was successful,
          //Just keep it here for simplicity.
          m_objWebUserState.CartChanged = true;

          m_tblValidation.Visible = false;

          int iQuantity = 0;
          bool bProceed = false;

          if (CartItemType.IsValidItemQuantity(m_txtQuantity.Text.Trim())) {
            //Should be able to convert but don't assume
            try {
              iQuantity = Convert.ToInt32(m_txtQuantity.Text);
              bProceed = true;
            } catch {
              bProceed = false;
            }
          }

          if (bProceed)  //Valid quantity
          {
            try {
              if (CartMode != ShoppingCartModeType.UPDATE) {
                CartItemCollectionType.Insert(m_objWebUserState, m_objProduct, iQuantity);  //Item is not currently in cart
                CartItemCollectionType.RemoveFromCache(m_objWebUserState);                  //Force Load next time

              } else {
                CartItemCollectionType.UpdateQuantity(m_objWebUserState, CartItemId, iQuantity);
                CartItemCollectionType.RemoveFromCache(m_objWebUserState);  //Force Load next time
              }

              Response.Redirect(m_sCurrentUrlFull, false);  //Refresh page and cart display

            } catch (Exception objEx) {
              VTNBSAppExceptionHandler.LogException(new VTNBSAppException(objEx), ExceptionModeType.SILENT);

              m_lblValidation.Text = "A problem occurred while adding this item to the shopping cart.";

              bProceed = false;
            };
          }

          if (!bProceed)
            m_tblValidation.Visible = true;
        }

      } catch (Exception objEx) {
        VTNBSAppExceptionHandler.LogException(new VTNBSAppException(objEx), ExceptionModeType.NOT_SILENT);
      }
    }

    private void m_lnkRemove_Click(object sender, System.EventArgs e) {
      try {
        if (!m_objWebUserState.IsSignedIn())
          Response.Redirect(SignInURL, false);
        else {
          //This next line sets a flag so next time cart is loaded, it won't be pulled from Cache.
          //Rather than surgically put this line further down, when I know the cart update was successful,
          //Just keep it here for simplicity.
          m_objWebUserState.CartChanged = true;

          //Remove item from shopping cart - assume item exists in cart, since button not available otherwise

          m_tblValidation.Visible = false;

          bool bProceed = true;

          if (bProceed) {
            try {
              CartItemCollectionType.Remove(m_objWebUserState, m_objProduct.ProductId);
              CartItemCollectionType.RemoveFromCache(m_objWebUserState);  //Force Load next time

              //Response.Redirect(Request.RawUrl, false);		//Refresh page and cart display
              Response.Redirect(Utility.ConstructNonSSLAppPath("ShoppingCart.aspx"), false);  //Redirect to cart page

            } catch (Exception objEx) {
              VTNBSAppExceptionHandler.LogException(new VTNBSAppException(objEx), ExceptionModeType.SILENT);

              m_lblValidation.Text = "A problem occurred while removing this item to the shopping cart.";

              bProceed = false;
            }
          }

          if (!bProceed)
            m_tblValidation.Visible = true;
        }

      } catch (Exception objEx) {
        VTNBSAppExceptionHandler.LogException(new VTNBSAppException(objEx), ExceptionModeType.NOT_SILENT);

        //m_lblValidation.Text = "A problem occurred while removing this item to the shopping cart.";
      }
    }
  }

}
