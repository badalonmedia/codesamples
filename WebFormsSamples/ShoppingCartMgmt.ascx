<%@ Control Language="c#" AutoEventWireup="false" Codebehind="ShoppingCartMgmt.ascx.cs" Inherits="VNWeb.UserControls.ShoppingCartMgmt" TargetSchema="http://schemas.microsoft.com/intellisense/ie5" %>
<table class="ByCategoryHeader_Left" border="0" cellSpacing="0" cellPadding="0" width="95%">
	<tr>
		<td>
			<table class="ByCategoryHeader_Left" border="0" cellSpacing="0" cellPadding="0" width="100%">
				<tr id="m_trLinks_SignedOut" height="1" runat="server">
					<td><IMG src="images/set2_top_left.gif" width="17" height="23"></td>
					<td class="Set2Top" vAlign="middle" width="100%" align="center">Shopping Cart
					</td>
					<td><IMG src="images/set2_top_right.gif" width="9" height="23"></td>
				</tr>
				<tr id="m_trLinks_SignedIn" height="1" runat="server">
					<td><IMG src="images/set2_top_left_big.jpg" width="17" height="50"></td>
					<td class="Set2TopBig" vAlign="middle" width="100%" align="center">Shopping Cart
						<br>
						<br>
						<asp:hyperlink id="m_lnkDetailedView" runat="server" Target="_self" CssClass="CartDetailedView">Manage Cart</asp:hyperlink>&nbsp;&nbsp;
						<asp:hyperlink id="m_lnkCheckout" runat="server" Target="_self" CssClass="CartDetailedView">Checkout</asp:hyperlink>&nbsp;
						<%--<br>						
						<asp:HyperLink id="m_lnkOrders" runat="server" CssClass="CartDetailedView" Target="_self">Order History</asp:HyperLink>&nbsp;&nbsp;
						<asp:hyperlink id="m_lnkImport" CssClass="CartDetailedView" runat="server" Target="_self">Import Last Order</asp:hyperlink>&nbsp;&nbsp; --%>
					</td>
					<td><IMG src="images/set2_top_right_big.jpg" width="9" height="50"></td>
				</tr>
			</table>
		</td>
	</tr>
	<tr>
		<td>
			<table border="0" cellSpacing="0" cellPadding="3" width="100%">
				<tr>
					<td class="Set2Left"></td>
					<td width="100%">
						<!--Cart Starts -->
						<table border="0" cellSpacing="0" cellPadding="0" width="100%" bgColor="#ffffff">
							<tr vAlign="top">
								<td vAlign="top"><asp:datagrid id="m_dgCart" runat="server" CssClass="CartItem" GridLines="None" AutoGenerateColumns="False"
										OnItemCommand="ShoppingCart_Command" Width="100%" ShowFooter="True" ShowHeader="True" CellPadding="0" CellSpacing="0">
										<HeaderStyle></HeaderStyle>
										<AlternatingItemStyle></AlternatingItemStyle>
										<ItemStyle></ItemStyle>
										<Columns>
											<asp:BoundColumn Visible="false" DataField="CartItemId"></asp:BoundColumn>
											<ASP:TemplateColumn HeaderText="">
												<HeaderStyle></HeaderStyle>
												<ItemStyle></ItemStyle>
												<HeaderTemplate>
													<table width="100%" class="CartItem" cellspacing="0" cellpadding="0" height="1px">
														<tr height="1px">
															<%--<td width="25 % " align="center">
																<b>Part #</b>
															</td>--%>
															<td width="50%" align="center">
																<b>Product</b>
															</td>
															<td width="25%" align="center">
																<b>Total</b>
															</td>
														</tr>
														<!-- <tr valign="top" height="1px">
													<td colspan="3" height="1px">
														<img src="images/set2_bottom_lineonly.gif" width="100%" height="1px"> 														
													</td>
												</tr> -->
													</table>
													<img src="images/LeftPanel_ItemSep.gif" width="100%" height="1px">
												</HeaderTemplate>
												<ItemTemplate>
													<table width="100%" class="CartItem" cellspacing="0" cellpadding="0">
														<tr valign="top">
															<%--<td width="25%">
																< % # DataBinder.Eval(Container.DataItem, "PartNum")% >
															</td> --%>
															<td width="75%">
																<strong>
																	<%#DataBinder.Eval(Container.DataItem, "PartNum")%>
																</strong>
																<br>
																<%#GetProductLinkHTML((int)DataBinder.Eval(Container.DataItem, "ProductId"), (int)DataBinder.Eval(Container.DataItem, "CartItemId"), (string)DataBinder.Eval(Container.DataItem, "ProductName"), VNWeb.Components.AppConstants.CART_PRODUCT_MAX_CHARS)%>
															</td>
															<td width="25%" align="right">
																<asp:LinkButton id="m_lnkRemove" runat="server" CommandName="RemoveFromCart" CssClass="CartItemRemove">Remove</asp:LinkButton>
															</td>
														</tr>
														<tr>															
															<td>
																<%#DataBinder.Eval(Container.DataItem, "Quantity")%>
																x
																<%#Convert.ToDecimal(DataBinder.Eval(Container.DataItem, "Price")).ToString("C", PrimaryCurrencyFormat)%>
																=
															</td>
															<td align="right">
																<%#(Convert.ToDecimal(DataBinder.Eval(Container.DataItem, "Price")) * Convert.ToInt32(DataBinder.Eval(Container.DataItem, "Quantity"))).ToString("C", PrimaryCurrencyFormat)%>
															</td>
														</tr>
														<tr>
															<td width="50%" align="left">
															&nbsp;
															<%-- # GetInvalidQuantityHTML(((VNWeb.Components.Data.CartItemType)Container.DataItem)) --%>
															</td>
															<%--<td colspan="2" width="100%" align="right">--%>
															<td width="50%" align="right">
																<i>
																	<!-- VNWeb.Components.Data.ProductType.GetAltPrice(Convert.ToDecimal(DataBinder.Eval(Container.DataItem, "Price")), Convert.ToInt32(DataBinder.Eval(Container.DataItem, "Quantity")), CurrencyFactor_Local).ToString("C", AltCurrencyFormat) -->
																	<%#string.Format(FormatString_Local,  VNWeb.Components.Data.ProductType.GetAltPrice(Convert.ToDecimal(DataBinder.Eval(Container.DataItem, "Price")), Convert.ToInt32(DataBinder.Eval(Container.DataItem, "Quantity")), CurrencyFactor_Local))%>
																</i>
															</td>
														</tr>
													</table>
													<img src="images/LeftPanel_ItemSep.gif" width="100%" height="1px">
												</ItemTemplate>
												<FooterTemplate>
													<table width="100%" class="CartItem" cellspacing="0" cellpadding="0">
														<!-- <tr>
													<td width="100%" colspan="3">
														<img src="images/set2_bottom_lineonly.gif" width="100%" height="1px">
													</td>
												</tr> -->
														<tr>
															<%--	<td width="25 % " align="left">
																&nbsp;
															</td> --%>
															<td>
																<b>Invoice Total:</b>
															</td>
															<td align="right">
																<b>
																	<%=CartTotal_Local.ToString("C", PrimaryCurrencyFormat)%>
																</b>
															</td>
														</tr>
														<tr>
															<td width="100%" colspan="2" align="right">
																<b><i>
																		<!-- VNWeb.Components.Data.ProductType.GetAltPrice(CartTotal_Local, 1, CurrencyFactor_Local).ToString("C", AltCurrencyFormat) -->
																		<%=string.Format(FormatString_Local, VNWeb.Components.Data.ProductType.GetAltPrice(CartTotal_Local, 1, CurrencyFactor_Local))%>
																	</i></b>
															</td>
														</tr>
													</table>
												</FooterTemplate>
											</ASP:TemplateColumn>
										</Columns>
										<PagerStyle Visible="False"></PagerStyle>
									</asp:datagrid></td>
							</tr>
						</table>
						<asp:label id="m_lblCartEmpty" runat="server" CssClass="CartEmpty">Your Shopping Cart is currently empty.</asp:label>
						<!-- Cart Ends --><IMG src="images/non_image.jpg" width="4" height="1">
					</td>
					<td class="Set2Right"><IMG src="images/non_image.jpg" width="3" height="1"></td>
				</tr>
			</table>
		</td>
	</tr>
	<tr>
		<td>
			<table class="ByCategoryHeader_Left" border="0" cellSpacing="0" cellPadding="0" width="100%">
				<tr height="1">
					<td vAlign="top"><IMG src="images/set2_bottom_left_small.gif" width="17" height="5"></td>
					<td class="Set2BottomSmall" vAlign="middle" width="100%" align="left"></td>
					<td vAlign="top"><IMG src="images/set2_bottom_right_small.gif" width="9" height="5"></td>
				</tr>
			</table>
		</td>
	</tr>
</table>
