// --- Copyright (c) notice NevoWeb ---
//  Copyright (c) 2014 SARL NevoWeb.  www.nevoweb.com.
// Author: D.C.Lee and Fabio Parigi
// ------------------------------------------------------------------------
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED
// TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF
// CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
// ------------------------------------------------------------------------
// This copyright notice may NOT be removed, obscured or modified without written consent from the author.
// --- End copyright notice --- 

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.UI.WebControls;
using System.Xml;
using System.Data;
using DotNetNuke.Common;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Services.Exceptions;
using DotNetNuke.Services.Installer.Log;
using NBrightCore.common;
using NBrightCore.render;
using Nevoweb.DNN.NBrightBuy.Components;
using Nevoweb.DNN.NBrightBuy.Admin;
//using Categories = Nevoweb.DNN.NBrightBuy.Admin.Categories;
using NBrightDNN;
using GenericParsing;

using Nevoweb.DNN.NBrightBuy.Base;
using Nevoweb.DNN.NBrightBuy.Components;
using DataProvider = DotNetNuke.Data.DataProvider;


namespace Nevoweb.DNN.NBrightBuyExcelImport
{

    /// -----------------------------------------------------------------------------
    /// <summary>
    /// The ViewNBrightGen class displays the content
    /// </summary>
    /// -----------------------------------------------------------------------------
    public partial class Import : NBrightBuyAdminBase
    {
        private Dictionary<int, int> _recordXref;
        private List<int> _productList;

        #region Event Handlers

        private String _ctrlkey = "";
        private NBrightBuyController _modCtrl;
        private Dictionary<int, List<String>> _courseCatXrefs;
        private Dictionary<String, List<int>> _courseXrefCats;

        override protected void OnInit(EventArgs e)
        {
            base.OnInit(e);

            try
            {
                _modCtrl = new NBrightBuyController();

                _ctrlkey = (String)HttpContext.Current.Session["nbrightbackofficectrl"];

                #region "load templates"

                var t2 = "Importbody.html";
                // Get Display Header
                var rpDataHTempl = GetTemplateData(t2);
                rpData.ItemTemplate = NBrightBuyUtils.GetGenXmlTemplate(rpDataHTempl, StoreSettings.Current.Settings(), PortalSettings.HomeDirectory);

                #endregion


            }
            catch (Exception exc)
            {
                //display the error on the template (don;t want to log it here, prefer to deal with errors directly.)
                var l = new Literal();
                l.Text = exc.ToString();
                Controls.Add(l);
            }

        }

        protected override void OnLoad(EventArgs e)
        {
            try
            {
                base.OnLoad(e);

                if (Page.IsPostBack == false)
                {
                    PageLoad();
                }
            }
            catch (Exception exc) //Module failed to load
            {
                //display the error on the template (don;t want to log it here, prefer to deal with errors directly.)
                var l = new Literal();
                l.Text = exc.ToString();
                Controls.Add(l);
            }
        }

        private void PageLoad()
        {
            if (UserId > 0) // only logged in users can see data on this module.
            {
                // display header
                base.DoDetail(rpData, new NBrightInfo());
            }
        }

        #endregion

        #region  "Events "

        protected void CtrlItemCommand(object source, RepeaterCommandEventArgs e)
        {
            var cArg = e.CommandArgument.ToString();
            var param = new string[3];

            switch (e.CommandName.ToLower())
            {
                case "import":
                    param[0] = "";
                    var importCSV = GenXmlFunctions.GetGenXml(rpData, "", StoreSettings.Current.FolderTempMapPath);
                    var nbi = new NBrightInfo(false);

                    //trasformo il CSV in un dt e poi nel file xml e poi continuo con l'importazione standard.
                    //var importXML = DataTableToXML();

                    nbi.XMLData = importCSV;

                    _recordXref = new Dictionary<int, int>();
                    _productList = new List<int>();

                    DoImport(nbi);
                    Validate();
                    NBrightBuyUtils.SetNotfiyMessage(ModuleId, "completed", NotifyCode.ok);
                    Response.Redirect(NBrightBuyUtils.AdminUrl(TabId, param), true);
                    break;
                case "importimages":
                    param[0] = "";
                    var importImages = GenXmlFunctions.GetGenXml(rpData, "", StoreSettings.Current.FolderTempMapPath);
                    var nbimage = new NBrightInfo(false);
                    nbimage.XMLData = importImages;
                    DoImportImages(nbimage);
                    Response.Redirect(NBrightBuyUtils.AdminUrl(TabId, param), true);
                    break;
                case "cancel":
                    param[0] = "";
                    Response.Redirect(NBrightBuyUtils.AdminUrl(TabId, param), true);
                    break;
            }

        }


