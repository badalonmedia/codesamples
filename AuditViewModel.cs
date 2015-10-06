using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Xml.Serialization;
using System.Linq;
using System.Text.RegularExpressions;
using Ernest.OptimumRCMAudit.Entities;
using Ernest.OptimumRCMAudit.Main.Components;

namespace Ernest.OptimumRCMAudit.Main.Models
{
    [XmlRoot("item")]
    public class AuditExceptionTypeModel
    {
        //values must match Name col in AuditExceptionType db table
        static public string InUploadButInvalid = "E4";
        static public string InUploadButNotInMaster = "E1";
        static public string InMasterButNotInUpload = "E2";
        static public string InBothWithTemplateDifferences = "E3A";
        static public string InBothWithFacilityDifferences = "E3B";
        static public string InUploadButHandleManually = "E5";
    }

    [XmlRoot("item")]
    public class AuditMasterItemModel
    {
        public AuditMasterItemModel()
        {
            ProcessedYN = false;
        }

        public int AuditMasterItemId { get; set; }        
        public string Login { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string Facilities { get; set; }
        public string Templates { get; set; }
        public bool ProcessedYN { get; set; }
    }
    
    [XmlRoot("item")]
    public class AuditExceptionModel
    {
        public int AuditExceptionId { get; set; }
        public int AuditExceptionTypeId { get; set; }
        public int DisplayOrder { get; set; }
        public string AuditExceptionType { get; set; }
        public string Message { get; set; }
        public string AuditExceptionTypeName { get; set; }
        public string UserNameFromOptimum { get; set; }
        public string UserNameFromMaster { get; set; }
        public string FacilitiesFromOptimum { get; set; }
        public string FacilitiesFromMaster { get; set; }
        public string TemplatesFromOptimum { get; set; }
        public string TemplatesFromMaster { get; set; }
    }    

    [XmlRoot("item")]
    public class AuditViewModel
    {
        [XmlIgnore]
        private IUnitOfWork _unitOfWork;

        [XmlIgnore]
        public IUnitOfWork TheUnitOfWork
        {
            get { return (_unitOfWork); }
            set
            {
                _unitOfWork = value;
            }
        }        

        public AuditViewModel()
        {
         
        }

        public AuditViewModel(string auditUserId)   
        {
            InternalUserId = auditUserId;  // _unitOfWork.AspNetUsers.GetAll().Where(u => u.UserName == controller.User.Identity.Name).FirstOrDefault().Id;
         
        }        
        public AuditViewModel(IUnitOfWork unitOfWork, string auditUserId)
        {
            _unitOfWork = unitOfWork;
            InternalUserId = auditUserId;         
        }

        public string InternalUserId { get; set; }
        
        public int AuditId { get; set; }

        [Required(ErrorMessage = "The upload is required")]
        [Display(Name = "Upload: ")]        
        public int ExtractId { get; set; }        

        public DateTime AuditStartDate { get; set; }
        public DateTime? AuditEndDate { get; set; }
                

        [Required(ErrorMessage = "The audit name is required")]
        [Display(Name = "Audit Name:")]
        [StringLength(200)]
        public string Name { get; set; }
        public string NameSave { get; set; }

        #region DB Ops

        /// <summary>
        /// Saves existing Audit.
        /// </summary>
        /// <returns>Id of existing audit</returns>
        public int UpdateAndSave()
        {
            var existingAudit = _unitOfWork.Audits.GetAll()
                        .Where(a => a.AuditId == this.AuditId).ToList().FirstOrDefault();
            existingAudit.Name = this.Name;
            _unitOfWork.Save();
            return (existingAudit.AuditId);
        }

        /// <summary>
        /// Saves new Audit.
        /// </summary>
        /// <returns>Id of new audit</returns>
        public int AddAndSave()
        {
            Audit newAudit = new Audit();
            newAudit.Name = this.Name;
            newAudit.AuditStartDate = DateTime.Now;
            newAudit.AuditEndDate = null;
            newAudit.AuditUserId = InternalUserId;
            newAudit.ExtractId = this.ExtractId;
            _unitOfWork.Audits.Add(newAudit);
            _unitOfWork.Save();
            return (newAudit.AuditId);
        }

