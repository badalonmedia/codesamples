using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Net;
using System.Configuration;
using System.Xml.Serialization;
using System.Text;
using System.Xml;
using System.IO;
using GenericApp.RadiumRCMAudit.Main.Models;
using GenericApp.RadiumRCMAudit.Main.Components;

namespace GenericApp.RadiumRCMAudit.Main.Controllers {
  [Authorize]
  public class AuditController : BaseController {
    public AuditController() {}
    public AuditController(IUnitOfWork unitOfWork) : base(unitOfWork) {}

#region Zoom to New Page

    //[Route("{auditId:int}")]
    public ActionResult ShowResultsOnly(int auditId) {
      AuditViewModel audit = AuditViewModel.GetAudit(_unitOfWork, auditId);

      return View("AuditResults", audit);
    }

    //[Route("{auditId:int}")]
    public ActionResult ShowSnapshotOnly(int auditId) {
      AuditViewModel audit = AuditViewModel.GetAudit(_unitOfWork, auditId);

      return View("AuditSnapshot", audit);
    }

    //[Route("{auditId:int}")]
    public ActionResult ShowUploadOnly(int auditId) {
      AuditViewModel audit = AuditViewModel.GetAudit(_unitOfWork, auditId);

      return View("AuditUpload", audit);
    }

#endregion

        public ActionResult ManageAudits(int? auditId)
        {
          ViewBag.Extracts = new SelectList(_unitOfWork.Extracts.GetAll().OrderByDescending(e => e.DateAdded), "ExtractId", "Name");

          AuditViewModel audit;

          if (auditId != null) {
            audit = AuditViewModel.GetAudit(_unitOfWork, (int) auditId);

            if (audit == null) {
              throw new ApplicationException(string.Format("The Audit (ID: {0}) could not be loaded.", auditId));
            }
          } else {
            audit = new AuditViewModel();  //want auditId == 0
          }

          return View("ManageAudits", audit);
        }

        [AjaxAuthorize]
        [HttpPost]
        public JsonResult DeleteAuditJS(AuditViewModel audit) {
          var ajaxResponse =
              new AjaxResponse{Success = false, Message = string.Format("There was a problem deleting audit, ID: {0}", audit.AuditId)};

          if (audit.AuditId == 0)  //shouldn't happen
          {
            return Json(ajaxResponse);
          }

          try {
            int result = _unitOfWork.Audits.DeleteAudit(audit.AuditId);

            if (result < 1) {
              return Json(ajaxResponse);
            }

            ajaxResponse.Message = "Success";
            ajaxResponse.Success = true;
          } catch (Exception ex) {
            ErrorTools.HandleError(ex, ErrorLevel.NonFatal);  //just log, no redirect
          }

          return Json(ajaxResponse);
        }

        [AjaxAuthorize]
        [HttpPost]
        public JsonResult EditAuditJS(int auditId) {
          var ajaxResponse = new AjaxResponse{Success = false, Message = "There was a problem loading the audit."};
          AuditViewModel model = new AuditViewModel();

          try {
            var audit = _unitOfWork.Audits.GetAll()
                            .Where(a => a.AuditId == auditId)
                            .ToList()
                            .FirstOrDefault();

            if (audit == null) {
              return (Json(ajaxResponse));
            }

            model.AuditId = auditId;
            model.ExtractId = (int) audit.ExtractId;
            model.Name = audit.Name;
            model.NameSave = audit.Name;
            ajaxResponse.Success = true;
            ajaxResponse.TheData = model;
          } catch (Exception ex) {
            ErrorTools.HandleError(ex, ErrorLevel.NonFatal);  //just log, no redirect
          }

          return (Json(ajaxResponse));
        }

        [AjaxAuthorize]
        [HttpPost]
        public JsonResult GetAuditNameJS(int auditId) {
          var ajaxResponse = new AjaxResponse{Success = false, Message = "There was a problem loading the audit."};

          try {
            AuditViewModel model = new AuditViewModel();

            var audit = _unitOfWork.Audits.GetAll()
                            .Where(a => a.AuditId == auditId)
                            .ToList()
                            .FirstOrDefault();

            if (audit != null) {
              model.Name = audit.Name;
            }

            ajaxResponse.Success = true;
            ajaxResponse.TheData = model.Name;
          } catch (Exception ex) {
            ErrorTools.HandleError(ex, ErrorLevel.NonFatal);  //just log, no redirect
          }

          return (Json(ajaxResponse));
        }

#region Load - Client Side

