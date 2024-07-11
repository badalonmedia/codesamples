using System;
using System.Collections;
using Microsoft.VisualBasic;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;
using eDiscoverySupportClasses.eDiscoverySupportSpace;
using eDiscoveryAppPoolUnload;

namespace eDiscoveryAppPool {
public enum AppStatus {
  APP_BUSY = 0,
  APP_AVAILABLE = 1,
  APP_UNKNOWN = 2

}

public class AppPoolItem {
  private object m_objApplication;  //Application object
  private AppStatus m_eStatus;
  private string m_sClass;  //For CreateObject
  private string m_sProcessName;
  private int m_iProcessID;
  private DateTime m_dtCreateDate;
  private DateTime m_dtChangeDate;
  private string m_sAssembly;  //For reference only
  private int m_iKey;          //store the key used in the hashtable
  private int m_iCounter;      //Usage counter

  public AppPoolItem() {
    m_objApplication = null;
    m_eStatus = AppStatus.APP_UNKNOWN;
    m_sClass = "";
    m_sAssembly = "";
    m_iCounter = 0;
    m_iProcessID = ProcessManager.NON_PROCESS_ID;

    m_dtCreateDate = DateTime.MinValue;
    m_dtChangeDate = DateTime.MinValue;
  }

  public string ProcessName {
    get {
      return (m_sProcessName);
    }
    set {
      m_sProcessName = value;
    }
  }

  public int ProcessID {
    get {
      return (m_iProcessID);
    }
    set {
      m_iProcessID = value;
    }
  }

  public int Counter {
    get {
      return (m_iCounter);
    }
    set {
      m_iCounter = value;
    }
  }

  public DateTime CreateDate {
    get {
      return (m_dtCreateDate);
    }
    set {
      m_dtCreateDate = value;
    }
  }

  public DateTime ChangeDate {
    get {
      return (m_dtChangeDate);
    }
    set {
      m_dtChangeDate = value;
    }
  }

  public AppStatus Status {
    get {
      return (m_eStatus);
    }
    set {
      m_eStatus = value;
    }
  }

  public object Application {
    get {
      return (m_objApplication);
    }
    set {
      m_objApplication = value;
    }
  }

  public string Class {
    get {
      return (m_sClass);
    }
    set {
      m_sClass = value;
    }
  }

  public string Assembly {
    get {
      return (m_sAssembly);
    }
    set {
      m_sAssembly = value;
    }
  }

  public int Key {
    get {
      return (m_iKey);
    }
    set {
      m_iKey = value;
    }
  }
}

public class AppPool {
  private static bool m_bUseProcesses = true;  //NOTE: Kill switch for use of processes - in case of problems
  public const int APP_INVALID = -1;
  private static Hashtable m_hshAppPool = new Hashtable();
  private static bool m_bLocked = false;
  private static int m_iRestartInterval = 100;  //DEFAULT: Restart apps after n uses

  public static bool UseProcesses {
    get {
      return (m_bUseProcesses);
    }
    set {
      m_bUseProcesses = value;
    }
  }

  public static bool IsLocked() {
    return (m_bLocked);
  }

  public static void Lock() {
    m_bLocked = true;
  }

  public static void UnLock() {
    m_bLocked = false;
  }

  public static int RestartInterval {
    get {
      return (m_iRestartInterval);
    }
    set {
      m_iRestartInterval = value;
    }
  }

