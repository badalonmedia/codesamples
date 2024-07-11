using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System.Transactions;
using System.Configuration;
using Microsoft.VisualBasic;  //for DateDiff
using GeneralUtilities;
using LeadCollectionDAL;
using LCSAppExceptionSystem;
using LCSLogging;
using LCSSvcErrorCodes;
using LCSSharedDataContracts;
using LCSDataStore;
using LCSLoggingCommon;
using HallmarkTools;

namespace LCSApiService {
/// <summary>
/// LCS API Service Implementation
/// </summary>
public class LCSApiSvc : ILCSApiService {
  private LCS _dbLCS = null;
  private string _sConn = null;
  private Logging _objLogging = null;

  //LOGGING: If this service were being used as a class, in process, I would always rely
  //on initializing the logging in the constructor and cleaning it up in the destructor,
  //but when being used as a service out of process, that model is not as reliable.  So,
  //the LogIt method in this class, is equipped to create the logging object if needed.
  //Additionally, each method here will sever the logging connection to prevent file contention.

  /// <summary>
  /// Releases unmanaged resources and performs other cleanup operations before the
  /// <see cref="LCSApiSvc"/> is reclaimed by garbage collection.
  /// </summary>
  ~LCSApiSvc() {
    SafeDisposeLCS(_dbLCS);

    KillLogging();
  }

  /// <summary>
  /// Initializes a new instance of the <see cref="LCSApiService"/> class.
  /// </summary>
  public LCSApiSvc() {
    _sConn = ConfigConstants.LCS_DB;

    //REVISIT: Would be great to determine identity of caller here.
    //So I can prepopulate the CreatedBy column

    if (ConfigConstants.LoggingYN) {
      if (_objLogging == null) {
        _objLogging = new Logging((Hashtable) ConfigurationManager.GetSection("loggingSettings"));
        _objLogging.AddEntry("LOGGING INITIATED");
      }
    }
  }

  /// <summary>
  /// Authenticates the specified user.
  /// </summary>
  /// <param name="sUser">User Id</param>
  /// <param name="sPwd">Password</param>
  /// <returns>Success/Failure</returns>
  private bool Authenticate(string sUser, string sPwd) {
    //Assumes that caller credentials are always encrypted
    bool bResult = true;
    Hashtable hshUsers = (Hashtable) ConfigurationManager.GetSection("apiService_Users");

    if (ConfigConstants.TargetCredentialsEncryptedYN)  //means that credentials are encrypted in web.config
    {
      if (!hshUsers.ContainsKey(sUser))
        bResult = false;
      else if ((string) hshUsers[sUser] != sPwd)
        bResult = false;

    } else {
      // Uses crypto utilities provided by Bogdan
      if (!hshUsers.ContainsKey(CryptoUtilities.Decrypt(sUser)))
        bResult = false;
      else if ((string) hshUsers[CryptoUtilities.Decrypt(sUser)] != CryptoUtilities.Decrypt(sPwd))
        bResult = false;
    }

    return (bResult);
  }

  /// <summary>
  /// Safely disposes the LCS data context.
  /// </summary>
  /// <param name="objDb">Data context</param>
  private void SafeDisposeLCS(LCS objDb) {
    if (objDb != null) {
      try {
        objDb.Dispose();
        objDb = null;

      } catch {
      }
    }
  }

  /// <summary>
  /// Release logging object resources.
  /// </summary>
  private void KillLogging() {
    try {
      _objLogging.Close();
    } catch {
    } finally {
      _objLogging = null;
    }
  }

  /// <summary>
  /// Wrapper to logging object.
  /// </summary>
  /// <param name="sEntry">The entry to log</param>
  private void LogIt(string sEntry) {
    try {
      if (ConfigConstants.LoggingYN) {
        if (_objLogging == null) {
          _objLogging = new Logging((Hashtable) ConfigurationManager.GetSection("loggingSettings"));
          _objLogging.AddEntry("LOGGING INITIATED");
        }

        _objLogging.AddEntry(sEntry);
      }

    } catch {
    }
  }

  /// <summary>
  /// Wrapper to logging object.
  /// </summary>
  /// <param name="sEntry">The entry to log</param>
  private void VerboseLogIt(string sEntry) {
    if (ConfigConstants.VerboseLoggingYN)
      LogIt(sEntry);
  }

  /// <summary>
  /// Ensures data context is not null.
  /// </summary>
  private void CheckDB() {
    if (_dbLCS == null)
      _dbLCS = new LCS(_sConn);
  }

  /// <summary>
  /// Validate lead request against certain conditions.
  /// </summary>
  /// <param name="objLCSRequest">The lead request</param>
  /// <param name="sErrorCode">Error code to be returned</param>
  private void ValidateLCSRequest(LCSRequestInfoType objLCSRequest, ref string sErrorCode) {
    //First inspect email address
    if (StringUtilities.IsNullOrEmptyOrSpaces(objLCSRequest.UserEmailAddress))
      sErrorCode = LCSSvcErrorCodeLookup.Email_Empty;

    if (!WebUtilities.IsValidEmailYN(objLCSRequest.UserEmailAddress.Trim()))
      sErrorCode = LCSSvcErrorCodeLookup.Email_Invalid;

    //Inspect site code
    if (StringUtilities.IsNullOrEmptyOrSpaces(objLCSRequest.SiteCode))
      sErrorCode = LCSSvcErrorCodeLookup.SiteCode_Empty;

    //Inspect return url
    if (StringUtilities.IsNullOrEmptyOrSpaces(objLCSRequest.ReturnURL))
      sErrorCode = LCSSvcErrorCodeLookup.ReturnURL_Empty;

    //Inspect param name
    if (StringUtilities.IsNullOrEmptyOrSpaces(objLCSRequest.ParamName))
      sErrorCode = LCSSvcErrorCodeLookup.ParamName_Empty;

    //Inspect asset codes
    if (objLCSRequest.AssetList == null || objLCSRequest.AssetList.Count < 1)
      sErrorCode = LCSSvcErrorCodeLookup.AssetList_Empty;

    //REVISIT: Do any further inspection?
  }

