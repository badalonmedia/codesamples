using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using GenAPI.OrmModels;
using GenAPI.Models;
using GenAPI.Cryptography;

namespace GenAPI.Components.Authentication {
  /// <summary>
  /// Helper code for token management and authentication.
  /// </summary>
  public class TokenHelper : IDisposable {
    private readonly IWebHostEnvironment _env;
    private readonly IConfigurationModel _config;
    private readonly ILogger _logger;
    public TokenHelper(IWebHostEnvironment env, IConfigurationModel config, ILogger logger) {
      _env = env;
      _config = config;
      _logger = logger;
    }

    /// <summary>
    /// Validate refresh token.
    /// </summary>
    /// <param name="refreshToken"></param>
    /// <returns>True/False</returns>
    public bool IsValidRefreshToken(string refreshToken) {
      return !String.IsNullOrWhiteSpace(refreshToken) && refreshToken.Length == _config.Jwt.RefreshTokenLength;
    }

    /// <summary>
    /// Generate refresh token value - Currently as a cryptographically secure random string
    /// </summary>
    /// <returns>RT value</returns>
    private string GenerateRefreshTokenValue() {
      return CryptoHelper.GetCryptoRandomString(_config.Jwt.RefreshTokenLength);
    }

    /// <summary>
    /// Generate new refresh token.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="verifiedUser"></param>
    /// <returns>Refresh Token</returns>
    private UserRefreshToken GenerateRefreshToken(GenAPIContext dbContext, User verifiedUser) {
      var refreshTokenNew = GenerateRefreshTokenShared(dbContext, verifiedUser);
      return refreshTokenNew;
    }

    /// <summary>
    /// Generate refresh token, but delete existing.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="refreshTokenCurrent"></param>
    /// <returns>Refresh Token</returns>
    private async Task<UserRefreshToken> GenerateRefreshToken(GenAPIContext dbContext, UserRefreshToken refreshTokenCurrent) {
      //var refreshTokens = dbContext.UserRefreshTokens.Where(r => r.UserId == refreshTokenCurrent.UserId && r.RefreshToken == refreshTokenCurrent.RefreshToken);
      var refreshTokens = await dbContext.UserRefreshTokens.FirstOrDefaultAsync(r => r.UserId == refreshTokenCurrent.UserId && r.RefreshToken == refreshTokenCurrent.RefreshToken);
      dbContext.UserRefreshTokens.RemoveRange(refreshTokens);  //delete the prior refresh token for this user
      var refreshTokenNew = GenerateRefreshTokenShared(dbContext, refreshTokenCurrent.User);
      return refreshTokenNew;
    }

    /// <summary>
    /// Private helper code for refresh token generation.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="verifiedUser"></param>
    /// <returns></returns>
    private UserRefreshToken GenerateRefreshTokenShared(GenAPIContext dbContext, User verifiedUser) {
      var token = new UserRefreshToken(){
          DateAdded = DateTime.UtcNow,
          RefreshToken = GenerateRefreshTokenValue(),
          UserId = verifiedUser.UserId,
          DateExpires = DateTime.UtcNow.AddSeconds(_config.Jwt.RefreshTokenExpiresSeconds)};

      verifiedUser.UserRefreshTokens.Add(token);
      return token;
    }

    /// <summary>
    /// Create claims incorporated into JWT.
    /// </summary>
    /// <param name="verifiedUser"></param>
    /// <param name="submittedUser"></param>
    /// <returns>Array of Claims</returns>
    private Claim[] GenerateClaims(User verifiedUser, UserAuthParamsModel submittedUser) {
      var claims = new List<Claim>();

      claims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));
      claims.Add(new Claim(JwtRegisteredClaimNames.Iat, DateTime.UtcNow.ToString()));
      //claims.Add(new Claim(JwtRegisteredClaimNames.Sub, verifiedUser.UserName));
      claims.Add(new Claim(JwtRegisteredClaimNames.Sub, verifiedUser.UserId.ToString()));
      claims.Add(new Claim(ClaimTypes.NameIdentifier, verifiedUser.UserId.ToString()));
      claims.Add(new Claim(ClaimTypes.Name, verifiedUser.UserName));
      claims.Add(new Claim(ClaimTypes.UserData, verifiedUser.UserDesc));
      claims.Add(new Claim(ClaimTypes.Role, verifiedUser.UserRole.UserRoleName));

      //custom claim containing business user name (email address))
      if (!string.IsNullOrWhiteSpace(submittedUser?.BusinessUserName)) {
        claims.Add(new Claim(_config.Jwt.BusinessUserNameClaimType, submittedUser.BusinessUserName));
      }