        /// <summary>
        /// Gets an Audit.
        /// </summary>
        /// <param name="unitOfWork">UOW instance for db.</param>
        /// <param name="auditId">The audit Id.</param>
        /// <returns></returns>
        static public AuditViewModel GetAudit(IUnitOfWork unitOfWork, int auditId)
        {
            var audit = unitOfWork.Audits.GetAll().Where(a => a.AuditId == auditId)
                .Select(a => new AuditViewModel
                {
                    AuditId = a.AuditId,
                    AuditStartDate = a.AuditStartDate,
                    AuditEndDate = a.AuditEndDate,                    
                    InternalUserId = a.AuditUserId,
                    Name = a.Name,
                    ExtractId = (int)a.ExtractId
                })
                .SingleOrDefault();

            if (audit != null)
            {
                audit.TheUnitOfWork = unitOfWork;                
            }

            return (audit);
        }

        /// <summary>
        /// Gets all Audits.
        /// </summary>
        /// <param name="unitOfWork">UOW instance for db operations.</param>
        /// <returns>List of Audits</returns>
        static public List<AuditViewModel> GetAudits(IUnitOfWork unitOfWork)
        {
            var extracts = unitOfWork.Audits.GetAll()
                .Select(e => new AuditViewModel
                {
                    AuditId = e.AuditId,
                    Name = e.Name,
                    AuditStartDate = e.AuditStartDate,
                    AuditEndDate = e.AuditEndDate,
                    InternalUserId = e.AuditUserId,
                    ExtractId = (int)e.ExtractId
                })
                .ToList();

            return (extracts);
        }

        /// <summary>
        /// Gets exceptions for an Audit.
        /// </summary>
        /// <param name="unitOfWork">UOW instance for db operations.</param>
        /// <param name="auditId">The audit Id.</param>
        /// <returns>List of Audit exceptions</returns>
        static public List<AuditExceptionModel> GetAuditExceptions(IUnitOfWork unitOfWork, int auditId)
        {
            var audits = unitOfWork.AuditExceptions.GetAll().Where(a => a.AuditId == auditId)
                .Select(e => new AuditExceptionModel
                {
                    AuditExceptionId = e.AuditExceptionId,
                    AuditExceptionType = e.AuditExceptionType.Description,
                    Message = e.Message,
                    AuditExceptionTypeName = e.AuditExceptionType.Name,
                    AuditExceptionTypeId = e.AuditExceptionTypeId,
                    DisplayOrder = (int)e.AuditExceptionType.DisplayOrder,
                    UserNameFromOptimum = e.UserNameFromOptimum,
                    UserNameFromMaster = e.UserNameFromMaster,
                    FacilitiesFromOptimum = e.FacilitiesFromOptimum,
                    FacilitiesFromMaster = e.FacilitiesFromMaster,
                    TemplatesFromOptimum = e.TemplatesFromOptimum,
                    TemplatesFromMaster = e.TemplatesFromMaster
                })
                .OrderBy(e => e.DisplayOrder)
                .ToList();

            return (audits);
        }

        /// <summary>
        /// Gets master items used in Audit.
        /// </summary>
        /// <param name="unitOfWork">UOW instance for db operations.</param>
        /// <param name="auditId">The audit Id.</param>
        /// <returns></returns>
        static public List<AuditMasterItemModel> GetAuditMasterItems(IUnitOfWork unitOfWork, int auditId)
        {
            var items = unitOfWork.AuditMasterItems.GetAll().Where(i => i.AuditId == auditId)
                .Select(i => new AuditMasterItemModel
                {
                    AuditMasterItemId = i.AuditMasterItemId,
                    Login = i.Login,
                    LastName = i.LastName,
                    FirstName = i.FirstName,
                    Facilities = i.Facilities,
                    Templates = i.Templates
                })
                .ToList();

            return (items);
        }

