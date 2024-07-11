using System;
using System.Web;
using System.Diagnostics;
using System.IO;

using VNWEB40_GeneralUtilities;
using VNWEB40_Shared;
using VNWEB40_BLL;

namespace VNWEB40.Components {

  public interface IBasePage  // Simple recipe - required for inheritor to
                              // implement
  {
    string GetPageTitle();
  }

  public enum AuthenticationLevelType {
    NONE,
    USER,
    ADMIN

  }

  /// <summary>
  /// Summary description for BasePage
  /// </summary>
  public abstract class BasePage : System.Web.UI.Page {
    // protected AuthenticationLevelEnum _eAuthenticationRequired =
    // AuthenticationLevelEnum.Any;   //default
    protected bool _bHasRequiredAuthenticationYN = false;

    protected bool _bAuthenticationRequiredYN = true;    // default
    protected bool _bAuthenticationAdminOnlyYN = false;  // default

    protected DateTime _dtRightNow = DateTime.Now;

    protected DateTime _dtAuditStart;
    protected DateTime _dtAuditEnd;

    protected MyState _objCurrentState = null;

    // protected string _sCurrentUrl = string.Empty;
    protected string _sCurrentUrlFull = string.Empty;
    // protected string _sCurrentBasePath = string.Empty;
    protected string _sCurrentUrlQueryString = string.Empty;

    protected string _sPageName = string.Empty;
    protected string _sFullLoginURL = string.Empty;

    public BasePage() {}

#region Helpers

    virtual public string GetPageTitle() {
      if (AppSettings.PageTitles.ContainsKey(_sPageName))
        return (AppSettingsGeneral.AppName + " - " +
                (string) AppSettings.PageTitles[_sPageName]);
      else
        return (AppSettingsGeneral.AppName);
    }

    public bool IsPageCurrentYN(string sPage) {
      return (_sPageName == sPage.ToLower());
    }

    public bool HasRequiredAuthenticationYN {
      get { return (_bHasRequiredAuthenticationYN); }
    }

    private bool UseOverlayYN() {
      bool bResult = false;

      // Within this site, to show dedicated product page - use explicit
      // dedicated page.

      // if (sReferringPage == "storehome.aspx" || sReferringPage ==
      // "clients.aspx") //REVISIT: Add list
      if (WebUtilities.ReferringHostContains(
              AppSettings.HostsAllowingOverlay)) {
        // if (WebUtilities.ReferringPageContains("storehome") ||
        // WebUtilities.ReferringPageContains("clients"))
        //{
        if (TheQueryStringItems.UseOverlayYN == 1)
          bResult = true;  // ok to use overlay rather than dedicated page

        //}
      }

      return (bResult);
    }

#endregion

#region Events

