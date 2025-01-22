// bidsprnt.cpp : Defines the class behaviors for the application.
//

//CMS: main implementation file for BIDS 2000 Report Printer application
/*
This project is loosely based on the Wordpad project that Microsoft makes available
to developers.  Many of the word processor features have been removed, and several
new specific features have been added.  There is still some code remaining from the
original Microsoft source code.  Look for functions with comments preceded by CMS for 
source code created specifically for this project.  Within functions provided by Microsoft,
look for comments preceded by CMS for lines added for this project.

*/


#include "stdafx.h"
#include "bidsprnt.h"
#include "mainfrm.h"
#include "ipframe.h"
#include "bidsprntdoc.h"
#include "bidsprntview.h"
#include "strings.h"
#include "key.h"
#include "doctype.h"
#include "aboutbox.h"
#include "fileversion.h"
#include <locale.h>
#include <winnls.h>
#include <winreg.h>
#include "propsdialog.h"
#include "myfiledialog.h"


extern BOOL AFXAPI AfxFullPath(LPTSTR lpszPathOut, LPCTSTR lpszFileIn);
static BOOL RegisterHelper(LPCTSTR* rglpszRegister, LPCTSTR* rglpszSymbols, 
	BOOL bReplace);

#ifdef _DEBUG
#undef THIS_FILE
static char BASED_CODE THIS_FILE[] = __FILE__;
#endif

int CBIDSApp::m_nOpenMsg = RegisterWindowMessage(_T("BIDSPrntOpenMessage"));
int CBIDSApp::m_nPrinterChangedMsg = RegisterWindowMessage(_T("BIDSPrntPrinterChanged"));

const int CBIDSApp::m_nPrimaryNumUnits = 4;
const int CBIDSApp::m_nNumUnits = 7;

CUnit CBIDSApp::m_units[7] = 
{
//	TPU, 	SmallDiv,	MedDiv,	LargeDiv,	MinMove,	szAbbrev,			bSpace
CUnit(1440,	180,		720,	1440,		90,			IDS_INCH1_ABBREV,	FALSE),//inches
CUnit(568,	142,		284,	568,		142,		IDS_CM_ABBREV,		TRUE),//centimeters
CUnit(20,	120,		720,	720,		100,		IDS_POINT_ABBREV,	TRUE),//points
CUnit(240,	240,		1440,	1440,		120,		IDS_PICA_ABBREV,	TRUE),//picas
CUnit(1440,	180,		720,	1440,		90,			IDS_INCH2_ABBREV,	FALSE),//in
CUnit(1440,	180,		720,	1440,		90,			IDS_INCH3_ABBREV,	FALSE),//inch
CUnit(1440,	180,		720,	1440,		90,			IDS_INCH4_ABBREV,	FALSE)//inches
};

static UINT DoRegistry(LPVOID lpv)
{
	ASSERT(lpv != NULL);
	((CBIDSApp*)lpv)->UpdateRegistry();
	return 0;

}

/////////////////////////////////////////////////////////////////////////////
// CBIDSApp

BEGIN_MESSAGE_MAP(CBIDSApp, CWinApp)
	//{{AFX_MSG_MAP(CBIDSApp)
	ON_COMMAND(ID_APP_ABOUT, OnAppAbout)
	ON_COMMAND(ID_FILE_OPEN_DATA, OnFileOpenData)
	ON_COMMAND(ID_FILE_OPEN_REPORTS, OnFileOpenReports)
	ON_COMMAND(ID_APP_EXIT, OnAppExit)
	ON_UPDATE_COMMAND_UI(ID_FILE_DELETE, OnUpdateFileDelete)
	ON_UPDATE_COMMAND_UI(ID_FILE_PROPERTIES, OnUpdateFileProperties)
	ON_COMMAND(ID_FILE_DELETE, OnFileDelete)
	ON_COMMAND(ID_FILE_PROPERTIES, OnFileProperties)
	//}}AFX_MSG_MAP
END_MESSAGE_MAP()


