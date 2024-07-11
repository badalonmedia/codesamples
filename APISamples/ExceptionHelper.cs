using System;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using GenAPI.Models;
using GenAPI.Components.Exceptions;
using GenAPI.Components.Http;
using GenAPI.DataContractsDotNetCore;

namespace GenAPI.Components {
  /// <summary>
  /// Shortcuts to generating exception text for various purposes.  Based also on environment - give more detail in development env.
  /// </summary>
  public class ExceptionHelper {
    /// <summary>
    /// Construct string from Problem Details instance.
    /// </summary>
    /// <param name="problemDetails"></param>
    /// <returns>problem details text</returns>
    static public string GetProblemDetailsContent(ProblemDetails problemDetails) {
      var pdText = new StringBuilder();
      pdText.Append($"Status Code: {problemDetails.Status}, Details: {problemDetails.Detail}, Title: {problemDetails.Title}, Type: {problemDetails.Type}, Instance: {problemDetails.Instance}");

      if (problemDetails.Extensions != null) {
        //pdText.Append("    Extensions-> ");

        foreach (string key in problemDetails.Extensions.Keys) {
          pdText.Append($" ,{key}: {(problemDetails.Extensions[key] ?? " NULL ").ToString()}");
        }
      }

      return pdText.ToString();
    }

    /// <summary>
    /// Generate and log exception info.
    /// </summary>
    /// <param name="ex"></param>
    /// <param name="response"></param>
    /// <param name="logger"></param>
    /// <param name="env"></param>
    /// <param name="config"></param>
    /// <param name="parameters"></param>
    /// <returns>exception info for response</returns>
    static public ApiExceptionContractModel HandleException(CustomApiException ex, HttpResponse response, ILogger logger, IWebHostEnvironment env, ConfigurationModel config, object parameters) {
      if (ex.StatusCode == 0) {
        ex.StatusCode = StatusCodes.Status500InternalServerError;
      }

      if (String.IsNullOrWhiteSpace(ex.StandardTitle)) {
        ex.StandardTitle = HttpStatusTitles.Status500InternalServerError;
      }

      response.StatusCode = ex.StatusCode;                                        //Set status code before LogError, else logged status code may be wrong - depending on the type of error.
      var exceptionDataForResponse = GetExceptionResult(env, ex, parameters);     //Based on env
      var exceptionDataInternal = GetExceptionResult(env, ex, parameters, true);  //Force full detail

      var jsonSettings = new JsonSerializerSettings(){
          //TODO: Anything else useful?
          NullValueHandling = NullValueHandling.Include,
          MaxDepth = 100,
          StringEscapeHandling = StringEscapeHandling.Default,
      };

      logger.LogError($"{exceptionDataInternal.ExceptionDetails.ToString()}", config, ex.StatusCode);
      return exceptionDataForResponse;
    }

    /// <summary>
    /// Generate exception info and convert to JSON
    /// </summary>
    /// <param name="env"></param>
    /// <param name="ex"></param>
    /// <param name="parameters"></param>
    /// <param name="forceFullDetail"></param>
    /// <returns>exception info as JSON</returns>
    static public string GetExceptionSerializedResult(IWebHostEnvironment env, CustomApiException ex, object parameters, bool forceFullDetail = false) {
      var exceptionInfo = GetExceptionResult(env, ex, parameters);
      return JsonConvert.SerializeObject(exceptionInfo, Formatting.Indented);
    }

    /// <summary>
    /// Generate exception info.
    ///
    /// Included extended details if env is Development or forceFullDetail is true.
    /// </summary>
    /// <param name="env"></param>
    /// <param name="ex"></param>
    /// <param name="parameters"></param>
    /// <param name="forceFullDetail"></param>
    /// <returns></returns>
    static private ApiExceptionContractModel GetExceptionResult(IWebHostEnvironment env, CustomApiException ex, object parameters, bool forceFullDetail = false) {
      if (forceFullDetail || env.IsDevelopment()) {
        //return more detailed info in development env

        var exceptionDetails = new ApiExceptionExtendedContractModel(){
            //Message = defaultMsg,
            //Exception = (ex != null ? ex.Message : defaultMsg),
            ExceptionMessage = ex.Message,
            InnerExceptionMessage = ex?.InnerException?.Message,
            StackTrace = ex?.StackTrace

            //NLog provides much of this exception info automatically via the LayoutRenderes in nlog.config.  But it doesn't always match
            //the exception properties I pull here.  I believe it depends on whether the exception is caught by a catch block,
            //or whether it's handled by middleware, and whethe the exception being handled has become an Inner Exception in the process.
            //So...I'm providing exception detail that's redundant by title, but not necessarily by content.
        };

        return new ApiExceptionContractModel()  //ProblemDetails fields
            {
                Title = ex.StandardTitle,
                //Detail = (ex != null ? ex.Message : defaultMsg),
                Detail = ex.Message,
                Status = ex.StatusCode,
                Type = ex.TypeUrl,
                Instance = ex.Instance,

                ExceptionDetails = exceptionDetails};
      } else  //no extended exception info being provided
      {
        return new ApiExceptionContractModel()  //ProblemDetails fields
            {
                Title = ex.StandardTitle,
                Detail = ex.Message,
                Status = ex.StatusCode,
                Type = ex.TypeUrl,
                Instance = ex.Instance};
      }
    }
  }
}
