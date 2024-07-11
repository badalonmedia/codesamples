using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;
using System.Text.RegularExpressions;
using System.Web.Mail;
using LetsGo.BusinessLogicLayer;
using TMTestLeadReceiveService.com.arvixevps.letsgo.com.LetsGo.leadverify;

namespace TMLeadReceiveHelper {
public class TMLeadReceiveHelper {
  //Cliff: Revisit this and add some truly consolidate error email code.
  private bool SendErrorEmail(string sSubject, string sBody) {
    MailMessage objMessage = new MailMessage();
    objMessage.BodyFormat = MailFormat.Text;
    objMessage.To = Globals.WebmasterNotifyEmail;
    objMessage.From = Globals.MainSystemEmail;
    objMessage.Subject = sSubject;
    objMessage.Body = sBody.ToString();

    SmtpMail.SmtpServer = Globals.SmtpServer;

    try {
      SmtpMail.Send(objMessage);
    } catch {
      return (false);
    }

    return (true);
  }

  public string ReceiveLeadInternal(
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
    try {
      int iVendorID = 0;
      bool bIsTestSystemYN = Convert.ToBoolean(ConfigurationManager.AppSettings["IsTestSystemYN"]);

      bool bPerformPhoneVerificationYN;

      if (dophoneverify != null)
        bPerformPhoneVerificationYN = (dophoneverify == "1" ? true : false);
      else  //is null so use appsetting
        bPerformPhoneVerificationYN = Convert.ToBoolean(ConfigurationManager.AppSettings["PerformPhoneVerificationYN"]);

      if ((svccode ?? string.Empty).Trim() == string.Empty)  //GET OUT
        return ("ERROR: Service code was not provided");

      bool bIsVendorValidYN = Vendor.IsValidYN(svccode.Trim(), ref iVendorID);

      if (!bIsVendorValidYN)  //GET OUT
        return ("ERROR: Service Code does not exist or is inactive");

      //Proceed - Vendor service code is good

      Leads objLead = null;

      string sValidationResult = ValidateLead(
          svccode,
          fname,
          lname,
          homephone,
          officecellphone,  //optional
          emailaddress,
          moveweight,  //one of our numeric ID's
          movedate,    //mm/dd/yy
          fromstate,
          fromcity,
          frompostalcode,
          tostate,
          tocity,
          topostalcode,  //optional
          comments,
          ref objLead);

      if (sValidationResult != string.Empty)
        return (sValidationResult);  //get out and don't continue

      bool bDowngradeToPending = false;
      string sPendingReason = string.Empty;

      objLead.IPAddress = "N/A";  //REVISIT: just leave blank?

      Vendor objVendor = Vendor.SelectOne(iVendorID);

      if (objVendor.AutoPendingYN)  //auto pending is true, so send to pending regardless
      {
        bDowngradeToPending = true;  //save lead but as pending, not approved

        sPendingReason = objVendor.AutoPendingReason;  // "Vendor Auto Pending";
      } else if (bPerformPhoneVerificationYN) {
        //assume home phone is populated

        string sTeleSignPwd = SiteSettings.GetSiteSettings().TeleSignInternalPwd;

        TMLeadVerificationService objSvc = new TMLeadVerificationService();
        VerificationResult objPhoneResult =
            objSvc.VerifyPhoneNumber(objLead.HomePhone, objLead.LastName, sTeleSignPwd);

        if (objPhoneResult == null ||
            !objPhoneResult.IsSuccessYN) {
          bDowngradeToPending = true;  //save lead but as pending, not approved

          sPendingReason = "Home Phone verification, TeleSign error";
        } else if (!objPhoneResult.IsAuthenticatedYN) {
          bDowngradeToPending = true;  //save lead but as pending, not approved

          sPendingReason = "Home Phone verification, Internal Auth";
        } else if (!objPhoneResult.IsPreValidYN) {
          bDowngradeToPending = true;  //save lead but as pending, not approved

          sPendingReason = "Home Phone verification, 10 or 1+ 10 digits";
        } else if (!objPhoneResult.IsMatchYN) {
          bDowngradeToPending = true;  //save lead but as pending, not approved

          sPendingReason = "Home Phone verification, no match";
        }

        if (!bDowngradeToPending && objLead.OfficePhone != string.Empty)  //verify office/cell phone as well
        {
          objPhoneResult =
              objSvc.VerifyPhoneNumber(objLead.OfficePhone, objLead.LastName, sTeleSignPwd);

          if (objPhoneResult == null ||
              !objPhoneResult.IsSuccessYN) {
            bDowngradeToPending = true;  //save lead but as pending, not approved

            sPendingReason = "Office/Cell Phone verification, TeleSign error";
          } else if (!objPhoneResult.IsAuthenticatedYN) {
            bDowngradeToPending = true;  //save lead but as pending, not approved

            sPendingReason = "Office/Cell Phone verification, Internal Auth";
          } else if (!objPhoneResult.IsPreValidYN) {
            bDowngradeToPending = true;  //save lead but as pending, not approved

            sPendingReason = "Office/Cell Phone verification, 10 or 1+ 10 digits";
          } else if (!objPhoneResult.IsMatchYN) {
            bDowngradeToPending = true;  //save lead but as pending, not approved

            sPendingReason = "Office/Cell Phone verification, no match";
          }
        }
      }

      //phone verification is done, proceed with saving lead
      int iLeadID = 0;
      bool bVendorLeadResult = false;

      objLead.LeadID = 0;
      objLead.DateArrived = DateTime.Now;

      //TODO: Add error handling here!!!

      if (savelead == "1") {
        if (bIsTestSystemYN)  //test system
        {
          iLeadID = Leads.InsertTest(objLead);

          bVendorLeadResult = VendorLeads.AddTestLead(iLeadID, iVendorID);

          objLead.LeadID = iLeadID;

        } else  //live system
        {
          LeadsFacade objLeadFacade = new LeadsFacade(objLead);

          string sLeadResult = objLeadFacade.SendVendorLead(sPendingReason);

          if (sLeadResult != string.Empty)
            throw new ApplicationException(sLeadResult);

          bVendorLeadResult = VendorLeads.AddLead(objLead.LeadID, iVendorID);
        }
      }

      return (objLead.LeadID.ToString());

    } catch (Exception objEx) {
      StringBuilder sEmailBody = new StringBuilder(string.Empty);
      sEmailBody.AppendFormat("First Name: {0}", fname);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("Last Name: {0}", lname);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("Home Phone: {0}", homephone);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("Office/Cell Phone: {0}", officecellphone);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("Email Address: {0}", emailaddress);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("Move Weight: {0}", moveweight);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("Move Date: {0}", movedate);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("From State: {0}", fromstate);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("From City: {0}", fromcity);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("From Zip: {0}", frompostalcode);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("To State: {0}", tostate);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("To City: {0}", tocity);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("To Zip: {0}", topostalcode);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("Comments: {0}", comments);
      sEmailBody.AppendLine();
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("EXCEPTION INFO: {0}", objEx.Message);
      sEmailBody.AppendLine();
      sEmailBody.AppendLine();

      if (objEx.InnerException != null) {
        sEmailBody.AppendFormat("INNER EXCEPTION INFO: {0}", objEx.InnerException.Message);
        sEmailBody.AppendLine();
      }

      bool bErrorMailResult =
          SendErrorEmail(string.Format("Lead Receive Error: Vendor Svc Code: {0}", svccode),
                         sEmailBody.ToString());

      return (string.Format("ERROR: A runtime error occurred while processing the lead: {0}", objEx.Message));

    } finally {
    }
  }