        [AjaxAuthorize]
        [HttpPost]
        public JsonResult LoadUploadItemsJS(DTParameters param) {
          //check if AuditId is 0 since when pager first loads, all grids are triggered,
          //but no audit has been selected yet.  would be better to avoid the trigger
          //but at least make sure we don't hit the db.
          if (param == null || param.PrimaryId == 0) {
            DTResult<ExtractItemModel> emptyResult = new DTResult<ExtractItemModel>{
                draw = param.Draw,
                data = new List<ExtractItemModel>(),
                recordsFiltered = 0,
                recordsTotal = 0};

            return (Json(emptyResult));
          }

          try {
            //param.PrimaryId is AuditId.  We need the corresponding ExtractId
            var extract = _unitOfWork.Audits.GetAll().Where(a => a.AuditId == param.PrimaryId).SingleOrDefault();

            if (extract == null || extract.ExtractId == null) {
              return Json(new {error = "Extract or ExtractId is null"});
            }

            param.PrimaryId = (int) extract.ExtractId;

            var dtSource = ExtractViewModel.GetExtractItems(_unitOfWork, param.PrimaryId);

            List<ExtractItemModel> data = new ResultSet_ExtractItems().GetResult(param.Search.Value, param.SortOrder, param.Start, param.Length, dtSource, null);
            int count = new ResultSet_ExtractItems().Count(param.Search.Value, dtSource, null);

            DTResult<ExtractItemModel> result = new DTResult<ExtractItemModel>{
                draw = param.Draw,
                data = data,
                recordsFiltered = count,
                recordsTotal = count};

            return Json(result);
          } catch (Exception ex) {
            ErrorTools.HandleError(ex, ErrorLevel.NonFatal);  //just log, no redirect
            return Json(new {error = ex.Message});
          }
        }
        [AjaxAuthorize]
        [HttpPost]
        public JsonResult LoadAuditMasterJS(DTParameters param) {
          //check if AuditId is 0 since when pager first loads, all grids are triggered,
          //but no audit has been selected yet.  would be better to avoid the trigger
          //but at least make sure we don't hit the db.
          if (param == null || param.PrimaryId == 0) {
            DTResult<AuditMasterItemModel> emptyResult = new DTResult<AuditMasterItemModel>{
                draw = param.Draw,
                data = new List<AuditMasterItemModel>(),
                recordsFiltered = 0,
                recordsTotal = 0};

            return (Json(emptyResult));
          }

          try {
            var dtSource = AuditViewModel.GetAuditMasterItems(_unitOfWork, param.PrimaryId);
            List<AuditMasterItemModel> data = new ResultSet_AuditMasterItems().GetResult(param.Search.Value, param.SortOrder, param.Start, param.Length, dtSource, null);
            int count = new ResultSet_AuditMasterItems().Count(param.Search.Value, dtSource, null);

            DTResult<AuditMasterItemModel> result = new DTResult<AuditMasterItemModel>{
                draw = param.Draw,
                data = data,
                recordsFiltered = count,
                recordsTotal = count};

            return Json(result);
          } catch (Exception ex) {
            ErrorTools.HandleError(ex, ErrorLevel.NonFatal);  //just log, no redirect
            return Json(new {error = ex.Message});
          }
        }

        [AjaxAuthorize]
        [HttpPost]
        public JsonResult LoadAuditResultsJS(DTParameters param) {
          //check if AuditId is 0 since when pager first loads, all grids are triggered,
          //but no audit has been selected yet.  would be better to avoid the trigger
          //but at least make sure we don't hit the db.
          if (param == null || param.PrimaryId == 0) {
            DTResult<AuditExceptionModel> emptyResult = new DTResult<AuditExceptionModel>{
                draw = param.Draw,
                data = new List<AuditExceptionModel>(),
                recordsFiltered = 0,
                recordsTotal = 0};

            return (Json(emptyResult));
          }

          try {
            var dtSource = AuditViewModel.GetAuditExceptions(_unitOfWork, param.PrimaryId);
            List<AuditExceptionModel> data = new ResultSet_AuditExceptions().GetResult(param.Search.Value, param.SortOrder, param.Start, param.Length, dtSource, null);
            int count = new ResultSet_AuditExceptions().Count(param.Search.Value, dtSource, null);

            DTResult<AuditExceptionModel> result = new DTResult<AuditExceptionModel>{
                draw = param.Draw,
                data = data,
                recordsFiltered = count,
                recordsTotal = count};

            return Json(result);
          } catch (Exception ex) {
            ErrorTools.HandleError(ex, ErrorLevel.NonFatal);  //just log, no redirect
            return Json(new {error = ex.Message});
          }
        }

        [AjaxAuthorize]
        [HttpPost]
        public JsonResult LoadAuditsJS(DTParameters param) {
          try {
            var dtSource = AuditViewModel.GetAudits(_unitOfWork);

            List<AuditViewModel> data = new ResultSet_Audits().GetResult(param.Search.Value, param.SortOrder, param.Start, param.Length, dtSource, null);
            int count = new ResultSet_Audits().Count(param.Search.Value, dtSource, null);

            DTResult<AuditViewModel> result = new DTResult<AuditViewModel>{
                draw = param.Draw,
                data = data,
                recordsFiltered = count,
                recordsTotal = count};

            return Json(result);
          } catch (Exception ex) {
            ErrorTools.HandleError(ex, ErrorLevel.NonFatal);  //just log, no redirect
            return Json(new {error = ex.Message});
          }
        }

#endregion