CBIDSArgs::CBIDSArgs(void)	//CMS
{
	m_nRunType = RUNTYPE_EMPTY;			//CMS: by default, launch app with empty grid
	m_nTp = TO_FILE;					//CMS: assume TP to a file by default
	m_nRunSplitter = RUN_SPLITTER_YES;	//CMS: run splitter
	m_bForceTextMode = TRUE;

}

		
BOOL CBIDSArgs::ParseArgs(LPSTR lpszParam)	//CMS
{
	char szCmd[MAX_CMD_LEN + 1];
	char *pToken;


	//	Break out the cmd line params.  The cmd line is stored in 
	//	the member var. : m_lpCmdLine

	strcpy(szCmd, lpszParam);

	if (strlen(szCmd))	//CMS: at least one arg present	
	{
		//CMS: first arg should be a filename
		pToken = strtok(szCmd, " ");

		if (!pToken)	//Couldn't read token for some reason, wrong delimiter?		
		{
			m_nRunType = RUNTYPE_EMPTY;		//assume no file argument
			return(true);
		}

		//CMS: first token not empty, but still may be NO_FILE_NAME specifier
		if (!strcmp(pToken, NO_FILE_NAME))
			m_nRunType = RUNTYPE_EMPTY;			//no file argument
		else
		{
			m_sDataFileName = (CString) pToken;		//save filename
			m_nRunType = RUNTYPE_FILE;
		}

		//CMS: get next arg
		pToken = strtok(NULL, " ");
		m_nRunSplitter = (pToken ? atoi(pToken) : RUN_SPLITTER_YES);

		//CMS: get next arg
		pToken = strtok(NULL, " ");
		
		if (pToken)
			m_nTp = (atoi(pToken) == TO_PRINTER ? TO_PRINTER : TO_FILE);
		else
			m_nTp = TO_FILE;	//default
		
	}

	return(TRUE);

}



/////////////////////////////////////////////////////////////////////////////
// CBIDSApp construction

CBIDSApp::CBIDSApp(): m_optionsText(0)
{
	_tsetlocale(LC_ALL, _T(""));

	m_nFilterIndex = 1;
	DWORD dwVersion = ::GetVersion();
	m_bWin4 = (BYTE)dwVersion >= 4;
#ifndef _UNICODE
	m_bWin31 = (dwVersion > 0x80000000 && !m_bWin4);
#endif

	m_nDefFont = (m_bWin4) ? DEFAULT_GUI_FONT : ANSI_VAR_FONT;
	m_dcScreen.Attach(::GetDC(NULL));
	m_bLargeIcons = m_dcScreen.GetDeviceCaps(LOGPIXELSX) >= 120;
	
	m_pView = NULL;					//CMS: init pointer to view
	m_bDocHasBeenOpened = FALSE;	//CMS
	m_bFontFlag = FALSE;			//CMS: do not set default font

	m_rectPageMarginDef.SetRect(1800, 1440, 1800, 1440);	//CMS: default margins
	//the margins are specified in twips.  In inches, they are 1.25, 1, 1.25, 1
	
}


CBIDSApp::~CBIDSApp()
{
	if (m_dcScreen.m_hDC != NULL)
		::ReleaseDC(NULL, m_dcScreen.Detach());
}


/////////////////////////////////////////////////////////////////////////////
// The one and only CBIDSApp object

CBIDSApp theApp;

// Register the application's document templates.  Document templates
//  serve as the connection between documents, frame windows and views.
static CSingleDocTemplate DocTemplate(
		IDR_MAINFRAME,
		RUNTIME_CLASS(CBIDSDoc),
		RUNTIME_CLASS(CMainFrame),       // main SDI frame window
		RUNTIME_CLASS(CBIDSView));

// This identifier was generated to be statistically unique for your app.
// You may change it if you prefer to choose a specific identifier.
static const CLSID BASED_CODE clsid =
{ 0x73FDDC80L, 0xAEA9, 0x101A, { 0x98, 0xA7, 0x00, 0xAA, 0x00, 0x37, 0x49, 0x59} };

/////////////////////////////////////////////////////////////////////////////
// CBIDSApp initialization

