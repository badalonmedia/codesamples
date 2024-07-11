using System;
using System.Text;
using System.Data;
using System.Net;
using System.IO;

namespace GeneralUtilities {
/// <summary>
/// Class for submitting HTTP forms via POST and GET.
/// </summary>
public class HttpUtilities {
  /// <summary>
  /// Submits an HTTP form from datatable content.
  /// </summary>
  /// <param name="dtContent">Data table containing data to submit.</param>
  /// <param name="sFormUrl">HTTP form URL.</param>
  /// <param name="sProxy">Proxy URL.</param>
  /// <param name="sSubmitMethod">GET or POST.</param>
  /// <param name="iDelay">Delay, in miliseconds, between submissions.</param>
  /// <param name="iMaxConnections">Max connections to be opened by HTTP connection manager.</param>
  public static void SubmitHttpForm(DataTable dtContent,
                                    string sFormUrl,
                                    string sProxy,
                                    string sSubmitMethod,
                                    int iDelay,
                                    int iMaxConnections) {
    string sSubmitMethodUse =
        StringUtilities.TrimNull(sSubmitMethod).ToUpper();

    if (sSubmitMethodUse == "POST")
      SubmitHttpFormPost(dtContent,
                         sFormUrl,
                         sProxy,
                         iDelay,
                         iMaxConnections);
    else if (sSubmitMethodUse == "GET")
      SubmitHttpFormGet(dtContent,
                        sFormUrl,
                        sProxy,
                        iDelay,
                        iMaxConnections);

    else
      throw new ApplicationException("Unknown form submission method, must be POST or GET.");
  }

  /// <summary>
  /// Submits an HTTP form via GET from datatable content.
  /// </summary>
  /// <param name="dtContent">Data table containing data to submit.</param>
  /// <param name="sFormUrl">HTTP form URL.</param>
  /// <param name="sProxy">Proxy URL.</param>
  private static void SubmitHttpFormGet(DataTable dtContent,
                                        string sFormUrl,
                                        string sProxy) {
    //0 for iMaxConnections means don't even set that property
    //0 for iDelay means no delay
    SubmitHttpFormGet(dtContent, sFormUrl, sProxy, 0, 0);
  }

  /// <summary>
  /// Submits an HTTP form via GET from datatable content.
  /// </summary>
  /// <param name="dtContent">Data table containing data to submit.</param>
  /// <param name="sFormUrl">HTTP form URL.</param>
  /// <param name="sProxy">Proxy URL.</param>
  /// <param name="iDelay">Delay, in miliseconds, between submissions.</param>
  private static void SubmitHttpFormGet(DataTable dtContent,
                                        string sFormUrl,
                                        string sProxy,
                                        int iDelay) {
    //0 for iMaxConnections means don't even set that property
    SubmitHttpFormGet(dtContent, sFormUrl, sProxy, iDelay, 0);
  }

  /// <summary>
  /// Submits an HTTP form via GET from datatable content.
  /// </summary>
  /// <param name="dtContent">Data table containing data to submit.</param>
  /// <param name="sFormUrl">HTTP form URL.</param>
  /// <param name="sProxy">Proxy URL.</param>
  /// <param name="iDelay">Delay, in miliseconds, between submissions.</param>
  /// <param name="iMaxConnections">Max connections to be opened by HTTP connection manager.</param>
  private static void SubmitHttpFormGet(DataTable dtContent,
                                        string sFormUrl,
                                        string sProxy,
                                        int iDelay,  //Cliff 3/26/09: Added.  In ms
                                        int iMaxConnections) {
    if (dtContent == null || dtContent.Rows.Count < 1)
      throw new ApplicationException("Data table is null or empty");

    //Now we know the datatable has stuff in it

    StringBuilder sData = null;
    HttpWebRequest objRequest = null;
    Stream objStream = null;
    StreamReader objStreamReader = null;
    HttpWebResponse objResponse = null;

    try {
      //Iterate over rows - one lead per row, submit each one
      foreach (DataRow dr in dtContent.Rows) {
        sData = new StringBuilder(string.Empty);

        int iColCount = 0;

        //Construct querystring for submission
        foreach (DataColumn dc in dtContent.Columns) {
          if (iColCount > 0)
            sData.Append("&");
          else
            sData.Append("?");

          string sColVal = string.Empty;

          //Added additional processing here to avoid
          //applying UrlEncode to null
          if (dr[dc.ColumnName] != null && dr[dc.ColumnName] != DBNull.Value)
            sColVal = (string) dr[dc.ColumnName];

          //Added UrlEncode, and null comparison
          sData.Append(string.Format("{0}={1}",
                                     dc.ColumnName,
                                     System.Web.HttpUtility.UrlEncode(sColVal)));

          iColCount++;
        }

        objRequest =
            (HttpWebRequest) WebRequest.Create(sFormUrl + sData.ToString());

        objRequest.Method = "GET";  //Seems not to be needed

        objRequest.MaximumAutomaticRedirections = 4;
        objRequest.MaximumResponseHeadersLength = 4;
        objRequest.AllowWriteStreamBuffering = false;

        //This is kind of a black box property because I don't know
        //how these connections are managed. But .NET does some sort of cleanup
        //behind the scenes of these connections, so that 25 connections enabled
        //me to send 170 leads, whereas 2 connections caused a timeout.
        if (iMaxConnections > 0)
          objRequest.ServicePoint.ConnectionLimit = iMaxConnections;

        Console.WriteLine(sFormUrl + sData.ToString());

        objRequest.Credentials = CredentialCache.DefaultCredentials;
        objResponse = (HttpWebResponse) objRequest.GetResponse();

        objStream = objResponse.GetResponseStream();

        //if you want to read the contents returned from the
        //request, then uncomment the next two lines.

        objStream.Flush();  //Flush and close for each submit
        objStream.Close();

        objStream.Dispose();

        //Delay - if iDelay > 0.
        //Trying to avoid issues with sending too many records too quickly.
        if (iDelay > 0)
          System.Threading.Thread.Sleep(iDelay);
      }

    } catch (Exception objEx) {
      throw objEx;

    } finally {
      if (objResponse != null) {
        try {
          objResponse.Close();
        } catch {
        }
      }

      if (objStreamReader != null) {
        try {
          objStreamReader.Close();
        } catch {
        }
      }

      if (objStreamReader != null) {
        try {
          objStreamReader.Dispose();
        } catch {
        }
      }
    }
  }

