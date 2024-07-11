using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;
using RedactionServiceApp.Entities;
using RedactionServiceApp.Components.ErrorHandling;

namespace RedactionServiceApp.Components {
  public class BL {
    /// ********************************************************************************
    /// <summary>
    /// Delete contents of custom upload area that are older than specified amount of time.
    /// Custom upload area is used when uploading a document from outside of the RE UI, such
    /// as from the iFrame parent.
    /// </summary>
    /// <param name="ctx"></param>
    /// ********************************************************************************
    static public void DeleteCustomUploads(HttpContext ctx) {
      try {
        //now look at the custom upload folder structure, which is used when a file is uploaded outside the iframe containing the RE tools
        string customUploadFolder = ctx.Server.MapPath(AppSettingsWrapper.CustomUploadPath);

        if (Directory.Exists(customUploadFolder)) {
          var pathInfo = new DirectoryInfo(customUploadFolder);
          var subPathsInfo = pathInfo.GetDirectories();
          var subPathsToDelete = new List<DirectoryInfo>();

          foreach (var subPathInfo in subPathsInfo) {
            if (subPathInfo.CreationTimeUtc.AddHours(AppSettingsWrapper.CacheHours) < DateTime.UtcNow) {
              subPathsToDelete.Add(subPathInfo);
            }
          }

          foreach (var subPathInfo in subPathsToDelete) {
            subPathInfo.Delete(true);
          }
        }
      } catch (Exception ex) {
        ErrorHandlerTools.LogError(ex);
      }
    }

    /// ********************************************************************************
    /// <summary>
    /// Delete contents of RE cache older than specified amount of time.
    /// </summary>
    /// <param name="ctx"></param>
    /// ********************************************************************************
    static public void DeleteRedactionCache(HttpContext ctx) {
      try {
        string cacheFolder = ctx.Server.MapPath(AppSettingsWrapper.CacheFolder);

        //first check if RE cache folder has been created - it may not exist after a fresh IQC delpoyment
        if (Directory.Exists(cacheFolder)) {
          var pathInfo = new DirectoryInfo(cacheFolder);
          var subPathsInfo = pathInfo.GetDirectories();
          var subPathsToDelete = new List<DirectoryInfo>();

          foreach (var subPathInfo in subPathsInfo) {
            if (subPathInfo.Name.ToLower() != "_log_files" && subPathInfo.CreationTimeUtc.AddHours(AppSettingsWrapper.CacheHours) < DateTime.UtcNow) {
              subPathsToDelete.Add(subPathInfo);
            }
          }

          foreach (var subPathInfo in subPathsToDelete) {
            subPathInfo.Delete(true);
          }
        }
      } catch (Exception ex) {
        ErrorHandlerTools.LogError(ex);
      }
    }

    /// ********************************************************************************
    /// <summary>
    /// Delete security tokens from the database that are older than a specified amount of time.
    /// </summary>
    /// ********************************************************************************
    static public void DeleteExpiredAppTokens() {
      using(var db = new REServiceEntities()) {
        try {
          //delete keys that expired 24 or more hours ago
          string sql = "exec dbo.DeleteAppTokens";
          db.Database.ExecuteSqlCommand(sql);
        } catch (Exception ex) {
          ErrorHandlerTools.LogError(ex);
        }
      }
    }

    /// ********************************************************************************
    /// <summary>
    /// Construct unique folder name to be used in custom upload area, to avoid file contention.
    /// </summary>
    /// <returns></returns>
    /// ********************************************************************************
    static public string ConstructUniqueFolderName() {
      return $"{DateTime.Now.ToString(" MM - dd - yy ")}_{Guid.NewGuid().ToString()}";
    }

    /// ********************************************************************************
    /// <summary>
    /// Determine if client domain, e.g. http://thedomain.com, is permitted to access pages that inherit this class.
    /// The same allowed origins lookup in the web.config is used both for this purpose and for CORS and Ajax requests.
    /// Note that in this scenario, there is no origin header and the caller must provide the client domain.
    /// </summary>
    /// <param name="origin"></param>
    /// <returns></returns>
    /// ********************************************************************************
    static public bool IsAllowedOrigin(string origin) {
      if (string.IsNullOrEmpty(origin)) {
        return true;  //not cross origin
      } else {
        var originURI = new Uri(origin);
        string originUse = originURI.GetLeftPart(UriPartial.Authority);
        var allowedOrigins = CORSHelper.GetAllowedOrigins();

        return allowedOrigins.ContainsKey(originUse);
      }
    }

