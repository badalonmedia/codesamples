<%@ Page Language="vb" AutoEventWireup="false" CodeBehind="VendorLogin.aspx.vb" Inherits="TopMoving.VendorLogin1" %>
<%@ Register TagPrefix="cc1" Namespace="Sax.Security" Assembly="Sax.Security.Community" %>
<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.0 Transitional//EN">
<html>
	<head>
		<title>Top Moving Company 4 U - Vendor Login</title>
		<meta name="GENERATOR" content="Microsoft Visual Studio .NET 7.1"/>
		<meta name="CODE_LANGUAGE" content="Visual Basic .NET 7.1"/>
		<meta name="vs_defaultClientScript" content="JavaScript"/>
		<meta name="vs_targetSchema" content="http://schemas.microsoft.com/intellisense/ie5"/>
		<link href="basic.css" type="text/css" rel="stylesheet"/>
	</head>
	<body>
		<form id="frmLogin" method="post" runat="server">
			<table cellspacing="1" cellpadding="1" border="0" align="center">
				<tr valign="top">
					<td align="center" colspan="2">
						<h3>Topmovingcompany4u.com&nbsp;Vendor&nbsp;Login</h3>
					</td>
				</tr>
				<tr> 
					<td align="right">Username:</td>
					<td><asp:textbox id="txtUserName" runat="server" columns="20" maxlength="50"></asp:textbox>
						<asp:requiredfieldvalidator id="RequiredFieldValidator1" runat="server" errormessage="*" controltovalidate="txtUserName"></asp:requiredfieldvalidator>
					</td>
				</tr>
				<tr>
					<td align="right">Password:</td>
					<td><asp:textbox id="txtPassword" runat="server" columns="20" maxlength="20" textmode="Password"></asp:textbox>
						<asp:requiredfieldvalidator id="RequiredFieldValidator2" runat="server" errormessage="*" controltovalidate="txtPassword"></asp:requiredfieldvalidator>
					</td>
				</tr>
				<tr><td colspan="2">
                <cc1:humanverification id="HumanVerification1" runat="server"></cc1:humanverification>
                </td></tr>
				<tr>
					<td align="center" colspan="2"><asp:button id="btnLogin" runat="server" text="  Login  "></asp:button>
						<asp:label id="lblMsg" runat="server" forecolor="Red"></asp:label></td>
				</tr>
			</table>
		</form>
	</body>
</html>
