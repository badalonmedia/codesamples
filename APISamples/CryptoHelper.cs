using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace GenAPI.Cryptography {
  /// <summary>
  /// Cryptography helper code.
  /// </summary>
  public class CryptoHelper {
    /// <summary>
    /// Get cryptographically strong string of random characters, without specifying the permitted characters.
    /// </summary>
    /// <param name="length"></param>
    /// <returns>the random string</returns>
    static public string GetCryptoRandomString(int length) {
      return GetCryptoRandomString(length, "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890-_");
    }

    /// <summary>
    /// Get cryptographically strong string of random characters.  The permitted characters are specified.
    /// </summary>
    /// <param name="length"></param>
    /// <param name="allowedChars"></param>
    /// <returns>the random string</returns>
    static public string GetCryptoRandomString(int length, string allowedChars) {
      using var crypto = new RNGCryptoServiceProvider();

      var bytes = new byte[length];
      byte[] rndBuffer = null;
      int maxRandomNumber = byte.MaxValue - ((byte.MaxValue + 1) % allowedChars.Length);
      var result = new char[length];

      crypto.GetBytes(bytes);

      for (int charIndex = 0; charIndex < length; charIndex++) {
        byte value = bytes[charIndex];

        while (value > maxRandomNumber) {
          if (rndBuffer == null) {
            rndBuffer = new byte[1];
          }

          crypto.GetBytes(rndBuffer);
          value = rndBuffer[0];
        }

        result[charIndex] = allowedChars[value % allowedChars.Length];
      }

      return new string(result);
    }

    /// <summary>
    /// Generate hashed password using salt.
    /// </summary>
    /// <param name="clearPwd"></param>
    /// <param name="saltBase64"></param>
    /// <returns>Hashed pwd and Salt</returns>
    static private(string HashedPwd, string SaltBase64) GenerateHashedPwdInternal(string clearPwd, string saltBase64) {
      byte[] salt;

      if (String.IsNullOrEmpty(saltBase64)) {
        salt = new byte[512 / 8];
        using(var rng = RandomNumberGenerator.Create()) {
          rng.GetBytes(salt);
        }

        saltBase64 = Convert.ToBase64String(salt);
      } else {
        salt = Convert.FromBase64String(saltBase64);
      }

      //create 512 hash using HMACSHA512 with 10,000 iterations
      string hashedPwd = Convert.ToBase64String(KeyDerivation.Pbkdf2(
          password
          : saltBase64 + clearPwd,  //prepended salt
            salt
          : salt,
            prf
          : KeyDerivationPrf.HMACSHA512,
            iterationCount : 10000,
            numBytesRequested : 512 / 8));

      return (hashedPwd, saltBase64);
    }

    /// <summary>
    /// Generate hashed password using salt that will be generated.
    /// </summary>
    /// <param name="clearPwd"></param>
    /// <returns>Hashed pwd and Salt</returns>
    static public(string HashedPwd, string SaltBase64) GenerateHashedPwd(string clearPwd) {
      return GenerateHashedPwdInternal(clearPwd, null);
    }

    /// <summary>
    /// Generate hashed password using salt that will be provided.
    /// </summary>
    /// <param name="clearPwd"></param>
    /// <param name="saltBase64"></param>
    /// <returns>Hashed pwd and Salt</returns>
    static public(string HashedPwd, string SaltBase64) GenerateHashedPwd(string clearPwd, string saltBase64) {
      return GenerateHashedPwdInternal(clearPwd, saltBase64);
    }

    /// <summary>
    /// Validate clear pwd against it's hashed equivalent.
    /// </summary>
    /// <param name="clearPwd"></param>
    /// <param name="saltBase64"></param>
    /// <param name="hashedPwd"></param>
    /// <returns>Validation result</returns>
    public static bool IsClearPwdValid(string clearPwd, string saltBase64, string hashedPwd) {
      (string HashedPwd, string SaltBase64) hashInfo = GenerateHashedPwdInternal(clearPwd, saltBase64);

      return GenerateHashedPwdInternal(clearPwd, saltBase64).HashedPwd == hashedPwd;
    }
  }
}
