using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Annotations;
using GenAPI.Models;
using GenAPI.DataContractsDotNetCore;
using GenAPI.Components;

namespace GenAPI.Controllers {
  [Route("[controller]")]
  [Authorize(Roles = Models.UserRole.Billing)]
  [ApiController]
  public class BillingController : GenAPIBaseController {
    //NOTE: For documentation purposes, if I have the params in a separate model class, Swashbuckle doesn't pick up certain details.
    //So for billing/billingcases I'm specifying each parameter in the method signature though it's messy.

    public BillingController(ConfigurationModel config, IWebHostEnvironment env, ILogger<CasesController> logger) : base(config, env, logger) {
    }

#region API& Documentation Attributes
    [SwaggerOperation(
        Summary = "Returns case detail for billing app.",
        Description = @"

        Sample Request URI:

            https
        :  //{domain}/billing/billingcases?receivedAtBCStartDate=1-2-2020&receivedAtBCEndDate=1-31-2020&trialTypeList=BP&includeTestTrials=false&includeTestCases=false&PageSize=500&PageNumber=1

        ",
        OperationId = "Billing_BillingCases",
        Tags = new[]{"Billing"})]
        [Produces("application/json")]
        [SwaggerResponse(StatusCodes.Status200OK, "The case data was returned.", typeof(CaseDetailsContractModel))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "One or more parameters is invalid.", typeof(ContractErrorModel))]
        [SwaggerResponse(StatusCodes.Status401Unauthorized, "The tokens provided are either missing, invalid or have expired.", typeof(ContractErrorModel))]
        [SwaggerResponse(StatusCodes.Status403Forbidden, "The tokens provided do not correspond to an authorized user for this operation.", typeof(ContractErrorModel))]
        [SwaggerResponse(StatusCodes.Status404NotFound, "No case data was found based on the parameters provided.", typeof(ContractBaseModel))]
        [SwaggerResponse(StatusCodes.Status500InternalServerError, "An unexpected server error occurred.", typeof(ContractBaseModel))]
        //[SwaggerRequestExample()]
        [HttpGet("/[controller]/BillingCases", Name = "BillingCases")]
#endregion
        public async Task<ActionResult<CaseDetailsContractModel>> GetBillingCasesAsync(
            [ FromQuery ] BillingParamsModel billingParams,
            [ FromQuery ] PagingParamsModel pagingParams) {
      SetContentType();

      _logger.ControllerActionStart(ControllerContext);

      var parameters = new {billingParams = billingParams, pagingParams = pagingParams};

      //inspect paging params

      try {
        var(totalRecords, caseResult) = await BL.GetBillingCasesAsync(_configuration,
                                                                      billingParams,
                                                                      pagingParams,
                                                                      _logger);

        if (caseResult == null || caseResult.Count() < 1) {
          string defaultMsg = $"Status Code: {StatusCodes.Status404NotFound}, Exception: No matching data was found";
          _logger.LogError($"{ExceptionHelper.GetExceptionSerializedResult(_env, null, defaultMsg, parameters, true)}, Parameters: {parameters}",
                           _configuration,
                           StatusCodes.Status404NotFound);
          return NotFound(ExceptionHelper.GetExceptionResult(_env, null, defaultMsg));
        }

        //paging calculations for response
        var pagingMetrics = new PagingContractModel(totalRecords, pagingParams.PageNumber, pagingParams.PageSize);

        _logger.ControllerActionSuccess(ControllerContext);
        return Ok(new {pagingMetrics = pagingMetrics, results = caseResult});  //TODO: Revisit use of contracts
      } catch (Exception ex) {
        string defaultMsg = $"Status Code: {StatusCodes.Status500InternalServerError}, Exception: {ex.GetType().Name} - An error occurred while completing your request";
        _logger.LogError(ex, $"{ExceptionHelper.GetExceptionSerializedResult(_env, ex, defaultMsg, parameters, true)}, Parameters: {parameters}",
                         _configuration,
                         StatusCodes.Status500InternalServerError);
        return StatusCode(StatusCodes.Status500InternalServerError, ExceptionHelper.GetExceptionResult(_env, ex, defaultMsg));
      }
    }
  }
}