  //Start helper apps and store instances
  public static void Load(Hashtable hshHelperApps, Hashtable hshHelperAppPoolSize, Hashtable hshHelperAppProcesses) {
    int iKeyCounter = 0;
    string sTemp;
    int iInstances;
    string sProcessName;
    int iCount;
    ProcessManager objProcessMgr = new ProcessManager();

    foreach (DictionaryEntry objAppEntry in hshHelperApps) {
      //Console.WriteLine("***DEBUG: ENTRY...{0}", objAppEntry.Value.ToString().Trim());

      string sClass = objAppEntry.Value.ToString().Trim();

      if (sClass != "")  //Value attrib in app.config must be non-empty
      {
        //Console.WriteLine("APP HASH TABLE: {0}", sClass);
        //Console.WriteLine("APP HASH TABLE: {0}", objAppEntry.Key );

        sTemp = objAppEntry.Key.ToString().Trim();

        try {
          //Assembly key in both hash tables must be exactly the same
          if (hshHelperAppProcesses.ContainsKey(sTemp)) {
            Console.WriteLine("FOUND POOL KEY");

            sProcessName = hshHelperAppProcesses [sTemp]
                               .ToString()
                               .Trim();

            Console.WriteLine("FOUND POOL KEY: {0}", sProcessName);
          } else
            sProcessName = "";

        } catch {
          sProcessName = "";
        }

        //Check if this Assembly is in the hshHelperAppPoolSize hash table
        try {
          //Assembly key in both hash tables must be exactly the same
          if (hshHelperAppPoolSize.ContainsKey(sTemp)) {
            Console.WriteLine("FOUND POOL KEY");

            iInstances = Convert.ToInt32(hshHelperAppPoolSize[sTemp]);

            Console.WriteLine("FOUND POOL KEY: {0}", iInstances);
          } else
            iInstances = 1;

        } catch {
          iInstances = 1;  //Disregard entries in app pool hash table, if any
        }

        //Now create application object

        for (iCount = 0; iCount < iInstances; iCount++) {
          AppPoolItem objItem = new AppPoolItem();

          objItem.Application = null;          //place holder
          objItem.Class = sClass;              //save app class name
          objItem.ProcessName = sProcessName;  //save process name
          objItem.Assembly = sTemp.ToUpper();  //save app class name

          try {
            iKeyCounter++;

            if (sProcessName != "") {
              objProcessMgr.SaveStatePrior(sProcessName);
            }

            objItem.Application = Interaction.CreateObject(objItem.Class, "");

            if (sProcessName != "") {
              objProcessMgr.SaveStatePost(sProcessName);
            }

            //Console.WriteLine("GETDIFFERENCE LENGTH {0}: ", objProcessMgr.DifferenceCount());

            if (objProcessMgr.DifferenceCount() == 1) {
              //Only save the Process ID if it is the only new one since CreateObject called

              Console.WriteLine("PROCESS ID: {0}", objProcessMgr.BaseProcessID());
              objItem.ProcessID = objProcessMgr.BaseProcessID();
            }

            objItem.CreateDate = DateTime.Now;
            objItem.ChangeDate = DateTime.Now;
            objItem.Key = iKeyCounter;  //save key
            objItem.Status = AppStatus.APP_AVAILABLE;

            m_hshAppPool.Add(iKeyCounter, objItem);

            AppPoolUnload.HideApp(objItem.Application);  //v1.5 - new

            Application.DoEvents();
          } catch (Exception objEx) {
            Console.WriteLine("EXCEPTION: {0}", objEx.Message);

            //Problem launching application
            objItem.Application = null;
            objItem = null;
          }
        }
      }
    }

    objProcessMgr = null;
  }

  public static void Unload()  //Shutdown helper apps
  {
    if (m_hshAppPool.Count > 0) {
      AppPoolItem objItem;
      ArrayList lstRemove = new ArrayList();

      foreach (DictionaryEntry objAppEntry in m_hshAppPool) {
        objItem = (AppPoolItem) objAppEntry.Value;

        AppPoolUnload.Unload(objItem.Class, objItem.Application, objItem.ProcessID);  //Close the app

        //Console.WriteLine("ASSEMBLY: {0}", objItem.Assembly);

        //m_hshAppPool.Remove(objAppEntry.Key);	//v1.5 - for restarting the app pool
        lstRemove.Add(objAppEntry.Key);
      }

      foreach (object obj in lstRemove) {
        m_hshAppPool.Remove(obj);  //v1.5 - for restarting the app pool
      }

      lstRemove = null;
    }
  }

