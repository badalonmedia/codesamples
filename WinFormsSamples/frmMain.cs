using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using System.Diagnostics;
using System.Configuration;
using System.Xml;
using System.IO;

namespace ModalMasher {
/// <summary>
/// Summary description for Form1.
/// </summary>
public class frmMain : System.Windows.Forms.Form {
  //Data to read from app.config

  private string m_sUserConfigPath;
  private string m_sAppName;
  private int m_iScanIntervalStartup;

  private bool m_bAboutShowing = false;

  private System.Windows.Forms.ContextMenu mnuMain;
  private System.Windows.Forms.MenuItem menuItem_Toggle;
  private System.Windows.Forms.MenuItem menuItem_Close;
  private System.Windows.Forms.MenuItem menuItem_About;
  private System.ComponentModel.IContainer components;
  private System.Windows.Forms.NotifyIcon notifyIcon_Main;
  private System.Windows.Forms.NotifyIcon notifyIcon_Paused;
  private System.Windows.Forms.NotifyIcon notifyIcon_Scanning;

  public frmMain() {
    //
    // Required for Windows Form Designer support
    //
    InitializeComponent();

    //
    // TODO: Add any constructor code after InitializeComponent call
    //
  }

  /// <summary>
  /// Clean up any resources being used.
  /// </summary>
  protected override void Dispose(bool disposing) {
    if (disposing) {
      if (components != null) {
        components.Dispose();
      }
    }
    base.Dispose(disposing);
  }

#region Windows Form Designer generated code
  /// <summary>
  /// Required method for Designer support - do not modify
  /// the contents of this method with the code editor.
  /// </summary>
  private void InitializeComponent() {
    this.components = new System.ComponentModel.Container();
    System.Resources.ResourceManager resources = new System.Resources.ResourceManager(typeof(frmMain));
    this.notifyIcon_Main = new System.Windows.Forms.NotifyIcon(this.components);
    this.mnuMain = new System.Windows.Forms.ContextMenu();
    this.menuItem_About = new System.Windows.Forms.MenuItem();
    this.menuItem_Toggle = new System.Windows.Forms.MenuItem();
    this.menuItem_Close = new System.Windows.Forms.MenuItem();
    this.notifyIcon_Paused = new System.Windows.Forms.NotifyIcon(this.components);
    this.notifyIcon_Scanning = new System.Windows.Forms.NotifyIcon(this.components);
    //
    // notifyIcon_Main
    //
    this.notifyIcon_Main.ContextMenu = this.mnuMain;
    this.notifyIcon_Main.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIcon_Main.Icon")));
    this.notifyIcon_Main.Text = "";
    this.notifyIcon_Main.Visible = true;
    //
    // mnuMain
    //
    this.mnuMain.MenuItems.AddRange(new System.Windows.Forms.MenuItem[]{
        this.menuItem_About,
        this.menuItem_Toggle,
        this.menuItem_Close});
    //
    // menuItem_About
    //
    this.menuItem_About.Index = 0;
    this.menuItem_About.Text = "About";
    this.menuItem_About.Click += new System.EventHandler(this.menuItem_About_Click);
    //
    // menuItem_Toggle
    //
    this.menuItem_Toggle.Index = 1;
    this.menuItem_Toggle.Text = "Pause";
    this.menuItem_Toggle.Click += new System.EventHandler(this.menuItem_Toggle_Click);
    //
    // menuItem_Close
    //
    this.menuItem_Close.Index = 2;
    this.menuItem_Close.Text = "Close";
    this.menuItem_Close.Click += new System.EventHandler(this.menuItem_Close_Click);
    //
    // notifyIcon_Paused
    //
    this.notifyIcon_Paused.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIcon_Paused.Icon")));
    this.notifyIcon_Paused.Text = "";
    //
    // notifyIcon_Scanning
    //
    this.notifyIcon_Scanning.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIcon_Scanning.Icon")));
    this.notifyIcon_Scanning.Text = "";
    //
    // frmMain
    //
    this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
    this.ClientSize = new System.Drawing.Size(292, 273);
    this.Name = "frmMain";
    this.Text = "Form1";
    this.Closing += new System.ComponentModel.CancelEventHandler(this.frmMain_Closing);
    this.Load += new System.EventHandler(this.frmMain_Load);
  }
#endregion