BOOL CBIDSApp::InitInstance()
{

	if (!cmdInfo.ParseArgs(m_lpCmdLine))	//CMS: parse command-line arguments
		return(FALSE);
	
	//CMS: see if app is already running with same document open
	if (::FindWindow(szBIDSPrintClass, NULL) && IsDocOpen(cmdInfo.m_sDataFileName))
		return(FALSE);

	SetRegistryKey(szRegKey);	
	LoadOptions();	//load application options stored in registry - margins

	Enable3dControls();

	switch (m_nCmdShow)
	{
		case SW_HIDE:
		case SW_SHOWMINIMIZED:
		case SW_MINIMIZE:
		case SW_SHOWMINNOACTIVE:
			break;
		case SW_RESTORE:
		case SW_SHOW:
		case SW_SHOWDEFAULT:
		case SW_SHOWNA:
		case SW_SHOWNOACTIVATE:
		case SW_SHOWNORMAL:
		case SW_SHOWMAXIMIZED:
			if (m_bMaximized)
				m_nCmdShow = SW_SHOWMAXIMIZED;
			break;
	}

	int nCmdShow = m_nCmdShow;

	LoadAbbrevStrings();

	NotifyPrinterChanged((m_hDevNames == NULL));

	free((void*) m_pszHelpFilePath);
//	m_pszHelpFilePath = _T("WORDPAD.HLP");		//CMS: implement b2k help file

	// Initialize OLE libraries
	if (!AfxOleInit())
	{
		AfxMessageBox(IDP_OLE_INIT_FAILED);
		return(FALSE);
	}

	if (LoadLibrary(_T("RICHED32.DLL")) == NULL)	// Initialize RichEdit control
	{
		AfxMessageBox(IDS_RICHED_LOAD_FAIL, MB_OK | MB_ICONEXCLAMATION);
		return(FALSE);
	}

	// Standard initialization
	// If you are not using these features and wish to reduce the size
	//  of your final executable, you should remove from the following
	//  the specific initialization routines you do not need.
	
	LoadStdProfileSettings();  // Load standard INI file options (including MRU)
	
	// Connect the COleTemplateServer to the document template.
	//  The COleTemplateServer creates new documents on behalf
	//  of requesting OLE containers by using information
	//  specified in the document template.
	m_server.ConnectTemplate(clsid, &DocTemplate, TRUE);
	// Note: SDI applications register server objects only if /Embedding
	//   or /Automation is present on the command line.

	// make sure the main window is showing

//	m_bPromptForType = FALSE;
	OnFileNew();
//	m_bPromptForType = TRUE;
	
	m_nCmdShow = -1;

	if (m_pMainWnd == NULL) // i.e. OnFileNew failed
		return(FALSE);

	// CMS: Disable File Manager drag/drop open
	m_pMainWnd->DragAcceptFiles(FALSE);

	// When a server application is launched stand-alone, it is a good idea
	//  to update the system registry in case it has been damaged.
	// do registry stuff in separate thread
#ifndef _UNICODE
	if (m_bWin31) // no threads on Win32s
		UpdateRegistry();
	else
#endif
		AfxBeginThread(DoRegistry, this, THREAD_PRIORITY_IDLE);
	
	CFileVersion ProductInfo((CString)AfxGetApp()->m_pszExeName + ".EXE");	//CMS
	m_pMainWnd->SetWindowText(ProductInfo.GetProductName());	//CMS: set window caption

	//CMS: store some values locally from the args object
	m_nTp = cmdInfo.m_nTp;
	m_nRunSplitter = cmdInfo.m_nRunSplitter;
	m_sDrive = getenv("BLDRIVE");	//CMS: BLDRIVE, if exists, contains data destination

	//CMS: **If the BLDRIVE env variable has a value, then this is where the app will
	//look for Report and Data files.  Otherwise, the current drive will be used.	
		
	m_sDataFileName = cmdInfo.m_sDataFileName;
	m_sDataFileName.MakeUpper();		
	
	if (cmdInfo.m_nRunType == RUNTYPE_FILE)	//CMS: show print dialog automatically
	{
		theApp.OpenDocumentFile(theApp.cmdInfo.m_sDataFileName);

		if (cmdInfo.m_nTp == TO_PRINTER && m_pView != NULL)	//send to printer			
			m_pView->DoPrinting();			
		
	}

	return(TRUE);
}


BOOL CBIDSApp::IsDocOpen(LPCTSTR lpszFileName)
{
	if (lpszFileName[0] == NULL)
		return(FALSE);

	TCHAR szPath[_MAX_PATH];
	AfxFullPath(szPath, lpszFileName);
	ATOM atom = GlobalAddAtom(szPath);
	ASSERT(atom != NULL);
	if (atom == NULL)
		return FALSE;
	EnumWindows(StaticEnumProc, (LPARAM)&atom);
	if (atom == NULL)
		return TRUE;
	DeleteAtom(atom);
	return FALSE;
}


BOOL CALLBACK CBIDSApp::StaticEnumProc(HWND hWnd, LPARAM lParam)
{
	TCHAR szClassName[30];
	GetClassName(hWnd, szClassName, 30);
	if (lstrcmp(szClassName, szBIDSPrintClass) != 0)
		return TRUE;

	ATOM* pAtom = (ATOM*)lParam;
	ASSERT(pAtom != NULL);
	DWORD dw = NULL;
	::SendMessageTimeout(hWnd, m_nOpenMsg, NULL, (LPARAM)*pAtom,
		SMTO_ABORTIFHUNG, 500, &dw);
	if (dw)
	{
		::SetForegroundWindow(hWnd);
		DeleteAtom(*pAtom);
		*pAtom = NULL;
		return FALSE;
	}

	return TRUE;
}


CDockState& CBIDSApp::GetDockState(int nDocType, BOOL bPrimary)
{
	return m_optionsText.GetDockState(bPrimary);
}