  /// <summary>
  /// Submits an HTTP form via POST from datatable content.
  /// </summary>
  /// <param name="dtContent">Data table containing data to submit.</param>
  /// <param name="sFormUrl">HTTP form URL.</param>
  /// <param name="sProxy">Proxy URL.</param>
  private static void SubmitHttpFormPost(DataTable dtContent,
                                         string sFormUrl,
                                         string sProxy) {
    //0 for connections will tell the overload not to try and set that property
    //0 for iDelay means no delay

    SubmitHttpFormPost(dtContent, sFormUrl, sProxy, 0, 0);
  }

  /// <summary>
  /// Submits an HTTP form via POST from datatable content.
  /// </summary>
  /// <param name="dtContent">Data table containing data to submit.</param>
  /// <param name="sFormUrl">HTTP form URL.</param>
  /// <param name="sProxy">Proxy URL.</param>
  /// <param name="iDelay">Delay, in miliseconds, between submissions.</param>
  private static void SubmitHttpFormPost(DataTable dtContent,
                                         string sFormUrl,
                                         string sProxy,
                                         int iDelay) {
    //0 for connections will tell the overload not to try and set that property

    SubmitHttpFormPost(dtContent, sFormUrl, sProxy, iDelay, 0);
  }

  /// <summary>
  /// Submits an HTTP form via POST from datatable content.
  /// </summary>
  /// <param name="dtContent">Data table containing data to submit.</param>
  /// <param name="sFormUrl">HTTP form URL.</param>
  /// <param name="sProxy">Proxy URL.</param>
  /// <param name="iDelay">Delay, in miliseconds, between submissions.</param>
  /// <param name="iMaxConnections">Max connections to be opened by HTTP connection manager.</param>
  private static void SubmitHttpFormPost(DataTable dtContent,
                                         string sFormUrl,
                                         string sProxy,
                                         int iDelay,
                                         int iMaxConnections) {
    if (dtContent == null || dtContent.Rows.Count < 1)
      throw new ApplicationException("Data table is null or empty");

    //Now we know the datatable has stuff in it

    StringBuilder sData = null;
    HttpWebRequest objRequest = null;
    Stream objStream = null;

    try {
      //Iterate over rows - one lead per row, submit each one
      foreach (DataRow dr in dtContent.Rows) {
        objRequest = (HttpWebRequest) WebRequest.Create(sFormUrl);
        sData = new StringBuilder(string.Empty);

        int iColCount = 0;

        //Construct string to be submitted - represents one lead
        foreach (DataColumn dc in dtContent.Columns) {
          if (iColCount > 0)
            sData.Append("&");

          string sColVal = string.Empty;

          //Added additional processing here to avoid
          //applying UrlEncode to null
          if (dr[dc.ColumnName] != null && dr[dc.ColumnName] != DBNull.Value)
            sColVal = (string) dr[dc.ColumnName];

          //Added UrlEncode, and null comparison
          sData.Append(string.Format("{0}={1}",
                                     dc.ColumnName,
                                     System.Web.HttpUtility.UrlEncode(sColVal)));

          iColCount++;
        }

        Console.WriteLine(sData.ToString());

        byte[] arrBuffer = Encoding.UTF8.GetBytes(sData.ToString());

        objRequest.Method = "POST";
        objRequest.ContentType = "application/x-www-form-urlencoded";
        objRequest.ContentLength = arrBuffer.Length;
        objRequest.AllowWriteStreamBuffering = false;

        //This is kind of a black box property because I don't know
        //how these connections are managed. But .NET does some sort of cleanup
        //behind the scenes of these connections, so that 25 connections enabled
        //me to send 170 leads, whereas 2 connections caused a timeout.
        if (iMaxConnections > 0)
          objRequest.ServicePoint.ConnectionLimit = iMaxConnections;

        if (StringUtilities.IsNullOrEmptyOrSpaces(sProxy))
          objRequest.Proxy = null;
        else
          objRequest.Proxy = new WebProxy(sProxy, true);

        objStream = objRequest.GetRequestStream();

        objStream.Write(arrBuffer, 0, arrBuffer.Length);
        objStream.Flush();  //Flush and close for each submit
        objStream.Close();

        objStream.Dispose();

        //Delay - if iDelay > 0.
        //Trying to avoid issues with sending too many records too quickly.
        if (iDelay > 0)
          System.Threading.Thread.Sleep(iDelay);
      }

    } catch (Exception objEx) {
      throw objEx;

    } finally {
      if (objStream != null) {
        try {
          objStream.Flush();
        } catch {
        }

        try {
          objStream.Close();
        } catch {
        }
      }
    }
  }
}

}