  /// <summary>
  /// Gets the active campaign (if any) associated with the specified asset code.
  /// </summary>
  /// <param name="sAssetCode">The asset code</param>
  /// <param name="gAssetUid">The asset guid to be returned</param>
  /// <param name="gSiteGroupUid">The site groupd guid to be returned</param>
  /// <param name="sErrorCode">The error code to be returned</param>
  /// <returns>Guid of active campaign</returns>
  private System.Guid GetEligibleCampaign(
      string sAssetCode,
      ref System.Nullable<System.Guid> gAssetUid,
      ref System.Nullable<System.Guid> gSiteGroupUid,
      ref string sErrorCode) {
    string sMethodName = string.Empty;

    try {
      sErrorCode = string.Empty;
      sMethodName = DebuggingUtilities.GetCurrentMethodName(0);

      LogIt(string.Format("{0}: Begin", sMethodName));

      System.Guid gResult = System.Guid.Empty;

      LCS_spGetCampaign_ByAssetCodeResult objCampaign = null;

      var collCampaign = _dbLCS.LCS_spGetCampaign_ByAssetCode(sAssetCode).ToList();

      //Look at count of collection...
      //Should never be more than 1, but may be 0 if campaign has been deleted

      if (collCampaign.Count == 1) {
        objCampaign = collCampaign[0];

        //There is 1 campaign for this asset, now check status
        if (objCampaign.CampaignStatusCode == DBConstants.CampaignStatusType.ACTIVE) {
          gResult = (System.Guid) objCampaign.CampaignUid;

          //Also save some campaign info
          gAssetUid = (System.Guid) objCampaign.AssetUid;
          gSiteGroupUid = (System.Guid) objCampaign.SiteGroupUid;

          VerboseLogIt(string.Format("{0}: Eligible Campaign: {1}", sMethodName, gResult.ToString()));
        }

      } else if (collCampaign.Count > 1)  //Should not happen, but handle it
        sErrorCode = LCSSvcErrorCodeLookup.AssetCampaignCount_DataError;

      //Otherwise, count is 0 and we don't have to do anything

      return (gResult);

    } catch (Exception objEx) {
      LogIt(string.Format("{0}: Error: {1}", sMethodName, objEx.Message));

      AppUtilities.LogExceptionWrapper(new LCSAppException(objEx), LCSExceptionModeType.Silent);

      sErrorCode = LCSSvcErrorCodeLookup.UnknownError;

    } finally {
      LogIt(string.Format("{0}: End", sMethodName));
    }

    return (System.Guid.Empty);
  }

  /// <summary>
  /// CMS calls this method in the stub service which passes through to this method
  /// to get the asset keys that were part of a prior lead request.
  /// This is how the CMS persists this asset info on a page.
  /// </summary>
  /// <remarks>
  /// The asset keys are not the same as internal LCS asset codes.
  /// </remarks>
  /// <param name="gRequestVerificationUid">The guid that points to the lead request</param>
  /// <param name="sUser">Service user id</param>
  /// <param name="sPwd">Service user password</param>
  /// <param name="sErrorCode">Error code to be returned</param>
  /// <returns>List of asset keys</returns>
  public List<string> GetCMSAssetKeys(
      System.Guid gRequestVerificationUid,
      string sUser,
      string sPwd,
      ref string sErrorCode) {
    if (!Authenticate(sUser, sPwd))  //Authenticate
    {
      sErrorCode = LCSSvcErrorCodeLookup.Authentication_Failed;
      LogIt(string.Format("Authentication Failed"));
      return (null);
    }

    string sMethodName = string.Empty;
    List<string> collAssetKeysResult = null;

    try {
      sMethodName = DebuggingUtilities.GetCurrentMethodName(0);
      sErrorCode = string.Empty;

      LogIt(string.Format("{0}: Begin", sMethodName));
      LogIt(string.Format("{0}: RequestVerificationUid: {1}", sMethodName, gRequestVerificationUid.ToString()));

      CheckDB();

      var collAssetKeys =
          _dbLCS.LCS_spGetCMSAssetKeys_ByQuestionStageUid(gRequestVerificationUid).ToList();

      LogIt(string.Format("{0}: Asset Count: {1}", sMethodName, collAssetKeys.Count.ToString()));

      if (collAssetKeys.Count > 0) {
        collAssetKeysResult = new List<string>();

        // Iterate through the results and construct simple list of asset keys
        foreach (LCS_spGetCMSAssetKeys_ByQuestionStageUidResult objAssetKey in collAssetKeys) {
          collAssetKeysResult.Add(objAssetKey.AssetKey);

          VerboseLogIt(string.Format("{0}: Asset Key: {1}", sMethodName, objAssetKey.AssetKey));
        }
      }

      return (collAssetKeysResult);

    } catch (Exception objEx) {
      LogIt(string.Format("{0}: Error: {1}", sMethodName, objEx.Message));

      AppUtilities.LogExceptionWrapper(new LCSAppException(objEx), LCSExceptionModeType.Silent);

      sErrorCode = LCSSvcErrorCodeLookup.UnknownError;

    } finally {
      LogIt(string.Format("{0}: End", sMethodName));

      SafeDisposeLCS(_dbLCS);

      KillLogging();
    }

    return (null);
  }

