using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Newtonsoft.Json;
using GenAPI.Models;
using GenAPI.Components.Exceptions;
using GenAPI.Components.Http;

namespace GenAPI.Components.Authentication {
  public static class JwtEventExtensions {
    /// <summary>
    /// Event to handle when a JWT is successfully validation.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="env"></param>
    /// <param name="logger"></param>
    static public void TokenValidated(this TokenValidatedContext context, IWebHostEnvironment env, ILogger logger) {
      logger.LogDebug("TokenValidated START");

      return;
    }

    /// <summary>
    /// Event to handler when a forbidden resource is requested.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="config"></param>
    /// <param name="env"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    static public async Task Forbidden(this ForbiddenContext context, IConfigurationModel config, IWebHostEnvironment env, ILogger logger) {
      logger.LogDebug("Forbidden START");

      context.Response.ContentType = HttpContentTypes.JsonErrorContent;
      context.Response.StatusCode = StatusCodes.Status403Forbidden;

      string defaultMsg = "403 Forbidden";
      var newEx = new CustomForbiddenException(defaultMsg);

      logger.DebugGivingUp(defaultMsg);

      logger.LogError($"{ExceptionHelper.GetExceptionSerializedResult(env, newEx, null, true)}",
                      config,
                      StatusCodes.Status403Forbidden);

      await context.Response.WriteAsync(ExceptionHelper.GetExceptionSerializedResult(env, newEx, null));

      return;
    }

    /// <summary>
    /// Event to handle challenge during authentication process.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="config"></param>
    /// <param name="env"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    static public async Task Challenge(this JwtBearerChallengeContext context, IConfigurationModel config, IWebHostEnvironment env, ILogger logger) {
      logger.LogDebug("Challenge START");

      string defaultMsg;

      if (context.AuthenticateFailure is SecurityTokenExpiredException) {
        //This has been handled already in AuthenticationFailed event.
        logger.LogDebug($"Exception Type is {context.AuthenticateFailure.GetType().FullName}, deferring to AuthenticationFailed.");
      } else if (context.AuthenticateFailure is null) {
        defaultMsg = $"Status Code: {StatusCodes.Status401Unauthorized}, An authentication error occurred, possibily an empty JWT";
        var newEx = new CustomUnauthorizedException(defaultMsg);

        logger.DebugGivingUp(defaultMsg);

        logger.LogError($"{ExceptionHelper.GetExceptionSerializedResult(env, newEx, null, true)}",
                        config,
                        StatusCodes.Status401Unauthorized);

        if (!context.Response.HasStarted) {
          context.Response.ContentType = HttpContentTypes.JsonErrorContent;
          context.Response.StatusCode = StatusCodes.Status401Unauthorized;                                    //Cliff 8/13/2021: Added to prevent 200 with empty JWT (though correctly no data is returned)
          await context.Response.WriteAsync(ExceptionHelper.GetExceptionSerializedResult(env, newEx, null));  //Cliff 8/13/2021: Added to provide more info about empty JWT error
        }
      } else  //Some other exception occurred - not sure if AuthenticationFailed event fired
      {
        defaultMsg = $"Exception Type is {context.AuthenticateFailure.GetType().FullName}, giving up.";

        var newEx = new CustomUnauthorizedException(defaultMsg, context.AuthenticateFailure);

        //handle other authentication issues such as invalid tokens - AuthenticationFailed event may not be not triggered.
        logger.DebugGivingUp(defaultMsg);

        logger.LogError($"{ExceptionHelper.GetExceptionSerializedResult(env, newEx, null, true)}",
                        config,
                        StatusCodes.Status401Unauthorized);

        if (!context.Response.HasStarted) {
          context.Response.ContentType = HttpContentTypes.JsonErrorContent;
          context.Response.StatusCode = StatusCodes.Status401Unauthorized;                                    //Cliff 8/13/2021: Added to prevent 200 for any other JWT edge cases, such as "empty" which is handled above
          await context.Response.WriteAsync(ExceptionHelper.GetExceptionSerializedResult(env, newEx, null));  //Cliff 8/13/2021: Added to provide more info about empty JWT error
        }
      }

