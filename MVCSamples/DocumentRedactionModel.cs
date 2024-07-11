using System;
using System.Web.Configuration;
using System.Web;
using System.Text;
using System.Linq;
using GenericApp.ErrorHandler;
using GenericApp.Components;

namespace GenericApp.Models {
  public class DocumentRedactionModel {
    public DocumentRedactionModel() {
      ReCacheFolder = WebConfigurationManager.AppSettings.Get("reCacheFolder");  //needs to be in web.config
      ReCacheHours = Convert.ToInt32(Utility.AppSettingsWrapper.ConfigManager["reCacheHours"]);
      ReValidExtensions = (string) Utility.AppSettingsWrapper.ConfigManager["reValidExtensions"];
      //ReConfig = REProcessControl.GetConfig();
    }

#region Properties
    public string ReCacheFolder {
      get;
      set;
    }
    public string ReValidExtensions {
      get;
      set;
    }
    public int ReCacheHours {
      get;
      set;
    }
    //  public string ReCacheTempFoldersToRemove { get; set; }
#endregion

#region Methods

    /// <summary>
    /// This method will split the string and return the custom fieldID
    /// </summary>
    /// <param name="customFieldID"></param>
    /// <returns>int</returns>
    public static int GetParsedCustomFieldID(string customFieldID) {
      int customFieldIDToReturn = 0;
      try {
        if (!String.IsNullOrEmpty(customFieldID)) {
          customFieldID = customFieldID.Split('-')[0];
          int.TryParse(customFieldID, out customFieldIDToReturn);
        }
      } catch (Exception ex) {
        ErrorHandlerTools.LogError(ex);
        throw;
      }
      return customFieldIDToReturn;
    }

    public static bool IsRedactable(bool regionalUpload, string fileName, string customFieldId) {
      try {
        string ext = System.IO.Path.GetExtension(fileName ?? string.Empty).Trim().ToLower();

        int customFieldIDToPass = GetParsedCustomFieldID(customFieldId);

        //Checking whether redaction is enabled or not.
        bool isRedactionEnabled = UploaderWrapper.IsRedactionEnabled(customFieldIDToPass);

        bool result;

        if (!regionalUpload && isRedactionEnabled) {
          string validExtensions = new DocumentRedactionModel().ReValidExtensions;

          if (string.IsNullOrWhiteSpace(validExtensions)) {
            result = false;
          } else {
            var exts = validExtensions.ToLower().Split(new char[]{','}, System.StringSplitOptions.RemoveEmptyEntries).ToList();
            var extsLookup = exts.ToDictionary(x => x, x => x);

            result = (extsLookup != null && extsLookup.ContainsKey(ext));
          }
        } else {
          result = false;
        }

        return result;
      } catch (Exception ex) {
        ErrorHandlerTools.LogError(ex);
        throw;
      }
    }

    /// <summary>
    /// Gets the relative path for saving output file.  This is a variation on the RE version, which doesn't derive the root path properly.
    /// </summary>
    /// <param name="fid">The fid.</param>
    /// <param name="fileName">Name of the file.</param>
    /// <param name="rootPath">The root path.</param>
    /// <returns></returns>
    public static string GetSaveFileRelativePath(string fid, string fileName, string rootPath) {
      string fullPath = string.Format("{0}/{1}/ufor/{2}/{3}",
                                      rootPath,
                                      new DocumentRedactionModel().ReCacheFolder,
                                      fid,
                                      fileName);

      return fullPath;
    }

#endregion
  }
}