        #endregion

        private String GetTemplateData(String templatename)
        {
            var controlMapPath = HttpContext.Current.Server.MapPath("/DesktopModules/NBright/NBrightBuyExcelImport");
            var templCtrl = new NBrightCore.TemplateEngine.TemplateGetter(PortalSettings.Current.HomeDirectoryMapPath, controlMapPath, "Themes\\config", "");
            var templ = templCtrl.GetTemplateData(templatename, Utils.GetCurrentCulture());
            templ = Utils.ReplaceSettingTokens(templ, StoreSettings.Current.Settings());
            templ = Utils.ReplaceUrlTokens(templ);
            return templ;
        }


        private void DoImportImages(NBrightInfo nbi)
        {
            var imagesFolderPath = nbi.GetXmlProperty("genxml/textbox/imagesdir1");


            // list file inside directory
            //string[] files = Directory.GetFiles(imagesFolderPath, "*ProfileHandler.cs", SearchOption.TopDirectoryOnly);

            var imageFiles = Directory.EnumerateFiles(imagesFolderPath, "*.*", SearchOption.AllDirectories)
            .Where(s => s.EndsWith(".png") || s.EndsWith(".jpg"));



        }


        private void DoImport(NBrightInfo nbi)
        {
            //check parameters

            var csvDelimiter = nbi.GetXmlProperty("genxml/textbox/csvdelimiter");

            var csvFile = StoreSettings.Current.FolderTempMapPath + "\\" + nbi.GetXmlProperty("genxml/hidden/hiddatafile");
            if (System.IO.File.Exists(csvFile))
            {
                IEnumerable<String> lines;

                try
                {
                    lines = File.ReadAllLines(csvFile);
                }
                catch (Exception e)
                {
                    Exceptions.LogException(e);
                    NBrightBuyUtils.SetNotfiyMessage(ModuleId, "xmlloadfail", NotifyCode.fail, ControlPath + "/App_LocalResources/Import.ascx.resx");
                    return;
                }

            //check parameters ok
            
            //cicle on csv rows and create a datatable

                
                String header = lines.First();
                var headers = header.Split(new[] { csvDelimiter }, StringSplitOptions.RemoveEmptyEntries);
                DataTable tbl = new DataTable();
                for (int i = 0; i < headers.Length; i++)
                {
                    tbl.Columns.Add(headers[i]);
                }
                var data = lines.Skip(1);
                foreach (var line in data)
                {
                    var fields = line.Split(new[] { csvDelimiter }, StringSplitOptions.RemoveEmptyEntries);
                    DataRow newRow = tbl.Rows.Add();
                    newRow.ItemArray = fields;
                }

                //trasformo il datatable in un xml nel formato xmlbrightbuy uguale a quello di esportazione

                string xmlDaimportare = "";
                xmlDaimportare = DataTableToNbrightBuyXml(tbl);

                //now the file is on the standard version so i use standard nbright sysytem for import

                TextReader tr = new StringReader(xmlDaimportare);
                XmlDocument xmlFile = new XmlDocument();
                xmlFile.Load(tr);

                if (true) // (GenXmlFunctions.GetField(rpData, "importproducts") == "True")
                {
                    ImportRecord(xmlFile, "PRD");
                    ImportRecord(xmlFile, "PRDLANG");
                    ImportRecord(xmlFile, "PRDXREF");
                }

                if (GenXmlFunctions.GetField(rpData, "importcategories") == "True")
                {
                    ImportRecord(xmlFile, "CATEGORY");
                    ImportRecord(xmlFile, "CATEGORYLANG");
                }

                RelinkNewIds();

            }

        }