  /// <summary>
  /// Gets the unanswered custom questions, including available responses.
  /// </summary>
  /// <param name="gQuestionStageUid">guid for QuestionStage table entry</param>
  /// <param name="sErrorCode">Error code to be returned</param>
  /// <returns>List of questions and available responses</returns>
  private List<CustomQuestion> GetUnansweredQuestions_WithResponses(
      System.Guid gQuestionStageUid,
      ref string sErrorCode) {
    string sMethodName = string.Empty;

    try {
      sMethodName = DebuggingUtilities.GetCurrentMethodName(0);
      sErrorCode = string.Empty;

      LogIt(string.Format("{0}: Begin", sMethodName));

      CheckDB();

      var collQuestion =
          _dbLCS.LCS_spGetUnansweredCustomQuestions_ByStageUid(gQuestionStageUid).ToList();

      //Massage results to fit in data contract

      if (collQuestion.Count < 1)  //Should not happen unless GUID is bogus or old
      {
        VerboseLogIt(string.Format("{0}: No Questions For: {1}", sMethodName, gQuestionStageUid.ToString()));

        return (null);
      }

      //Build collection to be returned.
      //The idea is that I'm exposing only the required fields in the data contract
      //that the client proxy will have.  So I need to populate those fields here.

      List<CustomQuestion> collQuestionSVC = new List<CustomQuestion>();

      foreach (LCS_spGetUnansweredCustomQuestions_ByStageUidResult objQuestion in collQuestion) {
        CustomQuestion objQuestionSVC = new CustomQuestion();

        objQuestionSVC.IsRequiredYN = (bool) objQuestion.IsRequiredYN;
        objQuestionSVC.QuestionText = objQuestion.QuestionText;
        objQuestionSVC.QuestionType = objQuestion.QuestionType;
        objQuestionSVC.Position = (int) objQuestion.Position;
        objQuestionSVC.CampaignQuestionUid = (System.Guid) objQuestion.CampaignQuestionUid;

        VerboseLogIt(string.Format("{0}: Questions: {1}", sMethodName, objQuestion.QuestionText));

        var collResponse =  //Get available responses for each question
            _dbLCS.LCS_spGetCustomResponses_ByCampaignQuestionUid(objQuestionSVC.CampaignQuestionUid).ToList();

        List<CustomResponse> collResponseSVC = new List<CustomResponse>();

        foreach (LCS_spGetCustomResponses_ByCampaignQuestionUidResult objResponse in collResponse) {
          CustomResponse objResponseSVC = new CustomResponse();

          objResponseSVC.ResponseText = objResponse.ResponseText;
          objResponseSVC.CampaignResponseUid = (System.Guid) objResponse.CampaignResponseUid;
          objResponseSVC.Position = (int) objResponse.Position;

          VerboseLogIt(string.Format("{0}: Response: {1}", sMethodName, objResponse.ResponseText));

          collResponseSVC.Add(objResponseSVC);
        }

        objQuestionSVC.Responses = collResponseSVC;

        collQuestionSVC.Add(objQuestionSVC);
      }

      return (collQuestionSVC);

    } catch (Exception objEx) {
      LogIt(string.Format("{0}: Error: {1}", sMethodName, objEx.Message));

      AppUtilities.LogExceptionWrapper(new LCSAppException(objEx), LCSExceptionModeType.Silent);

      sErrorCode = LCSSvcErrorCodeLookup.UnknownError;

    } finally {
      LogIt(string.Format("{0}: End", sMethodName));
    }

    return (null);
  }

  /// <summary>
  /// Gets the unanswered custom questions, including available responses And header info.
  /// </summary>
  /// <remarks>
  /// This method is called by the CQF app.
  /// </remarks>
  /// <param name="gQuestionStageUid">guid for QuestionStage table entry</param>
  /// <param name="sUser">Service user id</param>
  /// <param name="sPwd">Service user password</param>
  /// <param name="sErrorCode">Error code to be returned</param>
  /// <returns>Package containing header info along with questions and available responses</returns>
  public CQFPackage GetUnansweredQuestions_WithResponsesAndHeader(
      System.Guid gQuestionStageUid,
      string sUser,
      string sPwd,
      ref string sErrorCode) {
    if (!Authenticate(sUser, sPwd))  //Authenticate
    {
      sErrorCode = LCSSvcErrorCodeLookup.Authentication_Failed;
      LogIt(string.Format("Authentication Failed"));
      return (null);
    }

    string sMethodName = string.Empty;

    try {
      sErrorCode = string.Empty;
      sMethodName = DebuggingUtilities.GetCurrentMethodName(0);

      LogIt(string.Format("{0}: Begin", sMethodName));

      CheckDB();

      CQFPackage objCQF = null;

      //Get basic info about this request
      var collCQFInfo = _dbLCS.LCS_spGetCQFInfo_ByStageUid(gQuestionStageUid).ToList();

      if (collCQFInfo.Count < 1)  //Can't find record in QuestionStage table for this GUID
      {
        VerboseLogIt(string.Format("{0}: Cannot find question stage entry {1}", sMethodName, gQuestionStageUid.ToString()));

        //QuestionStage record doesn't exist.

        return (null);
      }

      //At this point, basic info is presumed to be ok

      objCQF = new CQFPackage();

      //Populate members to be returned in data contract
      objCQF.ImageURL = collCQFInfo[0].ImageURL;
      objCQF.ReturnURL = collCQFInfo[0].ReturnURL;
      objCQF.CMSParamName = collCQFInfo[0].ParamName;
      objCQF.CompletedOn = collCQFInfo[0].CompletedOn;
      objCQF.IsExpiredYN = collCQFInfo[0].IsExpiredYN;
      objCQF.IsQuestionsToAnswerYN = collCQFInfo[0].IsQuestionsToAnswerYN;

      //Now get actual question and response data - leverage private method in this project

      //Only getting the questions if this request is still valid

      if (objCQF.CompletedOn != null ||
          (bool)(objCQF.IsExpiredYN ?? false) ||
          !(objCQF.IsQuestionsToAnswerYN ?? false))
        objCQF.Questions = null;
      else
        objCQF.Questions = GetUnansweredQuestions_WithResponses(gQuestionStageUid, ref sErrorCode);

      return (objCQF);

    } catch (Exception objEx) {
      LogIt(string.Format("{0}: Error: {1}", sMethodName, objEx.Message));

      AppUtilities.LogExceptionWrapper(new LCSAppException(objEx), LCSExceptionModeType.Silent);

      sErrorCode = LCSSvcErrorCodeLookup.UnknownError;

    } finally {
      LogIt(string.Format("{0}: End", sMethodName));

      SafeDisposeLCS(_dbLCS);

      KillLogging();
    }

    return (null);
  }

