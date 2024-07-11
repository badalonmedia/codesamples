using System;
using System.Xml;
using System.Drawing;
using System.Collections;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ModalMasher {
public class ModalMasher {
  private static string m_sCodeName;                                //For debugging
  private static int m_iScanInterval;                               //Time between dialog scans - in milliseconds
  private static int m_iWaitInterval1Default;                       //Default time to wait for user intervention for dialog - in milliseconds
  private static int m_iWaitIntervalNDefault;                       //Default time to wait for user intervention for dialog - in milliseconds
  private static Hashtable m_hshGeneralConfig = new Hashtable();    //General config options
  private static Hashtable m_hshModalsReference = new Hashtable();  //Config for processes
  private static Hashtable m_hshModalsReference_ThisProcess;        //For specific process only - used by callback
  //private static Process m_objThisProcess;
  //private static ProcessThread m_objThisThread;

  private static Hashtable m_hshModalsCurrent = new Hashtable();  //Config for modals currently open

  private static bool m_bIsLocked = false;
  private static TimerCallback m_tmrDelegate = new TimerCallback(ScanTimerAction);
  private static Object m_objState = null;
  private static System.Threading.Timer m_tmrScanTimer = new System.Threading.Timer(m_tmrDelegate, m_objState, Timeout.Infinite, Timeout.Infinite);
  private static bool m_bIsActivated = false;
  private static bool m_bIsPaused = false;
  private static bool m_bLogging = false;
  private static StreamWriter m_objLog = null;

  private static NotifyIcon m_objTray;
  private static Icon m_objIconMain;
  private static Icon m_objIconScanning;
  private static Icon m_objIconPaused;

  private static int m_iScanIntervalStartup;

  private const string LOG_PATH = "LOG";
  private const int MAX_CAPTION_LENGTH = 256;
  //private const int SCAN_INTERVAL_STARTUP = 5000;		//How long to wait before timer starts

  private const int SCAN_INTERVAL_DEFAULT = 3;           //Default if config file error
  private const int WAIT_FOR_ACTION_TIME1_DEFAULT = 10;  //Default if config file error
  private const int WAIT_FOR_ACTION_TIMEN_DEFAULT = 5;   //Default if config file error
  private const int KEY_CODE_DEFAULT = 27;               //Default if config file error

  public static int ScanIntervalStartup {
    get {
      return m_iScanIntervalStartup;
    }

    set {
      m_iScanIntervalStartup = value;
    }
  }

  public static NotifyIcon Tray {
    get {
      return m_objTray;
    }

    set {
      m_objTray = value;
    }
  }

  public static Icon IconPaused {
    get {
      return m_objIconPaused;
    }

    set {
      m_objIconPaused = value;
    }
  }

  public static Icon IconMain {
    get {
      return m_objIconMain;
    }

    set {
      m_objIconMain = value;
    }
  }

  public static Icon IconScanning {
    get {
      return m_objIconScanning;
    }

    set {
      m_objIconScanning = value;
    }
  }

  private static string ItemFix(string sItem, int iTargetLen, char cPad) {
    int iLen = sItem.Length;
    string sResult = sItem;
    int iCount;
    string sPadAll = "";
    string sPadChar = cPad.ToString();

    if (iLen < iTargetLen) {
      for (iCount = 0; iCount < iTargetLen - iLen; iCount++)  //Construct left padding
        sPadAll += sPadChar;                                  //Must be a better way?!

      sResult = sPadAll + sResult;
    }

    return (sResult);
  }

  private static StreamWriter StartLogFile()  //, StreamWriter objStream)
  {
    DateTime dtNow = DateTime.Now;
    string sFile;
    string sLogPath;

    //Construct log file name
    sFile = "EDDDIALOG_";
    sFile += ItemFix(dtNow.Year.ToString(), 4, '0');   //Year
    sFile += ItemFix(dtNow.Month.ToString(), 2, '0');  //Month
    sFile += ItemFix(dtNow.Day.ToString(), 2, '0');    //Day
    sFile += ".";
    sFile += ItemFix(dtNow.Hour.ToString(), 2, '0');    //Hour
    sFile += ItemFix(dtNow.Minute.ToString(), 2, '0');  //Minute
    sFile += ItemFix(dtNow.Second.ToString(), 2, '0');  //Seconds

    sFile += ".LOG";  //Hardcode extension

    //Console.WriteLine("LOG FILE NAME: {0}", sFile);
    //Console.WriteLine("APP PATH: {0}", Path.GetDirectoryName(Application.ExecutablePath));

    sLogPath = Path.GetDirectoryName(Application.ExecutablePath) + "\\" + LOG_PATH;

    if (!Directory.Exists(sLogPath))
      Directory.CreateDirectory(sLogPath);  //Create Log directory

    return (new StreamWriter(sLogPath + "\\" + sFile, true, Encoding.ASCII));
  }

  public static void AppendLog(string sText) {
    try {
      m_objLog.WriteLine("{0}", sText);
      m_objLog.Flush();
    } catch {
      Console.WriteLine("ERROR WRITING TO LOG FILE");
    }
  }

  public static bool IsActivated() {
    return (m_bIsActivated);
  }

  public static bool IsPaused() {
    return (m_bIsPaused);
  }

  public static void WaitLocked() {
    do {
    } while (m_bIsLocked);
  }

  public static void Activate(bool bLogging, string sConfigPath) {
    m_tmrScanTimer.Change(Timeout.Infinite, Timeout.Infinite);

    try {
      LoadUserConfig(sConfigPath);
    } catch {
      MessageBox.Show("An error occurred while reading the user configuration information.  Dialog Suppression will not be activated.", "Error");
      return;  //Get out
    }

    m_bIsActivated = true;

    m_bLogging = bLogging;  //Enable logging as well

    if (m_bLogging) {
      m_objLog = StartLogFile();

      //Start log file output
      StringBuilder objBuffer = new StringBuilder();
      StringWriter objString = new StringWriter(objBuffer);
      objString.Write("{0}: Start of Log File", DateTime.Now);
      ModalMasher.AppendLog(objString.ToString());
      objBuffer = null;

      WriteUserConfigToLog();
    }

    m_tmrScanTimer.Change(m_iScanIntervalStartup, m_iScanInterval);
  }

  public static void Resume() {
    m_tmrScanTimer.Change(m_iScanInterval, m_iScanInterval);

    m_bIsPaused = false;

    if (m_bLogging)
      ModalMasher.AppendLog(DateTime.Now + ": Resumed");

    m_objTray.Icon = m_objIconMain;
  }

  public static void Pause() {
    m_tmrScanTimer.Change(Timeout.Infinite, Timeout.Infinite);

    m_bIsPaused = true;

    if (m_bLogging)
      ModalMasher.AppendLog(DateTime.Now + ": Paused");

    m_objTray.Icon = m_objIconPaused;
  }

  public static void Deactivate() {
    m_tmrScanTimer.Change(Timeout.Infinite, Timeout.Infinite);

    m_bIsActivated = false;

    if (m_bLogging) {
      m_objLog.Flush();
      m_objLog.Close();

      m_objLog = null;
    }

    m_tmrScanTimer.Dispose();
    m_tmrDelegate = null;

    //Add other cleanup code?

    try {
      m_hshGeneralConfig.Clear();
    } catch {
    }

    m_hshGeneralConfig = null;

    try {
      m_hshModalsReference.Clear();
    } catch {
    }

    m_hshModalsReference = null;
  }

  private static void ScanTimerAction(Object objState) {
    //Scanning processes/threads

    if (!ModalMasher.IsLocked()) {
      m_objTray.Icon = m_objIconScanning;
      Lock(true);  //Lock data
      ScanForNewDialogs();
      ScanExistingDialogs();
      Unlock();  //Release
      m_objTray.Icon = m_objIconMain;
    }
  }

  public static void Lock(bool bWait) {
    if (bWait) {
      do {
      } while (m_bIsLocked);
    }

    m_bIsLocked = true;
  }

  public static void Unlock() {
    m_bIsLocked = false;
  }

  public static bool IsLocked() {
    return (m_bIsLocked);
  }

  public static int ScanInterval {
    get {
      return m_iScanInterval;
    }

    set {
      m_iScanInterval = value;
    }
  }

  public static int WaitInterval1Default {
    get {
      return m_iWaitInterval1Default;
    }

    set {
      m_iWaitInterval1Default = value;
    }
  }

  public static int WaitIntervalNDefault {
    get {
      return m_iWaitIntervalNDefault;
    }

    set {
      m_iWaitIntervalNDefault = value;
    }
  }

  private static void WriteUserConfigToLog() {
    Hashtable objDialogHash;

    ModalMasher.AppendLog("");
    ModalMasher.AppendLog("Config File Settings...");

    foreach (DictionaryEntry objItem in m_hshModalsReference) {
      //Each key is process name and value is another hash table containing dialog list

      ModalMasher.AppendLog("PROCESS: " + objItem.Key.ToString());

      objDialogHash = (Hashtable) objItem.Value;

      foreach (DictionaryEntry objItem2 in objDialogHash) {
        ModalMasher.AppendLog(" DIALOG: " + objItem2.Key.ToString());
        ModalMasher.AppendLog("  CAPTION: " + ((ModalReferenceEntry) objItem2.Value).Caption);
        ModalMasher.AppendLog("  KEYCODE: " + ((ModalReferenceEntry) objItem2.Value).KeyCode);
        ModalMasher.AppendLog("  WAIT1: " + ((ModalReferenceEntry) objItem2.Value).WaitForActionTime1);
        ModalMasher.AppendLog("  WAITN: " + ((ModalReferenceEntry) objItem2.Value).WaitForActionTimeN);
      }
    }
  }

  private static void ConfigError(string sCodeName, string sParam, string sValue) {
    MessageBox.Show(sCodeName + ": An error occurred while reading " + sParam + " from config file. Default of " + sValue + " is being used.");
  }

  public static void LoadUserConfig(string sConfigPath) {
    m_sCodeName = "LoadUserConfig";

    XmlTextReader objReader;
    string sElement = "";
    Hashtable hshDialogHash = new Hashtable();
    ModalReferenceEntry objModalRef = new ModalReferenceEntry();
    string sProcessName = "";
    string sModalCaption = "";

    objReader = new XmlTextReader(sConfigPath);
    objReader.WhitespaceHandling = WhitespaceHandling.None;

    while (objReader.Read())  //Traverse XML file
    {
      switch (objReader.NodeType) {
        case XmlNodeType.Element:
          sElement = objReader.Name;

          //Console.WriteLine("ELEMENT NAME: {0}", objReader.Name);
          //Console.WriteLine("ELEMENT VALUE: {0}", objReader.Value);
          //Console.WriteLine("DEPTH: {0}", objReader.Depth);

          //Console.WriteLine("ATTRIB NAME: {0}", "name");
          //Console.WriteLine("ATTRIB VALUE: {0}", sValue);

          if (sElement == "process")  //Start of "Process" node
          {
            hshDialogHash = new Hashtable();                //Re-init Dialog hashtable
            sProcessName = objReader.GetAttribute("name");  //Save process name attrib

          } else if (sElement == "dialog")  //Start of "Dialog" node
          {
            objModalRef = new ModalReferenceEntry();
            sModalCaption = objReader.GetAttribute("caption");  //Save dialog caption attrib
            objModalRef.Caption = sModalCaption;
          }

          break;

        case XmlNodeType.EndElement:
          sElement = objReader.Name;

          //Console.WriteLine("END ELEMENT NAME: {0}", objReader.Name);
          //Console.WriteLine("END ELEMENT VALUE: {0}", objReader.Value);

          if (sElement == "process")  //Entry for "Process"
          {
            m_hshModalsReference.Add(sProcessName, hshDialogHash);

          } else if (sElement == "dialog")  //Entry for "Dialog"
          {
            hshDialogHash.Add(sModalCaption, objModalRef);

            //Console.WriteLine("REF: TIME1: {0}", objModalRef.WaitForActionTime1);
            //Console.WriteLine("REF: TIMEN: {0}", objModalRef.WaitForActionTimeN);
            //Console.WriteLine("REF: KEYCODE: {0}", objModalRef.KeyCode);
            //Console.WriteLine("REF: CAPTION: {0}", objModalRef.Caption);
          }

          break;

        case XmlNodeType.Text:
          //Console.WriteLine("TEXT NAME: {0}", objReader.Name);
          //Console.WriteLine("TEXT VALUE: {0}", objReader.Value);

          if (sElement == "scanIntervalTime") {
            try {
              m_hshGeneralConfig.Add(sElement, objReader.Value.ToString());  //Add section to hash table
            } catch {
              m_hshGeneralConfig.Add(sElement, SCAN_INTERVAL_DEFAULT.ToString());  //Add section to hash table
              ConfigError(m_sCodeName, sElement, SCAN_INTERVAL_DEFAULT.ToString());
            }
            //Console.WriteLine("***ELEMENT NAME: {0}", objReader.Name.ToString());
            //Console.WriteLine("***ELEMENT VALUE: {0}", objReader.Value.ToString());
          } else if (sElement == "waitForActionTime1Default") {
            try {
              m_hshGeneralConfig.Add(sElement, objReader.Value.ToString());  //Add section to hash table
            } catch {
              m_hshGeneralConfig.Add(sElement, WAIT_FOR_ACTION_TIME1_DEFAULT.ToString());  //Add section to hash table
              ConfigError(m_sCodeName, sElement, WAIT_FOR_ACTION_TIME1_DEFAULT.ToString());
            }

          } else if (sElement == "waitForActionTimeNDefault") {
            try {
              m_hshGeneralConfig.Add(sElement, objReader.Value.ToString());  //Add section to hash table
            } catch {
              m_hshGeneralConfig.Add(sElement, WAIT_FOR_ACTION_TIMEN_DEFAULT.ToString());  //Add section to hash table
              ConfigError(m_sCodeName, sElement, WAIT_FOR_ACTION_TIMEN_DEFAULT.ToString());
            }

          } else if (sElement == "waitForActionTime1") {
            try {
              objModalRef.WaitForActionTime1 = Convert.ToInt32(objReader.Value);
            } catch {
              objModalRef.WaitForActionTime1 = WAIT_FOR_ACTION_TIME1_DEFAULT;
              ConfigError(m_sCodeName, sElement, WAIT_FOR_ACTION_TIME1_DEFAULT.ToString());
            }

          } else if (sElement == "waitForActionTimeN") {
            try {
              objModalRef.WaitForActionTimeN = Convert.ToInt32(objReader.Value);
            } catch {
              objModalRef.WaitForActionTimeN = WAIT_FOR_ACTION_TIMEN_DEFAULT;
              ConfigError(m_sCodeName, sElement, WAIT_FOR_ACTION_TIMEN_DEFAULT.ToString());
            }

          } else if (sElement == "keycode") {
            try {
              objModalRef.KeyCode = Convert.ToInt32(objReader.Value);
            } catch {
              objModalRef.KeyCode = KEY_CODE_DEFAULT;
              ConfigError(m_sCodeName, "keycode", KEY_CODE_DEFAULT.ToString());
            }
          }

          break;

        case XmlNodeType.Attribute:
          //Console.WriteLine("ATTRIB NAME: {0}", objReader.Name);
          //Console.WriteLine("ATTRIB VALUE: {0}", objReader.Value);

          break;
      }
    }

    objReader.Close();

    objReader = null;

    //Console.WriteLine("SCAN INTERVAL: {0}", m_hshGeneralConfig["scanIntervalTime"].ToString());
    //Console.WriteLine("WAIT INTERVAL: {0}", m_hshGeneralConfig["waitForActionTimeDefault"].ToString());

    try {
      m_iScanInterval = 1000 * Convert.ToInt32(m_hshGeneralConfig ["scanIntervalTime"]
                                                   .ToString());
    } catch {
      m_iScanInterval = 1000 * SCAN_INTERVAL_DEFAULT;
      ConfigError(m_sCodeName, "scanIntervalTime", SCAN_INTERVAL_DEFAULT.ToString());
    }

    try {
      m_iWaitInterval1Default = Convert.ToInt32(m_hshGeneralConfig ["waitForActionTime1Default"]
                                                    .ToString());
    } catch {
      m_iWaitInterval1Default = WAIT_FOR_ACTION_TIME1_DEFAULT;
      ConfigError(m_sCodeName, "waitForActionTime1Default", WAIT_FOR_ACTION_TIME1_DEFAULT.ToString());
    }

    try {
      m_iWaitIntervalNDefault = Convert.ToInt32(m_hshGeneralConfig ["waitForActionTimeNDefault"]
                                                    .ToString());
    } catch {
      m_iWaitIntervalNDefault = WAIT_FOR_ACTION_TIMEN_DEFAULT;
      ConfigError(m_sCodeName, "waitForActionTimeNDefault", WAIT_FOR_ACTION_TIMEN_DEFAULT.ToString());
    }
  }

  private static void ScanProcessThreads(Process objProcess) {
    //Traverse all threads from the current process and enum all windows attached to the thread
    foreach (ProcessThread objThread in objProcess.Threads) {
      Win32API.EnumThreadProc funcCallback = new Win32API.EnumThreadProc(EnumThreadProc_Callback);

      Win32API.EnumThreadWindows(objThread.Id, funcCallback, IntPtr.Zero);
    }
  }

  public static void ScanForNewDialogs() {
    Console.WriteLine("Scanning processes for new dialogs...");

    //REVISIT: See if possible to rearrange loops for better efficiency

    foreach (DictionaryEntry objItem in m_hshModalsReference) {
      Process[] objProcesses = Process.GetProcessesByName(objItem.Key.ToString());

      //Set list of captions for this process from config file
      m_hshModalsReference_ThisProcess = (Hashtable) objItem.Value;

      foreach (Process objProcess in objProcesses) {
        //Console.WriteLine("Process NAME: {0}, ID : {1}", objItem.Key.ToString(), objProcess.Id);

        ScanProcessThreads(objProcess);
      }
    }
  }

  private static bool EnumThreadProc_Callback(IntPtr iptrHwnd, IntPtr lParam) {
    ModalReferenceEntry objRef;
    ModalCurrentEntry objCurrent;

    // get window caption
    Win32API.W32STRING sLimitedLengthWindowTitle;
    Win32API.GetWindowText(iptrHwnd, out sLimitedLengthWindowTitle, MAX_CAPTION_LENGTH);

    String sWindowTitle = sLimitedLengthWindowTitle.szText;

    if (sWindowTitle.Length == 0)
      return (true);

    if (Win32API.GetParent(iptrHwnd).Equals((IntPtr) 0))  //Don't want to kill parent windows
    {
      //Console.WriteLine("Exiting...this is a parent window...");
      return (true);
    }

    // find this caption in the list of banned captions
    //foreach (ListViewItem item in listView1.Items)
    foreach (DictionaryEntry objItem in m_hshModalsReference_ThisProcess) {
      objRef = (ModalReferenceEntry) objItem.Value;

      if (sWindowTitle.StartsWith(objRef.Caption))  //Found target modal dialog
      {
        if (!m_hshModalsCurrent.ContainsKey(iptrHwnd)) {
          //Get thread and process containing hwnd
          int iThreadID;
          int iProcessID;
          Process objThisProcess;

          iThreadID = Win32API.GetWindowThreadProcessId(iptrHwnd, out iProcessID);
          objThisProcess = Process.GetProcessById(iProcessID);

          Console.WriteLine("DEBUGGING: H-0x{0:X8} : P-{1} : T-0x{2:X8}", (int) iptrHwnd, iProcessID, iThreadID);

          StringBuilder objBuffer = new StringBuilder();
          StringWriter objString = new StringWriter(objBuffer);
          objString.Write("{0}: Found New Dialog: {1}->{2}, PID: {3}, TID: 0x{4:X8}, HWND: 0x{5:X8}",
                          DateTime.Now, objThisProcess.ProcessName, objRef.Caption, iProcessID, iThreadID, (int) iptrHwnd);
          ModalMasher.AppendLog(objString.ToString());
          objBuffer = null;

          Console.WriteLine("{0}: Found New Dialog: {1}->{2}, PID: {3}, TID: 0x{4:X8}, HWND: 0x{5:X8}",
                            DateTime.Now, objThisProcess.ProcessName, objRef.Caption, iProcessID, iThreadID, (int) iptrHwnd);

          objRef.MostRecentOccurrence = DateTime.Now;
          objRef.NumOccurrences++;

          objCurrent = new ModalCurrentEntry();

          objCurrent.Hwnd = iptrHwnd;
          objCurrent.KeyCode = objRef.KeyCode;
          objCurrent.Caption = objRef.Caption;
          objCurrent.OccurrenceDate = objRef.MostRecentOccurrence;
          objCurrent.ProcessName = objThisProcess.ProcessName;
          objCurrent.ProcessID = iProcessID;
          objCurrent.ThreadID = iThreadID;

          //If specified intervals are -1 then assume that user has disabled this dialog from suppression
          if (objRef.WaitForActionTime1 != -1 && objRef.WaitForActionTimeN != -1) {
            if (objRef.NumOccurrences < 2)  //First occurrence
              objCurrent.WaitForActionTime = objRef.WaitForActionTime1;
            else  //Not first occurrence
              objCurrent.WaitForActionTime = objRef.WaitForActionTimeN;

            m_hshModalsCurrent.Add(iptrHwnd, objCurrent);  //Add to hash table
          }
        }
      }
    }

    return true;
  }

  private static void HandleDialog(ModalCurrentEntry objCurrent) {
    StringBuilder objBuffer = new StringBuilder();
    StringWriter objString = new StringWriter(objBuffer);
    objString.Write("{0}: Closing Dialog: {1}->{2}, PID: {3}, TID: 0x{4:X8}, HWND: 0x{5:X8}",
                    DateTime.Now, objCurrent.ProcessName, objCurrent.Caption, objCurrent.ProcessID, objCurrent.ThreadID, (int) objCurrent.Hwnd);
    ModalMasher.AppendLog(objString.ToString());
    objBuffer = null;

    Console.WriteLine("{0}: Closing Dialog: {1}->{2}, PID: {3}, TID: 0x{4:X8}, HWND: 0x{5:X8}",
                      DateTime.Now, objCurrent.ProcessName, objCurrent.Caption, objCurrent.ProcessID, objCurrent.ThreadID, (int) objCurrent.Hwnd);
    //Console.WriteLine("Parent Window: {0}", Win32API.GetParent(objCurrent.Hwnd));

    //Win32API.SetFocus(hwnd);	//Not working - //Make sure dialog has input focus

    //Win32API.SendMessage(hwnd, Win32API.WM_SYSKEYDOWN, 83, 0);

    //Should I have a delay in between KEYDOWN AND UP?

    //SendKeys.Send("{ESC}");		//Works but Dialog has to have input focus first!!!!!

    //Win32API.SendMessage(hwnd, Win32API.WM_SYSKEYDOWN, 83, 0);

    //Win32API.SendMessage(objCurrent.Hwnd, Win32API.WM_COMMAND, Win32API.BN_CLICKED, 2);

    //Win32API.SendMessage(objCurrent.Hwnd, Win32API.WM_KEYDOWN, objCurrent.KeyCode, 0); //Better: This works even without focus
    //Win32API.SendMessage(objCurrent.Hwnd, Win32API.WM_KEYUP, objCurrent.KeyCode, 0); //Better: This works even without focus

    //Win32API.SendMessage(objCurrent.Hwnd, Win32API.WM_CHAR, objCurrent.KeyCode, 0); //Better: This works even without focus

    if (objCurrent.KeyCode != 0)  //Using 0 to denote - NO KEYPRESS, JUST CLOSE
    {
      //Should I have a delay in between?
      try {
        Win32API.SendMessage(objCurrent.Hwnd, Win32API.WM_KEYDOWN, objCurrent.KeyCode, 0);
      } catch {
      }

      try {
        Win32API.SendMessage(objCurrent.Hwnd, Win32API.WM_KEYUP, objCurrent.KeyCode, 0);
      } catch {
      }
    }

    //Finish the process off with forcing the window closed
    try {
      Win32API.SendMessage(objCurrent.Hwnd, Win32API.WM_SYSCOMMAND, Win32API.SC_CLOSE, 0);
    } catch {
    }

    //Console.WriteLine("DEBUGGING KEYCODE: {0}", objCurrent.KeyCode);
  }

  //Scan existing dialogs to see who has expired
  public static void ScanExistingDialogs() {
    Console.WriteLine("Scanning existing dialogs for expiration...");

    ModalCurrentEntry objCurrent;
    Hashtable hshDeleted = new Hashtable();

    foreach (DictionaryEntry objItem in m_hshModalsCurrent) {
      objCurrent = (ModalCurrentEntry) objItem.Value;
      TimeSpan dtDiff = DateTime.Now - objCurrent.OccurrenceDate;

      //Check if dialog should expire - try to close right away if time is 0 (< 1)
      if (objCurrent.WaitForActionTime < 1 || dtDiff.Seconds > objCurrent.WaitForActionTime) {
        HandleDialog(objCurrent);

        //Mark this item as deleted
        hshDeleted.Add(objCurrent.Hwnd, objCurrent.Hwnd);
      }
    }

    //Now remove handled dialogs from current hash table - so they're not processed again
    foreach (DictionaryEntry objItem in hshDeleted)
      m_hshModalsCurrent.Remove(objItem.Value);

    hshDeleted.Clear();
    hshDeleted = null;
  }

  //Debugging only
  public static void ShowUserConfig() {
    Hashtable objDialogHash;

    foreach (DictionaryEntry objItem in m_hshModalsReference) {
      //Each key is process name and value is another hash table containing dialog list

      Console.WriteLine("PROCESS: {0}", objItem.Key.ToString());

      objDialogHash = (Hashtable) objItem.Value;

      foreach (DictionaryEntry objItem2 in objDialogHash) {
        Console.WriteLine(" DIALOG: {0}", objItem2.Key.ToString());

        Console.WriteLine("  CAPTION: {0}", ((ModalReferenceEntry) objItem2.Value).Caption);
        Console.WriteLine("  KEYCODE: {0}", ((ModalReferenceEntry) objItem2.Value).KeyCode);
        Console.WriteLine("  WAIT1: {0}", ((ModalReferenceEntry) objItem2.Value).WaitForActionTime1);
        Console.WriteLine("  WAITN: {0}", ((ModalReferenceEntry) objItem2.Value).WaitForActionTimeN);
      }
    }
  }
}

public class ModalReferenceEntry {
  string sCaption;            //Caption of target dialog
  int m_iWaitForActionTime1;  //Time to wait after encountering this dialog for the first time - in seconds
  int m_iWaitForActionTimeN;  //Time to wait after encountering this dialog thereafter - in seconds
  int m_iKeyCode;             //Key code to push - 13-ENTER, 27-ESC, etc.
  int m_iNumOccurrences;      //Count occurrences of this dialog
  DateTime m_dtMostRecentOccurrence;

  public ModalReferenceEntry() {
    m_iNumOccurrences = 0;
    sCaption = "";
  }

  public string Caption {
    get {
      return sCaption;
    }

    set {
      sCaption = value;
    }
  }

  public int WaitForActionTime1 {
    get {
      return m_iWaitForActionTime1;
    }

    set {
      m_iWaitForActionTime1 = value;
    }
  }

  public int WaitForActionTimeN {
    get {
      return m_iWaitForActionTimeN;
    }

    set {
      m_iWaitForActionTimeN = value;
    }
  }

  public int KeyCode {
    get {
      return m_iKeyCode;
    }

    set {
      m_iKeyCode = value;
    }
  }

  public int NumOccurrences {
    get {
      return m_iNumOccurrences;
    }

    set {
      m_iNumOccurrences = value;
    }
  }

  public DateTime MostRecentOccurrence {
    get {
      return m_dtMostRecentOccurrence;
    }

    set {
      m_dtMostRecentOccurrence = value;
    }
  }
}

public class ModalCurrentEntry {
  IntPtr iptrHwnd;  //Handle of dialog
  DateTime m_dtOccurrenceDate;
  int m_iWaitForActionTime;
  int m_iKeyCode;   //Key code to push - 13-ENTER, 27-ESC, etc.
  string sCaption;  //Caption of target dialog - not required
  string sProcessName;
  int iProcessID;
  int iThreadID;

  public ModalCurrentEntry() {
    sCaption = "";
  }

  public string ProcessName {
    get {
      return sProcessName;
    }

    set {
      sProcessName = value;
    }
  }

  public int ProcessID {
    get {
      return iProcessID;
    }

    set {
      iProcessID = value;
    }
  }

  public int ThreadID {
    get {
      return iThreadID;
    }

    set {
      iThreadID = value;
    }
  }

  public DateTime OccurrenceDate {
    get {
      return m_dtOccurrenceDate;
    }

    set {
      m_dtOccurrenceDate = value;
    }
  }

  public IntPtr Hwnd {
    get {
      return iptrHwnd;
    }

    set {
      iptrHwnd = value;
    }
  }

  public string Caption {
    get {
      return sCaption;
    }

    set {
      sCaption = value;
    }
  }

  public int WaitForActionTime {
    get {
      return m_iWaitForActionTime;
    }

    set {
      m_iWaitForActionTime = value;
    }
  }

  public int KeyCode {
    get {
      return m_iKeyCode;
    }

    set {
      m_iKeyCode = value;
    }
  }
}

}