void CBIDSApp::SaveOptions()
{
	WriteProfileInt(szSection, szWordSel, m_bWordSel);
	WriteProfileInt(szSection, szUnits, GetUnits());
	WriteProfileInt(szSection, szMaximized, m_bMaximized);
	WriteProfileBinary(szSection, szFrameRect, (BYTE*)&m_rectInitialFrame, 
		sizeof(CRect));
	WriteProfileBinary(szSection, szPageMargin, (BYTE*)&m_rectPageMargin, 
		sizeof(CRect));

	//CMS: some unsused options were removed from this routine
}


void CBIDSApp::LoadOptions()
{
	BYTE* pb = NULL;
	UINT nLen = 0;

	HFONT hFont = (HFONT)GetStockObject(DEFAULT_GUI_FONT);
	if (hFont == NULL)
		hFont = (HFONT)GetStockObject(ANSI_VAR_FONT);
	VERIFY(GetObject(hFont, sizeof(LOGFONT), &m_lf));

	m_bWordSel = GetProfileInt(szSection, szWordSel, TRUE);
	TCHAR buf[2];
	buf[0] = NULL;
	GetLocaleInfo(GetUserDefaultLCID(), LOCALE_IMEASURE, buf, 2);
	int nDefUnits = buf[0] == '1' ? 0 : 1;
	SetUnits(GetProfileInt(szSection, szUnits, nDefUnits));
	m_bMaximized = GetProfileInt(szSection, szMaximized, (int)FALSE);

	if (GetProfileBinary(szSection, szFrameRect, &pb, &nLen))
	{
		ASSERT(nLen == sizeof(CRect));
		memcpy(&m_rectInitialFrame, pb, sizeof(CRect));
		delete pb;
	}
	else
		m_rectInitialFrame.SetRect(0,0,0,0);

	CRect rectScreen(0, 0, GetSystemMetrics(SM_CXSCREEN), 
		GetSystemMetrics(SM_CYSCREEN));
	CRect rectInt;
	rectInt.IntersectRect(&rectScreen, &m_rectInitialFrame);
	if (rectInt.Width() < 10 || rectInt.Height() < 10)
		m_rectInitialFrame.SetRect(0, 0, 0, 0);

	if (GetProfileBinary(szSection, szPageMargin, &pb, &nLen))
	{
		ASSERT(nLen == sizeof(CRect));
		memcpy(&m_rectPageMargin, pb, sizeof(CRect));
		delete pb;
	}
	else	//CMS: use default margins
		//m_rectPageMargin.SetRect(1800, 1440, 1800, 1440);
		m_rectPageMargin = m_rectPageMarginDef;	//CMS: better style

}


void CBIDSApp::LoadAbbrevStrings()
{
	for (INT i = 0;i < m_nNumUnits; i++)
		m_units[i].m_strAbbrev.LoadString(m_units[i].m_nAbbrevID);
}


BOOL CBIDSApp::ParseMeasurement(LPTSTR buf, int& lVal)
{
	TCHAR* pch;
	if (buf[0] == NULL)
		return FALSE;
	float f = (float)_tcstod(buf,&pch);

	// eat white space, if any
	while (isspace(*pch))
		pch++;

	if (pch[0] == NULL) // default
	{
		lVal = (f < 0.f) ? (int)(f*GetTPU()-0.5f) : (int)(f*GetTPU()+0.5f);
		return TRUE;
	}

	for (int i=0;i<m_nNumUnits;i++)
	{
		if (lstrcmpi(pch, GetAbbrev(i)) == 0)
		{
			lVal = (f < 0.f) ? (int)(f*GetTPU(i)-0.5f) : (int)(f*GetTPU(i)+0.5f);
			return TRUE;
		}
	}
	return FALSE;
}


void CBIDSApp::PrintTwips(TCHAR* buf, int nValue, int nDec)
{
	ASSERT(nDec == 2);
	int div = GetTPU();
	int lval = nValue;
	BOOL bNeg = FALSE;
	
	int* pVal = new int[nDec+1];

	if (lval < 0)
	{
		bNeg = TRUE;
		lval = -lval;
	}

	for (int i=0;i<=nDec;i++)
	{
		pVal[i] = lval/div; //integer number
		lval -= pVal[i]*div;
		lval *= 10;
	}

	i--;

	if (lval >= div/2)
		pVal[i]++;

	while ((pVal[i] == 10) && (i != 0))
	{
		pVal[i] = 0;
		pVal[--i]++;
	}

	while (nDec && pVal[nDec] == 0)
		nDec--;

	_stprintf(buf, _T("%.*f"), nDec, (float)nValue/(float)div);

	if (m_units[m_nUnits].m_bSpaceAbbrev)
		lstrcat(buf, _T(" "));

	lstrcat(buf, GetAbbrev());
	delete []pVal;
}

/////////////////////////////////////////////////////////////////////////////
// CBIDSApp commands