  /// <summary>
  /// Processes the request for a Lead item.  Called my stub service, which receives request from CMS.
  /// </summary>
  /// <param name="objLCSRequest">Lead item request info</param>
  /// <param name="bValidate">Indicates whether request info should be validated here</param>
  /// <param name="sUser">Service user id</param>
  /// <param name="sPwd">Service user password</param>
  /// <param name="sCampaignRedirectURL">The URL for the Custom Question form.</param>
  /// <param name="gRequestVerificationUid">Same as gQuestionStageUid. Guid that points to Question Stage entry</param>
  /// <param name="sErrorCode"></param>
  public void ProcessLeadItemRequest(
      LCSRequestInfoType objLCSRequest,
      bool bValidate,
      string sUser,
      string sPwd,
      ref string sCampaignRedirectURL,
      ref System.Guid gRequestVerificationUid,
      ref string sErrorCode) {
    if (!Authenticate(sUser, sPwd))  //Authenticate
    {
      sErrorCode = LCSSvcErrorCodeLookup.Authentication_Failed;
      LogIt(string.Format("Authentication Failed"));
      return;
    }

    string sMethodName = string.Empty;

    try {
      sErrorCode = string.Empty;
      sMethodName = DebuggingUtilities.GetCurrentMethodName(0);

      LogIt(string.Format("{0}: Begin", sMethodName));
      LogIt(string.Format("{0}: Visitor: {1}", sMethodName, objLCSRequest.UserEmailAddress));

      sErrorCode = string.Empty;  //Initialize error code

      gRequestVerificationUid = System.Guid.Empty;
      sCampaignRedirectURL = string.Empty;

      //System.Nullable<System.Guid> gAssetUid = System.Guid.Empty;
      System.Nullable<System.Guid> gSiteUid = System.Guid.Empty;
      DateTime dtRightNow = DateTime.Now;
      System.Nullable<System.Guid> gSiteGroupUid = System.Guid.Empty;
      System.Nullable<System.Guid> gSiteVisitorUid = System.Guid.Empty;
      System.Nullable<System.Guid> gQuestionStageUid = System.Guid.Empty;
      System.Nullable<int> iQuestionStageId = 0;
      System.Nullable<System.Guid> gAssetGuid = System.Guid.Empty;
      int iNumRequired = 0;  //Assume no questions to answer
      bool IsQuestionsToAnswerYN = false;
      Hashtable hshAssets = null;  //To build list of assets to save for this request
      System.Nullable<System.Guid> gQuestionStageAssetUid = System.Guid.Empty;
      System.Nullable<int> iQuestionStageAssetId = 0;

      //**Inspect Request Info
      if (bValidate) {
        ValidateLCSRequest(objLCSRequest, ref sErrorCode);

        if (sErrorCode != string.Empty) {
          sCampaignRedirectURL = string.Empty;
          gRequestVerificationUid = System.Guid.Empty;

          LogIt(string.Format("{0}: Failed Validation", sMethodName));
          LogIt(string.Format("{0}: Error: {1}", sMethodName, sErrorCode));

          return;
        }
      }

      LogIt(string.Format("{0}: No Validation or Passed Validation", sMethodName));

      //Determine if at least one of the assets has unanswered required
      //custom questions associated with it.
      //Once we find one, we don't have to check the rest of them.

      CheckDB();

      LogIt(string.Format("{0}: Determine if Questions: Begin", sMethodName));
      LogIt(string.Format("{0}: Asset Count: {1}", sMethodName, objLCSRequest.AssetList.Count.ToString()));

      bool bFoundEligibleCampaign = false;

      foreach (LCSAssetType objAsset in objLCSRequest.AssetList) {
        VerboseLogIt(string.Format("{0}: Asset Code: {1}", sMethodName, objAsset.AssetCode));

        if (!StringUtilities.IsNullOrEmptyOrSpaces(objAsset.AssetCode)) {
          string sAssetCode = objAsset.AssetCode.Trim();

          //Check if each asset is part of an eligible campaign

          System.Guid gCampaignUid = GetEligibleCampaign(sAssetCode,
                                                         ref gAssetGuid,
                                                         ref gSiteGroupUid,
                                                         ref sErrorCode);

          if (sErrorCode != string.Empty) {
            sCampaignRedirectURL = string.Empty;
            gRequestVerificationUid = System.Guid.Empty;
            return;  //Get out
          }

          if (gCampaignUid != System.Guid.Empty) {
            bFoundEligibleCampaign = true;

            LogIt(string.Format("{0}: Eligible Campaign: {1}", sMethodName, gCampaignUid.ToString()));

            //Before checking for questions, save this asset code and guid so
            //they can be inserted into the QuestionStageAsset table if there are
            //questions to answer.  Note that we may even be saving assets that currently
            //have no questions to answer, though they are part of a campaign.  No harm in including
            //them in this process, since in the meanwhile, they have questions associated with them.

            if (hshAssets == null)
              hshAssets = new Hashtable();

            if (!hshAssets.Contains(sAssetCode))
              hshAssets.Add(sAssetCode, gAssetGuid);

            //Asset is part of an elgible campaign, so proceed and
            //see if there are questions this visitor has to answer.

            //The idea here is that once we know an asset corresponds to a campaign with
            //questions that need to be answered, I don't want to keep checking the other
            //assets for the same thing.
            //But I do need to continue to build my list of assets so I can save them
            //in the QuestionStageAsset table.
            if (!IsQuestionsToAnswerYN) {
              iNumRequired =
                  (int) _dbLCS.LCS_fnGetNumUnansweredRequiredCQ_ByCampaignUidAndEmailAddr(
                      gCampaignUid,
                      objLCSRequest.UserEmailAddress);

              if (iNumRequired > 0)  // && !bSaveAssetKeysYN)
              {
                IsQuestionsToAnswerYN = true;

                LogIt(string.Format("{0}: Questions: {1}", sMethodName, iNumRequired.ToString()));

                //break;  //Get out of loop
              }
            }
          }
        }

      }  //For

      if (!bFoundEligibleCampaign)  //DO NOT continue if no asset part of viable campaign
        return;

      //At this point we know at least one asset is part of an active campaign

      LogIt(string.Format("{0}: Determine if Questions: End", sMethodName));

      //Now process the request

      LogIt(string.Format("{0}: Process Request: Begin", sMethodName));

      //Ensure site visitor is in the db.
      //Not in a transaction because I want to do it regardless.
      LogIt(string.Format("{0}: Update Site Visitor: Begin", sMethodName));

      int iResult = 0;

      // If multiple assets, they are assumed to be part of campaigns that are
      //in the same site group.

      iResult = _dbLCS.LCS_spProcessSiteVisitor(
          objLCSRequest.UserEmailAddress,
          dtRightNow,
          //This is the last SiteGroupUid encountered in the above loop.  Are they all the same?
          gSiteGroupUid,
          objLCSRequest.SiteCode,
          ConfigConstants.DbUserId,
          ConfigConstants.VisitorApprovalDuration,
          ref gSiteUid,
          ref gSiteVisitorUid);

      if (iResult != 0 || ConversionUtilities.IsGuidNullOrEmpty(gSiteVisitorUid))
        throw new ApplicationException(string.Format("Problem updating site visitor: {0}", objLCSRequest.UserEmailAddress));

      LogIt(string.Format("{0}: Update Site Visitor: End", sMethodName));

      if (IsQuestionsToAnswerYN) {
        using(TransactionScope objTran = new TransactionScope()) {
          //Now generate Campaign Custom Question URL GUID

          LogIt(string.Format("{0}: Generating CQF URL", sMethodName));

          iResult = _dbLCS.LCS_spGenerateCampaignRedirectGUID(
              IsQuestionsToAnswerYN,
              gSiteVisitorUid,
              //Would be nice if this were the asset that triggered the request, but....
              //BUT keep in mind that this is the most recent AssetUid in the loop above.
              //THIS IS A POTENTIAL Issue if all assets in a request aren't part of campaigns that are in the same SiteGroup!!!!
              gAssetGuid,
              gSiteUid,
              objLCSRequest.ReturnURL,
              objLCSRequest.ParamName,
              ConfigConstants.DbUserId,
              dtRightNow,
              dtRightNow,
              ref gQuestionStageUid,
              ref iQuestionStageId);

          if (iResult != 0 || iQuestionStageId == 0 || ConversionUtilities.IsGuidNullOrEmpty(gQuestionStageUid))
            throw new ApplicationException(string.Format("Problem updating question stage table"));

          //Now I need to save all the assets associated with this request.
          //The reason for this is so that the CQF can know which assets, and thus which
          //campaigns are involved in the questionnaire.

          if (hshAssets != null) {
            LogIt(string.Format("{0}: Recording Assets for LCS", sMethodName));

            foreach (DictionaryEntry objAsset in hshAssets) {
              iResult = _dbLCS.LCS_spAddQuestionStageAsset(
                  gQuestionStageUid,
                  (System.Guid) objAsset.Value,
                  (string) objAsset.Key,
                  ConfigConstants.DbUserId,
                  ref gQuestionStageAssetUid,
                  ref iQuestionStageAssetId);

              if (iResult != 0 || iQuestionStageAssetId == 0 || ConversionUtilities.IsGuidNullOrEmpty(gQuestionStageAssetUid))
                throw new ApplicationException(string.Format("Problem updating question stage asset table for asset: {0}", (string) objAsset.Key));
            }
          }

          gRequestVerificationUid =
              (System.Guid)(gQuestionStageUid ?? System.Guid.Empty);  //Save this for return to caller

          //Construct the Campaign Custom Question URL
          string sURL = ConfigConstants.CampaignQuestionRootURL +
                        WebUtilities.ConstructUrlItem("?", QueryStringParams.UserStage, gQuestionStageUid.ToString());

          sCampaignRedirectURL = sURL;  //return to caller

          LogIt(string.Format("{0}: CQF URL: {1}", sMethodName, sURL));

          LogIt(string.Format("{0}: Recording Assets for CMS: Begin", sMethodName));

          foreach (LCSAssetType objAsset in objLCSRequest.AssetList) {
            System.Nullable<int> iAssetKeyId = 0;

            iResult = _dbLCS.LCS_spAddCMSAssetKey(gQuestionStageUid,
                                                  objAsset.AssetKey,
                                                  ConfigConstants.DbUserId,
                                                  ref iAssetKeyId);

            if (iResult != 0 || iAssetKeyId == 0)
              throw new ApplicationException(string.Format("Problem adding asset key: {0}", string.Empty));
          }

          //Complete the Transaction
          objTran.Complete();
        }

      } else {
        //None of the lead assets are part of campaigns for which this visitor has to answer
        //required questions.
        //So generate a lead for each lead asset.

        if (hshAssets != null) {
          LogIt(string.Format("{0}: Generating Leads for SiteVisitorUid: Begin: {1}", sMethodName, gSiteVisitorUid));

          System.Nullable<System.Guid> gLeadUid = System.Guid.Empty;
          System.Nullable<int> iLeadId = 0;

          using(TransactionScope objTran = new TransactionScope()) {
            System.Guid gAssetUid = System.Guid.Empty;

            foreach (DictionaryEntry objAsset in hshAssets) {
              gAssetUid = new System.Guid(objAsset.Value.ToString());

              iResult = _dbLCS.LCS_spAddLead(
                  gSiteVisitorUid,
                  gAssetUid,
                  gSiteUid,
                  dtRightNow,
                  ConfigConstants.DbUserId,
                  dtRightNow,
                  ref iLeadId,
                  ref gLeadUid);

              if (iResult != 0 || iLeadId == 0 || ConversionUtilities.IsGuidNullOrEmpty(gLeadUid))
                throw new ApplicationException(string.Format("Problem saving lead for SiteVisitorUid: {0}", gSiteVisitorUid));

              VerboseLogIt(string.Format("{0}: Saved Lead: {1}", sMethodName, gLeadUid));
            }

            //Complete the Transaction
            objTran.Complete();
          }

          using(TransactionScope objTran = new TransactionScope()) {
            try {
              LogIt(string.Format("{0}: Processing WRS Data for Visitor {1}: Begin", sMethodName, objLCSRequest.UserEmailAddress));

              //NOTE: I'm following the same steps as if there were unanswered questions,
              //only because I want to leverage the work this proc does.
              iResult = _dbLCS.LCS_spGenerateCampaignRedirectGUID(
                  IsQuestionsToAnswerYN,
                  gSiteVisitorUid,
                  gAssetGuid,  //REVISIT: Would be nice if this were the asset that triggered the request, but....
                  //BUT keep in mind that this is the most recent AssetUid in the loop above.
                  //THIS IS A POTENTIAL Issue of all assets in a request aren't part of campaigns that are in the same SiteGroup!!!!
                  gSiteUid,
                  string.Empty,
                  string.Empty,
                  ConfigConstants.DbUserId,
                  dtRightNow,
                  dtRightNow,
                  ref gQuestionStageUid,
                  ref iQuestionStageId);

              if (iResult != 0 || iQuestionStageId == 0 || ConversionUtilities.IsGuidNullOrEmpty(gQuestionStageUid))
                throw new ApplicationException(string.Format("Problem updating question stage table"));

              var collCQFInfo =
                  _dbLCS.LCS_spGetCQFInfo_ByStageUid(gQuestionStageUid).ToList();

              if (collCQFInfo.Count != 1)
                throw new ApplicationException(string.Format("Problem find Question Stage record for gQuestionStageUid: {0}", gQuestionStageUid));

              //Deal with Hallmark data
              ProcessContactDemosFromWRSWrapper(collCQFInfo[0],
                                                ref sErrorCode);

            } catch (Exception objEx) {
              LogIt(string.Format("{0}: Error: {1}", sMethodName, objEx.Message));

              AppUtilities.LogExceptionWrapper(new LCSAppException(objEx), LCSExceptionModeType.Silent);

              sErrorCode = LCSSvcErrorCodeLookup.UnknownError;

              throw objEx;

            } finally {
              LogIt(string.Format("{0}: Processing WRS Data for Visitor: End", sMethodName, objLCSRequest.UserEmailAddress));
            }

            //Complete the Transaction
            objTran.Complete();
          }
        }
      }

    } catch (Exception objEx) {
      LogIt(string.Format("{0}: Error: {1}", sMethodName, objEx.Message));

      AppUtilities.LogExceptionWrapper(new LCSAppException(objEx), LCSExceptionModeType.Silent);

      sErrorCode = LCSSvcErrorCodeLookup.UnknownError;

    } finally {
      LogIt(string.Format("{0}: End", sMethodName));

      SafeDisposeLCS(_dbLCS);

      KillLogging();
    }
  }

