// bidsprntdoc.cpp : implementation of the CBIDSDoc class
//
/*
	CMS 2/19/99

	Most of this code is provided by Microsoft.  I only removed some unneeded
	functionality.
*/

#include "stdafx.h"
#include "bidsprnt.h"
#include "bidsprntdoc.h"
#include "bidsprntview.h"
#include "doctype.h"
#include "chicdial.h"
#include "cntritem.h"
#include "srvritem.h"
#include "formatba.h"
#include "mainfrm.h"
#include "ipframe.h"
#include "helpids.h"
#include "strings.h"
#include "docopt.h"


#ifdef _DEBUG
#undef THIS_FILE
static char BASED_CODE THIS_FILE[] = __FILE__;
#endif

extern BOOL AFXAPI AfxFullPath(LPTSTR lpszPathOut, LPCTSTR lpszFileIn);
extern UINT AFXAPI AfxGetFileTitle(LPCTSTR lpszPathName, LPTSTR lpszTitle, UINT nMax);

#ifndef OFN_EXPLORER
#define OFN_EXPLORER 0x00080000L
#endif
/////////////////////////////////////////////////////////////////////////////
// CBIDSDoc
IMPLEMENT_DYNCREATE(CBIDSDoc, CRichEditDoc)

BEGIN_MESSAGE_MAP(CBIDSDoc, CRichEditDoc)
	//{{AFX_MSG_MAP(CBIDSDoc)
	//}}AFX_MSG_MAP
END_MESSAGE_MAP()

/////////////////////////////////////////////////////////////////////////////
// CBIDSDoc construction/destruction

CBIDSDoc::CBIDSDoc()
{
	m_nDocType = -1;
	m_nNewDocType = -1;

}


BOOL CBIDSDoc::OnNewDocument()
{
	if (!CRichEditDoc::OnNewDocument())
		return FALSE;

 	//correct type already set in theApp.m_nNewDocType;
 	int nDocType = (IsEmbedded()) ? RD_EMBEDDED : theApp.m_nNewDocType;

	GetView()->SetDefaultFont(IsTextType(nDocType));
	SetDocType(nDocType);
	
	theApp.m_bDocHasBeenOpened = FALSE;		//CMS

	m_pDocFile = NULL;	//CMS: init pointer

	return TRUE;

}


void CBIDSDoc::ReportSaveLoadException(LPCTSTR lpszPathName,
	CException* e, BOOL bSaving, UINT nIDP)
{
	if (!m_bDeferErrors && e != NULL)
	{
		ASSERT_VALID(e);
		if (e->IsKindOf(RUNTIME_CLASS(CFileException)))
		{
			switch (((CFileException*)e)->m_cause)
			{
			case CFileException::fileNotFound:
			case CFileException::badPath:
				nIDP = AFX_IDP_FAILED_INVALID_PATH;
				break;
			case CFileException::diskFull:
				nIDP = AFX_IDP_FAILED_DISK_FULL;
				break;
			case CFileException::accessDenied:
				nIDP = bSaving ? AFX_IDP_FAILED_ACCESS_WRITE :
						AFX_IDP_FAILED_ACCESS_READ;
				if (((CFileException*)e)->m_lOsError == ERROR_WRITE_PROTECT)
					nIDP = IDS_WRITEPROTECT;
				break;
			case CFileException::tooManyOpenFiles:
				nIDP = IDS_TOOMANYFILES;
				break;
			case CFileException::directoryFull:
				nIDP = IDS_DIRFULL;
				break;
			case CFileException::sharingViolation:
				nIDP = IDS_SHAREVIOLATION;
				break;
			case CFileException::lockViolation:
			case CFileException::badSeek:
			case CFileException::generic:
			case CFileException::invalidFile:
			case CFileException::hardIO:
				nIDP = bSaving ? AFX_IDP_FAILED_IO_ERROR_WRITE :
						AFX_IDP_FAILED_IO_ERROR_READ;
				break;
			default:
				break;
			}
			CString prompt;
			AfxFormatString1(prompt, nIDP, lpszPathName);
			AfxMessageBox(prompt, MB_ICONEXCLAMATION, nIDP);
			return;
		}
	}

	CRichEditDoc::ReportSaveLoadException(lpszPathName, e, bSaving, nIDP);
	return;
}


BOOL CBIDSDoc::OnOpenDocument(LPCTSTR lpszPathName) 
{

	if (m_lpRootStg != NULL) // we are embedded
	{
		// we really want to use the converter on this storage
		m_nNewDocType = RD_EMBEDDED;
	}
	else	
		m_nNewDocType = RD_TEXT;	//CMS	

	if (!CRichEditDoc::OnOpenDocument(lpszPathName))
		return FALSE;
		
	theApp.m_sDataFileName = (CString) lpszPathName;
	theApp.m_sDataFileName.MakeUpper();

	theApp.m_bDocHasBeenOpened = TRUE;

	m_pDocFile = NULL;	//CMS: init pointer
			
	return(TRUE);

}


void CBIDSDoc::Serialize(CArchive& ar)
{
	COleMessageFilter* pFilter = AfxOleGetMessageFilter();
	ASSERT(pFilter != NULL);
	pFilter->EnableBusyDialog(FALSE);
	if (ar.IsLoading())
		SetDocType(m_nNewDocType);
	CRichEditDoc::Serialize(ar);
	pFilter->EnableBusyDialog(TRUE);
}


class COIPF : public COleIPFrameWnd
{
public:
	CFrameWnd* GetMainFrame() { return m_pMainFrame;}
	CFrameWnd* GetDocFrame() { return m_pDocFrame;}
};


