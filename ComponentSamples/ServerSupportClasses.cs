
using System;
using System.Collections;
using System.Diagnostics;
using Microsoft.VisualBasic;
using eDiscoverySupportClasses.eDiscoverySupportSpace;

namespace eDiscoveryServerSupportClasses {
public class RemotingHelperApps {
  public RemotingHelperApps() {
  }

  public static void KillHelperApps(ref Hashtable hshMappings) {
    foreach (DictionaryEntry objEntry in hshMappings) {
      try {
        //Try to shut down apps at the Process level

        Console.Write("{0}> ", DateTime.Now);
        Console.WriteLine("SERVER: SHUTTING DOWN APPLICATION {0}", ((ComponentMapping) objEntry.Value).ApplicationClassName);

        Process objProcess = new Process();
        objProcess = Process.GetProcessById(((ComponentMapping) objEntry.Value).ProcessID);
        objProcess.Kill();
        objProcess = null;

      } catch {
      }

      ((ComponentMapping) objEntry.Value).Application = null;
    }
  }

  public static void LoadHelperApps(ref Hashtable hshMappings, Hashtable hshHelperModules, Hashtable hshHelperApps) {
    ComponentMapping objMapping;
    ProcessManager objProcessMgr;
    string sModule;

    objProcessMgr = new ProcessManager();

    foreach (DictionaryEntry objAppEntry in hshHelperApps) {
      //Console.WriteLine("***DEBUG: ENTRY...{0}", objAppEntry.Value.ToString().Trim());

      if (objAppEntry.Value.ToString().Trim() != "") {
        objMapping = new ComponentMapping();

        objMapping.ProcessID = -1;                                       //place holder
        objMapping.Application = null;                                   //place holder
        objMapping.ApplicationClassName = objAppEntry.Value.ToString();  //save app class name

        sModule = "";

        //Console.WriteLine("APP HASH TABLE: {0}", objAppEntry.Key );

        //Fix this use of hashtable!!
        foreach (DictionaryEntry objModuleEntry in hshHelperModules) {
          //Console.WriteLine("MODULE HASH TABLE: {0}", objModuleEntry.Key );

          if (objModuleEntry.Key.ToString().Trim().ToUpper() == objAppEntry.Key.ToString().Trim().ToUpper()) {
            sModule = (string) objModuleEntry.Value;
          }
        }

        try {
          if (sModule != "") {
            objProcessMgr.SaveStatePrior(sModule);
          }

          objMapping.Application = Interaction.CreateObject(objMapping.ApplicationClassName, "");

          //Console.WriteLine("LAUNCHED APP");

          if (sModule != "") {
            objProcessMgr.SaveStatePost(sModule);
          }

          if (objProcessMgr.DifferenceCount() == 1) {
            //Only save the Process ID if it is the only new one since CreateObject called
            objMapping.ProcessID = objProcessMgr.BaseProcessID();
          }

          Console.Write("{0}> ", DateTime.Now);
          Console.WriteLine("SERVER: LAUNCHED APPLICATION {0}", objMapping.ApplicationClassName);
        } catch {
          //Problem launching application
          objMapping.Application = null;
          objMapping.ProcessID = -1;
        }

        hshMappings.Add(objAppEntry.Key.ToString().Trim().ToUpper(), objMapping);

        objMapping = null;
      }
    }

    objProcessMgr = null;
  }
}
}
