using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Collections;
using LCSSharedDataContracts;

namespace LCSApiService {
/// <summary>
/// LCS API Service Interface
/// </summary>
[ServiceContract]
public interface ILCSApiService {
  /// <summary>
  /// Gets the unanswered custom questions, including available responses and header info.
  /// </summary>
  /// <param name="gQuestionStageUid">QuestionStage table unique id.</param>
  /// <returns></returns>
  [OperationContract]
  CQFPackage GetUnansweredQuestions_WithResponsesAndHeader(
      System.Guid gQuestionStageUid,
      string sUser,
      string sPwd,
      ref string sErrorCode);

  /// <summary>
  /// Processes the request for a Lead item.
  /// </summary>
  /// <param name="sSiteCode">The site code.</param>
  /// <param name="sAssetCode">The asset code.</param>
  /// <param name="sUserEmailAddress">The site visitor email address.</param>
  /// <param name="sReturnURL">The return URL.</param>
  /// <param name="sCampaignRedirectURL">The URL for the Custom Question form.</param>
  [OperationContract]
  void ProcessLeadItemRequest(LCSRequestInfoType objLCSRequest,
                              bool bValidate,
                              string sUser,
                              string sPwd,
                              ref string sCampaignRedirectURL,
                              ref System.Guid gRequestVerificationUid,
                              ref string sErrorCode);

  [OperationContract] List<string> GetCMSAssetKeys(
      System.Guid gRequestVerificationUid,
      string sUser,
      string sPwd,
      ref string sErrorCode);

  /// <summary>
  /// Saves the lead responses.
  /// </summary>
  /// <param name="gQuestionStageUid">QuestionStage table unique id.</param>
  /// <param name="collQuestions">The collection of questions/responses.</param>
  /// <param name="iCreatedBy">The system user id.</param>
  /// <returns></returns>
  [OperationContract]
  int SaveLeadResponses(
      System.Guid gQuestionStageUid,
      List<CustomQuestion> collQuestions,
      int iCreatedBy,
      string sUser,
      string sPwd,
      ref string sErrorCode);
}

/// <summary>
/// Data Contract between LCS API Service and Consumer.
/// Exposes custom questions as well as some header info.
/// </summary>
[DataContract]
public class CQFPackage {
  private string sCMSParamName = string.Empty;
  private string sImageURL = string.Empty;
  private string sReturnURL = string.Empty;
  private List<CustomQuestion> collQuestions = null;
  private System.Nullable<bool> bIsExpiredYN = false;            //Cliff 2/9/09: Added
  private System.Nullable<bool> bIsQuestionsToAnswerYN = false;  //Cliff 2/9/09: Added
  private System.Nullable<DateTime> dtCompletedOn = null;        //Cliff 2/9/09: Added

  [DataMember]
  public System.Nullable<bool> IsQuestionsToAnswerYN {
    get { return bIsQuestionsToAnswerYN; }
    set { bIsQuestionsToAnswerYN = value; }
  }

  [DataMember]
  public System.Nullable<bool> IsExpiredYN {
    get { return bIsExpiredYN; }
    set { bIsExpiredYN = value; }
  }

  [DataMember]
  public System.Nullable<DateTime> CompletedOn {
    get { return dtCompletedOn; }
    set { dtCompletedOn = value; }
  }

  [DataMember]
  public string CMSParamName {
    get { return sCMSParamName; }
    set { sCMSParamName = value; }
  }

  [DataMember]
  public string ImageURL {
    get { return sImageURL; }
    set { sImageURL = value; }
  }

  [DataMember]
  public string ReturnURL {
    get { return sReturnURL; }
    set { sReturnURL = value; }
  }

  [DataMember]
  public List<CustomQuestion> Questions {
    get { return collQuestions; }
    set { collQuestions = value; }
  }
}

/// <summary>
/// Data Contract between LCS API Service and Consumer.
/// Exposes a custom question.
/// </summary>
[DataContract]
public class CustomQuestion {
  private System.Nullable<System.Guid> gCampaignQuestionUid = null;  //System.Guid.Empty;
  private int iPosition = 0;
  private bool bIsRequiredYN = true;
  private string sQuestionText = string.Empty;
  private string sQuestionType = string.Empty;
  private List<CustomResponse> collResponses = null;

  [DataMember]
  public System.Nullable<System.Guid> CampaignQuestionUid {
    get { return gCampaignQuestionUid; }
    set { gCampaignQuestionUid = value; }
  }

  [DataMember]
  public int Position {
    get { return iPosition; }
    set { iPosition = value; }
  }

  [DataMember]
  public bool IsRequiredYN {
    get { return bIsRequiredYN; }
    set { bIsRequiredYN = value; }
  }

  [DataMember]
  public string QuestionText {
    get { return sQuestionText; }
    set { sQuestionText = value; }
  }

  [DataMember]
  public string QuestionType {
    get { return sQuestionType; }
    set { sQuestionType = value; }
  }

  [DataMember]
  public List<CustomResponse> Responses {
    get { return collResponses; }
    set { collResponses = value; }
  }
}

/// <summary>
/// Data Contract between LCS API Service and Consumer.
/// Exposes a custom response.
/// </summary>
[DataContract]
public class CustomResponse {
  private System.Nullable<System.Guid> gCampaignResponseUid = null;  //System.Guid.Empty;
  //private System.Guid gCampaignQuestionUid = System.Guid.Empty;
  private int iPosition = 0;
  private string sResponseText = string.Empty;
  private string sLeadResponseText = string.Empty;

  [DataMember]
  public System.Nullable<System.Guid> CampaignResponseUid {
    get { return gCampaignResponseUid; }
    set { gCampaignResponseUid = value; }
  }

  //[DataMember]
  //public System.Guid CampaignQuestionUid
  //{
  //    get { return gCampaignQuestionUid; }
  //    set { gCampaignQuestionUid = value; }
  //}

  [DataMember]
  public int Position {
    get { return iPosition; }
    set { iPosition = value; }
  }

  [DataMember]
  public string ResponseText {
    get { return sResponseText; }
    set { sResponseText = value; }
  }

  [DataMember]
  public string LeadResponseText {
    get { return sLeadResponseText; }
    set { sLeadResponseText = value; }
  }
}

}
