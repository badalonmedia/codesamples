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
  public class UploadsController : BaseController {
    public UploadsController() {}
    public UploadsController(IUnitOfWork unitOfWork) : base(unitOfWork) {}

        public ActionResult ManageUploads(int? uploadId)
        {
          ExtractViewModel upload;

          if (uploadId != null) {
            upload = ExtractViewModel.GetExtract(_unitOfWork, (int) uploadId);

            if (upload == null) {
              throw new ApplicationException(string.Format("The Upload (ID: {0}) could not be loaded.", uploadId));
            }
          } else {
            upload = new ExtractViewModel();  //want extractId == 0
          }

          return View("ManageUploads", upload);
        }

        [AjaxAuthorize]
        [HttpPost]
        public JsonResult EditUploadJS(int uploadId) {
          var ajaxResponse = new AjaxResponse{Success = false, Message = "There was a problem loading the upload."};
          ExtractViewModel model = new ExtractViewModel();

          try {
            var extract = ExtractViewModel.GetExtract(_unitOfWork, uploadId);

            if (extract == null) {
              return (Json(ajaxResponse));
            }

            model.ExtractId = uploadId;
            model.Name = extract.Name;
            model.NameSave = extract.Name;

            ajaxResponse.Success = true;
            ajaxResponse.TheData = model;
          } catch (Exception ex) {
            ErrorTools.HandleError(ex, ErrorLevel.NonFatal);  //just log, no redirect
          }

          return (Json(ajaxResponse));
        }

        [AjaxAuthorize]
        [HttpPost]
        public JsonResult DeleteUploadJS(ExtractViewModel extract) {
          var ajaxResponse = new AjaxResponse{Success = false, Message = string.Format("There was a problem deleting upload, ID: {0}", extract.ExtractId)};

          if (extract.ExtractId == 0)  //shouldn't happen
          {
            return Json(ajaxResponse);
          }

          try {
            var existingExCount = _unitOfWork.Audits.GetAll().Where(a => a.ExtractId == extract.ExtractId).Count();

            if (existingExCount > 0)  //can't delete, its is in use
            {
              ajaxResponse.Message = "Cannot delete an upload used in an audit";
              return Json(ajaxResponse);
            }

            int result = ExtractViewModel.DeleteExtract(_unitOfWork, (int) extract.ExtractId);

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
        public JsonResult SaveAndUploadDataJS() {
          var ajaxResponse = new AjaxResponse{Success = false, Message = "An error occurred while saving the upload."};

          //added additional try/catches for debugging a problem

          try {
            //should have already been caught by client, but check again
            if (!ModelState.IsValid) {
              ajaxResponse.Message = "Please complete all required form fields.";
              return Json(ajaxResponse);
            }
          } catch (Exception ex) {
            ErrorTools.HandleError(ex, ErrorLevel.NonFatal);  //just log, no redirect
            ajaxResponse.Message = "There was a problem validating your input, please try again.";
            return Json(ajaxResponse);
          }

          //validation
          HttpPostedFileBase hpf;
          try {
            if (Request.Files.Count != 1) {
              ajaxResponse.Message = "More than one upload file was specified.";
              return Json(ajaxResponse);
            }

            hpf = Request.Files[0] as HttpPostedFileBase;
            if (hpf.ContentLength == 0) {
              ajaxResponse.Message = "The specified upload file is empty.";
              return Json(ajaxResponse);
            }

            int maxFileBytes = Properties.Settings.Default.MaxUploadSize;
            if (hpf.ContentLength > maxFileBytes) {
              ajaxResponse.Message = string.Format("The upload file should not exceed {0} bytes.", maxFileBytes);
              return Json(ajaxResponse);
            }

            if (!Request.Files[0].FileName.ToLower().EndsWith(".csv")) {
              ajaxResponse.Message = "The upload file must be a CSV file.";
              return Json(ajaxResponse);
            }
          } catch (Exception ex) {
            ErrorTools.HandleError(ex, ErrorLevel.NonFatal);  //just log, no redirect
            ajaxResponse.Message = "There was a problem validating the specified file.";
            return Json(ajaxResponse);
          }

          string extractName;
          string internalUserId;
          ExtractViewModel extract;
          ajaxResponse.Success = false;

          try {
            extractName = Request.Form["extractNameJS"];
            internalUserId = Utility.GetAspNetUserName(this);
            extract = ExtractViewModel.NewExtract(_unitOfWork, extractName, internalUserId);

            if (!extract.IsUniqueYN())  //Uniqueness of extract
            {
              ajaxResponse.Message = "The upload name already exists.";
              return Json(ajaxResponse);
            }
          } catch (Exception ex) {
            ErrorTools.HandleError(ex, ErrorLevel.NonFatal);  //just log, no redirect
            ajaxResponse.Message = "There was a problem uniqueness of upload file.";
            return Json(ajaxResponse);
          }

          try {
            extract.ExtractId = extract.AddAndSave();

            if (extract.ExtractId == 0) {
              return Json(ajaxResponse);
            }

            ajaxResponse.Id = extract.ExtractId;
          } catch (Exception ex) {
            ErrorTools.HandleError(ex, ErrorLevel.NonFatal);  //just log, no redirect
            ajaxResponse.Message = "There was a problem saving the main upload record.";
            return Json(ajaxResponse);
          }

          //proceed with upload
          try {
            extract.UploadExtract(hpf.InputStream);

            ajaxResponse.Message = "Success";
            ajaxResponse.Success = true;
          } catch (Exception ex) {
            ErrorTools.HandleError(ex, ErrorLevel.NonFatal);  //just log, no redirect
            ajaxResponse.Message = "An error occurred while uploading the data.";
          }

          return Json(ajaxResponse);
        }

        [AjaxAuthorize]
        [HttpPost]
        public JsonResult SaveUploadJS(ExtractViewModel extract) {
          var ajaxResponse = new AjaxResponse{Success = false, Message = "An error occurred while saving the upload."};

          //should have already been caught by client, but check again
          if (!ModelState.IsValid) {
            ajaxResponse.Message = "Please complete all required form fields.";
            return Json(ajaxResponse);
          }

          try {
            extract.TheUnitOfWork = _unitOfWork;

            if (!extract.IsUniqueYN())  //Uniqueness of extract
            {
              ajaxResponse.Message = "The upload name already exists.";
              return Json(ajaxResponse);
            }

            int id = extract.UpdateAndSave();

            ajaxResponse.Message = "Success";
            ajaxResponse.Success = true;
          } catch (Exception ex) {
            ErrorTools.HandleError(ex, ErrorLevel.NonFatal);  //just log, no redirect
          }

          return Json(ajaxResponse);
        }

#region Load - Client Side

        [AjaxAuthorize]
        [HttpPost]
        public JsonResult LoadUploadItemsJS(DTParameters param)  //, int extractId)    //, int auditId)
        {
          try {
            var dtSource = ExtractViewModel.GetExtractItems(_unitOfWork, param.PrimaryId);

            //var dtsource = _unitOfWork.AuditExtracts.GetAll()
            //.Select(s => new ManageStatusesViewModel { StatusId = s.StatusId, Name = s.Name, Description = s.Description, IncludeInAuditYN = s.IncludeInAuditYN }).ToList();
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
        public JsonResult LoadUploadsJS(DTParameters param) {
          try {
            var dtSource = ExtractViewModel.GetExtracts(_unitOfWork);

            List<ExtractViewModel> data = new ResultSet_Extracts().GetResult(param.Search.Value, param.SortOrder, param.Start, param.Length, dtSource, null);
            int count = new ResultSet_Extracts().Count(param.Search.Value, dtSource, null);

            DTResult<ExtractViewModel> result = new DTResult<ExtractViewModel>{
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
        public JsonResult GetUploadNameJS(int uploadId) {
          var ajaxResponse = new AjaxResponse{Success = false, Message = "An error occurred while getting the upload name."};

          try {
            string uploadName;

            var upload = ExtractViewModel.GetExtract(_unitOfWork, uploadId);

            if (upload != null) {
              uploadName = upload.Name;
            } else {
              uploadName = "unknown";
            }

            ajaxResponse.Success = true;
            ajaxResponse.TheData = uploadName;
          } catch (Exception ex) {
            ErrorTools.HandleError(ex, ErrorLevel.NonFatal);  //just log, no redirect
          }

          return (Json(ajaxResponse));
        }

        public ActionResult ShowUploadDetailOnly(int uploadId) {
          ExtractViewModel upload = ExtractViewModel.GetExtract(_unitOfWork, uploadId);

          return View("UploadDetail", upload);
        }

#region CSV Export

        public ActionResult ExportUploads() {
          string fileName = "RadiumRCM_Audits.csv";
          var uploads = ExtractViewModel.GetExtracts(_unitOfWork)
                            .Select(a => new {
                                        InternalUser = a.InternalUserId,
                                        a.Name,
                                        a.DateAdded})
                            .OrderBy(a => a.Name)
                            .ToList();

          MemoryStream csvStream = Utility.GenerateCsvStream(fileName, uploads);
          return File(csvStream, "text/csv", fileName);
        }

        public ActionResult ExportUploadData(int uploadId, string uploadName) {
          string fileName = string.Format("RadiumRCM_UploadData_{0}.csv", Utility.SanitizeFileName(uploadName));
          var extractItems = ExtractViewModel.GetExtractItems(_unitOfWork, uploadId)
                                 .Select(e => new {
                                             e.UserName,
                                             e.LastName,
                                             e.FirstName,
                                             e.EmployeeID,
                                             e.SecurityGroup})
                                 .OrderBy(e => e.UserName)
                                 .ToList();

          MemoryStream csvStream = Utility.GenerateCsvStream(fileName, extractItems);
          return File(csvStream, "text/csv", fileName);
        }

#endregion
  }
}