void CBIDSApp::OnAppAbout()		//CMS
{
	CAboutBox dlg;

	dlg.DoModal();

}


int CBIDSApp::ExitInstance()
{
	m_pszHelpFilePath = NULL;

	FreeLibrary(GetModuleHandle(_T("RICHED32.DLL")));
	SaveOptions();

	return(CWinApp::ExitInstance());

}


BOOL CBIDSApp::RunSplitter(void)	//CMS
{
	//run splitter app against BIDS.LOG - create new splitter when time permits
	
	//splitter exists, now check for BIDS.LOG file

	BOOL bResult = TRUE;	//don't quit application

	//Check for existence of BIDS.LOG file
	if (GetFileAttributes(EXTERNAL_LOG_FILE_NAME) == 0xFFFFFFFF)	//error
	{		
		INT nResult = AfxMessageBox(IDS_BIDSLOG_NONE,
			MB_YESNO | MB_ICONQUESTION | MB_DEFBUTTON1 | MB_SYSTEMMODAL);

		if (nResult != IDYES)
			bResult = FALSE;	//quit application upon return			
	}
	else
	{
		//check for existence of splitter app
		if (GetFileAttributes(SPLITTER_APP_NAME) == 0xFFFFFFFF)	//error
		{
			INT nResult = AfxMessageBox(IDS_SPLITTER_MISSING,
				MB_YESNO | MB_ICONQUESTION | MB_DEFBUTTON1 | MB_SYSTEMMODAL);

			if (nResult != IDYES)
				bResult = FALSE;	//quit application upon return
		}
		else
		{
			//BIDS.LOG and splitter app exist, so run it
			INT nResult = system(SPLITTER_APP_NAME);
			//INT nResult = WinExec(SPLITTER_APP_NAME, SW_SHOW);
			
			while(GetFileAttributes(EXTERNAL_LOG_FILE_NAME) != 0xFFFFFFFF)
				;	//loop while BIDS.LOG still exists - not really the best way to do this
		}
	}

	return(bResult);	//return decision to quit app or not

}


// prompt for file name - used for open and save as
// static function called from app
BOOL CBIDSApp::PromptForFileName(CString sCap, CString sPath, CString& fileName, UINT nIDSTitle, 
	DWORD dwFlags, BOOL bOpenFileDialog, int *pType)
{

	CString sFileFilter = "All Files (*.*)|*.*|Text Files (*.txt)|*.txt||";

	CMyFileDialog dlgFile(bOpenFileDialog, NULL, NULL, dwFlags, sFileFilter, NULL);
	CString title;		
	
	CString sFullPath = (m_sDrive.GetLength() < 1 ? "" : m_sDrive) + sPath;		//CMS: use BLDRIVE if available		
	
	dlgFile.m_ofn.lpstrTitle = sCap;		//CMS
	dlgFile.m_ofn.lpstrFile = fileName.GetBuffer(_MAX_PATH);	//CMS
	dlgFile.m_ofn.lpstrInitialDir = sFullPath;	//CMS

	BOOL bRet = (dlgFile.DoModal() == IDOK) ? TRUE : FALSE;

	fileName.ReleaseBuffer();

	if (bRet)
	{
		if (bOpenFileDialog)
			m_nFilterIndex = dlgFile.m_ofn.nFilterIndex;

		if (pType != NULL)
		{
			int nIndex = (int)dlgFile.m_ofn.nFilterIndex - 1;
			ASSERT(nIndex >= 0);			
			*pType = RD_DEFAULT;
		}
	}

	return(bRet);
}


BOOL CBIDSApp::OnDDECommand(LPTSTR /*lpszCommand*/) 
{
	return(FALSE);
}


#define REGENTRY(key, value) _T(key) _T("\0\0") _T(value)
#define REGENTRYX(key, valuename, value) _T(key) _T("\0") _T(valuename) _T("\0") _T(value)