        public string DataTableToNbrightBuyXml(DataTable tbl)
        {
            // open template file
            var templatePath = StoreSettings.Current.FolderUploadsMapPath;
            var PRDxmltemplatepath=  templatePath.Substring(0, templatePath.IndexOf("Portals")) + @"DesktopModules\NBright\NBrightBuyExcelImport\PRD_template.xml";
            var PRDLANGxmltemplatepath = templatePath.Substring(0, templatePath.IndexOf("Portals")) + @"DesktopModules\NBright\NBrightBuyExcelImport\PRDLANG_template.xml";
            var CATEGORYxmltemplatepath = templatePath.Substring(0, templatePath.IndexOf("Portals")) + @"DesktopModules\NBright\NBrightBuyExcelImport\CATEGORY_template.xml";
            var CATEGORYLANGxmltemplatepath = templatePath.Substring(0, templatePath.IndexOf("Portals")) + @"DesktopModules\NBright\NBrightBuyExcelImport\CATEGORYDLANG_template.xml";
            string PRDxmltemplate; string PRDLANGxmltemplate; string CATEGORYxmltemplate; string CATEGORYLANGxmltemplate;

            // var xmlTemplateFile = new XmlDocument();
            if (System.IO.File.Exists(PRDxmltemplatepath)) { PRDxmltemplate = File.ReadAllText(PRDxmltemplatepath, System.Text.Encoding.GetEncoding("utf-8")); }
            else {PRDxmltemplate = "";}
            if (System.IO.File.Exists(PRDLANGxmltemplatepath)) { PRDLANGxmltemplate = File.ReadAllText(PRDLANGxmltemplatepath, System.Text.Encoding.GetEncoding("utf-8")); }
            else { PRDLANGxmltemplate = ""; }

            if (System.IO.File.Exists(CATEGORYxmltemplatepath)) { CATEGORYxmltemplate = File.ReadAllText(CATEGORYxmltemplatepath, System.Text.Encoding.GetEncoding("utf-8")); }
            else { CATEGORYxmltemplate = ""; }
            if (System.IO.File.Exists(CATEGORYLANGxmltemplatepath)) { CATEGORYLANGxmltemplate = File.ReadAllText(CATEGORYLANGxmltemplatepath, System.Text.Encoding.GetEncoding("utf-8")); }
            else { CATEGORYLANGxmltemplate = ""; }

            var langList = DnnUtils.GetCultureCodeList(PortalId);
            var id = 1000;
            //langList[0] = "en-US";

            // CICLO UNA VOLTA SUL DT E CREO I PRODOTTI
            // CICLO tante volte quante sono le lingue e creo i prodotti
            // CICLO UNA VOLTA SUL DT E CREO LE CATEGORIE
            // CICLO tante volte quante sono le lingue e creo le categorie in lingua


            string finalXML = @"<root>"; ;

            foreach (DataRow row in tbl.Rows) // CICOLO SU DT
            {


                foreach (var lang in langList)
                {
                    //foreach (DataColumn col in tbl.Columns)
                    // funziona devo solo inserire i modelli ad ogni ciclo di riga devo verificare se il cod articolo cambia, se non cambia è un modello
                    {
                        // fields row from dt
                        var category1 = row["CATEGORY1_" + lang].ToString();
                        var category2 = row["CATEGORY2_" + lang].ToString();
                        var articleref = row["ARTICLEREF"].ToString();
                        var modelRef = row["MODELREF"].ToString();
                        var modelID = id.ToString();
                        var description = row["DESCRIPTION_" + lang].ToString();
                        var description2 = row["DESCRIPTION2_" + lang].ToString();
                        var image1name = articleref;

                        var currentPRD = PRDxmltemplate;
                        var currentPRDLANG = PRDLANGxmltemplate;

                        //PRD
                        id = id+1;
                        currentPRD = currentPRD.Replace("{{itemPRD.id}}", id.ToString());
                        currentPRD = currentPRD.Replace("{{itemPRD.txtproductref}}", articleref);
                        currentPRD = currentPRD.Replace("{{itemPRD.txtmodulref}}", modelRef);
                        currentPRD = currentPRD.Replace("{{itemPRD.txtmodulid}}", modelID);
                        currentPRD = currentPRD.Replace("{{itemPRD.image1name}}", image1name);

                        //PRDLANG
                        currentPRDLANG = currentPRDLANG.Replace("{{itemPRDLANG.parentitemid}}", id.ToString());
                        id = id+1;
                        currentPRDLANG = currentPRDLANG.Replace("{{itemPRDLANG.itemid}}", id.ToString());
                        currentPRDLANG = currentPRDLANG.Replace("{{itemPRDLANG.name}}", description + " " + description2);
                        currentPRDLANG = currentPRDLANG.Replace("{{itemPRDLANG.summary}}", description + " " + description2);
                        currentPRDLANG = currentPRDLANG.Replace("{{itemPRDLANG.txtmodelname}}", description + " " + description2);
                        currentPRDLANG = currentPRDLANG.Replace("{{itemPRDLANG.lang}}", lang);

                        finalXML += currentPRD + currentPRDLANG;

                    }

                }//languagelist
            }// foreach (DataRow row in tbl.Rows)



            finalXML += @"</root>";
            return finalXML;
        }// end function