  /// <summary>
  /// The main entry point for the application.
  /// </summary>
  [STAThread]
  static void Main() {
    Application.Run(new frmMain());
  }

  private void LoadAppConfig() {
    try {
      m_sUserConfigPath = Path.GetDirectoryName(Application.ExecutablePath) + "\\" + ConfigurationSettings.AppSettings ["user_config"]
                                                                                         .ToString();

      //MessageBox.Show(m_sUserConfigPath);

      m_sAppName = ConfigurationSettings.AppSettings ["app_name"]
                       .ToString();
      m_iScanIntervalStartup = 1000 * Convert.ToInt32(ConfigurationSettings.AppSettings ["scan_interval_startup"]
                                                          .ToString());

    } catch {
      MessageBox.Show("An error occurred while loading application configuration information.", "Error");
      Application.Exit();
    }
  }

  private bool IsAlreadyRunning() {
    return ((Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Application.ExecutablePath)).GetLength(0) > 1));
  }

  private void frmMain_Load(object sender, System.EventArgs e) {
    if (IsAlreadyRunning()) {
      //MessageBox.Show("Dialog Suppression is already active.", "Error");
      Application.Exit();
    }

    Application.ApplicationExit += new System.EventHandler(this.ApplicationExit);

    Form objForm = new Form();  //frmPaused.Show();

    LoadAppConfig();

    //objForm.Icon = System.Drawing.SystemIcons.

    this.Visible = false;
    this.ShowInTaskbar = false;
    this.WindowState = FormWindowState.Minimized;
    this.Text = m_sAppName;

    ModalMasher.Tray = notifyIcon_Main;
    ModalMasher.IconMain = notifyIcon_Main.Icon;
    ModalMasher.IconScanning = notifyIcon_Scanning.Icon;
    ModalMasher.IconPaused = notifyIcon_Paused.Icon;
    ModalMasher.ScanIntervalStartup = m_iScanIntervalStartup;
    ModalMasher.Activate(true, m_sUserConfigPath);

    if (!ModalMasher.IsActivated())
      Application.Exit();

    notifyIcon_Main.Text = this.Text;

    ModalMasher.ShowUserConfig();  //DEBUGGING: dump user config to console...
  }

  private void ShutDown() {
    //This  is not called by closing from tray icon but leave here anyway
    if (ModalMasher.IsActivated()) {
      ModalMasher.WaitLocked();
      ModalMasher.Deactivate();
    }

    notifyIcon_Main.Dispose();
    notifyIcon_Scanning.Dispose();
    notifyIcon_Paused.Dispose();
  }

  private void frmMain_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
  }

  private void menuItem_About_Click(object sender, System.EventArgs e) {
    if (!m_bAboutShowing)  //Prevent multiple About boxes
    {
      m_bAboutShowing = true;

      MessageBox.Show(this.Text, m_sAppName);

      m_bAboutShowing = false;
    }
  }

  private void menuItem_Toggle_Click(object sender, System.EventArgs e) {
    if (ModalMasher.IsPaused()) {
      ModalMasher.Resume();
      menuItem_Toggle.Text = "Pause";
    } else {
      ModalMasher.Pause();
      menuItem_Toggle.Text = "Resume";
    }
  }

  private void menuItem_Close_Click(object sender, System.EventArgs e) {
    ModalMasher.WaitLocked();
    ModalMasher.Deactivate();
    //notifyIcon_Main.Visible = false;

    Application.Exit();
  }

  private void ApplicationExit(object sender, System.EventArgs e) {
    //Console.WriteLine("EXITING");

    ShutDown();
  }
}
}
