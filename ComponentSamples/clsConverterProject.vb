Option Explicit On 

'Imports Microsoft.Office.Interop
Imports System.Runtime.InteropServices

Imports eDiscoveryConverterBaseClass.eDiscoveryConverterBase
Imports eDiscoveryObjectInterface
Imports eDiscoverySupportClasses.eDiscoverySupportSpace
Imports eDiscoveryServerConsoleSpace


Namespace eDiscoveryFileConverter_Project

    Public Class FileConverter_Project
        Inherits FileConverter_Base
        Implements IFileConverter_Template

        Private m_objProjectDoc As MSProject.Project
        Private m_objProjectApp As MSProject.Application

        Private Const APPLICATION_CLASS_DEFAULT As String = "MSProject.Application"
        Private Const PROCESS_NAME_DEFAULT As String = "WINPROJ"

        Private m_bDisposed As Boolean = False


        Public Sub New()
            MyBase.New()

            Initialize()

            m_sApplicationClass = APPLICATION_CLASS_DEFAULT
        End Sub


        Public Sub New(ByVal sSourceFileName_WithPath As String, _
            ByVal sTargetPathOnly As String, _
            Optional ByVal sUserID As String = USER_ID_DEFAULT, _
            Optional ByVal sPassword As String = PASSWORD_DEFAULT, _
            Optional ByVal eTargetFormat As ConverterTargets = ConverterTargets.FORMAT_DEFAULT, _
            Optional ByVal eImageQuality As ImageQuality = ImageQuality.QUALITY_DEFAULT, _
            Optional ByVal objSettings As Customization = Nothing, _
            Optional ByVal objApp As Object = Nothing, _
            Optional ByVal sPrinterName As String = PRINTER_NAME_DEFAULT, _
            Optional ByVal sPrinterCode As String = UNLOCK_KEY_DEFAULT, _
            Optional ByVal sHostName As String = HOST_NAME_DEFAULT)

            MyBase.New()

            Initialize()

            m_sSourceFilename_WithPath = sSourceFileName_WithPath.Trim
            m_sTargetPathOnly = sTargetPathOnly.Trim
            m_sUserID = sUserID.Trim
            m_sPassword = sPassword.Trim
            m_sApplicationClass = APPLICATION_CLASS_DEFAULT
            'm_objOLEApp = objApp

            m_sPrinterName = sPrinterName
            m_sPrinterCode = sPrinterCode

            m_sHostName = sHostName

            SetTargetFormat(eTargetFormat)  'sets image quality to default as well
            SetImageQuality(eImageQuality)
            SetCustomization(objSettings)

            SetOLEApp(objApp)       'Added 8/26

            Prepare()
        End Sub


        Public Overrides Sub Initialize() Implements IFileConverter_Template.Initialize
            ConverterCon.Write("{0}: {1}> ", m_sSourceFilename, DateTime.Now)
            ConverterCon.WriteLine("CONVERTER: INITIALIZE - BEGIN")

            MyBase.Initialize()

            ConverterCon.Write("{0}: {1}> ", m_sSourceFilename, DateTime.Now)
            ConverterCon.WriteLine("CONVERTER: INITIALIZE - END")
        End Sub


        Public Overrides Sub Prepare() Implements IFileConverter_Template.Prepare
            ConverterCon.Write("{0}: {1}> ", m_sSourceFilename, DateTime.Now)
            ConverterCon.WriteLine("CONVERTER: PREPARE - BEGIN")

            MyBase.Prepare()

            If m_bFatalErrorOccurred Then
                Exit Sub
            End If

            If m_bIsFileAvailable Then
                Dim bResult As Boolean

                m_objProjectApp = CType(m_objOLEApp, MSProject.Application)    'Easier to use boxed type
                m_objProjectApp.Visible = True  'DEBUGGING
                m_objProjectApp.WindowState = MSProject.PjWindowState.pjMinimized   'DEBUGGING



                Try     'how to trap errors that can occur here
                    ConverterCon.Write("{0}: {1}> ", m_sSourceFilename, DateTime.Now)
                    ConverterCon.WriteLine("CONVERTER: OPEN DOCUMENT - BEGIN")

                    bResult = m_objProjectApp.FileOpen(Name:=m_sSourceFileName_WithPath, _
                        ReadOnly:=True, _
                        IgnoreReadOnlyRecommended:=True)

                    m_objProjectDoc = m_objProjectApp.ActiveProject

                    ConverterCon.Write("{0}: {1}> ", m_sSourceFilename, DateTime.Now)
                    ConverterCon.WriteLine("CONVERTER: OPEN DOCUMENT - END")


                Catch
                    HandleException(m_sSourceFileName_WithPath, eDiscoveryErrorCode.ERROR_CODE_CANTOPENDOC, _
                       eDiscoveryErrorLevel.ERROR_LEVEL_FATAL)

                Finally

                End Try

                If m_bFatalErrorOccurred Then
                    Exit Sub
                End If

                m_bPrepared = True

            End If

            ConverterCon.Write("{0}: {1}> ", m_sSourceFilename, DateTime.Now)
            ConverterCon.WriteLine("CONVERTER: PREPARE - END")
        End Sub


        Protected Overrides Sub FormatFile() Implements IFileConverter_Template.FormatFile
            ConverterCon.Write("{0}: {1}> ", m_sSourceFilename, DateTime.Now)
            ConverterCon.WriteLine("CONVERTER: FORMATTING - BEGIN")

            If Not m_bPrepared Then
                Prepare()

            End If

            MyBase.FormatFile()

            If m_bIsFileAvailable Then
                m_bFormatted = True

            End If

            ConverterCon.Write("{0}: {1}> ", m_sSourceFilename, DateTime.Now)
            ConverterCon.WriteLine("CONVERTER: FORMATTING - END")
        End Sub


        Private Sub ProcessCommon(ByVal iView As Integer)
            'code called for both converting and extracting text            

            Dim bResult As Boolean
            
            Try
                'using try/catch becuase certain types of projects don't offer certain views

                m_objProjectApp.ViewApply(Name:=m_objProjectDoc.ViewList(iView))

                Try
                    bResult = m_objProjectApp.FilePrint(PageBreaks:=True, _
                        Draft:=True, _
                        Copies:=1, _
                        OnePageWide:=False, _
                        Preview:=False, _
                        Color:=False)
                    'ShowIEPrintDialog:=False)

                Catch
                    HandleException(m_sSourceFileName_WithPath, eDiscoveryErrorCode.ERROR_CODE_CANTPRINTDOC, _
                        eDiscoveryErrorLevel.ERROR_LEVEL_IGNORE)

                End Try


                'WaitForJob Moved here from ConvertFile because not every view is guarranteed printable
                WaitForJob()

            Catch
                HandleException(m_sSourceFileName_WithPath, eDiscoveryErrorCode.ERROR_CODE_MSPROJECT_VIEWNOTAPPLICABLE, _
                    eDiscoveryErrorLevel.ERROR_LEVEL_IGNORE)

            End Try
        End Sub


        Public Overrides Sub ConvertFile() Implements IFileConverter_Template.ConvertFile
            ConverterCon.Write("{0}: {1}> ", m_sSourceFilename, DateTime.Now)
            ConverterCon.WriteLine("CONVERTER: CONVERTING - BEGIN")

            Dim bRemotingErrorOccurred As Boolean

            ConverterCon.WriteLine("CONVERTER: WAITING FOR PRINT CONTROLLER")

            WaitForController(bRemotingErrorOccurred)

            If Not m_bPrintProcessChecked Then  'Submit request to restart printing service - if appropriate
                PrintControllerClient.RestartPrintingProcessIf(bRemotingErrorOccurred)
                m_bPrintProcessChecked = True   'Set flag so that this is only done once per object lifetime

            End If


            'print/convert document

            Dim iCounter As Integer

            'Iterate over all available views - ForEach not working
            For iCounter = 1 To m_objProjectDoc.ViewList.Count

                'ConverterCon.WriteLine("VIEW: {0}", m_objProjectDoc.ViewList(iCounter))

                'Check if current view in loop is part of Custimization settings
                If m_objSettings.GetValue(CustomizableProperties.MSPROJECT_VIEWS).SettingsList.ContainsKey(m_objProjectDoc.ViewList(iCounter)) Then

                    'ConverterCon.WriteLine("GOT ONE: {0}", m_objProjectDoc.ViewList(iCounter))

                    InitPrintDriver()

                    If Not m_bFormatted Then
                        FormatFile()

                    End If

                    If Not m_bPrepared Then
                        Prepare()

                    End If

                    If m_bIsFileAvailable Then
                        MyBase.ConvertFile()

                        ProcessCommon(iCounter)                        
                    End If

                End If

            Next iCounter

            ConverterCon.Write("{0}: {1}> ", m_sSourceFilename, DateTime.Now)
            ConverterCon.WriteLine("CONVERTER: CONVERTING - END")
        End Sub


        Public Overrides Sub ExtractText() Implements IFileConverter_Template.ExtractText
            ConverterCon.Write("{0}: {1}> ", m_sSourceFilename, DateTime.Now)
            ConverterCon.WriteLine("CONVERTER: EXTRACTING TEXT - BEGIN")

            Dim bRemotingErrorOccurred As Boolean

            ConverterCon.WriteLine("CONVERTER: WAITING FOR PRINT CONTROLLER")

            WaitForController(bRemotingErrorOccurred)

            If Not m_bPrintProcessChecked Then  'Submit request to restart printing service - if appropriate
                PrintControllerClient.RestartPrintingProcessIf(bRemotingErrorOccurred)
                m_bPrintProcessChecked = True   'Set flag so that this is only done once per object lifetime

            End If


            'extract text

            Dim iCounter As Integer

            'Iterate over all available views - ForEach not working
            For iCounter = 1 To m_objProjectDoc.ViewList.Count

                'ConverterCon.WriteLine("VIEW: {0}", m_objProjectDoc.ViewList(iCounter))

                'Check if current view in loop is part of Custimization settings
                If m_objSettings.GetValue(CustomizableProperties.MSPROJECT_VIEWS).SettingsList.ContainsKey(m_objProjectDoc.ViewList(iCounter)) Then

                    'ConverterCon.WriteLine("GOT ONE: {0}", m_objProjectDoc.ViewList(iCounter))

                    InitPrintDriver()

                    If Not m_bFormatted Then
                        FormatFile()

                    End If

                    If Not m_bPrepared Then
                        Prepare()

                    End If

                    If m_bIsFileAvailable Then
                        MyBase.ExtractText()

                        ProcessCommon(iCounter)                        

                    End If

                End If

            Next iCounter

            m_bTextExtracted = True     'Set flag for GetMetaData call

            ConverterCon.Write("{0}: {1}> ", m_sSourceFilename, DateTime.Now)
            ConverterCon.WriteLine("CONVERTER: EXTRACTING TEXT - END")
        End Sub


        Public Overrides Sub ExtractMetaData() Implements IFileConverter_Template.ExtractMetaData
            ConverterCon.Write("{0}: {1}> ", m_sSourceFilename, DateTime.Now)
            ConverterCon.WriteLine("CONVERTER: EXTRACT BASIC METADATA - BEGIN")

            If m_bIsFileAvailable Then
                MyBase.ExtractMetaData()

                'Now get metadata for this document type

                ExtractMSOfficeMetaData(CType(m_objProjectDoc, Object))    'Get Office App data

                Exit Sub
                

                Dim iCount As Integer
                Dim sKey As String
                Dim sValue As String

                'ConverterCon.WriteLine("DOC PROPERTIES LENGTH: {0}", m_objProjectDoc.BuiltinDocumentProperties.Count)

                For iCount = 1 To m_objProjectDoc.BuiltinDocumentProperties.Count
                    Try
                        sKey = CStr(m_objProjectDoc.BuiltinDocumentProperties(iCount).Name).Trim.ToUpper
                        sValue = CStr(m_objProjectDoc.BuiltinDocumentProperties(iCount).Value).Trim

                        If sValue <> String.Empty Then  'Only add non-empty values
                            If m_hshMetadataLookup.ContainsKey(sKey) Then
                                m_objMetaData.Add(m_hshMetadataLookup.Item(sKey), sValue)

                            End If
                        End If                        
                    Catch

                    End Try

                Next

            End If

            ConverterCon.Write("{0}: {1}> ", m_sSourceFilename, DateTime.Now)
            ConverterCon.WriteLine("CONVERTER: EXTRACT BASIC METADATA - END")
        End Sub


        ' Dispose(disposing As Boolean) executes in two distinct scenarios.
        ' If disposing is true, the method has been called directly 
        ' or indirectly by a user's code. Managed and unmanaged resources 
        ' can be disposed.
        ' If disposing equals false, the method has been called by the runtime
        ' from inside the finalizer and you should not reference other    
        ' objects. Only unmanaged resources can be disposed.
        Protected Overloads Overrides Sub Dispose(ByVal bDisposing As Boolean)

            ' Check to see if Dispose has already been called.
            If Not Me.m_bDisposed Then
                ConverterCon.Write("{0}: {1}> ", m_sSourceFilename, DateTime.Now)
                ConverterCon.WriteLine("CONVERTER: PROTECTED DISPOSE CALLED")

                ' If disposing equals true, dispose all managed and unmanaged resources.

                Try
                    ' ***Release unmanaged resources here

                    'Closing of document and application are combined into one statement

                    If m_bCloseAppWhenDone And Not IsNothing(m_objOLEApp) Then
                        Try
                            ConverterCon.Write("{0}: {1}> ", m_sSourceFilename, DateTime.Now)
                            ConverterCon.WriteLine("CONVERTER : CLOSE DOCUMENT - BEGIN")

                            m_objProjectApp.Quit(SaveChanges:=MSProject.PjSaveType.pjDoNotSave)

                            Marshal.ReleaseComObject(m_objOLEApp)
                            Marshal.ReleaseComObject(m_objProjectApp)

                            ConverterCon.Write("{0}: {1}> ", m_sSourceFilename, DateTime.Now)
                            ConverterCon.WriteLine("CONVERTER : CLOSE DOCUMENT - END")

                            ConverterCon.Write("{0}: {1}> ", m_sSourceFilename, DateTime.Now)
                            ConverterCon.WriteLine("CONVERTER: SHUT DOWN APPLICATION {0}", m_sApplicationClass)

                        Catch
                            'Will fail if process already killed by base class
                        End Try


                    End If

                    m_objOLEApp = Nothing
                    m_objProjectApp = Nothing


                    If (bDisposing) Then
                        ' ***Release managed resources here


                    End If


                    Me.m_bDisposed = True

                Finally
                    MyBase.Dispose(bDisposing)


                End Try

            End If

        End Sub

    End Class

End Namespace