        /// <summary>
        /// Determines whether Audit is unique.
        /// </summary>
        /// <returns>Is Audit unique</returns>
        public bool IsUniqueYN()
        {
            bool result = true;            

            if (AuditId == 0)     //new audit
            {
                var auditCount = _unitOfWork.Audits.GetAll().Where(a => a.Name == this.Name).Count();                
                
                if (auditCount > 0)
                {
                    result = false;
                }
            }
            else if (this.Name != this.NameSave)   //audit was modified
            {
                //does the audit name exist
                var auditCount = _unitOfWork.Audits.GetAll().Where(a => a.Name == this.Name && a.AuditId != this.AuditId).Count();

                if (auditCount > 0)
                {
                    result = false;
                }
            }

            return (result);
        }

        #endregion

        #region Business Logic

        /// <summary>
        /// Determines whether item should be excluded from Audit.
        /// </summary>
        /// <param name="extractItem">The extract item.</param>
        /// <returns>Whether item should be excluded</returns>
        private bool ExcludeFromAuditYN(ExtractItemModel extractItem)
        {
            return (string.IsNullOrWhiteSpace(extractItem.EmployeeID) ||
                string.IsNullOrWhiteSpace(extractItem.SecurityGroup) ||
                string.IsNullOrWhiteSpace(extractItem.UserName) ||
                extractItem.EmployeeID.Trim().ToLower() == "multiple");
            //    !extractItem.SecurityGroup.ToLower().Contains("template") ||
        }        

        /// <summary>
        /// Builds delimited string of Optimum templates for use in comparison.
        /// </summary>
        /// <param name="templates">The Optimum templates embedded in other text.</param>
        /// <param name="auditName">Name of the Audit.</param>
        /// <returns>Delimited string of Optimum templates for comparison</returns>
        static public string CalculateOptimumTemplatesForComparison(string templates, string auditName)
        {
            string templatesUse = (templates ?? string.Empty).Trim().ToLower();

            if (string.IsNullOrWhiteSpace(templatesUse))
            {
                return (string.Empty);
            }

            //pull out numerics as strings
            string[] arrTemplates = Regex.Split(templatesUse, @"\D+").Where(i => i.Length > 0).ToArray();            

            //convert to actual numeric type - by this point there should be only numeric strings
            List<int> arrTemplateVals = new List<int>();
            bool isError = false;

            foreach(string template in arrTemplates)
            {
                int number;
                if (int.TryParse((template ?? string.Empty).Trim(), out number))
                {
                    arrTemplateVals.Add(number);                    
                }
                else
                {
                    isError = true;                    
                }
            }

            if (isError)
            {
                //shouldn't happen but log it
                string msg = string.Format("One or more non-numeric Optimum Template was found during audit {0}: {1}", auditName, templates);
                ErrorTools.HandleError(new ApplicationException(msg), ErrorLevel.NonFatal);
            }

            var valsSorted = arrTemplateVals.OrderBy(o => o).ToList();            
            string templatesResult = Utility.IntListToDelimited(valsSorted, ",").Trim();            

            return (templatesResult);
        }

        /// <summary>
        /// Builds delimited string of Optimum facilities for use in comparison.
        /// </summary>
        /// <param name="facilities">The Optimum facilities embedded with other text.</param>
        /// <param name="auditName">Name of the Audit.</param>
        /// <returns>Delimited string of facilities to use in comparison</returns>
        static private string CalculateOptimumFacilitiesForComparison(string facilities, string auditName)
        {
            if (string.IsNullOrWhiteSpace(facilities))
            {
                return (string.Empty);
            }

            string optimumFacilitiesDelim = Properties.Settings.Default.OptimumFacilitiesDelim[1].ToString();
            List<string> exceptions = new List<string>();            
            string facResult = Utility.StringToSortedIntString(facilities, optimumFacilitiesDelim, ref exceptions).Trim();

            if (exceptions.Count > 0)
            {
                string msg = string.Format("One or more non-numeric Optimum Facility was found during audit {0}: {1}", auditName, facilities);
                ErrorTools.HandleError(new ApplicationException(msg), ErrorLevel.NonFatal);
            }

            return (facResult);            
        }