      context.HandleResponse();
    }

    /// <summary>
    /// Event to handle all authentication activity
    /// </summary>
    /// <param name="context"></param>
    static public void MessageReceived(this MessageReceivedContext context) {
      return;
    }

    /// <summary>
    /// Even that handles authentication failure.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="config"></param>
    /// <param name="env"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    static public async Task AuthenticationFailed(this AuthenticationFailedContext context, IConfigurationModel config, IWebHostEnvironment env, ILogger logger) {
      logger.LogDebug("AuthenticationFailed START");

      context.Response.ContentType = HttpContentTypes.JsonErrorContent;
      context.Response.StatusCode = StatusCodes.Status401Unauthorized;

      string defaultMsg;

      //If Auth failed due to an expired JWT, we can go further and try to renew it with a valid RT,
      //but otherwise give up.

      if (!(context.Exception is SecurityTokenExpiredException)) {
        defaultMsg = $"Status Code: {StatusCodes.Status401Unauthorized}, An authentication error occurred";
        var newEx = new CustomUnauthorizedException(defaultMsg, context.Exception);

        logger.DebugGivingUp($"Exception Type is {context.Exception.GetType().FullName}, giving up.");

        logger.LogError(context.Exception, $"{ExceptionHelper.GetExceptionSerializedResult(env, newEx, null, true)}",
                        config,
                        StatusCodes.Status401Unauthorized);

        await context.Response.WriteAsync(
            ExceptionHelper.GetExceptionSerializedResult(env, newEx, null));

        return;
      }

      //Authentication failed because JWT has expired - take a deeper look.
      //If we have a refresh token and it has not expired, then use it to issue a new JWT.

      logger.LogDebug("JWT has expired, read into Auth header to proceed.");

      context.Response.Headers.Add(HttpHeaderConstants.ExpIndicator, "jwt");  //at the minimum, we know the jwt expired, so indicate this in header

      JwtSecurityToken jwToken = null;

      try {
        string authHeader = context.Request.Headers[HttpHeaderConstants.Authorization];

        if (String.IsNullOrWhiteSpace(authHeader) || authHeader.Length < "Bearer ".Length) {
          defaultMsg = "JWT has expired and the Authorization header is empty or corrupt.";
          var newEx = new CustomUnauthorizedException(defaultMsg);

          logger.DebugGivingUp(defaultMsg);

          logger.LogError($"{ExceptionHelper.GetExceptionSerializedResult(env, newEx, null, true)}",
                          config,
                          StatusCodes.Status401Unauthorized);

          //should not happen since the header existed to determine expiration, but...
          await context.Response.WriteAsync(ExceptionHelper.GetExceptionSerializedResult(env, newEx, null));
          return;
        }

        string token = authHeader.Substring("Bearer ".Length);
        var tokenHandler = new JwtSecurityTokenHandler();
        jwToken = tokenHandler.ReadToken(token) as JwtSecurityToken;

        if (jwToken == null) {
          defaultMsg = "JWT has expired and unable to read JWT from Authorization header. It may be corrupt.";
          var newEx = new CustomUnauthorizedException(defaultMsg);

          logger.DebugGivingUp(defaultMsg);

          logger.LogError($"{ExceptionHelper.GetExceptionSerializedResult(env, newEx, null, true)}",
                          config,
                          StatusCodes.Status401Unauthorized);

          await context.Response.WriteAsync(ExceptionHelper.GetExceptionSerializedResult(env, newEx, null));
          return;
        }

        //var jwtSubject = TokenHelper.ReadClaim(jwToken.Claims, JwtRegisteredClaimNames.Sub);

        if (String.IsNullOrWhiteSpace(jwToken.Subject)) {
          defaultMsg = "JWT has expired and the Subject cannot be read from JWT. The JWT may be corrupt.";
          var newEx = new CustomUnauthorizedException(defaultMsg);

          logger.DebugGivingUp(defaultMsg);

          logger.LogError($"{ExceptionHelper.GetExceptionSerializedResult(env, newEx, null, true)}",
                          config,
                          StatusCodes.Status401Unauthorized);

          await context.Response.WriteAsync(ExceptionHelper.GetExceptionSerializedResult(env, newEx, null));
          return;
        }

        logger.LogDebug("Expired JWT is not null or whitespace. So look at refresh token.");

        //basic check of refresh token
        string refreshToken = context.Request.Headers[HttpHeaderConstants.Refresh];

        using var tokenHelper = new TokenHelper(env, config, logger);

        if (!tokenHelper.IsValidRefreshToken(refreshToken)) {
          defaultMsg = "JWT has expired.  Attempting to renew the JWT but the Refresh Token is invalid.";
          var newEx = new CustomUnauthorizedException(defaultMsg);

          logger.DebugGivingUp(defaultMsg);

          logger.LogError($"{ExceptionHelper.GetExceptionSerializedResult(env, newEx, null, true)}",
                          config,
                          StatusCodes.Status401Unauthorized);

          //await context.Response.WriteAsync(JsonConvert.SerializeObject(ExceptionHelper.GetExceptionResult(env, newEx, null).ToString()));
          await context.Response.WriteAsync(ExceptionHelper.GetExceptionSerializedResult(env, newEx, null));
          return;
        }

        logger.LogDebug("Refresh Token is valid, now check validate further and generate new one.");

        //Verify jwtToken.Subject claim / username against the suggested refresh token, and generate new refresh token
        //To clarify, a refresh token belongs to a specific user.  It also has an expiration.

        //var authResults = await tokenHelper.VerifyUserRefreshTokenAsync(jwToken.Subject, refreshToken, true);
        var authResults = await tokenHelper.VerifyUserRefreshTokenAsync(jwToken, refreshToken, true);

        if (authResults.ErrorOccurred) {
          if ((bool) authResults.IsRefreshTokenExpired) {
            defaultMsg = $"JWT has expired. Attempting to renew the JWT but the Refresh Token has expired: {authResults.ErrorMessage}";
            var newEx = new CustomUnauthorizedException(defaultMsg);

            logger.DebugGivingUp(defaultMsg);

            logger.LogError($"{ExceptionHelper.GetExceptionSerializedResult(env, newEx, null, true)}",
                            config,
                            StatusCodes.Status401Unauthorized);

            //Provide header indicating token expiration. This implies the JWT has expired as well.
            context.Response.Headers.Remove(HttpHeaderConstants.ExpIndicator);
            context.Response.Headers.Add(HttpHeaderConstants.ExpIndicator, "jwt,refresh");  //indicate that both the jwt and refresh tokens expired

            await context.Response.WriteAsync(ExceptionHelper.GetExceptionSerializedResult(env, newEx, null));

            return;
          } else {
            defaultMsg = $"JWT has expired. An error occurred while validating Refresh Token and generating new one: {authResults.ErrorMessage}";
            var newEx = new CustomUnauthorizedException(defaultMsg);

            logger.DebugGivingUp(defaultMsg);

            logger.LogError($"{ExceptionHelper.GetExceptionSerializedResult(env, newEx, null, true)}",
                            config,
                            StatusCodes.Status401Unauthorized);

            await context.Response.WriteAsync(ExceptionHelper.GetExceptionSerializedResult(env, newEx, null));

            return;
          }
        }

        //Generate new JWT since refresh token is good

        logger.LogDebug($"Generating new JWT for user {jwToken.Subject}.");

        var jwt = await tokenHelper.GenerateJWT(authResults.RefreshTokenNew.User, authResults.Claims, true);

        if (jwt == null) {
          defaultMsg = $"JWT has expired. An error occurred while generating new JWT for user {jwToken.Subject}.";
          var newEx = new CustomUnauthorizedException(defaultMsg);

          logger.DebugGivingUp(defaultMsg);

          logger.LogError($"{ExceptionHelper.GetExceptionSerializedResult(env, newEx, null, true)}",
                          config,
                          StatusCodes.Status401Unauthorized);

          await context.Response.WriteAsync(ExceptionHelper.GetExceptionSerializedResult(env, newEx, null));
          return;
        }

        logger.LogDebug($"Construct Auth header with new tokens.");

        //Send headers with new tokens - both new JWT and refresh tokens.
        //context.Response.Headers.Add("X-SSApi-Auth", JsonConvert.SerializeObject(allAuthResults));
        context.Response.Headers.Add(HttpHeaderConstants.JWT, new JwtSecurityTokenHandler().WriteToken(jwt));
        context.Response.Headers.Add(HttpHeaderConstants.Refresh, authResults.RefreshTokenNew.RefreshToken);
        context.Response.Headers.Add(HttpHeaderConstants.RefreshExp, authResults.RefreshTokenNew.DateExpires.ToString("r"));

        //Need a claims principal to convince pipeline that response is successfull and the controller should continue
        var ident = new ClaimsIdentity("Bearer");  //specifying auth type in constructor is critical to controller executing
        ident.AddClaims(authResults.Claims);
        var principal = new ClaimsPrincipal(ident);
        context.Principal = principal;
        context.Success();  //specify that all is well and proceed with rest of request pipeline - let controller action run

        logger.LogDebug("AuthenticationFailed SUCCESS");
      } catch (Exception ex) {
        defaultMsg = $"An error occurred while attempting to re-authenticate user {jwToken.Subject}.";
        var newEx = new CustomInternalServerException(defaultMsg, ex);

        logger.DebugGivingUp(defaultMsg);

        logger.LogError(ex, $"{ExceptionHelper.GetExceptionSerializedResult(env, newEx, null, true)}",
                        config,
                        StatusCodes.Status401Unauthorized);

        await context.Response.WriteAsync(ExceptionHelper.GetExceptionSerializedResult(env, newEx, null));
      }
    }
  }
}