  //Simulate validation performed by public web form
  public string ValidateLead(
      string sSvcCode,  //only for including in error emails
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
      string comments,
      ref Leads objLead) {
    StringBuilder sResult = new StringBuilder(string.Empty);
    objLead = new Leads();

    try {
      //FIRST NAME
      string sFirstName = (fname ?? string.Empty).Trim();
      if (sFirstName != string.Empty)
        objLead.FirstName = sFirstName;
      else  //invalid
        sResult.AppendFormat("{0}ERROR: First Name was not provided", sResult.Length > 0 ? ", " : string.Empty);

      //LAST NAME
      string sLastName = (lname ?? string.Empty).Trim();
      if (sLastName != string.Empty)
        objLead.LastName = sLastName;
      else  //invalid
        sResult.AppendFormat("{0}ERROR: Last Name was not provided", sResult.Length > 0 ? ", " : string.Empty);

      //HOME PHONE
      string sHomePhone = (homephone ?? string.Empty).Trim();
      if (sHomePhone != string.Empty)
        objLead.HomePhone = sHomePhone;
      else  //invalid
        sResult.AppendFormat("{0}ERROR: Home Phone was not provided", sResult.Length > 0 ? ", " : string.Empty);

      //EMAIL ADDRESS
      string sEmailAddress = (emailaddress ?? string.Empty).Trim();
      if (sEmailAddress == string.Empty)
        sResult.AppendFormat("{0}ERROR: Email Address was not provided", sResult.Length > 0 ? ", " : string.Empty);
      else if (!IsValidEmailYN(emailaddress.Trim()))
        sResult.AppendFormat("{0}ERROR: Email Address is not in the correct format", sResult.Length > 0 ? "," : string.Empty);
      else  //valid
        objLead.Email = sEmailAddress;

      //MOVE WEIGHT
      int iMoveWeightID;
      string sMoveWeightID = (moveweight ?? string.Empty).Trim();
      if (sMoveWeightID == string.Empty)
        sResult.AppendFormat("{0}ERROR: Move Weight was not provided", sResult.Length > 0 ? ", " : string.Empty);
      else {
        bool bIsMoveWeightNumericYN = int.TryParse(sMoveWeightID, out iMoveWeightID);

        if (!bIsMoveWeightNumericYN)
          sResult.AppendFormat("{0}ERROR: Move Weight must be numeric and one of the accepted values", sResult.Length > 0 ? ", " : string.Empty);
        else  //it's numeric but need to look it up
        {
          MoveWeight objMoveWeight = MoveWeight.SelectOne(iMoveWeightID);

          if (objMoveWeight == null)  //was not found
            sResult.AppendFormat("{0}ERROR: Move Weight must be numeric and one of the accepted values", sResult.Length > 0 ? ", " : string.Empty);
          else
            objLead.EstimateMoveWeightID = iMoveWeightID;
        }
      }

      //MOVE DATE
      DateTime dtMoveDate;
      string sMoveDate = (movedate ?? string.Empty).Trim();
      if (sMoveDate == string.Empty)
        sResult.AppendFormat("{0}ERROR: Move Date was not provided", sResult.Length > 0 ? ", " : string.Empty);
      else {
        bool bIsMoveDateValidYN = DateTime.TryParse(sMoveDate, out dtMoveDate);

        if (!bIsMoveDateValidYN)
          sResult.AppendFormat("{0}ERROR: Move Date must be a valid date in mm/dd/yy format", sResult.Length > 0 ? ", " : string.Empty);
        else
          objLead.MovingDate = dtMoveDate;
      }

      //FROM STATE
      string sFromState = (fromstate ?? string.Empty).Trim().ToUpper();
      if (sFromState == string.Empty)
        sResult.AppendFormat("{0}ERROR: Origin State was not provided", sResult.Length > 0 ? ", " : string.Empty);
      else {
        States objFromState = States.SelectOne(sFromState);

        if (objFromState == null)
          sResult.AppendFormat("{0}ERROR: Origin State is invalid", sResult.Length > 0 ? ", " : string.Empty);
        else
          objLead.MovingFromStateID = objFromState.StateID;
      }

      //FROM CITY
      string sFromCity = (fromcity ?? string.Empty).Trim();
      if (sFromCity != string.Empty)
        objLead.MovingFromCity = sFromCity;
      else  //invalid
        sResult.AppendFormat("{0}ERROR: Origin City was not provided", sResult.Length > 0 ? ", " : string.Empty);

      //FROM ZIP
      string sFromPostalCode = (frompostalcode ?? string.Empty).Trim();
      if (sFromPostalCode != string.Empty)
        objLead.MovingFromZip = sFromPostalCode;
      else  //invalid
        sResult.AppendFormat("{0}ERROR: Origin Postal Code was not provided", sResult.Length > 0 ? ", " : string.Empty);

      //TO STATE
      string sToState = (tostate ?? string.Empty).Trim().ToUpper();
      if (sToState == string.Empty)
        sResult.AppendFormat("{0}ERROR: Destination State was not provided", sResult.Length > 0 ? ", " : string.Empty);
      else {
        States objToState = States.SelectOne(sToState);

        if (objToState == null)
          sResult.AppendFormat("{0}ERROR: Destination State is invalid", sResult.Length > 0 ? ", " : string.Empty);
        else
          objLead.MovingToStateID = objToState.StateID;
      }

      //TO CITY
      string sToCity = (tocity ?? string.Empty).Trim();
      if (sToCity != string.Empty)
        objLead.MovingToCity = sToCity;
      else  //invalid
        sResult.AppendFormat("{0}ERROR: Destination City was not provided", sResult.Length > 0 ? ", " : string.Empty);

      //optional items
      objLead.OfficePhone = (officecellphone ?? string.Empty).Trim();
      objLead.MovingToZip = (topostalcode ?? string.Empty).Trim();
      objLead.Comments = (comments ?? string.Empty).Trim();

    } catch (Exception objEx) {
      StringBuilder sEmailBody = new StringBuilder(string.Empty);
      sEmailBody.AppendFormat("First Name: {0}", fname);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("Last Name: {0}", lname);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("Home Phone: {0}", homephone);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("Office/Cell Phone: {0}", officecellphone);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("Email Address: {0}", emailaddress);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("Move Weight: {0}", moveweight);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("Move Date: {0}", movedate);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("From State: {0}", fromstate);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("From City: {0}", fromcity);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("From Zip: {0}", frompostalcode);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("To State: {0}", tostate);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("To City: {0}", tocity);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("To Zip: {0}", topostalcode);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("Comments: {0}", comments);
      sEmailBody.AppendLine();

      bool bErrorMailResult =
          SendErrorEmail(string.Format("Lead Validation Error: Vendor Svc Code: {0}", sSvcCode),
                         sEmailBody.ToString());

      sResult.AppendFormat("{0}ERROR: A runtime error occurred during lead validation: {1}", sResult.Length > 0 ? ", " : string.Empty, objEx.Message);

      return (sResult.ToString());

    } finally {
    }

    if (sResult.ToString() != string.Empty) {
      StringBuilder sEmailBody = new StringBuilder(string.Empty);
      sEmailBody.AppendFormat("Validation Message: {0}", sResult.ToString());  //include this as well
      sEmailBody.AppendLine();
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("First Name: {0}", fname);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("Last Name: {0}", lname);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("Home Phone: {0}", homephone);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("Office/Cell Phone: {0}", officecellphone);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("Email Address: {0}", emailaddress);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("Move Weight: {0}", moveweight);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("Move Date: {0}", movedate);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("From State: {0}", fromstate);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("From City: {0}", fromcity);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("From Zip: {0}", frompostalcode);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("To State: {0}", tostate);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("To City: {0}", tocity);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("To Zip: {0}", topostalcode);
      sEmailBody.AppendLine();
      sEmailBody.AppendFormat("Comments: {0}", comments);
      sEmailBody.AppendLine();

      //string sErrorMailResult =
      bool bErrorMailResult =
          SendErrorEmail(string.Format("Lead Failed Validation: Vendor Svc Code: {0}", sSvcCode),
                         sEmailBody.ToString());

      //return (sErrorMailResult);  //REVISIT: COMMENT OUT
    }

    return (sResult.ToString());
  }

  private bool IsValidEmailYN(string sItem) {
            Regex objPattern = new Regex(@"^(([^<>()[\]\\.,;:\s@\""]+(\.[^<>()[\]\\.,;:\s@\""]+)*)|(\"".+\""))@((\[[0-9]{
      1, 3}\.[0-9]{
      1, 3}\.[0-9]{
      1, 3}\.[0-9]{
      1, 3}\])|(([a-zA-Z\-0-9]+\.)+[a-zA-Z]{2,}))$");
            
            return (objPattern.IsMatch(sItem.ToLower()));
  }
}

}