        /// <summary>
        /// Builds delimited string of Master facilities for use in comparison.
        /// </summary>
        /// <param name="facilities">The facilities in raw format.</param>
        /// <param name="auditName">Name of the audit.</param>
        /// <returns>Delimited string of master facilities to use in comparison</returns>
        static private string CalculateDatabaseFacilitiesForComparison(string facilities, string auditName)
        {
            if (string.IsNullOrWhiteSpace(facilities))
            {
                return (string.Empty);
            }
            
            List<string> exceptions = new List<string>();
            string facResult = Utility.StringToSortedIntString(facilities, ",", ref exceptions).Trim();

            if (exceptions.Count > 0)
            {
                string msg = string.Format("One or more non-numeric DB Facility was found during audit {0}: {1}", auditName, facilities);
                ErrorTools.HandleError(new ApplicationException(msg), ErrorLevel.NonFatal);                
            }
            return (facResult);
        }

        /// <summary>
        /// Builds delimited string of Master templates for use in comparison.
        /// </summary>
        /// <param name="templates">The templates in raw format.</param>
        /// <param name="auditName">Name of the audit.</param>
        /// <returns>Delimited string of master templates to use in comparison</returns>
        static private string CalculateDatabaseTemplatesForComparison(string templates, string auditName)
        { 
            if (string.IsNullOrWhiteSpace(templates))
            {
                return (string.Empty);
            }
            
            List<string> exceptions = new List<string>();
            string result = Utility.StringToSortedIntString(templates, ",", ref exceptions).Trim();

            if (exceptions.Count > 0)
            {
                string msg = string.Format("One or more non-numeric DB Template was found during audit {0}: {1}", auditName, templates);
                ErrorTools.HandleError(new ApplicationException(msg), ErrorLevel.NonFatal);
            }

            return (result);
        }

        #endregion

        #region Audit

