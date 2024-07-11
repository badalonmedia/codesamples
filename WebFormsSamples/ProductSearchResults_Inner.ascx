<%@ Register TagPrefix="uc1" TagName="PageNavigation" Src="PageNavigation.ascx" %>
<%@ Register TagPrefix="uc1" TagName="ValidationUI" Src="ValidationUI.ascx" %>
<%@ Control Language="c#" AutoEventWireup="false" Codebehind="ProductSearchResults_Inner.ascx.cs" Inherits="VNWeb.UserControls.ProductSearchResults_Inner" TargetSchema="http://schemas.microsoft.com/intellisense/ie5" %>

<!-- Update Cart Button Button def -->
<script language="JavaScript" type="text/javascript">
<!-- Dummy comment to hide code from non-JavaScript browsers.

if (document.images) {
UpdateCart_off = new Image(); UpdateCart_off.src = "images/Update_Cart_b1.gif"
UpdateCart_over = new Image(); UpdateCart_over.src = "images/Update_Cart_b1_over.gif"

}

// End of dummy comment -->
</script>
<!-- Update Cart Button Button  end -->
<uc1:ValidationUI id="m_ctlValidation" runat="server"></uc1:ValidationUI>
<uc1:PageNavigation id="m_ctlPageNavigation_Top" runat="server"></uc1:PageNavigation>
<!-- <hr> -->
<!--  width="98%" align="left"> --> <!-- Needs to be adjusted as per DataGrid settings -->  <!-- 100% minus 2 for cellpadding -->
<asp:DataGrid id="m_dgResults" runat="server" AutoGenerateColumns="False" HorizontalAlign="Center"
	AllowSorting="False" width="100%" CellPadding="1" GridLines="None" AllowPaging="False" ShowFooter="false"
	ShowHeader="False" CssClass="ResultsGrid" OnItemCommand="ProductGrid_Command" OnItemDataBound="Grid_ItemDataBound">
	<HeaderStyle CssClass="ResultsGrid_Header"></HeaderStyle>
	<%--<AlternatingItemStyle BackColor="#EFEFEF"></AlternatingItemStyle> --%>
	<ItemStyle></ItemStyle>
	<Columns>
		<asp:BoundColumn Visible="false" DataField="Quantity"></asp:BoundColumn>
		<asp:BoundColumn Visible="false" DataField="ProductId"></asp:BoundColumn>
		<asp:BoundColumn Visible="false" DataField="CartItemId"></asp:BoundColumn>
		<asp:BoundColumn Visible="false" DataField="BaseQuantity"></asp:BoundColumn>
		<asp:BoundColumn Visible="false" DataField="MfgName"></asp:BoundColumn>
		<ASP:TemplateColumn HeaderText="">
			<HeaderStyle></HeaderStyle>
			<ItemStyle></ItemStyle>
			<ItemTemplate>
				<table width="100%" cellpadding="0" cellspacing="0">
					<tr>
						<td height="2px" style="background-color:#0000AA" width="100%">
						</td>
					</tr>
				</table>
				<table width="100%" class="ResultsGrid">
					<tr valign="top"> <!-- 12/13/06: Added Top attrib -->
					<td align="center" valign="middle" width="26%">												
							<%# VNWeb.Components.Data.ProductType.GetImageHTMLForProductListing((VNWeb.Components.Data.ProductType)Container.DataItem, false, false)%>
						</td>
						<td width="74%" valign="top"> <!-- was 60 -->
							<!-- Attempt to fix image -->
							<table width="100%" class="ResultsGrid" cellpadding="0" cellspacing="0">
								<tr>
									<td>
										<!-- Attempt to fix image -->
										<table width="100%" class="ResultsGrid">
											<tr>
												<td width="100%" valign="top" colspan="3">
													<strong>
														<%#DataBinder.Eval(Container.DataItem, "DisplayPosition")%>
														.</strong>&nbsp;
													<%#GetLongDescHTML(((VNWeb.Components.Data.ProductType)Container.DataItem))%>
													&nbsp;
													<%#((VNWeb.Components.Data.ProductType)Container.DataItem).NewFlag ? "<img src='images/new_indicator.gif' alt='New!' title='New!'>" : "&nbsp;"%>
													<%if (IsSignedInAsAdmin_Local) {%>
													<a href="<%#VNWeb.Components.Data.ProductType.GetAdminUrl(((VNWeb.Components.Data.ProductType)Container.DataItem).ProductId, CurrentUrlFull)%>" target="_self" class="ProductAdminLink">
														(Admin)</a>
													<%}%>
													<!-- GetNewInd((VNWeb.Components.Data.ProductType)Container.DataItem) --> <!-- </b></font> -->
													<!-- GetBestSellerInd((VNWeb.Components.Data.ProductType)Container.DataItem)  &nbsp;  &nbsp;   
							GetPromotionInd((VNWeb.Components.Data.ProductType)Container.DataItem) -->
												</td>
												</tr>
											<tr runat="server" id="m_trAdditionalInfo">
												<td valign="top" colspan="3">
													<%#DataBinder.Eval(Container.DataItem, "AdditionalInfo")%>
												</td>
											</tr>
											<tr>
												<td width="30%">
													Model:&nbsp;<%#DataBinder.Eval(Container.DataItem, "HardwarePlatform")%>
												</td>
												<td width="45%">
													Mfg Part #:
													<%#DataBinder.Eval(Container.DataItem, "MfgPartNum")%>
												</td>
												<td>
												</td>
											</tr>
											<tr>
												<td colspan="3">
													<table width="100%" class="ResultsGrid">
														<%if ((int)(SearchEngine.DisplayFlags & VNWeb.Components.SearchDisplayFlagsType.CATEGORY) != 0) {%>
														<tr>
															<td width="30%" align="left">
																Category:
															</td>
															<td width="70%" align="left">
																<a class=ProductDetail href='<%=VNWeb.Components.Utility.ConstructNonSSLAppPath("Category.aspx")%>?<%=VNWeb.Components.AppConstants.QUERYSTRING_CATEGORY_ID%>=<%#Server.UrlEncode(DataBinder.Eval(Container.DataItem, "CategoryId").ToString())%>'>
																	<%#DataBinder.Eval(Container.DataItem, "CategoryName")%>
																</a>
															</td>
														</tr>
														<%}%>
														<%if ((int)(SearchEngine.DisplayFlags & VNWeb.Components.SearchDisplayFlagsType.SUBCATEGORY) != 0) {%>
														<tr>
															<td width="30%" align="left">
																SubCategory:
															</td>
															<td width="70%" align="left">
																<a class=ProductDetail href='<%=VNWeb.Components.Utility.ConstructNonSSLAppPath("Category.aspx")%>?<%=VNWeb.Components.AppConstants.QUERYSTRING_CATEGORY_ID%>=<%#Server.UrlEncode(DataBinder.Eval(Container.DataItem, "CategoryId").ToString())%>&<%=VNWeb.Components.AppConstants.QUERYSTRING_SUBCATEGORY_ID%>=<%#Server.UrlEncode(DataBinder.Eval(Container.DataItem, "SubCategoryId").ToString())%>'>
																	<%#DataBinder.Eval(Container.DataItem, "SubCategoryName")%>
																</a>
															</td>
														</tr>
														<%}%>
														<%if ((int)(SearchEngine.DisplayFlags & VNWeb.Components.SearchDisplayFlagsType.MFG) != 0) {%>
														<tr>
															<td width="30%" align="left">
																Mfg:
															</td>
															<td width="70%" align="left">
																<a class=ProductDetail href='<%=VNWeb.Components.Utility.ConstructNonSSLAppPath("Mfg.aspx")%>?<%=VNWeb.Components.AppConstants.QUERYSTRING_MFG_ID%>=<%#Server.UrlEncode(DataBinder.Eval(Container.DataItem, "MfgId").ToString())%>'>
																	<%#DataBinder.Eval(Container.DataItem, "MfgName")%>
																</a>
															</td>
														</tr>
														<%}%>
														<%--<tr valign="top" runat="server" id="m_trShippingEstimate">	< %-- 3/19/07: Added --% >
														<td align="left" colspan="2">
														<span class="ProductDetail_Ship">< %#DataBinder.Eval(Container.DataItem, "ShippingEstimateName")% ></span>
														</td>
														</tr> --%>
														
														<!-- <tr>
									<td>
										Mfg Part #:
									</td>
									<td>
										<%#DataBinder.Eval(Container.DataItem, "MfgPartNum")%>
									</td>
								</tr>
								
								-->
													</table>
												</td>
											</tr>
										</table>
									</td>
									<!-- <td>   						
												
						
							
						</td> -->
								</tr>
								<tr>
									<td width="70%"> <!-- <td width="70%"> -->
										<table width="100%" class="ResultsGrid" border="0" cellpadding="0" cellspacing="0">
											<!-- <tr>
									<td width="90%"> 
										<table width="100%" class="ResultsGrid">						
											<tr>
												
												<td width="35%" align="left">
													
												</td>
											</tr>
										</table>
									</td>
								</tr> -->
											<tr>
												<td width="100%"> <!-- Outer Table -->
													<table width="100%" class="ResultsGrid">
														<tr>
															<!-- <td width="5%">
													&nbsp;
												</td> -->
															<td width="32%" align="left">
																<%-- if (!IsGuest_Local) { //12/24/06: Replaced with line below --%>
																<%if (IsSignedIn_Local || WebSite.BusinessScope == VNWeb.Components.Data.BusinessScopeType.RETAIL) {%>
																USD
																<%#((VNWeb.Components.Data.ProductType)Container.DataItem).Price.ToString("C", CurrencyFormat) %>
																<i>
																	<%#GetAltPriceHTML((VNWeb.Components.Data.ProductType)Container.DataItem)%>
																</i>
																<%} else { %>
																<a href="<%=SignInURL%>" target="_self" class=SignInPricing>&lt;sign in for 
																	pricing&gt;</a>
																<%} %>
															</td>
															<td width="28%" align="left">
																<%if (IsSignedIn_Local || WebSite.BusinessScope == VNWeb.Components.Data.BusinessScopeType.RETAIL) {%>
																<table cellpadding="0" cellspacing="0">
																	<tr>
																		<td>
																			<asp:TextBox id="m_txtQuantity" runat="server" Width="40px" MaxLength="3" CssClass="QuantityInput"></asp:TextBox>
																		</td>
																		<%--<asp:Button id="m_btnAddToCart" runat="server" Text="Update Cart" Width="75px" CommandName="AddToCart" ></asp:Button> --%>
																		<td width="5px">
																		</td>
																		<%if (CartMode == VNWeb.Components.ShoppingCartModeType.ADD) {%>
																		<td>
																			<asp:LinkButton id="m_btnAddToCart" runat="server" CommandName="AddToCart">
																				<IMG id="m_imgUpdateCart" runat="server" src="../images/ProductAdd.gif" alt="Add To Cart" title="Add To Cart"
																					align="middle"></asp:LinkButton>
																		</td>
																		<%} else%>
																		<%if (CartMode == VNWeb.Components.ShoppingCartModeType.UPDATE) {%>																		
																																				
																		<%--<td width="5px">
																		</td>--%>
																		<td>
																			<table cellspacing="0" cellpadding="0">
																			<tr>
																			<td align="center">
																			<asp:LinkButton id="m_btnUpdateCart" runat="server" CssClass="RemoveCartCenter" CommandName="AddToCart">Update</asp:LinkButton>
																			</td>
																			</tr>
																			<tr>																			
																			<td align="center">																			
																			<asp:LinkButton id="m_lnkRemove" runat="server" CssClass="RemoveCartCenter" CommandName="RemoveFromCart">Remove</asp:LinkButton>
																			</td>
																			</tr>
																			</table>
																		</td>
																		<%}%>
																	</tr>
																	
																</table>
																<%} else  { %>
																&nbsp;
																<%}%>
															</td>
															<td width="40%" align="left">
															<span class="ProductDetail_Ship"><%#DataBinder.Eval(Container.DataItem, "ShippingEstimateName")%></span>
															
															</td>
															<!--<td width="5%" align="left">
													&nbsp;
												</td> -->
														</tr>
														<%#GetInvalidQuantityHTML((VNWeb.Components.Data.ProductType)Container.DataItem)%>

														
													</table>
												</td>
											</tr>
										</table>
									</td>
									<!-- Attempt to fix image-->
								</tr>
							</table>
						</td>
						<!-- Attempt to fix image-->
						<!-- <td>
						<table class="ResultsGrid" cellspacing="0" cellpadding="0">
						<tr valign="middle"> -->
						<%--<td align="center" valign="middle" width="35%">												
							 < % # VNWeb.Components.Data.ProductType.GetImageHTMLForProductListing((VNWeb.Components.Data.ProductType)Container.DataItem, false, false) % >
						</td> --%>
						<!-- 	<td width="3%">														
						</td>  -->
					</tr>
					<!-- <tr>			
						</table>
						</td>						
						</tr> -->
				</table>
				<!-- <hr> -->
			</ItemTemplate>
		</ASP:TemplateColumn>
		<%--<ASP:TemplateColumn HeaderText="PartNo">
			<HeaderStyle></HeaderStyle>
			<ItemStyle></ItemStyle>
			<ItemTemplate>
				<table width="100%" class="ResultsGrid">
					<tr>
						<td>
						</td>
					</tr>
				</table>
			</ItemTemplate>
		</ASP:TemplateColumn> --%>
	</Columns>
	<PagerStyle Visible="False"></PagerStyle>
</asp:DataGrid>
<!--<hr> -->
<table width="100%" cellpadding="0" cellspacing="0" align="left">
	<tr>
		<td height="2" style="BACKGROUND-COLOR:#0000aa" width="100%">
		</td>
	</tr>
</table>
<br>
<uc1:PageNavigation id="m_ctlPageNavigation_Bottom" runat="server"></uc1:PageNavigation>
