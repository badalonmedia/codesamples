using System;
using System.Security.Cryptography;
using System.IO;
using System.Text;

namespace eDiscoveryMD5 {
public class eDiscoveryComputeMD5 {
  private static MD5CryptoServiceProvider m_objMD5 = new MD5CryptoServiceProvider();

  private static Byte[] GetSampleData_File(string sPath, int iSampleBytes) {
    int iNumBytes;
    FileStream objStream;
    Byte[] btSample = new Byte[iSampleBytes];  //Sample data

    try {
      objStream = new FileStream(sPath, FileMode.Open);
    } catch {
      objStream = null;
    }

    if (objStream != null) {
      BinaryReader objReader = new BinaryReader(objStream);

      iNumBytes = objReader.Read(btSample, 0, iSampleBytes);

      objReader.Close();
      objStream.Close();

      objReader = null;
      objStream = null;

      return (btSample);
    } else
      return (null);

    //Console.WriteLine("CHARS READ {0}" , iCharsRead);
  }

  public static byte[] ComputeHashFromStream(FileStream objStream) {
    try {
      if (objStream != null) {
        if (objStream.CanRead)
          return (m_objMD5.ComputeHash(objStream));
        else
          return (null);
      } else
        return (null);
    } catch (Exception objEx) {
      Console.WriteLine("MD5 COMPUTEFROMSTREAM: ERROR: {0}", objEx.Message);

      return (null);
    }
  }

  public static byte[] ComputeHashFromFile(string sPath) {
    try {
      //Console.WriteLine("COMPUTEFROMFILE: 1");

      if (sPath != string.Empty) {
        //Console.WriteLine("COMPUTEFROMFILE: 2");

        FileStream objStream = new FileStream(sPath, FileMode.Open);  //Source stream
        byte[] btResult = m_objMD5.ComputeHash(objStream);
        objStream.Close();
        objStream = null;

        //Console.WriteLine("COMPUTEFROMFILE: {0}", btTemp.GetLength(0));

        return (btResult);
      } else
        return (null);
    } catch (Exception objEx) {
      Console.WriteLine("MD5 COMPUTEFROMFILE: ERROR: {0}", objEx.Message);

      return (null);
    }
  }

  public static byte[] ComputeHashFromFilePartial(string sPath, int iSampleBytes) {
    try {
      if (sPath != string.Empty) {
        byte[] btResult;
        byte[] btSample = GetSampleData_File(sPath, iSampleBytes);

        if (btSample != null) {
          if (btSample.GetLength(0) > 0) {
            btResult = m_objMD5.ComputeHash(btSample);

            return (btResult);
          } else
            return (null);
        } else
          return (null);
      } else
        return (null);
    } catch (Exception objEx) {
      Console.WriteLine("MD5 COMPUTEFROMFILEPARTIAL: ERROR: {0}", objEx.Message);

      return (null);
    }
  }

  public static byte[] ComputeHashFromText(string sSource) {
    try {
      if (sSource != string.Empty)  //compute hash on string
        return (m_objMD5.ComputeHash(Encoding.ASCII.GetBytes(sSource)));
      else
        return (null);
    } catch (Exception objEx) {
      Console.WriteLine("MD5 COMPUTEFROMTEXT: ERROR: {0}", objEx.Message);

      return (null);
    }
  }

  public static string ConvertToString(byte[] btBytes) {
    StringBuilder sMD5 = new StringBuilder("");

    for (int iCount = 0; iCount < btBytes.Length; iCount++)
      sMD5.Append(btBytes [iCount]
                      .ToString("x2")
                      .ToUpper());

    return (sMD5.ToString());
  }

  public static int HashSize()  //Debugging
  {
    return (m_objMD5.HashSize);
  }
}
}