  /// <summary>
  /// Saves the lead responses.  Called by the CQF application.
  /// </summary>
  /// <param name="gQuestionStageUid">guid for QuestionStage table entry</param>
  /// <param name="collQuestions">The collection of questions and responses provided by visitor</param>
  /// <param name="iCreatedBy">The system user id for db updates</param>
  /// <param name="sUser">Service user id</param>
  /// <param name="sPwd">Service user password</param>
  /// <param name="sErrorCode">The error code to be returned</param>
  /// <returns>The number of responses provided by visitor</returns>
  public int SaveLeadResponses(
      System.Guid gQuestionStageUid,
      List<CustomQuestion> collQuestions,
      int iCreatedBy,
      string sUser,
      string sPwd,
      ref string sErrorCode) {
    if (!Authenticate(sUser, sPwd))  //Authenticate
    {
      sErrorCode = LCSSvcErrorCodeLookup.Authentication_Failed;
      LogIt(string.Format("Authentication Failed"));
      return (0);
    }

    string sMethodName = string.Empty;
    int iNumResponses = 0;  //Count of questions to return to caller as success/failure
    DateTime dtRespondedOn = DateTime.Now;
    DateTime dtCollectedOn = DateTime.Now;
    System.Nullable<System.Guid> gCampaignResponseUid = null;
    string sLeadResponseText = string.Empty;
    LCS_spGetCQFInfo_ByStageUidResult objStage = null;

    //First get the saved staging info for this request

    try {
      sMethodName = DebuggingUtilities.GetCurrentMethodName(0);
      sErrorCode = string.Empty;

      LogIt(string.Format("{0}: Begin", sMethodName));

      //Actually, First check the gQuestionStageUid guid
      if (ConversionUtilities.IsGuidNullOrEmpty(gQuestionStageUid))
        throw new ApplicationException("Request Verification GUID is Null or empty");

      //Continue
      LogIt(string.Format("{0}: RequestVerificationUid: {1}", sMethodName, gQuestionStageUid.ToString()));

      //And do basic check of question collection
      if (collQuestions == null || collQuestions.Count < 1)
        throw new ApplicationException("Collection of answered custom questions is Null or empty.");

      CheckDB();

      var collCQFInfo =
          _dbLCS.LCS_spGetCQFInfo_ByStageUid(gQuestionStageUid).ToList();

      if (collCQFInfo.Count != 1)
        throw new ApplicationException("Problem processing campaign custom question responses with request verification uid: " + gQuestionStageUid.ToString());

      objStage = collCQFInfo[0];

      LogIt(string.Format("{0}: Current Visitor: {1}", sMethodName, objStage.EmailAddress));

    } catch (Exception objEx) {
      LogIt(string.Format("{0}: Error: {1}", sMethodName, objEx.Message));

      AppUtilities.LogExceptionWrapper(new LCSAppException(objEx), LCSExceptionModeType.Silent);

      sErrorCode = LCSSvcErrorCodeLookup.UnknownError;

    } finally {
    }

    if (sErrorCode != string.Empty) {
      LogIt(string.Format("{0}: End", sMethodName));
      SafeDisposeLCS(_dbLCS);
      KillLogging();

      return (0);  //Get out, return 0 as # responses provided
    }

    //Now we have the request info from the QuestionStage table.
    //So iterate and save question responses.

    CheckDB();

    try {
      LogIt(string.Format("{0}: Saving Leads and Responses: Begin", sMethodName));

      int iResult = 0;
      bool bProcessWRS_YN = false;

      using(TransactionScope objTran = new TransactionScope()) {
        //VerboseLogIt(string.Format("{0}: Started Transaction", sMethodName));

        try {
          //Update the QuestionStage table - set CompletedOn

          iResult = _dbLCS.LCS_spSetQuestionStageCompletedOn_ByQuestionStageUid(
              gQuestionStageUid,
              dtRespondedOn,
              dtRespondedOn,
              ConfigConstants.DbUserId);

          if (iResult != 0)
            throw new ApplicationException(string.Format("Problem setting CompletedOn in QuestionStage table for GUID: {0}: ", gQuestionStageUid));

          VerboseLogIt(string.Format("{0}: Set CompletedOn", sMethodName));

          //Create Lead records before saving responses.  Could do it in either order.

          LogIt(string.Format("{0}: Saving Leads: Begin", sMethodName));

          var collStagedAssets =
              _dbLCS.LCS_spGetQuestionStageAssets_ByQuestionStageUid(gQuestionStageUid).ToList();

          //VerboseLogIt(string.Format("{0}: Got Staged Assets", sMethodName));

          //collection shouldn't be empty, wouldn't make sense

          System.Nullable<System.Guid> gLeadUid = System.Guid.Empty;
          System.Nullable<int> iLeadId = 0;

          if (collStagedAssets.Count < 1)
            throw new ApplicationException(string.Format("Could not load staged assets for QuestionStageUid: {0}", gQuestionStageUid));
          else  //iterate and generate leads
          {
            foreach (LCS_spGetQuestionStageAssets_ByQuestionStageUidResult objStagedAsset in collStagedAssets) {
              //VerboseLogIt(string.Format("{0}: Saving Lead", sMethodName));

              iResult = _dbLCS.LCS_spAddLead(
                  objStagedAsset.SiteVisitorUid,
                  objStagedAsset.AssetUid,
                  objStagedAsset.SiteUid,
                  dtCollectedOn,
                  ConfigConstants.DbUserId,
                  dtCollectedOn,
                  ref iLeadId,
                  ref gLeadUid);

              if (iResult != 0 || iLeadId == 0 || ConversionUtilities.IsGuidNullOrEmpty(gLeadUid))
                throw new ApplicationException(string.Format("Problem saving lead for QuestionStageUid: {0}", gQuestionStageUid));

              //REVISIT: something not quite right with exception handling here?

              VerboseLogIt(string.Format("{0}: Saved Lead: {1}", sMethodName, gLeadUid));
            }
          }

          LogIt(string.Format("{0}: Saving Leads: End", sMethodName));

          LogIt(string.Format("{0}: Saving Responses: Begin", sMethodName));

          //Now iterate over Questions/Responses

          foreach (CustomQuestion objQuestion in collQuestions) {
            if (objQuestion != null && objQuestion.Responses != null)  // && objQuestion.Responses.Count > 0)
            {
              LogIt(string.Format("{0}: Question: Text: {1}", sMethodName, objQuestion.QuestionText));
              LogIt(string.Format("{0}: Question: GUID: {1}", sMethodName, objQuestion.CampaignQuestionUid));

              System.Nullable<System.Guid> gLeadResponseUid = System.Guid.Empty;

              if (objQuestion.Responses.Count > 1) {
                //More than one response, so assume not a free form.
                //Assume Response GUIDs are provided
                foreach (CustomResponse objResponse in objQuestion.Responses) {
                  gCampaignResponseUid = objResponse.CampaignResponseUid;
                  sLeadResponseText = objResponse.LeadResponseText;

                  _dbLCS.LCS_spAddLeadResponse(gLeadUid,
                                               objQuestion.CampaignQuestionUid,
                                               gCampaignResponseUid,
                                               sLeadResponseText,
                                               dtRespondedOn,
                                               iCreatedBy,
                                               ref gLeadResponseUid);

                  if (gLeadResponseUid != System.Guid.Empty)
                    iNumResponses++;
                  else
                    throw new ApplicationException("Problem saving response to campaign custom question with uid: " + gQuestionStageUid.ToString());
                }

              } else if (objQuestion.Responses.Count == 1) {
                //Decide whether the response is free form or GUID

                CustomResponse objResponse = objQuestion.Responses[0];

                //The newer idea is that I'm saving the response text
                //along with the response guid, simply for
                //historical purposes.

                //If response guid is present, then use it.  Otherwise,
                //it's a free form question response and so use the response text.

                gCampaignResponseUid = objResponse.CampaignResponseUid;
                sLeadResponseText = objResponse.LeadResponseText;

                _dbLCS.LCS_spAddLeadResponse(gLeadUid,
                                             objQuestion.CampaignQuestionUid,
                                             gCampaignResponseUid,
                                             sLeadResponseText,
                                             dtRespondedOn,
                                             iCreatedBy,
                                             ref gLeadResponseUid);

                if (gLeadResponseUid != System.Guid.Empty)
                  iNumResponses++;
                else
                  throw new ApplicationException(string.Format("Problem saving response to campaign custom question with uid: {0}", gQuestionStageUid));
              }
            }
          }

          LogIt(string.Format("{0}: Saving Responses: End", sMethodName));

          //At this point, site visitor has completed custom questions,
          //So - if appropriate - get Contact and Demo responses from WRS
          //and record them.

          LogIt(string.Format("{0}: Processing WRS Data for Visitor {1}: Begin", sMethodName, objStage.EmailAddress));

          bProcessWRS_YN = true;
          //ProcessContactDemosFromWRS(objStage, ref sErrorCode);

          LogIt(string.Format("{0}: Processing WRS Data for Visitor: End", sMethodName, objStage.EmailAddress));

        } catch (Exception objEx) {
          LogIt(string.Format("{0}: Error: {1}", sMethodName, objEx.Message));

          AppUtilities.LogExceptionWrapper(new LCSAppException(objEx), LCSExceptionModeType.Silent);

          sErrorCode = LCSSvcErrorCodeLookup.UnknownError;

          throw objEx;

        } finally {
        }

        //Complete the Transaction
        objTran.Complete();
      }

      //Now doing two transactions - one for custom question responses and one for WRS.
      //My thinking is that I don't want to lose the custom question responses just because Hallmark failed.

      if (bProcessWRS_YN) {
        using(TransactionScope objTran = new TransactionScope()) {
          //VerboseLogIt(string.Format("{0}: Started Transaction", sMethodName));

          try {
            LogIt(string.Format("{0}: Processing WRS Data for Visitor {1}: Begin", sMethodName, objStage.EmailAddress));

            // Handle Hallmark here
            ProcessContactDemosFromWRSWrapper(objStage,
                                              ref sErrorCode);

          } catch (Exception objEx) {
            LogIt(string.Format("{0}: Error: {1}", sMethodName, objEx.Message));

            AppUtilities.LogExceptionWrapper(new LCSAppException(objEx), LCSExceptionModeType.Silent);

            sErrorCode = LCSSvcErrorCodeLookup.UnknownError;

            throw objEx;

          } finally {
            LogIt(string.Format("{0}: Processing WRS Data for Visitor: End", sMethodName, objStage.EmailAddress));
          }

          //Complete the Transaction
          objTran.Complete();
        }
      }

      LogIt(string.Format("{0}: Saving Leads and Responses: End", sMethodName));

    } catch (Exception objExTran)  //Exception occurred while creating transaction
    {
      LogIt(string.Format("{0}: Error: {1}", sMethodName, objExTran.Message));

      AppUtilities.LogExceptionWrapper(new LCSAppException(objExTran), LCSExceptionModeType.Silent);

      sErrorCode = LCSSvcErrorCodeLookup.UnknownError;

    } finally {
      LogIt(string.Format("{0}: End", sMethodName));
      SafeDisposeLCS(_dbLCS);
      KillLogging();
    }

    return (iNumResponses);
  }