void CBIDSDoc::OnDeactivateUI(BOOL bUndoable)
{
	if (GetView()->m_bDelayUpdateItems)
		UpdateAllItems(NULL);

	SaveState(m_nDocType);
	CRichEditDoc::OnDeactivateUI(bUndoable);
	COIPF* pFrame = (COIPF*)m_pInPlaceFrame;

	if (pFrame != NULL)
	{
		if (pFrame->GetMainFrame() != NULL)
			ForceDelayed(pFrame->GetMainFrame());

		if (pFrame->GetDocFrame() != NULL)
			ForceDelayed(pFrame->GetDocFrame());
	}
}


void CBIDSDoc::ForceDelayed(CFrameWnd* pFrameWnd)
{
	ASSERT_VALID(this);
	ASSERT_VALID(pFrameWnd);

	POSITION pos = pFrameWnd->m_listControlBars.GetHeadPosition();
	while (pos != NULL)
	{
		// show/hide the next control bar
		CControlBar* pBar =
			(CControlBar*)pFrameWnd->m_listControlBars.GetNext(pos);

		BOOL bVis = pBar->GetStyle() & WS_VISIBLE;
		UINT swpFlags = 0;
		if ((pBar->m_nStateFlags & CControlBar::delayHide) && bVis)
			swpFlags = SWP_HIDEWINDOW;
		else if ((pBar->m_nStateFlags & CControlBar::delayShow) && !bVis)
			swpFlags = SWP_SHOWWINDOW;
		pBar->m_nStateFlags &= ~(CControlBar::delayShow|CControlBar::delayHide);
		if (swpFlags != 0)
		{
			pBar->SetWindowPos(NULL, 0, 0, 0, 0, swpFlags|
				SWP_NOMOVE|SWP_NOSIZE|SWP_NOZORDER|SWP_NOACTIVATE);
		}
	}
}

/////////////////////////////////////////////////////////////////////////////
// CBIDSDoc Attributes
CLSID CBIDSDoc::GetClassID()
{
	return (m_pFactory == NULL) ? CLSID_NULL : m_pFactory->GetClassID();
}


void CBIDSDoc::SetDocType(int nNewDocType, BOOL bNoOptionChange)
{
	ASSERT(nNewDocType != -1);
	if (nNewDocType == m_nDocType)
		return;

	m_bRTF = !IsTextType(nNewDocType);
	if (bNoOptionChange)
		m_nDocType = nNewDocType;
	else
	{
		SaveState(m_nDocType);
		m_nDocType = nNewDocType;
		RestoreState(m_nDocType);
	}
}


CBIDSView* CBIDSDoc::GetView()
{
	POSITION pos = GetFirstViewPosition();
	return (CBIDSView* )GetNextView( pos );
}

/////////////////////////////////////////////////////////////////////////////
// CBIDSDoc Operations

CRichEditCntrItem *CBIDSDoc::CreateClientItem(REOBJECT* preo) const
{
	// cast away constness of this
	return new CBIDSPrintCntrItem (preo, (CBIDSDoc *)this);
}

/////////////////////////////////////////////////////////////////////////////
// CBIDSDoc server implementation

COleServerItem *CBIDSDoc::OnGetEmbeddedItem()
{
	// OnGetEmbeddedItem is called by the framework to get the COleServerItem
	//  that is associated with the document.  It is only called when necessary.

	CEmbeddedItem* pItem = new CEmbeddedItem(this);
	ASSERT_VALID(pItem);
	return pItem;
}

/////////////////////////////////////////////////////////////////////////////
// CBIDSDoc serialization

/////////////////////////////////////////////////////////////////////////////
// CBIDSDoc diagnostics

#ifdef _DEBUG
void CBIDSDoc::AssertValid() const
{
	CRichEditDoc::AssertValid();
}


void CBIDSDoc::Dump(CDumpContext& dc) const
{
	CRichEditDoc::Dump(dc);
}
#endif //_DEBUG

/////////////////////////////////////////////////////////////////////////////
// CBIDSDoc commands

int CBIDSDoc::MapType(int nType)
{
	if (nType == RD_OEMTEXT)
		nType = RD_TEXT;	

	return nType;
}


BOOL CBIDSDoc::OnCmdMsg(UINT nID, int nCode, void* pExtra, AFX_CMDHANDLERINFO* pHandlerInfo) 
{
	if (nCode == CN_COMMAND && nID == ID_OLE_VERB_POPUP)
		nID = ID_OLE_VERB_FIRST;

	return CRichEditDoc::OnCmdMsg(nID, nCode, pExtra, pHandlerInfo);
}


void CBIDSDoc::SaveState(int nType)
{
	if (nType == -1)
		return;

	nType = MapType(nType);
	CBIDSView* pView = GetView();

	if (pView != NULL)
	{
		CFrameWnd* pFrame = pView->GetParentFrame();
		ASSERT(pFrame != NULL);
		// save current state
		pFrame->SendMessage(WPM_BARSTATE, 0, nType);
	}
}


void CBIDSDoc::RestoreState(int nType)
{
	if (nType == -1)
		return;
	nType = MapType(nType);
	CBIDSView* pView = GetView();
	if (pView != NULL)
	{
		CFrameWnd* pFrame = pView->GetParentFrame();
		ASSERT(pFrame != NULL);
		// set new state 
		pFrame->SendMessage(WPM_BARSTATE, 1, nType);
	}
}


void CBIDSDoc::OnCloseDocument() 
{
	SaveState(m_nDocType);
	CRichEditDoc::OnCloseDocument();
	
}


void CBIDSDoc::PreCloseFrame(CFrameWnd* pFrameArg)
{

	CRichEditDoc::PreCloseFrame(pFrameArg);
	SaveState(m_nDocType);
}