    protected override void OnInit(EventArgs e) {
      Debug.WriteLine("BASE: ENTERING PAGEINIT: " + DateTime.Now.ToString());

      _objCurrentState = new MyState(DBConstants.WebSiteId.Store, false,
                                     AppSettingsGeneral.DefaultPriceLevelId);

      _sPageName = WebUtilities.GetCurrentPageName();

      _sFullLoginURL = WebUtilities.ConstructTopLevelAbsoluteURL(
          AppConstants.PAGE_STORE_LOGIN, AppSettingsGeneral.StoreBaseUrl,
          AppSettings.PagesRequiringSSL);

      if (AppSettings.PagesRequiringSSL.ContainsKey(_sPageName) &&
          !Request.IsSecureConnection) {
        string sNewUrl = Uri.UriSchemeHttps + Uri.SchemeDelimiter +
                         Request.Url.Authority + Request.Url.PathAndQuery;

        Response.Redirect(sNewUrl, false);

        // throw new ApplicationException("The requested page requires a secure
        // connection.");
      }

      // this.Title = GetPageTitle();

      // 8/9/12: Made a change so that loggin in/out works between store and
      // admin check the cookie first...

      HttpCookie objAuthCookie = SecureCookies.DecodeCookie(
          Request.Cookies[AppSettingsGeneral.AuthCookieName],
          AppSettingsGeneral.CookieCryptInfo);
      ;

      if (objAuthCookie == null || objAuthCookie.Value == null ||
          objAuthCookie.Value == "0")
        BLL.LogoutUser(AppSettingsGeneral.AuthCookieName,
                       AppSettingsGeneral.DefaultPriceLevelId,
                       DBConstants.WebSiteId.Store);

      // proceed as usual...
      if (!_objCurrentState.IsSignedIn())  // missing session info
      {
        if (objAuthCookie != null) {
          if (objAuthCookie.Value != null &&
              BLL.IsValidUserId(Convert.ToInt32(objAuthCookie.Value)))
          //&& objAuthCookie.Value != DBConstants.USER_SYSTEMID.ToString()
          // && objAuthCookie.Value != DBConstants.USER_NONID.ToString())
          {
            try {
              BLL.ReloadUser(Convert.ToInt32(objAuthCookie.Value),
                             DBConstants.WebSiteId.Store);

            } catch (System.Exception objEx) {
              VNAppExceptionHandler.LogException(
                  new VNAppException(
                      string.Format(
                          "An exception occurred while reloading the current user: Cookie Value {0}",
                          objAuthCookie.Value),
                      objEx),
                  VNExceptionModeType.NOT_SILENT);
            }
          }
        }
      }

      // Now check again if signed in and if the requested page requires being
      // signed in
      if (!_objCurrentState.IsSignedIn()) {
        if (AppSettings.PagesRequiringAuthentication.ContainsKey(_sPageName) ||
            AppSettings.PagesRequiringAdmin.ContainsKey(_sPageName)) {
          Response.Redirect(WebUtilities.ConstructTopLevelAbsoluteURL(
              AppConstants.PAGE_STORE_LOGIN, AppSettingsGeneral.StoreBaseUrl,
              AppSettings.PagesRequiringSSL));
        }

      } else {
        if (_objCurrentState.UserRole != DBConstants.UserRole.Admin &&
            AppSettings.PagesRequiringAdmin.ContainsKey(_sPageName)) {
          Response.Redirect(WebUtilities.ConstructTopLevelAbsoluteURL(
              AppConstants.PAGE_STORE_LOGIN, AppSettingsGeneral.StoreBaseUrl,
              AppSettings.PagesRequiringSSL));
        }
      }

      TheQueryStringItems = AppHelpers.GetPageParams(
          AppSettings.DefaultProductSortOrder);  // moved up here

      _sCurrentUrlFull =
          Request.Url
              .AbsoluteUri;  // Now saving this so we have the domain as well

      _sCurrentUrlQueryString =
          (Request.ServerVariables["QUERY_STRING"] ?? string.Empty).Trim();

      if (_sCurrentUrlQueryString != string.Empty)
        _sCurrentUrlQueryString =
            "?" + _sCurrentUrlQueryString;  // request object doesn't include ?

      // Hashtable hshPageExclude =
      // (Hashtable)ConfigurationManager.GetSection("pageDontSaveURL");

      // Handle product overlays here
      if (!IsPostBack &&
          IsPageCurrentYN(AppConstants.PAGE_STORE_PRODUCT_OVERLAY) &&
          !BLL.IsProductPageCookieActiveYN())
      //&& !_objCurrentState.IsTransfer)
      {
        if (!UseOverlayYN())
        // if
        // (!AppSettings.PagesAllowingProductOverlay.ContainsKey(_objCurrentState.ReferrerPageName
        // ?? string.Empty))
        {
          // construct URL to dedicated product page and redirect
          string sAltURL = WebUtilities.ConstructTopLevelAgnosticURL(
              string.Format("{0}{1}", AppConstants.PAGE_STORE_PRODUCT_DEDICATED,
                            _sCurrentUrlQueryString));

          //_objCurrentState.IsTransfer = true;

          BLL.ActivateProductPageCookie();  // says that a dedicated product
                                            // page is being displayed

          Server.Transfer(sAltURL);  // FOR NOW
        }
      }

      BLL.DeActivateProductPageCookie();  // reset the flag

      if (!IsPostBack &&
          !AppSettings.PagesDontSaveURL.ContainsKey(_sPageName)) {
        _objCurrentState.CallerURL = _sCurrentUrlFull;  // the prior url

        _objCurrentState.CallerPage = _sPageName;  // the prior page
      }

      base.OnInit(e);

      // REVISIT
      // Check if db lookup was loaded.
      // if (!DBConstants.IsLookupLoadedYN())
      // Startup.LoadDBLookup(); //Load it

      Debug.WriteLine("BASE: LEAVING PAGEINIT: " + DateTime.Now.ToString());
    }