      return claims.ToArray();
    }

    /// <summary>
    /// Create claims incorporated into JWT, do  not provide BusinessUserName.
    /// </summary>
    /// <param name="verifiedUser"></param>
    /// <returns>Array of Claims</returns>
    private Claim[] GenerateClaims(User verifiedUser) {
      return GenerateClaims(verifiedUser, null);
    }

    /// <summary>
    /// Reads a specific claim from a claims collection.
    /// </summary>
    /// <param name="claims"></param>
    /// <param name="claimType"></param>
    /// <returns>Claim</returns>
    static public string ReadClaim(IEnumerable<Claim> claims, string claimType) {
            string claimValue = (claims?.FirstOrDefault<Claim>(c => c.Type == claimType)?.Value ?? string.Empty);
            return claimValue;
    }

    /// <summary>
    /// Generate the JWT and log the request.
    /// </summary>
    /// <param name="user"></param>
    /// <param name="claims"></param>
    /// <param name="logIt"></param>
    /// <returns>JWT</returns>
    public async Task<JwtSecurityToken> GenerateJWT(User user, Claim[] claims, bool logIt) {
      using var bizLayer = new BL(null, _env, _config, _logger);
      string jwtKeyText = await bizLayer.GetJwtKeyAsync();
      var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKeyText));
      var signIn = new SigningCredentials(key, SecurityAlgorithms.HmacSha512);  //was 256

      var jwt = new JwtSecurityToken(_config.Jwt.Issuer,
                                     _config.Jwt.Audience,
                                     claims,
                                     expires
                                     : DateTime.UtcNow.AddSeconds(_config.Jwt.ExpiresSeconds),
                                       signingCredentials
                                     : signIn);

      string userName = TokenHelper.ReadClaim(claims, ClaimTypes.Name);

      if (logIt) {
        int apiUserId = Convert.ToInt32(TokenHelper.ReadClaim(claims, JwtRegisteredClaimNames.Sub));
        await bizLayer.AddTokenRequestToLog(apiUserId);
      }

      _logger.LogDebug($"Token generated for user: {userName}");

      return jwt;
    }

    /// <summary>
    /// Authenticate user and generate tokens.
    /// </summary>
    /// <param name="submittedUser"></param>
    /// <returns>AuthenticationResults instance</returns>
    public async Task<AuthenticationResults> AuthenticateUserAsync(UserAuthParamsModel submittedUser) {
      using var dbContext = new GenAPIContext();

      var authResults = new AuthenticationResults{
          ErrorMessage = string.Empty,
          Claims = null,
          RefreshTokenNew = null,
          ErrorOccurred = true,
          IsRefreshTokenExpired = null};

      var verifiedUser = await dbContext.Users.Include(u => u.UserRole).Where(u => u.UserName == submittedUser.UserName && u.Enabled).FirstOrDefaultAsync();

      if (verifiedUser == null) {
        authResults.ErrorMessage = $"Could not find an active user with username: {submittedUser.UserName}.";
        return (authResults);
      }

      //validate the hashed pwd

      bool hashValid = CryptoHelper.IsClearPwdValid(submittedUser.Password, verifiedUser.UserSalt, verifiedUser.Password);

      if (!hashValid) {
        authResults.ErrorMessage = $"An error occurred while authenticating user {submittedUser.UserName}.";
        return authResults;
      }

      //generate and store one-time-use refresh token and delete any existing ones first

      authResults.Claims = GenerateClaims(verifiedUser, submittedUser);
      authResults.RefreshTokenNew = GenerateRefreshToken(dbContext, verifiedUser);
      await dbContext.SaveChangesAsync();

      if (authResults.RefreshTokenNew == null) {
        authResults.ErrorMessage = $"An error occurred while generating new Refresh Token for user {submittedUser.UserName}.";
        return (authResults);
      }

      authResults.ErrorOccurred = false;

      return authResults;
    }

    /// <summary>
    /// Re-authenticate user via refresh token.
    /// </summary>
    /// <param name="jwt"></param>
    /// <param name="refreshTokenCurrentValue"></param>
    /// <param name="getNewRefreshToken"></param>
    /// <returns>AuthenticationResults instance</returns>
    public async Task<AuthenticationResults> VerifyUserRefreshTokenAsync(JwtSecurityToken jwt, string refreshTokenCurrentValue, bool getNewRefreshToken) {
      using var dbContext = new GenAPIContext();

      var authResults = new AuthenticationResults{
          ErrorMessage = string.Empty,
          Claims = null,
          RefreshTokenNew = null,
          ErrorOccurred = true,
          IsRefreshTokenExpired = false};

      int apiUserId = Convert.ToInt32(jwt.Subject);
      string userName = ReadClaim(jwt.Claims, ClaimTypes.Name);

      var refreshTokenCurrent =
          await dbContext.UserRefreshTokens.Include(t => t.User.UserRole).FirstOrDefaultAsync(t => t.RefreshToken == refreshTokenCurrentValue && t.User.UserId == apiUserId && t.User.Enabled);

      if (refreshTokenCurrent == null)  //could not find user with specified refresh token
      {
        authResults.ErrorMessage = $"Could not find Refresh Token ({refreshTokenCurrentValue}) that matches an active user with username: {userName}.";
        return authResults;
      }

      //refresh token is valid and belongs to a user, but check if expired

      if (refreshTokenCurrent.DateExpires <= DateTime.UtcNow) {
        authResults.ErrorMessage = $"Refresh Token ({refreshTokenCurrentValue}) for user {userName} has expired.";
        authResults.IsRefreshTokenExpired = true;
        return authResults;
      }

      if (getNewRefreshToken) {
        await dbContext.Entry(refreshTokenCurrent).Reference(t => t.User).LoadAsync();

        //Generate and store one-time use refresh token
        authResults.Claims = GenerateClaims(refreshTokenCurrent.User);
        authResults.RefreshTokenNew = await GenerateRefreshToken(dbContext, refreshTokenCurrent);
        await dbContext.SaveChangesAsync();

        if (authResults.RefreshTokenNew == null) {
          authResults.ErrorMessage = $"An error occurred while generating new Refresh Token for user {userName}.";
          return authResults;
        }

        authResults.ErrorOccurred = false;
        return authResults;
      }

      authResults.ErrorOccurred = false;
      return authResults;
    }

