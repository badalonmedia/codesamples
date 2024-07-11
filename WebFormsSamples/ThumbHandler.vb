
'HTTP Handler for product image thumbnails

Option Explicit On 
Option Strict On

Imports System.Web
Imports System.Data.SqlClient
Imports System.IO
Imports System.Drawing      	'for thumbnails
Imports System.Configuration    	'required for use of web.config or app.config


Public Class ThumbnailHandler
    Implements IHttpHandler

    Private m_objDBConn As SqlConnection
    Private m_objDBReader As SqlDataReader
    Private m_objDBCmd As SqlCommand
    Private m_sConn As String
    Private m_iWidth As Integer = CInt(ConfigurationSettings.AppSettings("thumb_width"))
    Private m_iHeight As Integer = CInt(ConfigurationSettings.AppSettings("thumb_height"))

    Public Function ThumbnailCallback() As Boolean
        Return False
    End Function


    Public Sub ProcessRequest(ByVal context As System.Web.HttpContext) Implements System.Web.IHttpHandler.ProcessRequest
        Dim sAbsPath As String
        Dim objFile As File
        Dim sFileName As String
        Dim sFileNameOnly As String
        Dim sExtOnly As String

        'FIRST: see if requested image file actually exists, if YES then show it

        sAbsPath = context.Request.PhysicalPath
        sFileName = sAbsPath.Substring(sAbsPath.LastIndexOf("\") + 1)   'MYFILE.JPG, GIF, JPEG
        sExtOnly = sFileName.Substring(sFileName.LastIndexOf(".") + 1)

        If objFile.Exists(sAbsPath) Then
            Dim fs As FileStream = File.OpenRead(sAbsPath)
            Dim arrByte(CInt(fs.Length)) As Byte

            fs.Read(arrByte, 0, CInt(fs.Length))

            Dim objMemStream As New MemoryStream(arrByte)
            Dim objBitmap As New Bitmap(objMemStream)
            Dim objCallback As Image.GetThumbnailImageAbort = New Image.GetThumbnailImageAbort(AddressOf ThumbnailCallback)
            Dim objThumb As Image = objBitmap.GetThumbnailImage(m_iWidth, m_iHeight, objCallback, IntPtr.Zero)

            context.Response.ContentType = "image/jpeg"   'force this value
            objThumb.Save(context.Response.OutputStream, Imaging.ImageFormat.Jpeg)     'write as jpeg to output stream

            'context.Response.BinaryWrite(arrByte)
            fs.Close()
        Else
            'OTHERWISE...file does not exist, so pull from database         

            sFileNameOnly = Left(sFileName, sFileName.Length - sExtOnly.Length - 1)    'MYFILE

            Dim sItemID As String = sFileNameOnly.Substring(sFileNameOnly.LastIndexOf("_") + 1)


            If Not IsNumeric(sItemID) Then  'does not work right
                sItemID = "0"   'construct invalid id
            End If

            m_sConn = ConfigurationSettings.AppSettings("web_productDB")
            m_objDBConn = New SqlConnection()

            'open db connection
            Try
                m_objDBConn.ConnectionString = m_sConn
                m_objDBConn.Open()
            Catch

            End Try

            m_objDBCmd = New SqlCommand()

            With m_objDBCmd
                .CommandText = "usp_item_getby_item_id"
                .Connection = m_objDBConn
                .CommandType = CommandType.StoredProcedure
                .Parameters.Add("@item_id", SqlDbType.Int).Value = CInt(sItemID)

                m_objDBReader = .ExecuteReader()    'try to use ExecuteScalar
            End With

            If m_objDBReader.Read() Then
                Dim iSize As Integer = CInt(m_objDBReader.Item("num_bytes"))
                Dim arrByte(iSize) As Byte

                m_objDBReader.GetBytes(m_objDBReader.GetOrdinal("item"), 0, arrByte, 0, iSize)

                m_objDBReader.Close()   'free up db resources ASAP
                m_objDBReader = Nothing
                m_objDBCmd = Nothing
                CloseDB(m_objDBConn)

                Dim objMemStream As New MemoryStream(arrByte)
                Dim objBitmap As New Bitmap(objMemStream)
                Dim objCallback As Image.GetThumbnailImageAbort = New Image.GetThumbnailImageAbort(AddressOf ThumbnailCallback)
                Dim objThumb As Image = objBitmap.GetThumbnailImage(m_iWidth, m_iHeight, objCallback, IntPtr.Zero)

                context.Response.ContentType = "image/jpeg"
                objThumb.Save(context.Response.OutputStream, Imaging.ImageFormat.Jpeg)     'write as jpeg to output stream

                'Clean up
                arrByte = Nothing
                objThumb.Dispose()
                objThumb = Nothing
            Else
                'no item found

                m_objDBReader.Close()
                m_objDBReader = Nothing
                m_objDBCmd = Nothing
                CloseDB(m_objDBConn)

                'send nothing back to output
            End If

        End If

    End Sub


    Public ReadOnly Property IsReusable() As Boolean Implements System.Web.IHttpHandler.IsReusable
        Get
            Return False
        End Get
    End Property


    Protected Overrides Sub Finalize()
        'Cleanup

        m_objDBCmd = Nothing

        If Not IsNothing(m_objDBReader) Then
            If Not m_objDBReader.IsClosed() Then
                m_objDBReader.Close()
                m_objDBReader = Nothing
            End If
        End If

        CloseDB(m_objDBConn)

        MyBase.Finalize()
    End Sub

End Class