    protected override void OnLoad(EventArgs e) {
      Debug.WriteLine("BASE: ENTERING PAGELOAD: " + DateTime.Now.ToString());

      // REVISIT

      // Are these NO CACHE settings sufficient?
      // Hey wait, this seems to only work in IE, not FF.
      Response.CacheControl = "no-cache";
      Response.AddHeader("Pragma", "no-cache");
      Response.Expires = -1;

      if (_objCurrentState.PriceLevelId ==
          0)  // Signals that we need to set a default
        _objCurrentState.PriceLevelId =
            AppSettingsGeneral
                .DefaultPriceLevelId;  // _objCurrentState.PriceLevelIdDefault;

      base.OnLoad(e);

      Debug.WriteLine("BASE: LEAVING PAGELOAD: " + DateTime.Now.ToString());
    }

    protected override void OnPreRender(EventArgs e) {
      Debug.WriteLine("BASE: ENTERING PRERENDER: " + DateTime.Now.ToString());

      base.OnPreRender(e);

      if (!AppSettings.PagesWithNoTitle.ContainsKey(_sPageName))
        this.Title = GetPageTitle();

      Debug.WriteLine("BASE: LEAVING PRERENDER: " + DateTime.Now.ToString());
    }

    protected override void OnUnload(EventArgs e) {
      Debug.WriteLine("BASE: ENTERING PAGEUNLOAD: " + DateTime.Now.ToString());

      base.OnUnload(e);

      //_objCurrentState.SubmitInProgressYN = false;

      Debug.WriteLine("BASE: LEAVING PAGEUNLOAD: " + DateTime.Now.ToString());
    }

#endregion

#region Properties

    public QueryStringItems TheQueryStringItems {
      get {
        return (
            (QueryStringItems)(ViewState["_objTheQueryStringItems"] ?? null));
      }
      set { ViewState["_objTheQueryStringItems"] = value; }
    }

    public string SubCategoryName  // so far for cat/subcat page header
    {
      get { return ((string)(ViewState["_sSubCategoryName"] ?? string.Empty)); }
      set { ViewState["_sSubCategoryName"] = value; }
    }

    public string DeviceName {
      get { return ((string)(ViewState["_sDeviceName"] ?? string.Empty)); }
      set { ViewState["_sDeviceName"] = value; }
    }

    public string ModelName {
      get { return ((string)(ViewState["_sModelName"] ?? string.Empty)); }
      set { ViewState["_sModelName"] = value; }
    }

    public string CategoryName  // so far for cat page header
    {
      get { return ((string)(ViewState["_sCategoryName"] ?? string.Empty)); }
      set { ViewState["_sCategoryName"] = value; }
    }

    public string MfgName  // so far for mfg page header
    {
      get { return ((string)(ViewState["_sMfgName"] ?? string.Empty)); }
      set { ViewState["_sMfgName"] = value; }
    }

    // decision to create properties vs just the protected variables is if I
    // want to use these from markup

    public string PageTitle {
      get {
        if (ViewState["_sPageTitle"] != null)
          return (ViewState ["_sPageTitle"]
                      .ToString());
        else
          return (GetPageTitle());  // Better than returning string.empty but
                                    // shouldn't happen
      }
      set { ViewState["m_sPageTitle"] = value; }
    }

    public MyState WebUserState {
      get { return (_objCurrentState); }
    }

    protected DateTime RightNow {
      get { return (_dtRightNow); }
      set { _dtRightNow = value; }
    }

    public string CrumbText  // REVISIT
    {
      get {
        if (ViewState["_sCrumbText"] != null)
          return (ViewState ["_sCrumbText"]
                      .ToString());
        else
          return (string.Empty);  // Better than returning string.empty but
                                  // shouldn't happen
      }
      set { ViewState["_sCrumbText"] = value; }
    }

#endregion
  }

}