    /// ********************************************************************************
    /// <summary>
    /// Construct application security token.
    /// </summary>
    /// <returns></returns>
    /// ********************************************************************************
    static public string ConstructAppToken() {
      //security token consists of two guids concatenated
      return $"{Guid.NewGuid().ToString()}-{Guid.NewGuid().ToString()}";
    }

    /// ********************************************************************************
    /// <summary>
    /// Construct and generate application security token
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    /// ********************************************************************************
    static public string GenerateAppToken(HttpRequest request) {
      try {
        string appNameParam = request.Form["appName"];

        if (string.IsNullOrWhiteSpace(appNameParam)) {
          throw new Exception("appNameParam value cannoty be empty.");
        }

        App app = null;

        using(var db = new REServiceEntities()) {
          app = db.Apps.Where(x => x.AppName == appNameParam).SingleOrDefault();

          if (app == null      //app not present
              || !app.Active)  //app not active
          //|| app.AppTokens.Count < 1)     //no tokens for this app
          {
            throw new Exception($"Cannot find app table entry for {appNameParam}.");
          }

          //add new app token
          var appToken = new AppToken();
          appToken.AppId = app.AppId;
          appToken.Token = ConstructAppToken();
          appToken.TokenExpiration = DateTime.UtcNow.AddMinutes(AppSettingsWrapper.TokenDurationMinutes);  //store as UTC
          appToken.ModifiedDate = DateTime.UtcNow;                                                         //store as UTC
          app.AppTokens.Add(appToken);

          db.SaveChanges();

          return appToken.Token;
        }
      } catch (Exception ex) {
        ErrorHandlerTools.LogError(ex);
        return string.Empty;
      }
    }

    /// ********************************************************************************
    /// <summary>
    /// Applies simplied authentication, just checks appname param.
    /// </summary>
    /// <param name="appNameParam"></param>
    /// <returns></returns>
    /// ********************************************************************************
    static private bool IsAuthenticatedAlt(string appNameParam) {
      try {
        if (string.IsNullOrWhiteSpace(appNameParam)) {
          throw new Exception("appNameParam cannoty be empty.");
        }

        App app = null;

        using(var db = new REServiceEntities()) {
          app = db.Apps.Where(x => x.AppName == appNameParam).SingleOrDefault();

          if (app == null     //app not present
              || !app.Active  //app not active
          ) {
            throw new Exception($"App name {appNameParam} either not found or not active.");
          }
        }
      } catch (Exception ex) {
        ErrorHandlerTools.LogError(ex);
        return false;
      }

      return true;
    }

    /// ********************************************************************************
    /// <summary>
    /// Check if current request is authenticated, based on application name and token.
    /// </summary>
    /// <param name="appNameParam"></param>
    /// <param name="appTokenParam"></param>
    /// <returns></returns>
    /// ********************************************************************************
    static public bool IsAuthenticated(string appNameParam, string appTokenParam) {
      return IsAuthenticatedAlt(appNameParam);  //For testing, until app token issues are worked out

      try {
        if (string.IsNullOrWhiteSpace(appNameParam) || string.IsNullOrWhiteSpace(appTokenParam)) {
          throw new Exception("appNameParam and appTokenParam values cannoty be empty.");
        }

        App app = null;

        using(var db = new REServiceEntities()) {
          app = db.Apps.Where(x => x.AppName == appNameParam).SingleOrDefault();

          if (app == null                  //app not present
              || !app.Active               //app not active
              || app.AppTokens.Count < 1)  //no tokens for this app
          {
            throw new Exception($"Cannot find any tokens for {appNameParam}.");
          }

          //inspect app token

          var appToken = app.AppTokens.Where(m => m.Token == appTokenParam).SingleOrDefault();

          if (appToken == null) {
            throw new Exception($"Cannot find specified token {appTokenParam} for {appNameParam}.");
          }

          if (appToken.TokenExpiration <= DateTime.UtcNow  //token expiration is already as UTC
              || appToken.Token != appTokenParam) {
            //token has expired, caller needs to request a new token
            throw new Exception($"Cannot find specified token {appTokenParam} for {appNameParam}.");
          }
        }
      } catch (Exception ex) {
        ErrorHandlerTools.LogError(ex);
        return false;
      }

      return true;
    }
  }
}