#region Deprecated

    /// <summary>
    /// Given a jwt string and secret key, parse and return all claims.
    /// </summary>
    /// <param name="jwt"></param>
    /// <param name="jwtKey"></param>
    /// <param name="withValidation"></param>
    /// <returns>The claims</returns>
    public(string validationResult, Dictionary<string, string>) ReadClaims(string jwt, string jwtKey, bool withValidation) {
      try {
        SecurityToken validatedToken;
        var validationParameters = new TokenValidationParameters();

        validationParameters.ValidateLifetime = withValidation;
        validationParameters.ValidAudience = _config.Jwt.Audience;
        validationParameters.ValidIssuer = _config.Jwt.Issuer;  //endpoint
        validationParameters.IssuerSigningKey =
            new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

        var principal = new JwtSecurityTokenHandler().ValidateToken(jwt, validationParameters, out validatedToken);

        //if the preceding assignment didn't throw an exception, the jwt is valid and we should be able
        //to proceed with reading the claims.

        var claims = new Dictionary<string, string>();

        foreach (var claim in principal.Claims) {
          if (!claims.ContainsKey(claim.Type)) {
            claims.Add(claim.Type, claim.Value);
          }
        }

        return (String.Empty, claims);

        //Regarding the various catch blocks below, at the moment, I'm simply returning either "Error" or "Success" with an
        //appropriate message.  At some point I may leverage the various catch blocks to take further specific action.
      } catch (SecurityTokenInvalidAudienceException) {
        //invalid audience
        return ("Invalid JWT Audience", null);
      } catch (SecurityTokenInvalidIssuerException) {
        //invalid issuer
        return ("Invalid JWT Issuer", null);
      } catch (SecurityTokenExpiredException) {
        //jwt has expired
        return ("JWT has expired", null);
      } catch (SecurityTokenInvalidLifetimeException) {
        return ("JWT has expired", null);
      } catch (SecurityTokenInvalidSignatureException) {
        return ("Invalid JWT Signature", null);
      } catch (SecurityTokenInvalidSigningKeyException) {
        //key is invalid
        return ("Invalid JWT Signing Key", null);
      } catch (ArgumentException) {
        //likely a malformed or invalid jwt
        return ("Invalid JWT", null);
      } catch (Exception) {
        //some other error occurred while validating the token
        return ("An error occurred while parsing the JWT", null);
      }
    }

#endregion

#region IDisposable Stuff
    private bool isDisposed;

    public void Dispose() {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) {
      if (isDisposed)
        return;

      if (disposing) {
        // free managed resources
      }

      // free native resources if there are any.
      isDisposed = true;
    }

    ~TokenHelper() {
      Dispose(false);
    }

#endregion
  }
}