static const TCHAR sz00[] = REGENTRY("%2", "%5");
static const TCHAR sz01[] = REGENTRY("%2\\CLSID", "%1");
static const TCHAR sz02[] = REGENTRY("%2\\Insertable", "");
static const TCHAR sz03[] = REGENTRY("%2\\protocol\\StdFileEditing\\verb\\0", "&Edit");
static const TCHAR sz04[] = REGENTRY("%2\\protocol\\StdFileEditing\\server", "%3");
static const TCHAR sz05[] = REGENTRY("CLSID\\%1", "%5");
static const TCHAR sz06[] = REGENTRY("CLSID\\%1\\ProgID", "%2");
static const TCHAR sz07[] = REGENTRY("CLSID\\%1\\InprocHandler32", "ole32.dll");
static const TCHAR sz08[] = REGENTRY("CLSID\\%1\\LocalServer32", "%3");
static const TCHAR sz09[] = REGENTRY("CLSID\\%1\\Verb\\0", "&Edit,0,2");
static const TCHAR sz10[] = REGENTRY("CLSID\\%1\\Verb\\1", "&Open,0,2");
static const TCHAR sz11[] = REGENTRY("CLSID\\%1\\Insertable", "");
static const TCHAR sz12[] = REGENTRY("CLSID\\%1\\AuxUserType\\2", "%4");
static const TCHAR sz13[] = REGENTRY("CLSID\\%1\\AuxUserType\\3", "%6");
static const TCHAR sz14[] = REGENTRY("CLSID\\%1\\DefaultIcon", "%3,1");
static const TCHAR sz15[] = REGENTRY("CLSID\\%1\\MiscStatus", "0");
static const TCHAR sz16[] = REGENTRY("%2\\shell\\open\\command", "%3 \"%%1\"");
static const TCHAR sz17[] = REGENTRY("%2\\shell\\print\\command", "%3 /p \"%%1\"");
static const TCHAR sz18[] = REGENTRY("%7", "%2");
static const TCHAR sz19[] = REGENTRY("%2", ""); // like sz00 only no long type name
static const TCHAR sz20[] = REGENTRY("%2\\shell\\printto\\command", "%3 /pt \"%%1\" \"%%2\" \"%%3\" \"%%4\"");
static const TCHAR sz21[] = REGENTRY("%2\\DefaultIcon", "%3,%8");
static const TCHAR sz22[] = REGENTRYX("%7\\ShellNew", "NullFile", "true");
static const TCHAR sz23[] = REGENTRYX("%7\\ShellNew", "Data", "{\\rtf1}");

#define NUM_REG_ARGS 8

static const LPCTSTR rglpszBIDSPrintRegister[] =
{sz00, sz02, sz03, sz05, sz09, sz10, sz11, sz15, NULL};

static const LPCTSTR rglpszBIDSPrintOverwrite[] =
{sz01, sz04, sz06, sz07, sz08, sz12, sz13, sz14, sz16, sz17, sz20, NULL};

static const LPCTSTR rglpszTxtExtRegister[] =
{sz18, sz22, NULL};
static const LPCTSTR rglpszTxtRegister[] =
{sz00, sz01, sz16, sz17, sz20, sz21, NULL};

static void RegisterExt(LPCTSTR lpszExt, LPCTSTR lpszProgID, UINT nIDTypeName,
	LPCTSTR* rglpszSymbols, LPCTSTR* rglpszExtRegister, 
	LPCTSTR* rglpszRegister, int nIcon)
{
	// don't overwrite anything with the extensions
	CString strWhole;
	VERIFY(strWhole.LoadString(nIDTypeName));
	CString str;
	AfxExtractSubString(str, strWhole, DOCTYPE_PROGID);

	rglpszSymbols[1] = lpszProgID;
	rglpszSymbols[4] = str;
	rglpszSymbols[6] = lpszExt;
	TCHAR buf[10];
	wsprintf(buf, _T("%d"), nIcon);
	rglpszSymbols[7] = buf;
	// check for .ext and progid
	CKey key;
	if (!key.Open(HKEY_CLASSES_ROOT, lpszExt)) // .ext doesn't exist
		RegisterHelper(rglpszExtRegister, rglpszSymbols, TRUE);
	key.Close();
	if (!key.Open(HKEY_CLASSES_ROOT, lpszProgID)) // ProgID doesn't exist (i.e. txtfile)
		RegisterHelper(rglpszRegister, rglpszSymbols, TRUE);
}


void CBIDSApp::UpdateRegistry()
{
	USES_CONVERSION;
	LPOLESTR lpszClassID = NULL;
	CDocTemplate* pDocTemplate = &DocTemplate;

	// get registration info from doc template string
	CString strServerName;
	CString strLocalServerName;
	CString strLocalShortName;

	if (!pDocTemplate->GetDocString(strServerName,
	   CDocTemplate::regFileTypeId) || strServerName.IsEmpty())
	{
		TRACE0("Error: not enough information in DocTemplate to register OLE server.\n");
		return;
	}
	if (!pDocTemplate->GetDocString(strLocalServerName,
	   CDocTemplate::regFileTypeName))
		strLocalServerName = strServerName;     // use non-localized name
	if (!pDocTemplate->GetDocString(strLocalShortName,
		CDocTemplate::fileNewName))
		strLocalShortName = strLocalServerName; // use long name

	ASSERT(strServerName.Find(' ') == -1);  // no spaces allowed

	::StringFromCLSID(clsid, &lpszClassID);
	ASSERT (lpszClassID != NULL);

	// get path name to server
	TCHAR szLongPathName[_MAX_PATH];
	TCHAR szShortPathName[_MAX_PATH];
	::GetModuleFileName(AfxGetInstanceHandle(), szLongPathName, _MAX_PATH);
	::GetShortPathName(szLongPathName, szShortPathName, _MAX_PATH);
	
	LPCTSTR rglpszSymbols[NUM_REG_ARGS];
	rglpszSymbols[0] = OLE2CT(lpszClassID);
	rglpszSymbols[1] = strServerName;
	rglpszSymbols[2] = szShortPathName;
	rglpszSymbols[3] = strLocalShortName;
	rglpszSymbols[4] = strLocalServerName;
	rglpszSymbols[5] = m_pszAppName;	// will usually be long, readable name
	rglpszSymbols[6] = NULL;

	if (RegisterHelper((LPCTSTR*)rglpszBIDSPrintRegister, rglpszSymbols, FALSE))
		RegisterHelper((LPCTSTR*)rglpszBIDSPrintOverwrite, rglpszSymbols, TRUE);

	// free memory for class ID
	ASSERT(lpszClassID != NULL);
	CoTaskMemFree(lpszClassID);
}