        /// <summary>
        /// Performs the Audit.
        /// </summary>
        /// <exception cref="System.ApplicationException">A problem occurred while generating database snapshot.</exception>
        public void PerformAudit()
        {
            //when complete present...
            //1-Items from upload not included in audit
            //2-items in upload but not in master
            //3-items in master but not in upload
            //4-items in both but with differences            
            
            Dictionary<string, ExtractItemModel> extractExclude = new Dictionary<string, ExtractItemModel>();
            List<ExtractItemModel> extractInclude = new List<ExtractItemModel>();            
            List<AuditException> auditExceptions = new List<AuditException>();            

            //Step 1: get extract contents, by default I'm not peristing it in the model
            var extractItems = ExtractViewModel.GetExtractItems(_unitOfWork, (int)this.ExtractId);
            
            //Step 2: determine which extract items can/should be used
            foreach(ExtractItemModel extractItem in extractItems)
            {
                if (!ExcludeFromAuditYN(extractItem))
                {
                    extractInclude.Add(extractItem);
                }
                else
                {
                    string userNameUse = (extractItem.UserName ?? string.Empty).Trim().ToLower();
                    if (!extractExclude.ContainsKey(userNameUse))
                    {
                        extractExclude.Add(userNameUse, extractItem);
                    }
                }
            }

            //Note: use extractInclude list as source for audit henceforth

            //Step #3: generate database snapshot against which to perform the audit, and build lookup
            var genResult = _unitOfWork.OptUsers.GenerateMasterUsersForAudit(this.AuditId);

            if (genResult < 1)
            {
                throw new ApplicationException("A problem occurred while generating database snapshot.");
            }

            //Step #4: fetch that database snapshot
            var databaseMaster = _unitOfWork.AuditMasterItems.GetAll()
                .Where(i => i.AuditId == this.AuditId)
                .Select(i => new AuditMasterItemModel
                {
                    AuditMasterItemId = i.AuditMasterItemId,
                    FirstName = i.LastName,                    
                    LastName = i.LastName,
                    Login = i.Login,                        
                    Facilities = i.Facilities,
                    Templates = i.Templates,
                    ProcessedYN = false     //derived col to monitor audit status
                })
                .ToDictionary(i => i.Login.ToLower(), i => i);            

            //Step #5: get some helper info to use during audit, e.g. id's for exception types      
            //ugly code but prevents need for lookups during the audit      

            int idInUploadButNotInMaster = _unitOfWork.AuditExceptionTypes.GetAll()
                .Where(t => t.Name == AuditExceptionTypeModel.InUploadButNotInMaster)
                .SingleOrDefault()
                .AuditExceptionTypeId;

            int idInMasterButNotInUpload = _unitOfWork.AuditExceptionTypes.GetAll()
                .Where(t => t.Name == AuditExceptionTypeModel.InMasterButNotInUpload)
                .SingleOrDefault()
                .AuditExceptionTypeId;

            int idInBothWithFacilityDifferences = _unitOfWork.AuditExceptionTypes.GetAll()
                .Where(t => t.Name == AuditExceptionTypeModel.InBothWithFacilityDifferences)
                .SingleOrDefault()
                .AuditExceptionTypeId;

            int idInBothWithTemplateDifferences = _unitOfWork.AuditExceptionTypes.GetAll()
                .Where(t => t.Name == AuditExceptionTypeModel.InBothWithTemplateDifferences)
                .SingleOrDefault()
                .AuditExceptionTypeId;

            int idInUploadButInvalid = _unitOfWork.AuditExceptionTypes.GetAll()
                .Where(t => t.Name == AuditExceptionTypeModel.InUploadButInvalid)
                .SingleOrDefault()
                .AuditExceptionTypeId;

            int idInUploadButHandleManually = _unitOfWork.AuditExceptionTypes.GetAll()
                .Where(t => t.Name == AuditExceptionTypeModel.InUploadButHandleManually)
                .SingleOrDefault()
                .AuditExceptionTypeId;            

            //Step #6: iterate over upload/extract and perform audit            
            foreach (ExtractItemModel extractItem in extractInclude)
            {
                string optTemplates = CalculateOptimumTemplatesForComparison(extractItem.SecurityGroup, this.Name);
                string optFac = CalculateOptimumFacilitiesForComparison(extractItem.EmployeeID, this.Name);

                if (!databaseMaster.ContainsKey(extractItem.UserName.ToLower()))
                {
                    AuditException ex = new AuditException();
                    ex.AuditId = this.AuditId;                    
                    ex.AuditExceptionTypeId = idInUploadButNotInMaster;
                    //ex.AuditExceptionType.Name = AuditExceptionTypeModel.InExtractButNotInMaster;
                    ex.Message = string.Format("User '{0}' occurs in upload but not in master", extractItem.UserName);
                    ex.UserNameFromOptimum = extractItem.UserName;
                    ex.UserNameFromMaster = string.Empty;
                    ex.FacilitiesFromMaster = string.Empty;
                    ex.FacilitiesFromOptimum = optFac;
                    ex.TemplatesFromOptimum = optTemplates;
                    ex.TemplatesFromMaster = string.Empty;                    
                    _unitOfWork.AuditExceptions.Add(ex);
                }                
                else 
                {
                    //in both but check for facility differences

                    AuditMasterItemModel dbItem = databaseMaster[extractItem.UserName.ToLower()];
                    dbItem.ProcessedYN = true;  //indicate that db entry has been processed
                    
                    string dbFac = CalculateDatabaseFacilitiesForComparison(dbItem.Facilities, this.Name);
                    string dbTemplates = CalculateDatabaseTemplatesForComparison(dbItem.Templates, this.Name);

                    if (optFac != dbFac)
                    {
                        AuditException ex = new AuditException();
                        ex.AuditId = this.AuditId;
                        ex.AuditExceptionTypeId = idInBothWithFacilityDifferences;                        
                        ex.Message = string.Format("User '{0}' occurs in both but facilities differ", extractItem.UserName);
                        ex.UserNameFromOptimum = extractItem.UserName;
                        ex.UserNameFromMaster = extractItem.UserName;
                        ex.FacilitiesFromMaster = dbFac;
                        ex.FacilitiesFromOptimum = optFac;
                        ex.TemplatesFromOptimum = optTemplates;
                        ex.TemplatesFromMaster = dbTemplates;
                        _unitOfWork.AuditExceptions.Add(ex);
                    }

                    //in both but check for template differences
                    if (optTemplates != dbTemplates)
                    {
                        AuditException ex = new AuditException();
                        ex.AuditId = this.AuditId;
                        ex.AuditExceptionTypeId = idInBothWithTemplateDifferences;
                        ex.Message = string.Format("User '{0}' occurs in both but templates differ", extractItem.UserName);
                        ex.UserNameFromOptimum = extractItem.UserName;
                        ex.UserNameFromMaster = extractItem.UserName;
                        ex.FacilitiesFromMaster = dbFac;
                        ex.FacilitiesFromOptimum = optFac;
                        ex.TemplatesFromOptimum = optTemplates;
                        ex.TemplatesFromMaster = dbTemplates;
                        _unitOfWork.AuditExceptions.Add(ex);
                    }
                }
            }

            //Step #7: make note of master items that were not processed
            foreach(string key in databaseMaster.Keys)
            {
                if (!databaseMaster[key].ProcessedYN && !extractExclude.ContainsKey(key))
                {
                    string dbFac = CalculateDatabaseFacilitiesForComparison(databaseMaster[key].Facilities, this.Name);
                    string dbTemplates = CalculateDatabaseTemplatesForComparison(databaseMaster[key].Templates, this.Name);

                    AuditException ex = new AuditException();
                    ex.AuditId = this.AuditId;
                    ex.AuditExceptionTypeId = idInMasterButNotInUpload;                    
                    ex.Message = string.Format("User '{0}' occurs in master but not in upload", key);
                    ex.UserNameFromMaster = key;     
                    ex.UserNameFromOptimum = string.Empty;                    
                    ex.FacilitiesFromMaster = dbFac;
                    ex.FacilitiesFromOptimum = string.Empty;
                    ex.TemplatesFromOptimum = string.Empty;
                    ex.TemplatesFromMaster = dbTemplates;                    
                    _unitOfWork.AuditExceptions.Add(ex);
                }
            }

            //Step #8: make note of extract items that were excluded - add them as exceptions
            foreach (ExtractItemModel extractItem in extractExclude.Values)
            {
                AuditException ex = new AuditException();
                string optTemplates = CalculateOptimumTemplatesForComparison(extractItem.SecurityGroup, this.Name);

                ex.AuditId = this.AuditId;
                ex.TemplatesFromMaster = string.Empty;
                ex.TemplatesFromOptimum = optTemplates;
                ex.UserNameFromMaster = string.Empty;
                ex.FacilitiesFromMaster = string.Empty;
                ex.UserNameFromOptimum = extractItem.UserName;

                if ((extractItem.EmployeeID ?? string.Empty).Trim().ToLower() == "multiple")
                {                    
                    ex.AuditExceptionTypeId = idInUploadButHandleManually;
                    ex.Message = string.Format("User '{0}' must be handled manually", extractItem.UserName);                    
                    ex.FacilitiesFromOptimum = extractItem.EmployeeID;                    
                }
                else
                {                    
                    string optFac = CalculateOptimumFacilitiesForComparison(extractItem.EmployeeID, this.Name);
                    ex.AuditExceptionTypeId = idInUploadButInvalid;
                    ex.Message = string.Format("User '{0}' from upload was excluded from audit", extractItem.UserName);                    
                    ex.FacilitiesFromOptimum = optFac;                    
                }

                _unitOfWork.AuditExceptions.Add(ex);
            }

             //Step #9: set end date             
             Audit audit = _unitOfWork.Audits.GetAll().Where(a => a.AuditId == this.AuditId).SingleOrDefault();
            audit.AuditEndDate = DateTime.Now;

            //Step #9: commit everyting - may take a minute
            _unitOfWork.Save();
        }

        #endregion
    }
}