  /// <summary>
  /// Determines whether Hallmark service is available, based on our config file.
  /// </summary>
  /// <returns>
  /// 	<c>true</c> if Hallmark service is available; otherwise, <c>false</c>.
  /// </returns>
  private bool IsHallmarkServiceAvailableYN() {
    //For now, just return our global flag.

    //Revisit, and test the service
    return (ConfigConstants.HallmarkAvailableYN);
  }

  /// <summary>
  /// Wrapper for handling calls to Hallmark.
  /// </summary>
  /// <param name="objStage">Info about current request and site visitor</param>
  /// <param name="sErrorCode">Error code to be returned</param>
  private void ProcessContactDemosFromWRSWrapper(LCS_spGetCQFInfo_ByStageUidResult objStage,
                                                 ref string sErrorCode) {
    string sMethodName = string.Empty;
    DateTime dtRightNow = DateTime.Now;

    try {
      sMethodName = DebuggingUtilities.GetCurrentMethodName(0);
      sErrorCode = string.Empty;

      LogIt(string.Format("{0}: Start: For Visitor: {1}", sMethodName, objStage.EmailAddress));

      //Make some decisions based on whether we want to call Hallmark,
      //and whether the service is available.

      bool bWantToUpdateContactInfoYN = false;
      bool bWantToUpdateDemoQuestionsYN = false;

      bool bIsHallmarkAvailableYN = IsHallmarkServiceAvailableYN();

      if (ConfigConstants.HallmarkDaysInterval == 0)  //Means ALWAYS fetch Hallmark data and update db
      {
        bWantToUpdateContactInfoYN = true;
        bWantToUpdateDemoQuestionsYN = true;
      } else if (objStage.LastWRSFetchOn == null || objStage.LastWRSFetchOn_SiteGroup == null) {
        if (objStage.LastWRSFetchOn == null)
          bWantToUpdateContactInfoYN = true;

        if (objStage.LastWRSFetchOn_SiteGroup == null)
          bWantToUpdateDemoQuestionsYN = true;

      } else  //Neither is null
      {
        // Check to see if Hallmark has been called within allotted
        //amount of time, or whether it needs to be called again.

        if (DateAndTime.DateAdd(DateInterval.Day,
                                ConfigConstants.HallmarkDaysInterval,
                                (DateTime) objStage.LastWRSFetchOn) <= dtRightNow)
          bWantToUpdateContactInfoYN = true;

        if (DateAndTime.DateAdd(DateInterval.Day,
                                ConfigConstants.HallmarkDaysInterval,
                                (DateTime) objStage.LastWRSFetchOn_SiteGroup) <= dtRightNow)
          bWantToUpdateDemoQuestionsYN = true;
      }

      VerboseLogIt(string.Format("{0}: Hallmark Available?: {1}", sMethodName, bIsHallmarkAvailableYN.ToString()));
      VerboseLogIt(string.Format("{0}: Want to Call Hallmark?: {1}",
                                 sMethodName, (bWantToUpdateContactInfoYN || bWantToUpdateDemoQuestionsYN).ToString()));
      VerboseLogIt(string.Format("{0}: Update Contact?: {1}", sMethodName, bWantToUpdateContactInfoYN.ToString()));
      VerboseLogIt(string.Format("{0}: Update Demo?: {1}", sMethodName, bWantToUpdateDemoQuestionsYN.ToString()));

      CheckDB();

      // Prepare logging items object for Hallmark tools dll call
      LoggingItems objLoggingItems = new LoggingItems();
      objLoggingItems.Logging = _objLogging;
      objLoggingItems.LoggingYN = ConfigConstants.LoggingYN;
      objLoggingItems.VerboseLoggingYN = ConfigConstants.VerboseLoggingYN;

      string sWRSLeadStatus = string.Empty;

      if (bWantToUpdateContactInfoYN || bWantToUpdateDemoQuestionsYN) {
        sWRSLeadStatus =
            (bIsHallmarkAvailableYN ? DBConstants.WRSLeadStatusType.Completed
             : DBConstants.WRSLeadStatusType.Pending);

        //If we've determined that the Hallmark service is available, then
        //try to create a completed WRS lead, i.e. get the Hallmark
        //data now and tie it to the lead, rather than creating a pending lead
        //and a request to get Hallmark data at some later date.

        HallmarkCoreTools.ProcessContactDemosFromWRS(
            objStage.WRS_Code,
            objStage.EmailAddress,
            null,  //new lead
            objStage.SiteGroupUid,
            objStage.SiteVisitorUid,
            bWantToUpdateContactInfoYN,
            bWantToUpdateDemoQuestionsYN,
            sWRSLeadStatus,
            _dbLCS,
            ConfigConstants.DbUserId,
            objLoggingItems,
            ref sErrorCode);
      }

    } catch (Exception objEx) {
      LogIt(string.Format("{0}: Error: {1}", sMethodName, objEx.Message));

      //Log the exception
      AppUtilities.LogExceptionWrapper(new LCSAppException(objEx), LCSExceptionModeType.Silent);

      sErrorCode = LCSSvcErrorCodeLookup.UnknownError;

      //Throw back to caller - want to end transaction gracefully
      throw new ApplicationException(string.Format("Problem occurred while processing WRS data for Visitor: {0}", objStage.EmailAddress),
                                     objEx);

    } finally {
      LogIt(string.Format("{0}: End: For Visitor: {1}", sMethodName, objStage.EmailAddress));
    }
  }
}

}