        private void ImportRecord(XmlDocument xmlFile, String typeCode, Boolean updaterecordsbyref = true)
        {
            var nodList = xmlFile.SelectNodes("root/item[./typecode='" + typeCode + "']");
            if (nodList != null)
            {
                foreach (XmlNode nod in nodList)
                {
                    var nbi = new NBrightInfo();
                    nbi.FromXmlItem(nod.OuterXml);
                    var olditemid = nbi.ItemID;

                    // check to see if we have a new record or updating a existing one, use the ref field to find out.
                    nbi.ItemID = -1;
                    nbi.PortalId = PortalId;

                    if (typeCode == "PRD" && updaterecordsbyref)
                    {
                        var itemref = nbi.GetXmlProperty("genxml/textbox/txtproductref");
                        if (itemref != "")
                        {
                            var l = ModCtrl.GetList(PortalId, -1, "PRD", " and NB3.ProductRef = '" + itemref.Replace("'", "''") + "' ");
                            if (l.Count > 0) nbi.ItemID = l[0].ItemID;
                        }
                    }
                    if (typeCode == "PRDLANG" && updaterecordsbyref)
                    {
                        if (_recordXref.ContainsKey(nbi.ParentItemId))
                        {
                            var l = ModCtrl.GetList(PortalId, -1, "PRDLANG", " and NB1.parentitemid = '" + _recordXref[nbi.ParentItemId].ToString("") + "' and NB1.Lang = '" + nbi.Lang + "'");
                            if (l.Count > 0) nbi.ItemID = l[0].ItemID;
                            nbi.ParentItemId = _recordXref[nbi.ParentItemId];
                        }
                    }
                    if (typeCode == "CATEGORY" && updaterecordsbyref)
                    {
                        var itemref = nbi.GetXmlProperty("genxml/textbox/txtcategoryref");
                        if (itemref != "")
                        {
                            var l = ModCtrl.GetList(PortalId, -1, "CATEGORY", " and [XMLData].value('(genxml/textbox/txtcategoryref)[1]','nvarchar(max)') = '" + itemref.Replace("'", "''") + "' ");
                            if (l.Count > 0) nbi.ItemID = l[0].ItemID;
                        }
                    }
                    if (typeCode == "CATEGORYLANG" && updaterecordsbyref)
                    {
                        if (_recordXref.ContainsKey(nbi.ParentItemId))
                        {
                            var l = ModCtrl.GetList(PortalId, -1, "CATEGORYLANG", " and NB1.parentitemid = '" + _recordXref[nbi.ParentItemId].ToString("") + "' and NB1.Lang = '" + nbi.Lang + "'");
                            if (l.Count > 0) nbi.ItemID = l[0].ItemID;
                            nbi.ParentItemId = _recordXref[nbi.ParentItemId];
                        }
                    }
                    if (typeCode == "GROUP" && updaterecordsbyref)
                    {
                        var itemref = nbi.GetXmlProperty("genxml/textbox/groupref");
                        if (itemref != "")
                        {
                            var l = ModCtrl.GetList(PortalId, -1, "GROUP", " and [XMLData].value('(genxml/textbox/groupref)[1]','nvarchar(max)') = '" + itemref.Replace("'", "''") + "' ");
                            if (l.Count > 0) nbi.ItemID = l[0].ItemID;
                        }
                    }
                    if (typeCode == "GROUPLANG" && updaterecordsbyref)
                    {
                        if (_recordXref.ContainsKey(nbi.ParentItemId))
                        {
                            var l = ModCtrl.GetList(PortalId, -1, "GROUPLANG", " and NB1.parentitemid = '" + _recordXref[nbi.ParentItemId].ToString("") + "' and NB1.Lang = '" + nbi.Lang + "'");
                            if (l.Count > 0) nbi.ItemID = l[0].ItemID;
                            nbi.ParentItemId = _recordXref[nbi.ParentItemId];
                        }
                    }
                    if (typeCode == "SETTINGS") // the setting exported are only the portal settings, not module.  So always update and don;t create new.
                    {
                        var l = ModCtrl.GetList(PortalId, 0, "SETTINGS", " and NB1.GUIDKey = 'NBrightBuySettings' ");
                        if (l.Count > 0) nbi.ItemID = l[0].ItemID;
                    }
                    //NOTE: if ORDERS are imported, we expect those to ALWAYS be new records, we don't want to delete any validate orders in this import.

                    // NOTE: we expect the records to be done in typecode order so we know parent and xref itemids.

                    var newitemid = ModCtrl.Update(nbi);
                    if (newitemid > 0) _recordXref.Add(olditemid, newitemid);
                    if (typeCode == "PRD") _productList.Add(newitemid);

                }


            }
        }