  public static int Reserve(string sAssembly) {
    //Revisit this - should I lock this array first??????
    AppPoolItem objItem;

    while (m_bLocked)  //Wait for application data to be released
      ;

    try {
      m_bLocked = true;  //Lock helper application data

      foreach (DictionaryEntry objAppEntry in m_hshAppPool) {
        objItem = (AppPoolItem) objAppEntry.Value;

        Console.WriteLine("RESERVE ASSEMBLY: {0}", objItem.Assembly.ToUpper());

        if (objItem.Application != null && objItem.Assembly.ToUpper() == sAssembly.Trim().ToUpper() && objItem.Status == AppStatus.APP_AVAILABLE) {
          objItem.Status = AppStatus.APP_BUSY;  //Reserve it
          objItem.Counter += 1;                 //Bump counter
          objItem.ChangeDate = DateTime.Now;
          m_bLocked = false;

          return ((int) objAppEntry.Key);
        }
      }
    } catch (Exception objEx) {
      m_bLocked = false;

      Console.WriteLine("RESERVE ERROR: {0}", objEx.Message);
    }

    m_bLocked = false;

    return (AppPool.APP_INVALID);  //no members of application pool are available
  }

  public static void Unreserve(int iKey) {
    try {
      if (m_hshAppPool.ContainsKey(iKey)) {
        AppPoolItem objItem = (AppPoolItem) m_hshAppPool[iKey];

        while (m_bLocked)
          ;

        m_bLocked = true;

        objItem.Status = AppStatus.APP_AVAILABLE;  //Release it
        objItem.ChangeDate = DateTime.Now;

        Console.WriteLine("UNRESERVED: {0}", iKey);

        //Now check to see if this application should be restarted/recycled

        if (objItem.Counter % AppPool.RestartInterval < 1 && m_bUseProcesses) {
          ProcessManager objProcessMgr = new ProcessManager();

          //Restart this helper application
          //ConverterCon.Write("{0}> ", DateTime.Now);
          //ConverterCon.WriteLine("CONVERTER SERVER: RESTARTING APPLICATION {0}({1})...", objItem.Class, objItem.Key);

          AppPoolUnload.Unload(objItem.Class, objItem.Application, objItem.ProcessID);

          Thread.Sleep(2000);

          objItem.Application = null;

          if (objItem.ProcessName != "") {
            objProcessMgr.SaveStatePrior(objItem.ProcessName);
          }

          objItem.Application = Interaction.CreateObject(objItem.Class, "");

          if (objItem.ProcessName != "") {
            objProcessMgr.SaveStatePost(objItem.ProcessName);
          }

          //Console.WriteLine("GETDIFFERENCE LENGTH {0}: ", objProcessMgr.DifferenceCount());

          if (objProcessMgr.DifferenceCount() == 1) {
            //Only save the Process ID if it is the only new one since CreateObject called

            Console.WriteLine("PROCESS ID: {0}", objProcessMgr.BaseProcessID());
            objItem.ProcessID = objProcessMgr.BaseProcessID();
          }

          objItem.CreateDate = DateTime.Now;
          objItem.ChangeDate = DateTime.Now;
          objItem.Counter = 0;

          AppPool.ShowAppPool();
        }

        //AppPoolUnload.Unload(objItem.Class, objItem.Application);	//DEBUGGING: TEST

        m_bLocked = false;
      }

    } catch (Exception objEx) {
      m_bLocked = false;

      Console.WriteLine("APPPOOL: UNRESERVE: EXCEPTION: {0}", objEx.Message);
    }
  }

  public static object GetAppObject(int iKey) {
    if (m_hshAppPool.ContainsKey(iKey))
      return (((AppPoolItem) m_hshAppPool[iKey]).Application);
    else
      return (null);
  }

  public static void ShowAppPool()  //For debugging
  {
    if (m_hshAppPool.Count > 0) {
      AppPoolItem objItem;

      foreach (DictionaryEntry objAppEntry in m_hshAppPool) {
        objItem = (AppPoolItem) objAppEntry.Value;

        Console.WriteLine("ASSEMBLY: {0}", objItem.Assembly);
        //Console.WriteLine("ASSEMBLY: {0}", objItem.Assembly);
      }
    }
  }

  public static Hashtable GetAppPool()  //Not sure I like this property
  {
    return (m_hshAppPool);
  }
}
}
