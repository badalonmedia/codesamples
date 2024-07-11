using System;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.Filters;
using GenAPI.Components.Caching;
using GenAPI.Components.Http;
using GenAPI.Models;
using GenAPI.Models.SwaggerDoc;
using GenAPI.OrmModels;
using GenAPI.Components.Authentication;
using GenAPI.DataContractsDotNetCore;

namespace GenAPI.Controllers {
  //Controller dedicated to authenticating users and providing tokens.
  [Route("[controller]")]
  [Authorize]  //comment out for dev work if needed
      [ApiController]
      [ApiVersion("0.9")]
      public class TokensController : GenAPIBaseController {
    public TokensController(IOptionsMonitor<ConfigurationModel> config, IWebHostEnvironment env, ILogger<TokensController> logger, IApiCache apiCache) : base(config, env, logger, apiCache) {
    }

#region API& Documentation Attributes
    [SwaggerOperation(
        Summary = "Generates a new JWT / Refresh Token pair upon success and returns them in Response headers as follows:\n\r\n\r" +
                  HttpHeaderConstants.JWT + ":  Type: String, Description: JSON Web Token required for all subsequent API operations\n\r" +
                  HttpHeaderConstants.Refresh + ":  Type: String, Description: Corresponding Refresh Token required to renew an expired JWT\n\r" +
                  HttpHeaderConstants.RefreshExp + ":  Type: String, Description: Expiration Date/Time of Refresh Token in UTC format",
        Description = "",
        OperationId = "Tokens_Generate",
        Tags = new[]{"Tokens"})]
        [Produces(HttpContentTypes.JsonContent, HttpContentTypes.JsonErrorContent)]
        [Consumes("application/json", "text/json")]
        [SwaggerResponse(StatusCodes.Status200OK, "The tokens were generated and returned in the response headers.  See specifics above.", typeof(GenerateTokenResultsContractModel))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "User or password is not well formed.", typeof(ApiExceptionContractModelForSwagger))]
        [SwaggerResponse(StatusCodes.Status401Unauthorized, "An authentication error has occurred.  See 400 status for structure of error info in the response.", Type = null)]
        [SwaggerResponse(StatusCodes.Status500InternalServerError, "An unexpected server error occurred.  See 400 status for structure of error info in the response.", Type = null)]
        //TODO: These response header attribs generate the content in the Swagger.JSON but RapidPDF isn't picking it up.
        [SwaggerResponseHeader(StatusCodes.Status200OK, HttpHeaderConstants.JWT, "string", "JSON Web Token required for all subsequent API operations")]
        [SwaggerResponseHeader(StatusCodes.Status200OK, HttpHeaderConstants.Refresh, "string", "Corresponding Refresh Token required to renew an expired JWT")]
        [SwaggerResponseHeader(StatusCodes.Status200OK, HttpHeaderConstants.RefreshExp, "string", "Expiration Date/Time of Refresh Token in UTC format", "date-time")]
        [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
        [AllowAnonymous]
        [HttpPost("/[controller]/generate", Name = "generate")]
#endregion
        public async Task<ActionResult<GenerateTokenContractModel>> GetTokensAsync(
            [ FromBody, SwaggerRequestBody("The username and password", Required = true) ] UserAuthParamsModel submittedUser) {
      var parameters = new {submittedUser = submittedUser.UserName};  //don't show password in response

      using var tokenHelper = new TokenHelper(_env, _config, _logger);

      var authResults = await tokenHelper.AuthenticateUserAsync(submittedUser);

      if (authResults.ErrorOccurred) {
        return Unauthorized(new {error = authResults.ErrorMessage});
      }

      //User lookup went ok, proceed

      var verifiedUser = new User{
          UserName = submittedUser.UserName};

      var jwt = await tokenHelper.GenerateJWT(verifiedUser, authResults.Claims, true);

#region Snippet for storing JWT in response body vs header
//construct JSON for successful response
//var authResult = new
//{
//    tokens = new
//    {
//        jwt = new JwtSecurityTokenHandler().WriteToken(jwt),
//        refreshToken = authResults.RefreshTokenNew.RefreshToken     //, tokenExpiration = refreshTokenNew.DateExpires }
//    }
//};
#endregion

      //send headers with new tokens
      Response.Headers.Add(HttpHeaderConstants.JWT, new JwtSecurityTokenHandler().WriteToken(jwt));
      Response.Headers.Add(HttpHeaderConstants.Refresh, authResults.RefreshTokenNew.RefreshToken);
      Response.Headers.Add(HttpHeaderConstants.RefreshExp, authResults.RefreshTokenNew.DateExpires.ToString("s"));  //UTC   // ("r");

      return Ok(new {results = new GenerateTokenContractModel()});
    }
  }
}