        private void RelinkNewIds()
        {
            var l = ModCtrl.GetList(PortalId, -1, "CATEGORY");
            foreach (var i in l)
            {
                if (_recordXref.ContainsKey(i.ParentItemId))
                {
                    i.ParentItemId = _recordXref[i.ParentItemId];
                    ModCtrl.Update(i);
                }
                var pcatid = i.GetXmlProperty("genxml/dropdownlist/ddlparentcatid");
                if (Utils.IsNumeric(pcatid) && pcatid != "0")
                {
                    if (_recordXref.ContainsKey(Convert.ToInt32(pcatid)))
                    {
                        i.SetXmlProperty("genxml/dropdownlist/ddlparentcatid", _recordXref[Convert.ToInt32(pcatid)].ToString());
                        ModCtrl.Update(i);
                    }
                }
            }

            l = ModCtrl.GetList(PortalId, -1, "CATCASCADE");
            foreach (var i in l)
            {
                UpdateXrefRecords(i);
            }

            l = ModCtrl.GetList(PortalId, -1, "CATXREF");
            foreach (var i in l)
            {
                UpdateXrefRecords(i);
            }

            l = ModCtrl.GetList(PortalId, -1, "PRDXREF");
            foreach (var i in l)
            {
                UpdateXrefRecords(i);
            }

        }

        private void UpdateXrefRecords(NBrightInfo nbi)
        {
            // Get new parentitemid  
            if (_recordXref.ContainsKey(nbi.ParentItemId)) nbi.ParentItemId = _recordXref[nbi.ParentItemId];
            // Get new xrefitemid  
            if (_recordXref.ContainsKey(nbi.XrefItemId)) nbi.XrefItemId = _recordXref[nbi.XrefItemId];
            // if we have a xref record update the guidkey
            if (nbi.ParentItemId > 0 && nbi.XrefItemId > 0)
            {
                nbi.GUIDKey = nbi.XrefItemId.ToString("") + "x" + nbi.ParentItemId.ToString("");
                //if we already have a record with this xref guid then we don;t need to update
                ModCtrl.Update(nbi);
            }

        }

        private void Validate()
        {
            foreach (var r in _productList)
            {
                // if product then validate the data.
                var prodData = ProductUtils.GetProductData(r, StoreSettings.Current.EditLanguage);
                if (prodData.Exists)
                {
                    prodData.Validate();
                    prodData.Save();
                }
            }
        }
    }


}