BOOL RegisterHelper(LPCTSTR* rglpszRegister, LPCTSTR* rglpszSymbols, 
	BOOL bReplace)
{
	ASSERT(rglpszRegister != NULL);
	ASSERT(rglpszSymbols != NULL);

	CString strKey;
	CString strValueName;
	CString strValue;

	// keeping a key open makes this go a bit faster
	CKey keyTemp;
	VERIFY(keyTemp.Create(HKEY_CLASSES_ROOT, _T("CLSID")));

	BOOL bResult = TRUE;
	while (*rglpszRegister != NULL)
	{
		LPCTSTR lpszKey = *rglpszRegister++;
		if (*lpszKey == '\0')
			continue;

		LPCTSTR lpszValueName = lpszKey + lstrlen(lpszKey) + 1;
		LPCTSTR lpszValue = lpszValueName + lstrlen(lpszValueName) + 1;

		strKey.ReleaseBuffer(
			FormatMessage(FORMAT_MESSAGE_FROM_STRING | 
			FORMAT_MESSAGE_ARGUMENT_ARRAY, lpszKey, NULL,	NULL, 
			strKey.GetBuffer(256), 256, (va_list*) rglpszSymbols));
		strValueName = lpszValueName;
		strValue.ReleaseBuffer(
			FormatMessage(FORMAT_MESSAGE_FROM_STRING | 
			FORMAT_MESSAGE_ARGUMENT_ARRAY, lpszValue, NULL, NULL, 
			strValue.GetBuffer(256), 256, (va_list*) rglpszSymbols));

		if (strKey.IsEmpty())
		{
			TRACE1("Warning: skipping empty key '%s'.\n", lpszKey);
			continue;
		}

		CKey key;
		VERIFY(key.Create(HKEY_CLASSES_ROOT, strKey));
		if (!bReplace)
		{
			CString str;
			if (key.GetStringValue(str, strValueName) && !str.IsEmpty())
				continue;
		}

		if (!key.SetStringValue(strValue, strValueName))
		{
			TRACE2("Error: failed setting key '%s' to value '%s'.\n",
				(LPCTSTR)strKey, (LPCTSTR)strValue);
			bResult = FALSE;
			break;
		}
	}

	return bResult;
}


void CBIDSApp::WinHelp(DWORD dwData, UINT nCmd) 
{
	if (nCmd == HELP_INDEX || nCmd == HELP_CONTENTS)
		nCmd = HELP_FINDER;
	CWinApp::WinHelp(dwData, nCmd);
}


BOOL CBIDSApp::PreTranslateMessage(MSG* pMsg) 
{
	if (pMsg->message == WM_PAINT)
		return FALSE;

	// CWinApp::PreTranslateMessage does nothing but call base
	return(CWinThread::PreTranslateMessage(pMsg));
}


void CBIDSApp::NotifyPrinterChanged(BOOL bUpdatePrinterSelection)
{
	if (bUpdatePrinterSelection)
		UpdatePrinterSelection(FALSE);

	POSITION pos = m_listPrinterNotify.GetHeadPosition();

	while (pos != NULL)
	{
		HWND hWnd = m_listPrinterNotify.GetNext(pos);
		::SendMessage(hWnd, m_nPrinterChangedMsg, 0, 0);
	}
}


BOOL CBIDSApp::IsIdleMessage(MSG* pMsg)
{
	if (pMsg->message == WM_MOUSEMOVE || pMsg->message == WM_NCMOUSEMOVE)
		return FALSE;
	return CWinApp::IsIdleMessage(pMsg);
}


