using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Services;
using System.IO;
using System.Drawing;
using System.Text;
using System.Configuration;

using VNWEB40_Shared;
using VNWEB40_GeneralUtilities;
using VNWEB40_BLL;

namespace VNWEB40_ImageProcessor {
/// <summary>
/// Summary description for SiteImageOperations
/// </summary>
[WebService(Namespace = "http://images.vtn.net/")]
[WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
[System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line.
    // [System.Web.Script.Services.ScriptService]
    public class SiteImageOperations : System.Web.Services.WebService {
  [WebMethod]
  public string Test() {
    return "Hello World";
  }

  private string ConstructProductImageVirtualPath(bool bIsPreviewYN, string sFileName, string sImageType, ref string sErrorMessage) {
    string sVirtualPath = "~/vtnbsfiles/products/";
    System.Exception objExUse = null;

    try {
      if (bIsPreviewYN)
        sVirtualPath += "preview";
      else if (sImageType == DBConstants.ImageType.Small)
        sVirtualPath += "small";
      else if (sImageType == DBConstants.ImageType.Medium)
        sVirtualPath += "med";
      else if (sImageType == DBConstants.ImageType.Large)
        sVirtualPath += "large";
      else
        throw new ApplicationException("Invalid image type");

      if (sFileName != string.Empty)
        sVirtualPath += WebUtilities.AddLeadingFwdSlash(sFileName);

      //sPhysicalPath = Server.MapPath(sVirtualPath);

      return (sVirtualPath);

    } catch (System.Exception objEx) {
      objExUse = objEx;

      sErrorMessage = objEx.Message;
      return (string.Empty);

    } finally {
      if (objExUse != null && ConfigurationManager.AppSettings["sendErrorEmailsYN"] == "1") {
        try {
          bool bMailResult = EmailUtilities.SMTPSendMail(
              ConfigurationManager.AppSettings["exceptionEmailFrom"],
              string.Format("{0}: VTNBS Image Processor Error", ConfigurationManager.AppSettings["envTag"]),
              string.Format("Message: {0}, Stack Trace: {1}", objExUse.Message, objExUse.StackTrace),
              ConfigurationManager.AppSettings["exceptionEmailTo"],  //for multiples - will be delimited in single string
              string.Empty,                                          //CC
              string.Empty,                                          //BCC
              false,
              ConfigurationManager.AppSettings["mailServer"],
              ConfigurationManager.AppSettings["exceptionEmailUser"],
              ConfigurationManager.AppSettings["exceptionEmailPwd"],
              string.Empty,
              string.Empty);
        } catch {
        };
      }
    }
  }

  private string ConstructRMAProductImageVirtualPath(string sFileName, ref string sErrorMessage) {
    string sVirtualPath = "~/vtnbsfiles/uploads";
    System.Exception objExUse = null;

    try {
      if (sFileName != string.Empty)
        sVirtualPath += WebUtilities.AddLeadingFwdSlash(sFileName);

      return (sVirtualPath);

    } catch (System.Exception objEx) {
      objExUse = objEx;

      sErrorMessage = objEx.Message;
      return (string.Empty);

    } finally {
      if (objExUse != null && ConfigurationManager.AppSettings["sendErrorEmailsYN"] == "1") {
        try {
          bool bMailResult = EmailUtilities.SMTPSendMail(
              ConfigurationManager.AppSettings["exceptionEmailFrom"],
              string.Format("{0}: VTNBS Image Processor Error", ConfigurationManager.AppSettings["envTag"]),
              string.Format("Message: {0}, Stack Trace: {1}", objExUse.Message, objExUse.StackTrace),
              ConfigurationManager.AppSettings["exceptionEmailTo"],  //for multiples - will be delimited in single string
              string.Empty,                                          //CC
              string.Empty,                                          //BCC
              false,
              ConfigurationManager.AppSettings["mailServer"],
              ConfigurationManager.AppSettings["exceptionEmailUser"],
              ConfigurationManager.AppSettings["exceptionEmailPwd"],
              string.Empty,
              string.Empty);

        } catch {
        };
      }
    }
  }

  [WebMethod]
  public string GetProductImageInfo(bool bIsPreviewYN, string sFileName, string sImageType, string sDelim, ref string sErrorMessage) {
    System.Exception objExUse = null;
    string sVirtualPath = ConstructProductImageVirtualPath(bIsPreviewYN, sFileName, sImageType, ref sErrorMessage);

    if (sErrorMessage != string.Empty)
      return (string.Empty);

    StringBuilder sInfo = new StringBuilder(string.Empty);
    Bitmap objImage = null;

    try {
      string sPhysicalPath = Server.MapPath(sVirtualPath);

      FileInfo objInfo = new FileInfo(sPhysicalPath);

      //pipe delimited

      sInfo.Append(string.Format("Size: {0} bytes{1}", objInfo.Length, sDelim));

      objImage = (Bitmap) Bitmap.FromFile(sPhysicalPath);

      sInfo.AppendLine(string.Format("Dims: {0}w x {1}h", objImage.Width, objImage.Height));

      return (sInfo.ToString());

    } catch (System.Exception objEx) {
      objExUse = objEx;

      sErrorMessage = objEx.Message;
      return (string.Empty);

    } finally {
      if (objImage != null)
        objImage.Dispose();

      if (objExUse != null && ConfigurationManager.AppSettings["sendErrorEmailsYN"] == "1") {
        try {
          bool bMailResult = EmailUtilities.SMTPSendMail(
              ConfigurationManager.AppSettings["exceptionEmailFrom"],
              string.Format("{0}: VTNBS Image Processor Error", ConfigurationManager.AppSettings["envTag"]),
              string.Format("Message: {0}, Stack Trace: {1}", objExUse.Message, objExUse.StackTrace),
              ConfigurationManager.AppSettings["exceptionEmailTo"],  //for multiples - will be delimited in single string
              string.Empty,                                          //CC
              string.Empty,                                          //BCC
              false,
              ConfigurationManager.AppSettings["mailServer"],
              ConfigurationManager.AppSettings["exceptionEmailUser"],
              ConfigurationManager.AppSettings["exceptionEmailPwd"],
              string.Empty,
              string.Empty);

        } catch {
        };
      }
    }
  }

  [WebMethod]
  public bool DeleteProductImage(bool bIsPreviewYN, string sFileName, string sImageType, ref string sErrorMessage) {
    System.Exception objExUse = null;
    string sVirtualPath = ConstructProductImageVirtualPath(bIsPreviewYN, sFileName, sImageType, ref sErrorMessage);

    if (sErrorMessage != string.Empty)
      return (false);

    string sPhysicalPath = string.Empty;

    try {
      sPhysicalPath = Server.MapPath(sVirtualPath);

      File.Delete(sPhysicalPath);

      return (!File.Exists(sPhysicalPath));  //make sure it's gone

    } catch (System.Exception objEx) {
      objExUse = objEx;

      sErrorMessage = objEx.Message;
      return (false);

    } finally {
      if (objExUse != null && ConfigurationManager.AppSettings["sendErrorEmailsYN"] == "1") {
        try {
          bool bMailResult = EmailUtilities.SMTPSendMail(
              ConfigurationManager.AppSettings["exceptionEmailFrom"],
              string.Format("{0}: VTNBS Image Processor Error", ConfigurationManager.AppSettings["envTag"]),
              string.Format("Message: {0}, Stack Trace: {1}", objExUse.Message, objExUse.StackTrace),
              ConfigurationManager.AppSettings["exceptionEmailTo"],  //for multiples - will be delimited in single string
              string.Empty,                                          //CC
              string.Empty,                                          //BCC
              false,
              ConfigurationManager.AppSettings["mailServer"],
              ConfigurationManager.AppSettings["exceptionEmailUser"],
              ConfigurationManager.AppSettings["exceptionEmailPwd"],
              string.Empty,
              string.Empty);

        } catch {
        };
      }
    }
  }

  [WebMethod]
  public bool DeleteRMAProductImage(string sFileName, ref string sErrorMessage) {
    System.Exception objExUse = null;
    string sVirtualPath = ConstructRMAProductImageVirtualPath(sFileName, ref sErrorMessage);

    if (sErrorMessage != string.Empty)
      return (false);

    string sPhysicalPath = string.Empty;

    try {
      sPhysicalPath = Server.MapPath(sVirtualPath);

      File.Delete(sPhysicalPath);

      return (!File.Exists(sPhysicalPath));  //make sure it's gone

    } catch (System.Exception objEx) {
      objExUse = objEx;

      sErrorMessage = objEx.Message;
      return (false);

    } finally {
      if (objExUse != null && ConfigurationManager.AppSettings["sendErrorEmailsYN"] == "1") {
        try {
          bool bMailResult = EmailUtilities.SMTPSendMail(
              ConfigurationManager.AppSettings["exceptionEmailFrom"],
              string.Format("{0}: VTNBS Image Processor Error", ConfigurationManager.AppSettings["envTag"]),
              string.Format("Message: {0}, Stack Trace: {1}", objExUse.Message, objExUse.StackTrace),
              ConfigurationManager.AppSettings["exceptionEmailTo"],  //for multiples - will be delimited in single string
              string.Empty,                                          //CC
              string.Empty,                                          //BCC
              false,
              ConfigurationManager.AppSettings["mailServer"],
              ConfigurationManager.AppSettings["exceptionEmailUser"],
              ConfigurationManager.AppSettings["exceptionEmailPwd"],
              string.Empty,
              string.Empty);

        } catch {
        };
      }
    }
  }

  [WebMethod]
  public string UploadRMAProductImage(string sFileName, byte[] arrImage, ref string sErrorMessage) {
    System.Exception objExUse = null;
    //specifying empty file name since I just want absolute path of correct folder
    string sVirtualPath = ConstructRMAProductImageVirtualPath(string.Empty, ref sErrorMessage);

    if (sErrorMessage != string.Empty) {
      VNAppExceptionHandler.LogException(
          new VNAppException(string.Format("{0}: FileName {1}", sErrorMessage, sFileName)),
          VNExceptionModeType.SILENT);

      return (string.Empty);
    }

    string sPhysicalPath = string.Empty;
    MemoryStream objMemStream = null;
    FileStream objFileStream = null;

    try {
      sPhysicalPath = string.Format("{0}{1}", WebUtilities.AddTrailingBackSlash(Server.MapPath(sVirtualPath)), sFileName);

      objMemStream = new MemoryStream(arrImage);
      objFileStream = new FileStream(sPhysicalPath, FileMode.Create);
      objMemStream.WriteTo(objFileStream);
      objMemStream.Close();
      objFileStream.Close();
      objFileStream.Dispose();

      if (File.Exists(sPhysicalPath))
        return (sPhysicalPath);
      else {
        sErrorMessage = "The new image was not created";
        return (string.Empty);
      }

    } catch (System.Exception objEx) {
      objExUse = objEx;

      sErrorMessage = objEx.Message;
      return (string.Empty);

    } finally {
      if (objMemStream != null)
        objMemStream.Close();

      if (objFileStream != null) {
        objFileStream.Close();
        objFileStream.Dispose();
      }

      if (objExUse != null && ConfigurationManager.AppSettings["sendErrorEmailsYN"] == "1") {
        try {
          bool bMailResult = EmailUtilities.SMTPSendMail(
              ConfigurationManager.AppSettings["exceptionEmailFrom"],
              string.Format("{0}: VTNBS Image Processor Error", ConfigurationManager.AppSettings["envTag"]),
              string.Format("Message: {0}, Stack Trace: {1}", objExUse.Message, objExUse.StackTrace),
              ConfigurationManager.AppSettings["exceptionEmailTo"],  //for multiples - will be delimited in single string
              string.Empty,                                          //CC
              string.Empty,                                          //BCC
              false,
              ConfigurationManager.AppSettings["mailServer"],
              ConfigurationManager.AppSettings["exceptionEmailUser"],
              ConfigurationManager.AppSettings["exceptionEmailPwd"],
              string.Empty,
              string.Empty);
        } catch {
        };
      }
    }
  }

  [WebMethod]
  public string UploadProductImage(bool bIsPreviewYN, string sOriginalFileName, byte[] arrImage, string sMfgPartNum, string sImageType, ref string sErrorMessage) {
    System.Exception objExUse = null;
    //specifying empty file name since I just want absolute path of correct folder
    string sVirtualPath = ConstructProductImageVirtualPath(bIsPreviewYN, string.Empty, sImageType, ref sErrorMessage);

    if (sErrorMessage != string.Empty) {
      VNAppExceptionHandler.LogException(
          new VNAppException(string.Format("{0}: MfgPartNum {1}", sErrorMessage, sMfgPartNum)),
          VNExceptionModeType.SILENT);

      return (string.Empty);
    }

    string sPhysicalPath = string.Empty;
    MemoryStream objMemStream = null;
    FileStream objFileStream = null;
    string sFinalFinalName = string.Empty;

    const int MAX_ATTEMPTS = 50;

    try {
      sPhysicalPath = WebUtilities.AddTrailingBackSlash(Server.MapPath(sVirtualPath));

      int iAttempt = 1;
      string sTentativeFullPath = string.Empty;
      string sTentativeFileName = string.Empty;

      do {
        sTentativeFileName = BLL.ConstructImageFileName(sOriginalFileName, sMfgPartNum, sImageType, iAttempt);
        sTentativeFullPath = sPhysicalPath + sTentativeFileName;

      } while (iAttempt++ < MAX_ATTEMPTS && File.Exists(sTentativeFullPath));

      //finished with loop...see if it succeeded or max attempts was reached

      if (File.Exists(sTentativeFullPath)) {
        sErrorMessage = "The max attempts was reached in trying to construct unique image file name";

        VNAppExceptionHandler.LogException(
            new VNAppException(string.Format("{0}: MfgPartNum {1}", sErrorMessage, sMfgPartNum)),
            VNExceptionModeType.SILENT);

        return (string.Empty);
      }

      //at this point we have a good filename to use for the new image

      objMemStream = new MemoryStream(arrImage);

      objFileStream = new FileStream(sTentativeFullPath, FileMode.Create);

      objMemStream.WriteTo(objFileStream);

      objMemStream.Close();

      objFileStream.Close();
      objFileStream.Dispose();

      if (File.Exists(sTentativeFullPath))
        return (sTentativeFileName);
      else {
        sErrorMessage = "The new image was not created";
        return (string.Empty);
      }

    } catch (System.Exception objEx) {
      objExUse = objEx;

      sErrorMessage = objEx.Message;
      return (string.Empty);

    } finally {
      if (objMemStream != null)
        objMemStream.Close();

      if (objFileStream != null) {
        objFileStream.Close();
        objFileStream.Dispose();
      }

      if (objExUse != null && ConfigurationManager.AppSettings["sendErrorEmailsYN"] == "1") {
        try {
          bool bMailResult = EmailUtilities.SMTPSendMail(
              ConfigurationManager.AppSettings["exceptionEmailFrom"],
              string.Format("{0}: VTNBS Image Processor Error", ConfigurationManager.AppSettings["envTag"]),
              string.Format("Message: {0}, Stack Trace: {1}", objExUse.Message, objExUse.StackTrace),
              ConfigurationManager.AppSettings["exceptionEmailTo"],  //for multiples - will be delimited in single string
              string.Empty,                                          //CC
              string.Empty,                                          //BCC
              false,
              ConfigurationManager.AppSettings["mailServer"],
              ConfigurationManager.AppSettings["exceptionEmailUser"],
              ConfigurationManager.AppSettings["exceptionEmailPwd"],
              string.Empty,
              string.Empty);
        } catch {
        };
      }
    }
  }

  [WebMethod]
  public bool ProductHasImagesYN(bool bIsPreviewYN, string sMfgPartNum, string sImageType, ref string sErrorMessage) {
    System.Exception objExUse = null;
    //See if this product has ANY files conforming to naming convention

    string sImageWildcard = BLL.ConstructImageFileNameWildcard(sMfgPartNum, sImageType);

    string sVirtualPath = ConstructProductImageVirtualPath(bIsPreviewYN, string.Empty, sImageType, ref sErrorMessage);

    if (sErrorMessage != string.Empty)
      return (false);

    string sPhysicalPath = string.Empty;

    try {
      sPhysicalPath = Server.MapPath(sVirtualPath);

      string[] lstFiles = Directory.GetFiles(sPhysicalPath, sImageWildcard, SearchOption.TopDirectoryOnly);

      return (lstFiles.Length > 0);

    } catch (System.Exception objEx) {
      objExUse = objEx;

      sErrorMessage = objEx.Message;
      return (false);

    } finally {
      if (objExUse != null && ConfigurationManager.AppSettings["sendErrorEmailsYN"] == "1") {
        try {
          bool bMailResult = EmailUtilities.SMTPSendMail(
              ConfigurationManager.AppSettings["exceptionEmailFrom"],
              string.Format("{0}: VTNBS Image Processor Error", ConfigurationManager.AppSettings["envTag"]),
              string.Format("Message: {0}, Stack Trace: {1}", objExUse.Message, objExUse.StackTrace),
              ConfigurationManager.AppSettings["exceptionEmailTo"],  //for multiples - will be delimited in single string
              string.Empty,                                          //CC
              string.Empty,                                          //BCC
              false,
              ConfigurationManager.AppSettings["mailServer"],
              ConfigurationManager.AppSettings["exceptionEmailUser"],
              ConfigurationManager.AppSettings["exceptionEmailPwd"],
              string.Empty,
              string.Empty);

        } catch {
        };
      }
    }
  }

  [WebMethod]
  public bool ProductImageExistsYN(bool bIsPreviewYN, string sFileName, string sImageType, ref string sErrorMessage) {
    System.Exception objExUse = null;

    string sVirtualPath = ConstructProductImageVirtualPath(bIsPreviewYN, sFileName, sImageType, ref sErrorMessage);

    if (sErrorMessage != string.Empty)
      return (false);

    string sPhysicalPath = string.Empty;

    try {
      sPhysicalPath = Server.MapPath(sVirtualPath);

      return (File.Exists(sPhysicalPath));

    } catch (System.Exception objEx) {
      objExUse = objEx;

      sErrorMessage = objEx.Message;
      return (false);

    } finally {
      if (objExUse != null && ConfigurationManager.AppSettings["sendErrorEmailsYN"] == "1") {
        try {
          bool bMailResult = EmailUtilities.SMTPSendMail(
              ConfigurationManager.AppSettings["exceptionEmailFrom"],
              string.Format("{0}: VTNBS Image Processor Error", ConfigurationManager.AppSettings["envTag"]),
              string.Format("Message: {0}, Stack Trace: {1}", objExUse.Message, objExUse.StackTrace),
              ConfigurationManager.AppSettings["exceptionEmailTo"],  //for multiples - will be delimited in single string
              string.Empty,                                          //CC
              string.Empty,                                          //BCC
              false,
              ConfigurationManager.AppSettings["mailServer"],
              ConfigurationManager.AppSettings["exceptionEmailUser"],
              ConfigurationManager.AppSettings["exceptionEmailPwd"],
              string.Empty,
              string.Empty);

        } catch {
        };
      }
    }
  }
}

}
