using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Services;

namespace TMTestLeadReceiveService {
/// <summary>
/// Summary description for TMLeadReceiveService
/// </summary>
[WebService(Namespace = "http://TMLeadReceiveInternalService.org/")]
[WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
[System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line.
    // [System.Web.Script.Services.ScriptService]
    public class TMLeadReceiveInternalService : System.Web.Services.WebService {
  [WebMethod]
  public string TestMethod(string test) {
    return "Hello World Internal" + test;
  }

  //This overload is intended for consumption internally
  [WebMethod]
  public string ReceiveLead(
      string svccode,        //system
      string savelead,       //system - 1 or 0
      string dophoneverify,  //system - 1 or 0
      string fname,
      string lname,
      string homephone,
      string officecellphone,  //optional
      string emailaddress,
      string moveweight,  //one of our numeric ID's
      string movedate,    //mm/dd/yy
      string fromstate,
      string fromcity,
      string frompostalcode,
      string tostate,
      string tocity,
      string topostalcode,  //optional
      string comments)      //optional
  {
    TMLeadReceiveHelper.TMLeadReceiveHelper objHelper = new TMLeadReceiveHelper.TMLeadReceiveHelper();

    string sResult = objHelper.ReceiveLeadInternal(
        svccode,
        savelead,
        dophoneverify,
        fname,
        lname,
        homephone,
        officecellphone,
        emailaddress,
        moveweight,
        movedate,
        fromstate,
        fromcity,
        frompostalcode,
        tostate,
        tocity,
        topostalcode,
        comments);

    return (sResult);
  }
}

}