        [AjaxAuthorize]
        [HttpPost]
        public JsonResult SaveAuditJS(AuditViewModel audit)  //save existing audit
        {
          var ajaxResponse = new AjaxResponse{Success = false, Message = "An error occurred while saving the audit."};

          //should have already been caught by client, but check again
          if (!ModelState.IsValid) {
            ajaxResponse.Message = "Please complete all required form fields.";
            return Json(ajaxResponse);
          }

          try {
            audit.TheUnitOfWork = _unitOfWork;

            if (!audit.IsUniqueYN())  //Uniqueness of audit name
            {
              ajaxResponse.Message = "The audit name already exists.";
              return Json(ajaxResponse);
            }

            //audit.InternalUserId = Utility.GetAspNetUserName(this);
            int id = audit.UpdateAndSave();

            ajaxResponse.Message = "Success";
            ajaxResponse.Success = true;
          } catch (Exception ex) {
            ErrorTools.HandleError(ex, ErrorLevel.NonFatal);  //just log, no redirect
          }

          return Json(ajaxResponse);
        }

        [AjaxAuthorize]
        [HttpPost]
        public JsonResult SaveAndRunAuditJS(AuditViewModel audit)  //save and run new audit
        {
          var ajaxResponse = new AjaxResponse{Success = false, Message = "An error occurred while running the audit."};

          //should have already been caught by client, but check again
          if (!ModelState.IsValid) {
            ajaxResponse.Message = "Please complete all required form fields.";
            return Json(ajaxResponse);
          }

          try {
            audit.TheUnitOfWork = _unitOfWork;

            if (!audit.IsUniqueYN()) {
              ajaxResponse.Message = "The audit name already exists.";
              return Json(ajaxResponse);
            }

            audit.InternalUserId = Utility.GetAspNetUserName(this);
            audit.AuditId = audit.AddAndSave();

            if (audit.AuditId == 0) {
              return Json(ajaxResponse);
            }

            audit.PerformAudit();

            ajaxResponse.Id = audit.AuditId;
            ajaxResponse.Message = "Success";
            ajaxResponse.Success = true;
          } catch (Exception ex) {
            ErrorTools.HandleError(ex, ErrorLevel.NonFatal);  //just log, no redirect
          }

          return Json(ajaxResponse);
        }

#region CSV Export

        public ActionResult ExportAuditSnapshot(int auditId, string auditName) {
          string fileName = string.Format("RadiumRCM_AuditSnapshot_{0}.csv", Utility.SanitizeFileName(auditName));
          var auditSnapshot = AuditViewModel.GetAuditMasterItems(_unitOfWork, auditId)
                                  .OrderBy(e => e.Login)
                                  .Select(i => new {
                                              Login = i.Login,
                                              LastName = i.LastName,
                                              FirstName = i.FirstName,
                                              Facilities = i.Facilities,
                                              Templates = i.Templates})
                                  .ToList();

          MemoryStream csvStream = Utility.GenerateCsvStream(fileName, auditSnapshot);
          return File(csvStream, "text/csv", fileName);
        }

        public ActionResult ExportAuditResults(int auditId, string auditName) {
          string fileName = string.Format("RadiumRCM_AuditResults_{0}.csv", Utility.SanitizeFileName(auditName));
          var auditResults = AuditViewModel.GetAuditExceptions(_unitOfWork, auditId)
                                 .OrderBy(e => e.DisplayOrder)
                                 .Select(e => new {
                                             e.AuditExceptionTypeName,
                                             e.AuditExceptionType,
                                             e.Message,
                                             e.UserNameFromRadium,
                                             e.UserNameFromMaster,
                                             e.FacilitiesFromRadium,
                                             e.FacilitiesFromMaster,
                                             e.TemplatesFromRadium,
                                             e.TemplatesFromMaster})
                                 .ToList();

          MemoryStream csvStream = Utility.GenerateCsvStream(fileName, auditResults);
          return File(csvStream, "text/csv", fileName);
        }

        public ActionResult ExportAuditUpload(int auditId, string auditName) {
          string fileName = string.Format("RadiumRCM_AuditUpload_{0}.csv", Utility.SanitizeFileName(auditName));

          var audit = AuditViewModel.GetAudit(_unitOfWork, auditId);
          var auditExtract = ExtractViewModel.GetExtractItems(_unitOfWork, audit.ExtractId)
                                 .Select(e => new {
                                             e.UserName,
                                             e.LastName,
                                             e.FirstName,
                                             e.EmployeeID,
                                             e.SecurityGroup})
                                 .OrderBy(e => e.UserName)
                                 .ToList();

          MemoryStream csvStream = Utility.GenerateCsvStream(fileName, auditExtract);
          return File(csvStream, "text/csv", fileName);
        }

        public ActionResult ExportAudits() {
          string fileName = "RadiumRCM_Audits.csv";
          var audits = AuditViewModel.GetAudits(_unitOfWork)
                           .Select(a => new {
                                       InternalUser = a.InternalUserId,
                                       a.Name,
                                       a.AuditStartDate,
                                       a.AuditEndDate})
                           .OrderBy(a => a.Name)
                           .ToList();

          MemoryStream csvStream = Utility.GenerateCsvStream(fileName, audits);
          return File(csvStream, "text/csv", fileName);
        }

#endregion
  }
}