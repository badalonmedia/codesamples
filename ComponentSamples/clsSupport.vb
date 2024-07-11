Option Explicit On 
Option Strict On

Imports System
Imports System.IO
Imports Microsoft.Win32
Imports System.Runtime.InteropServices
Imports System.Management
Imports System.Runtime.Remoting
Imports System.Runtime.Remoting.Lifetime
Imports System.Runtime.Remoting.Channels
Imports System.Runtime.Remoting.Channels.Tcp
Imports System.Net


Namespace EDDSupportSpace

    Public Class Network

        Public Shared Function GetHostName() As String
            Try
                GetHostName = Dns.GetHostName()

            Catch
                GetHostName = ""    'error

            End Try
        End Function

    End Class


    Public Class Win32API
        <DllImport("shell32.dll")> _
        Public Shared Function ShellExecute(ByVal hwnd As Integer, _
            ByVal lpszOp As String, _
            ByVal lpszFile As String, _
            ByVal lpszParams As String, _
            ByVal lpszDir As String, _
            ByVal FsShowCmd As Integer) As Integer
        End Function

        <DllImport("user32.dll")> _
        Public Shared Function FindWindow(ByVal lpClassName As String, _
            ByVal lpWindowName As String) As Long
        End Function

        <DllImport("kernel32.dll")> _
        Public Shared Function CopyFile(ByVal lpExistingFileName As String, ByVal lpNewFileName As String, _
            ByVal bFailIfExists As Long) As Long
        End Function


        <DllImport("kernel32.dll")> _
        Public Shared Function DeleteFile(ByVal lpFileName As String) As Long

        End Function
        

        Declare Function FindExecutable Lib "shell32.dll" Alias _
            "FindExecutableA" (ByVal lpFile As String, ByVal lpDirectory As _
            String, ByVal lpResult As String) As Long

    End Class


    'WMI/Print Queue support code
    Public Class PrintHelper

        Public Shared Function IsInQueue(ByVal iJobID As Integer, ByRef objJobProps As PropertyDataCollection) As Boolean
            IsInQueue = False

            Dim objQuery As ObjectQuery
            Dim objSearcher As ManagementObjectSearcher
            Dim objItems As ManagementObjectCollection
            Dim objItem As ManagementObject

            Try                
                objQuery = New ObjectQuery("SELECT * FROM Win32_PrintJob")
                objSearcher = New ManagementObjectSearcher(objQuery)
                objItems = objSearcher.Get()

                For Each objItem In objItems                    
                    'Console.WriteLine("SEARCHING JOB ID: {0}", objItem("JobId"))

                    If CType(objItem("JobId"), UInt32).ToString = iJobID.ToString Then
                        Console.WriteLine("JOB ID: {0}", iJobID)

                        objJobProps = objItem.Properties    'Send back job properties

                        objSearcher.Dispose()   
                        objSearcher = Nothing   
                        objQuery = Nothing

                        IsInQueue = True
                        Exit Function
                    End If
                Next

            Catch objEx As ManagementException
                Console.Write("{0}: > ", DateTime.Now)
                Console.WriteLine("NUMPRINTJOBS: MANAGEMENT EXCEPTION {0}", objEx.Message)

            Catch objEx As Exception
                Console.Write("{0}: > ", DateTime.Now)
                Console.WriteLine("NUMPRINTJOBS: EXCEPTION {0}", objEx.Message)
            End Try

            If Not IsNothing(objSearcher) Then
                objSearcher.Dispose()
                objSearcher = Nothing
                objQuery = Nothing
            End If

        End Function


        Public Overloads Shared Function NumPrintJobs(ByVal sQueue As String) As Integer
            If sQueue = "" Then
                Return NumPrintJobs()

            Else
                Dim objQuery As ObjectQuery
                Dim objSearcher As ManagementObjectSearcher
                Dim objItems As ManagementObjectCollection
                Dim objItem As ManagementObject
                Dim iNumJobs As Integer = 0

                Try
                    objQuery = New ObjectQuery("SELECT * FROM Win32_PrintJob")

                    'Would like to use WHERE clause, but printer name does not exactly match NAME field.
                    'Could not get LIKE operator to work.

                    objSearcher = New ManagementObjectSearcher(objQuery)
                    objItems = objSearcher.Get()

                    'NumPrintJobs = objItems.Count   'Count method not working?  ??

                    For Each objItem In objItems    'So do it the hard way
                        If CType(objItem("Name"), String).StartsWith(sQueue) Then
                            iNumJobs += 1

                        End If
                    Next

                Catch objEx As ManagementException
                    iNumJobs = -1   'Return error

                    Console.Write("{0}: > ", DateTime.Now)
                    Console.WriteLine("NUMPRINTJOBS: MANAGEMENT EXCEPTION {0}", objEx.Message)

                Catch objEx As Exception
                    iNumJobs = -1   'Return error

                    Console.Write("{0}: > ", DateTime.Now)
                    Console.WriteLine("NUMPRINTJOBS: EXCEPTION {0}", objEx.Message)
                End Try

                If Not IsNothing(objSearcher) Then
                    objSearcher.Dispose()
                    objSearcher = Nothing
                End If

                NumPrintJobs = iNumJobs
            End If

        End Function


        Public Overloads Shared Function NumPrintJobs() As Integer
            Dim objQuery As ObjectQuery
            Dim objSearcher As ManagementObjectSearcher
            Dim objItems As ManagementObjectCollection
            Dim objItem As ManagementObject
            Dim iNumJobs As Integer = 0

            Try
                objQuery = New ObjectQuery("SELECT * FROM Win32_PrintJob")
                objSearcher = New ManagementObjectSearcher(objQuery)
                objItems = objSearcher.Get()

                'NumPrintJobs = objItems.Count   'Count method not working?  ??

                For Each objItem In objItems    'So do it the hard way
                    iNumJobs += 1
                Next

            Catch objEx As ManagementException
                iNumJobs = -1   'Return error

                Console.Write("{0}: > ", DateTime.Now)
                Console.WriteLine("NUMPRINTJOBS: MANAGEMENT EXCEPTION {0}", objEx.Message)

            Catch objEx As Exception
                iNumJobs = -1   'Return error

                Console.Write("{0}: > ", DateTime.Now)
                Console.WriteLine("NUMPRINTJOBS: EXCEPTION {0}", objEx.Message)

            End Try

            If Not IsNothing(objSearcher) Then
                objSearcher.Dispose()
                objSearcher = Nothing
            End If

            NumPrintJobs = iNumJobs

        End Function


        Public Shared Function PrintJobs() As ManagementObjectCollection
            PrintJobs = Nothing     'Return error

            Dim objQuery As ObjectQuery
            Dim objSearcher As ManagementObjectSearcher

            Try
                objQuery = New ObjectQuery("SELECT * FROM Win32_PrintJob")
                objSearcher = New ManagementObjectSearcher(objQuery)
                PrintJobs = objSearcher.Get()

            Catch objEx As ManagementException
                PrintJobs = Nothing     'Return error
                Console.Write("{0}: > ", DateTime.Now)
                Console.WriteLine("PRINTJOBS: MANAGEMENT EXCEPTION {0}", objEx.Message)

            Catch objEx As Exception
                PrintJobs = Nothing     'Return error
                Console.Write("{0}: > ", DateTime.Now)
                Console.WriteLine("PRINTJOBS: EXCEPTION {0}", objEx.Message)

            End Try

            If Not IsNothing(objSearcher) Then
                objSearcher.Dispose()
                objSearcher = Nothing
            End If

            'Instruct caller to call Dispose on returned object            

        End Function

    End Class


    Public Class DebugHelper

        Public Shared Sub ShowExceptionStack(ByVal objException As Exception)
            'Dump all exception information to Console.Out
            Dim objEx As Exception

            Console.WriteLine("EXCEPTION TEXT: {0}", objException.Message)

            objEx = objException.InnerException

            Do While Not IsNothing(objEx)
                Console.WriteLine("EXCEPTION STACK: {0}", objEx.Message)

                objEx = objEx.InnerException
            Loop

        End Sub

    End Class


    Public Class ProcessManager
        'Implements IDisposable
        Private m_hshPrior As Hashtable
        Private m_hshPost As Hashtable
        Private m_hshDiff As Hashtable

        Public Shared NON_PROCESS_ID As Integer = -1


        Public Sub New()
            m_hshPrior = New Hashtable()
            m_hshPost = New Hashtable()
            m_hshDiff = New Hashtable()
        End Sub

        Public Sub SaveStatePrior()
            SaveState("", m_hshPrior)
        End Sub


        Public Sub SaveStatePrior(ByVal sName As String)
            SaveState(sName, m_hshPrior)
        End Sub


        Public Sub SaveStatePost()
            SaveState("", m_hshPost)
        End Sub


        Public Sub SaveStatePost(ByVal sName As String)
            SaveState(sName, m_hshPost)
        End Sub


        'Determine number of processes started between calls
        Public Function DifferenceCount() As Integer
            Return (m_hshPost.Count - m_hshPrior.Count)
        End Function


        Public Function BaseProcessID() As Integer
            BaseProcessID = NON_PROCESS_ID

            Dim objProcessEntry As DictionaryEntry

            'Make sure to call after calling both SaveStatePrior and SaveStatePost

            'Traverse Post hashtable and look for keys not occurring in Prior hashtable
            For Each objProcessEntry In m_hshPost
                If Not m_hshPrior.ContainsKey(objProcessEntry.Key) Then
                    BaseProcessID = CInt(objProcessEntry.Key)
                    Exit For
                End If
            Next

        End Function


        'Public Sub KillDifference()
        '    Dim objProcessEntry As DictionaryEntry
        '    Dim objProcess As Process

        '    'Make sure to call after calling both SaveStatePrior and SaveStatePost

        '    'Caution when calling this routine, it will kill all processes started between 
        '    'calling both SaveStatePrior and SaveStatePost

        '    For Each objProcessEntry In m_hshPost
        '        If Not m_hshPrior.ContainsKey(objProcessEntry.Key) Then
        '            objProcess = New Process()
        '            objProcess.GetProcessById(CType(objProcessEntry.Key, Integer))

        '            Try
        '                objProcess.Kill()

        '            Catch

        '            End Try
        '        End If
        '    Next
        'End Sub


        Private Sub SaveState(ByVal sName As String, ByRef hshProcesses As Hashtable)
            Dim arrProcesses As Process()
            Dim objProcess As Process

            If Not IsNothing(hshProcesses) Then
                hshProcesses.Clear()
                hshProcesses = Nothing
            End If

            hshProcesses = New Hashtable()

            If sName.Trim <> String.Empty Then
                arrProcesses = Process.GetProcessesByName(sName.Trim.ToUpper)
            Else
                arrProcesses = Process.GetProcesses()   'Get all processes
            End If

            For Each objProcess In arrProcesses
                ' Console.WriteLine("PROCESS: {0}", objProcess.MainModule)

                hshProcesses.Add(objProcess.Id, objProcess)
            Next
        End Sub


        Public Sub ShutDown()
            m_hshPrior = Nothing
            m_hshPost = Nothing
            m_hshDiff = Nothing
        End Sub

    End Class


    '<Serializable()> _
    'Public Enum EDDMetaDataExtent
    '    METADATA_BASICONLY = 0
    '    METADATA_ALL = 1
    '    METADATA_ALLOVERRIDE = 2    'extract text even if already done

    'End Enum


    <Serializable()> _
    Public Enum ExtractorMode
        EXTRACTOR_MODE_DEFAULT = 2

        EXTRACTOR_MODE_METADATAONLY = 0
        EXTRACTOR_MODE_EXTRACTONLY = 1
        EXTRACTOR_MODE_ALL = 2
    End Enum


    <Serializable()> _
    Public Enum EDDMetaDataID
        'Basic MetaData for all file types

        METADATA_ID_FILEDATECREATED = 1
        METADATA_ID_FILEDATEMODIFIED = 2
        METADATA_ID_FILEDATEACCESSED = 3
        METADATA_ID_FILESIZEBYTES = 4
        METADATA_ID_FILEATTRIBDIRECTORY = 5
        METADATA_ID_FILEATTRIBCOMPRESSED = 6
        METADATA_ID_FILEATTRIBHIDDEN = 7
        METADATA_ID_FILEATTRIBARCHIVE = 8
        METADATA_ID_FILEATTRIBENCRYPTED = 9
        METADATA_ID_FILEATTRIBNORMAL = 10
        METADATA_ID_FILEATTRIBREADONLY = 11
        METADATA_ID_FILEATTRIBSYSTEM = 12
        METADATA_ID_FILEATTRIBTEMP = 13     'temp file
        METADATA_ID_FILEMD5 = 14            'MD5 signature


        'Extended meta data for certain doc types - in general from BuiltInDocumentProperties object

        METADATA_ID_DOCTITLE = 201
        METADATA_ID_DOCSUBJECT = 202
        METADATA_ID_DOCAUTHOR = 203
        METADATA_ID_DOCKEYWORDS = 204
        METADATA_ID_DOCCOMMENTS = 205
        METADATA_ID_DOCTEMPLATE = 206
        METADATA_ID_DOCREVISION = 207
        METADATA_ID_DOCAPPNAME = 208
        METADATA_ID_DOCDATELASTPRINTED = 209
        METADATA_ID_DOCDATECREATED = 210
        METADATA_ID_DOCDATESAVED = 211
        METADATA_ID_DOCEDITINGTIME = 212  'most recent save date
        METADATA_ID_DOCNUMPAGES = 213
        METADATA_ID_DOCNUMWORDS = 214
        METADATA_ID_DOCNUMCHARS = 215
        METADATA_ID_DOCSECURITY = 216
        METADATA_ID_DOCMGR = 217
        METADATA_ID_DOCCOMPANY = 218
        METADATA_ID_DOCNUMLINES = 219
        METADATA_ID_DOCNUMBYTES = 220
        METADATA_ID_DOCNUMPARS = 221
        METADATA_ID_DOCNUMSLIDES = 222
        METADATA_ID_DOCNUMNOTES = 223
        METADATA_ID_DOCNUMHIDDENSLIDES = 224
        METADATA_ID_DOCNUMMEDIACLIPS = 225
        METADATA_ID_DOCNUMCHARSWSPACES = 226
        METADATA_ID_DOCCATEGORY = 227
        METADATA_ID_DOCFORMAT = 228
        METADATA_ID_DOCLASTAUTHOR = 229
        METADATA_ID_DOCTYPE = 230



        'Extended/Child MetaData used by certain extractors 

        METADATA_ID_MESSAGESUBJECT = 1001
        METADATA_ID_MESSAGESENDER = 1002
        METADATA_ID_MESSAGETO = 1003
        METADATA_ID_MESSAGECC = 1004
        METADATA_ID_MESSAGEBCC = 1005
        METADATA_ID_MESSAGEBODY = 1006
        METADATA_ID_MESSAGECREATEDATE = 1007
        METADATA_ID_MESSAGEMODIFIEDDATE = 1008
        METADATA_ID_MESSAGESENTDATE = 1009
        METADATA_ID_MESSAGEID = 1010
        METADATA_ID_MESSAGERECEIVEDATE = 1011
        METADATA_ID_MESSAGECLASS = 1012     'type of item - mail, journal, etc.
        METADATA_ID_MESSAGEFOLDERID = 1013
        METADATA_ID_MESSAGETHREADTOPIC = 1014
        METADATA_ID_MESSAGETHREADINDEX = 1015
        METADATA_ID_MESSAGEINSENTFOLDER = 1016
        METADATA_ID_MESSAGENUMBYTES = 1017
        METADATA_ID_MESSAGEPARENTID = 1018


        'Custom metadata - use is TBD
        METADATA_ID_CUSTOM1 = 5001
        METADATA_ID_CUSTOM2 = 5002
        METADATA_ID_CUSTOM3 = 5003
        METADATA_ID_CUSTOM4 = 5004
        METADATA_ID_CUSTOM5 = 5005
        METADATA_ID_CUSTOM6 = 5006
        METADATA_ID_CUSTOM7 = 5007
        METADATA_ID_CUSTOM8 = 5008
        METADATA_ID_CUSTOM9 = 5009
        METADATA_ID_CUSTOM10 = 5010


        'Metadata returned only by certain applications
        METADATA_ID_ZIPENTRYCOMMENT = 6001

        METADATA_ID_NOTESAUTHORLIST = 7001
        METADATA_ID_NOTESPRINCIPAL = 7002

    End Enum
    


    <Serializable()> _
    Public Class EDDMetaData
        Implements IDisposable

        Private m_sDateCreated As String
        'Private m_dtDateCreated As DateTime
        'Private m_lFileLength As Long
        'Private m_objFileProps As FileAttributes
        'Private m_arrTextContentFiles As Array

        Private m_hshMeta As Hashtable
        Private m_bDisposed As Boolean = False


        Public Sub New()
            m_hshMeta = New Hashtable()
        End Sub


        'Construct path of child MetaData file from path of data file
        Public Function ConstructPath(ByVal sPath As String) As String
            ConstructPath = sPath & ".METADATA.XML"
        End Function


        Public Sub Add(ByVal eKey As EDDMetaDataID, ByVal sEntry As String)
            If m_hshMeta.ContainsKey(eKey) Then
                m_hshMeta.Remove(eKey)

            End If

            m_hshMeta.Add(eKey, sEntry)
        End Sub


        Public Function GetValue(ByVal eKey As EDDMetaDataID) As String
            If m_hshMeta.ContainsKey(eKey) Then
                GetValue = m_hshMeta(eKey).ToString
            Else
                GetValue = String.Empty

            End If
        End Function


        Public Function GetCollection() As Hashtable
            GetCollection = m_hshMeta
        End Function


        'Provide a Count method
        Public Function Count() As Integer
            Count = m_hshMeta.Count
        End Function


        Public Sub ExtractBasicMetaData(ByVal sPath As String)
            Dim objFileInfo As New FileInfo(sPath)
            'Dim objMetaEntry As EDDMetaDataEntry


            With m_hshMeta
                'File Create Date
                'objMetaEntry = New EDDMetaDataEntry()
                'objMetaEntry.SetValue(objFileInfo.CreationTime.ToString)
                .Add(EDDMetaDataID.METADATA_ID_FILEDATECREATED, objFileInfo.CreationTime.ToString)
                'objMetaEntry = Nothing

                'File Accessed Date                
                .Add(EDDMetaDataID.METADATA_ID_FILEDATEACCESSED, objFileInfo.LastAccessTime.ToString)

                'File Modified Date
                .Add(EDDMetaDataID.METADATA_ID_FILEDATEMODIFIED, objFileInfo.LastWriteTime.ToString)

                'File Size
                .Add(EDDMetaDataID.METADATA_ID_FILESIZEBYTES, objFileInfo.Length.ToString)

                'File Arthive Attrib
                .Add(EDDMetaDataID.METADATA_ID_FILEATTRIBARCHIVE, Math.Abs((CInt((objFileInfo.Attributes And FileAttributes.Archive) <> 0))).ToString)

                'File Compressed Attrib
                .Add(EDDMetaDataID.METADATA_ID_FILEATTRIBCOMPRESSED, Math.Abs((CInt((objFileInfo.Attributes And FileAttributes.Compressed) <> 0))).ToString)

                'File Directory Attrib
                .Add(EDDMetaDataID.METADATA_ID_FILEATTRIBDIRECTORY, Math.Abs((CInt((objFileInfo.Attributes And FileAttributes.Directory) <> 0))).ToString)

                'File Encrypted Attrib
                .Add(EDDMetaDataID.METADATA_ID_FILEATTRIBENCRYPTED, Math.Abs((CInt((objFileInfo.Attributes And FileAttributes.Encrypted) <> 0))).ToString)

                'File Hidden Attrib
                .Add(EDDMetaDataID.METADATA_ID_FILEATTRIBHIDDEN, Math.Abs((CInt((objFileInfo.Attributes And FileAttributes.Hidden) <> 0))).ToString)

                'File Attrib Normal
                .Add(EDDMetaDataID.METADATA_ID_FILEATTRIBNORMAL, Math.Abs((CInt((objFileInfo.Attributes And FileAttributes.Normal) <> 0))).ToString)

                'File Attrib Readonly
                .Add(EDDMetaDataID.METADATA_ID_FILEATTRIBREADONLY, Math.Abs((CInt((objFileInfo.Attributes And FileAttributes.ReadOnly) <> 0))).ToString)

                'File System Attrib
                .Add(EDDMetaDataID.METADATA_ID_FILEATTRIBSYSTEM, Math.Abs((CInt((objFileInfo.Attributes And FileAttributes.System) <> 0))).ToString)

                'File Temp Attrib
                .Add(EDDMetaDataID.METADATA_ID_FILEATTRIBTEMP, Math.Abs((CInt((objFileInfo.Attributes And FileAttributes.Temporary) <> 0))).ToString)

            End With

            objFileInfo = Nothing
        End Sub        


        Public Sub Dispose() Implements IDisposable.Dispose
            Dispose(True)

            GC.SuppressFinalize(Me)
        End Sub


        Protected Overridable Sub Dispose(ByVal bDisposing As Boolean)

            ' Check to see if Dispose has already been called.
            If Not Me.m_bDisposed Then
                ' ***If disposing equals true, dispose all managed and unmanaged resources.

                If (bDisposing) Then
                    ' Dispose managed resources.

                    m_hshMeta.Clear()
                    m_hshMeta = Nothing
                End If

                ' ***Release unmanaged resources here. If disposing is false,        

            End If

            Me.m_bDisposed = True
        End Sub


        ' This Finalize method will run only if the 
        ' Dispose method does not get called.
        ' By default, methods are NotOverridable. 
        ' This prevents a derived class from overriding this method.
        Protected Overrides Sub Finalize()
            Dispose(False)
        End Sub

    End Class


    Public Class ComponentMapping   'Used by server apps
        Private m_sApplicationClassName As String
        Private m_objApplication As Object
        Private m_iProcessID As Integer     'REVISIT: would like to record process ID for each app


        Public Sub New()

        End Sub


        Public Property Application() As Object
            Get
                Return m_objApplication
            End Get
            Set(ByVal Value As Object)
                m_objApplication = Value
            End Set
        End Property


        Public Property ProcessID() As Integer
            Get
                Return m_iProcessID
            End Get
            Set(ByVal Value As Integer)
                m_iProcessID = Value
            End Set
        End Property


        Public Property ApplicationClassName() As String
            Get
                Return m_sApplicationClassName
            End Get
            Set(ByVal Value As String)
                m_sApplicationClassName = Value
            End Set
        End Property

    End Class


    <Serializable()> _
    Public Enum ObjectMode
        MODE_LOCAL = 0
        MODE_REMOTE = 1
    End Enum


    <Serializable()> _
    Public Enum ConverterTargets
        FORMAT_DEFAULT = 0

        FORMAT_TIFF = 0
        FORMAT_PDF = 1
    End Enum


    <Serializable()> _
    Public Enum ImageQuality
        QUALITY_DEFAULT = 5

        QUALITY_HIGHEST = 1
        QUALITY_HIGHER = 2
        QUALITY_HIGH = 3
        QUALITY_NORMAL = 4
        QUALITY_LOW = 5
        QUALITY_LOWER = 6
        QUALITY_LOWEST = 7
    End Enum


    Public Enum HashtableCapacity    'To be used in this class to avoid hardcoding of constants
        CAPACITY_RESULTFILES = 100      'Smaller than extractor value
        CAPACITY_ERRORS = 100
        CAPACITY_METADATAUNIVERSE = 100
    End Enum


    Public Class Utility

        Public Shared Function GetAssociatedApp(ByVal sPath As String) As String
            Dim sDummy As String
            Dim sBuffer As String
            Dim lRetval As Long


            Try
                sBuffer = Space(255)

                lRetval = Win32API.FindExecutable(sPath, sDummy, sBuffer)

                'If iRetval <= 32 Or sBuffer = "" Then 'error
                'GetDefaultBrowser = ""
                'Else
                'GetDefaultBrowser = sBuffer
                'End If

                If InStr(sBuffer, Chr(0)) <> 0 Then
                    Mid(sBuffer, InStr(sBuffer, Chr(0)), 1) = Chr(32)
                End If

                GetAssociatedApp = sBuffer.Trim

            Catch
                GetAssociatedApp = String.Empty
            End Try

        End Function


        Public Shared Function CreateEmptyFile(ByVal sPath As String) As Boolean
            Dim objStream As FileStream

            Try
                objStream = New FileStream(sPath, FileMode.Create)

            Catch
                Try
                    objStream.Close()
                Catch
                End Try

                Try
                    File.Delete(sPath)
                Catch
                End Try

                objStream = Nothing

                Return False
            End Try

            objStream.Flush()
            objStream.Close()
            objStream = Nothing

            Return True
        End Function


        Public Shared Function GetExtensionFromClass(ByVal sClass As String) As String
            'Get file extension from document class, e.g. Excel.Document.8
            Dim objKey As RegistryKey
            Dim sCLSID As String
            Dim sExt As String
            Dim sClassUse As String = sClass.Trim.ToUpper

            objKey = Registry.ClassesRoot.OpenSubKey(sClassUse & "\CLSID", False)
            sCLSID = CStr(objKey.GetValue("", ""))
            objKey.Close()

            objKey = Registry.ClassesRoot.OpenSubKey("CLSID\" & sCLSID & "\DefaultExtension", False)
            sExt = CStr(objKey.GetValue("", ""))
            objKey.Close()

            If sExt <> "" Then
                Dim iComma As Integer

                'parse out extension from ".NNN, ...
                iComma = InStr(sExt, ",")

                If iComma <> 0 Then     'found the comma
                    sExt = Mid(sExt, 1, iComma - 1)
                Else
                    sExt = ""   'give up
                End If
            End If

            GetExtensionFromClass = sExt
        End Function


        Public Shared Function CleanupPath(ByVal sPath As String) As String
            'Do misc cleanup on a file path - convert / to \

            Dim sResult As String = sPath.Trim
            Dim bDoubleSlash As Boolean

            sResult = sResult.Replace("/", "\")   'Get rid of forward slashes

            'Replace \\ with \ - but don't affect start of UNC path name

            If sResult.Length > 1 Then
                bDoubleSlash = (sResult.Substring(0, 2) = "\\")
            End If

            sResult = sResult.Replace("\\", "\")   'Get rid of double slashed

            If bDoubleSlash Then
                sResult = "\" & sResult     'Restore leading \
            End If

            'TODO: Other cleanup?

            CleanupPath = sResult
        End Function


        Public Shared Function AddTrailingSlash(ByVal sPath As String) As String
            Dim sUse As String = sPath.Trim            

            If Not sUse.EndsWith("\") Then
                sUse &= "\"
            End If

            AddTrailingSlash = sUse
        End Function


        Public Shared Function GetFileAvailable(ByVal sPath As String) As Boolean
            GetFileAvailable = True

            'Check to see if file is available for exclusive use
            Dim objStream As FileStream

            Try
                objStream = New FileStream(sPath, FileMode.Open, FileAccess.Write, FileShare.None)
                objStream.Close()
                objStream = Nothing
            Catch
                GetFileAvailable = False    'Assume file unavailable on any exception
            Finally

            End Try

        End Function


        Public Shared Function GetUniqueKey(ByVal hshLookup As Hashtable, ByVal sPath As String) As String
            Dim sPathUse As String = sPath.Trim.ToUpper
            Dim sPathNew As String
            Dim iCount As Integer = 1            
            Dim sExt As String
            Dim sFileRootUse As String

            sExt = Path.GetExtension(sPathUse)
            sPathNew = sPathUse            

            'Do While hshLookup.ContainsKey(sPathRoot & sFileRootUse & sExt)
            Do While hshLookup.ContainsKey(sPathNew)
                'Use some sort of scheme for getting a unique filename

                If sExt <> String.Empty Then
                    sPathNew = sPathUse & "." & iCount.ToString & sExt
                Else
                    sPathNew = sPathUse & "." & iCount.ToString
                End If

                iCount += 1                
            Loop

            GetUniqueKey = sPathNew.Trim.ToUpper
        End Function

    End Class

End Namespace

