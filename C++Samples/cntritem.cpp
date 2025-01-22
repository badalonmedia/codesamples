// cntritem.cpp : implementation of the CBIDSPrintCntrItem class
//

#include "stdafx.h"
#include "bidsprnt.h"
#include "bidsprntdoc.h"
#include "bidsprntview.h"
#include "cntritem.h"

#ifdef _DEBUG
#undef THIS_FILE
static char BASED_CODE THIS_FILE[] = __FILE__;
#endif

/////////////////////////////////////////////////////////////////////////////
// CBIDSPrintCntrItem implementation

IMPLEMENT_SERIAL(CBIDSPrintCntrItem , CRichEditCntrItem, 0)

CBIDSPrintCntrItem ::CBIDSPrintCntrItem (REOBJECT *preo, CBIDSDoc* pContainer)
	: CRichEditCntrItem(preo, pContainer)
{
}

/////////////////////////////////////////////////////////////////////////////
// CBIDSPrintCntrItem diagnostics

#ifdef _DEBUG
void CBIDSPrintCntrItem ::AssertValid() const
{
	CRichEditCntrItem::AssertValid();
}

void CBIDSPrintCntrItem ::Dump(CDumpContext& dc) const
{
	CRichEditCntrItem::Dump(dc);
}
#endif

/////////////////////////////////////////////////////////////////////////////
