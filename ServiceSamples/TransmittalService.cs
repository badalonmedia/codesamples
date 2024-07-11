using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using System.Timers;
using System.Runtime.InteropServices;
using TransmittalService.Components;
using TransmittalService.Components.ErrorHandling;

namespace TransmittalService {
public enum ServiceState {
  SERVICE_STOPPED = 0x00000001,
  SERVICE_START_PENDING = 0x00000002,
  SERVICE_STOP_PENDING = 0x00000003,
  SERVICE_RUNNING = 0x00000004,
  SERVICE_CONTINUE_PENDING = 0x00000005,
  SERVICE_PAUSE_PENDING = 0x00000006,
  SERVICE_PAUSED = 0x00000007,
}

[StructLayout(LayoutKind.Sequential)]
public struct ServiceStatus {
  public long dwServiceType;
  public ServiceState dwCurrentState;
  public long dwControlsAccepted;
  public long dwWin32ExitCode;
  public long dwServiceSpecificExitCode;
  public long dwCheckPoint;
  public long dwWaitHint;
};

public partial class TransmittalService : ServiceBase {
  private bool _busy = false;
  private static System.Timers.Timer TransmittalTimer = null;

  public TransmittalService() {
    InitializeComponent();
  }

  void StartTimer() {
    TransmittalTimer = new System.Timers.Timer(AppSettingsWrapper.PollingInterval);  //10 minutes - also, move to config
    TransmittalTimer.AutoReset = true;
    TransmittalTimer.Enabled = true;
    TransmittalTimer.Elapsed += OnTimer;
  }

  private void OnTimer(object sender, ElapsedEventArgs e) {
    if (_busy) {
      return;
    }

    EventLogHelper.WriteEntry("Timer fired...");

    _busy = true;
    //TransmittalTimer.AutoReset = false; //timer may fire one more time, then it will pause
    TransmittalTimer.Stop();

    try {
      TransmittalHelper.ProcessPendingTransmittals();
    } catch (Exception ex) {
      EventLogHelper.WriteError($"ERROR: {ex.Message}");
      ErrorHandlerTools.LogError(ex, null);
    } finally {
      TransmittalTimer.Start();
      _busy = false;
    }
  }

  [DllImport("advapi32.dll", SetLastError = true)]
  private static extern bool SetServiceStatus(IntPtr handle, ref ServiceStatus serviceStatus);

  protected override void OnStart(string[] args) {
    //update the service state to Start Pending.
    ServiceStatus serviceStatus = new ServiceStatus();
    serviceStatus.dwCurrentState = ServiceState.SERVICE_START_PENDING;
    serviceStatus.dwWaitHint = 100000;
    SetServiceStatus(this.ServiceHandle, ref serviceStatus);

    //set up event log for recording events
    EventLogHelper.Setup();
    EventLogHelper.WriteEntry($"Service started...version {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");

    Email.SendMail(AppSettingsWrapper.SmtpTo, AppSettingsWrapper.SmtpFrom, $"{AppSettingsWrapper.Environment}: Transmittal Service Started", "Transmittal Service Started", true, null);

    StartTimer();

    //update the service state to Running.
    serviceStatus.dwCurrentState = ServiceState.SERVICE_RUNNING;
    SetServiceStatus(this.ServiceHandle, ref serviceStatus);
    ServiceStatusHelper.UpdateStatus("Bioclinica Transmittal Service", "Running");
  }

  protected override void OnStop() {
    //update the service state to Start Pending.
    ServiceStatus serviceStatus = new ServiceStatus();
    serviceStatus.dwCurrentState = ServiceState.SERVICE_STOP_PENDING;
    serviceStatus.dwWaitHint = 100000;
    SetServiceStatus(this.ServiceHandle, ref serviceStatus);

    EventLogHelper.WriteEntry("Service stopped...");
    TransmittalTimer.Stop();

    Email.SendMail(AppSettingsWrapper.SmtpTo, AppSettingsWrapper.SmtpFrom, $"{AppSettingsWrapper.Environment}: Transmittal Service Stopped", "Transmittal Service Stopped", true, null);

    //update the service state to Running.
    serviceStatus.dwCurrentState = ServiceState.SERVICE_STOPPED;
    SetServiceStatus(this.ServiceHandle, ref serviceStatus);
    ServiceStatusHelper.UpdateStatus("Bioclinica Transmittal Service", "Stopped");
  }
}
}