void CBIDSApp::OnFileOpenData()		//CMS: for opening Data files
{
	// TODO: Add your command handler code here

	// prompt the user (with all document templates)
	CString sNewName;
	int nType = RD_DEFAULT;		//text files only

	m_pView->GetDocument()->SetModifiedFlag(FALSE);	//CMS: don't want a save prompt

	DWORD wFlags = OFN_HIDEREADONLY | OFN_FILEMUSTEXIST | OFN_NOCHANGEDIR;

	if (!PromptForFileName("Open BIDS 2000 Data File", DATA_PATH, sNewName, AFX_IDS_OPENFILE,
	  wFlags, TRUE, &nType))
		return; // open cancelled

	OpenDocumentFile(sNewName);
	// if returns NULL, the user has already been alerted

	m_bFontFlag = FALSE;	//should app set font to default?
	
}


void CBIDSApp::OnFileOpenReports()	//CMS
{
	// TODO: Add your command handler code here

	// prompt the user (with all document templates)
	CString sNewName;
	int nType = RD_DEFAULT;		//text files only

	m_pView->GetDocument()->SetModifiedFlag(FALSE);	//CMS: don't want a save prompt

	DWORD wFlags = OFN_HIDEREADONLY | OFN_FILEMUSTEXIST | OFN_NOCHANGEDIR;

	if (!PromptForFileName("Open BIDS 2000 Report File", REPORTS_PATH, sNewName, AFX_IDS_OPENFILE,
		wFlags, TRUE, &nType))
		return;		// open cancelled
	
	OpenDocumentFile(sNewName);

	m_bFontFlag = FALSE;	//should app set font to default?
	
}


void CBIDSApp::OnFileDelete(void)	//CMS
{		
	m_pView->GetDocument()->SetModifiedFlag(FALSE);	//CMS: don't want a save prompt

	//CMS: I commented out the message box below because I am letting the SHFileOperation
	//function do the confirmation.

//int nRes = AfxMessageBox(IDS_DELETE_FILE,
//MB_YESNO | MB_ICONQUESTION | MB_DEFBUTTON2 | MB_SYSTEMMODAL);

//	if (IDYES == nRes)
//	{
		CString sTempName = m_sDataFileName;	//save name in case of initialization on next line
		
		//delete the document file and move to Recycle Bin		

		SHFILEOPSTRUCT ops;

		ZeroMemory((void *) &ops, sizeof(ops));	//reset memory for the heck of it
		
		ops.hwnd = NULL;
		ops.wFunc = FO_DELETE;
		ops.pFrom = m_sDataFileName + '\0';	//should be double null-terminated
		ops.pTo = NULL;	
		//ops.fFlags = FOF_NOCONFIRMATION | FOF_ALLOWUNDO;//to recycle bin
		ops.fFlags = FOF_ALLOWUNDO;	//I think there is an occasional bug using FOF_NOCONFIRMATION!
				
		if (!SHFileOperation(&ops))
		{
			//make sure user did not abort
			if (!ops.fAnyOperationsAborted)
			{
				CString sMess;
				sMess.Format(IDS_DELETE_OK, ops.pFrom);
				AfxMessageBox(sMess);
				OnFileNew();
				m_bFontFlag = TRUE;
			}
			
			//This process does not remove filename from MRU list
					
		}		
		
		//SHFileOperation shows an error dialog in case of error, so I'm not handling it.
	
	//}
	
}


void CBIDSApp::OnAppExit()	//CMS
{	
	m_pView->GetDocument()->SetModifiedFlag(FALSE);	//CMS: don't want a save prompt
	
	CWinApp::OnAppExit();

}


void CBIDSApp::OnFileNew() 
{
	CWinApp::OnFileNew();
	
}


void CBIDSApp::OnFileProperties(void)	//CMS: show file properties dialog
{
	CPropsDialog dlgProps;

	dlgProps.DoModal();

}


void CBIDSApp::OnUpdateFileProperties(CCmdUI* pCmdUI)	//CMS
{
	pCmdUI->Enable(m_bDocHasBeenOpened);	//enable/disable File->Properties option
	
}


void CBIDSApp::OnUpdateFileDelete(CCmdUI* pCmdUI)	//CMS
{
	pCmdUI->Enable(m_bDocHasBeenOpened);	//enable/disable File->Delete option

	//CMS: The idea with the following call is to prevent MFC from showing
	//a Save dialog when making formatting changes and then
	//choosing a document from the MRU list.

	//This call will be made by MFC each time the File menu is clicked.

	m_pView->GetDocument()->SetModifiedFlag(FALSE);	//CMS: don't want a save prompt
	
}


BOOL CBIDSApp::GetFontFlag(void)	//CMS: public function to return flag
{
	return(m_bFontFlag);	//this flag says whether app should restore font to default

}

