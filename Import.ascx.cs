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
//using NBrightBuy.Components;
//using NBrightBuy.Admin;
//using Categories = Nevoweb.DNN.NBrightBuy.Admin.Categories;

using GenericParsing;

//using Nevoweb.DNN.NBrightBuy.Components;
using DataProvider = DotNetNuke.Data.DataProvider;

using Nevoweb.DNN.NBrightBuy.Base;
using Nevoweb.DNN.NBrightBuy.Components;
using NBrightDNN;

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

                    // transfor the input csv to a Datatble, and after I create the xml standart import file
                    //var importXML = DataTableToXML();

                    nbi.XMLData = importCSV;

                    _recordXref = new Dictionary<int, int>();
                    _productList = new List<int>();

                    DoImport(nbi); //make the import
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
            var categoryLevelNumber = Convert.ToInt32(nbi.GetXmlProperty("genxml/textbox/categorylevelnumber"));

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
                var headers = header.Split(Char.Parse(csvDelimiter));
                DataTable tbl = new DataTable();
                for (int i = 0; i < headers.Length; i++)
                {
                    tbl.Columns.Add(headers[i]);
                }
                var data = lines.Skip(1);
                foreach (var line in data)
                {
                    var fields = line.Split(Char.Parse(csvDelimiter));  //line.Split(new[] { csvDelimiter }, StringSplitOptions.RemoveEmptyEntries);
                    DataRow newRow = tbl.Rows.Add();
                    newRow.ItemArray = fields;
                }

                //convert the datatable to a xml file in xmlbrightbuy import file format

                string xmlToImport = "";
                xmlToImport = DataTableToNbrightBuyXml(tbl, categoryLevelNumber);

                //now the file is on the standard version so i use standard nbright sysytem for import

                TextReader tr = new StringReader(xmlToImport);
                XmlDocument xmlFile = new XmlDocument();
                xmlFile.Load(tr);

                ///////////////////// standad import procedure from nbrightbuy source code import

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

                if (GenXmlFunctions.GetField(rpData, "importcategories") == "True" )//&& GenXmlFunctions.GetField(rpData, "importproducts") == "True")
                {
                    //ImportRecord(xmlFile, "CATCASCADE");
                    ImportRecord(xmlFile, "CATXREF");
                }

                RelinkNewIds();

            }

        }

        public string DataTableToNbrightBuyXml(DataTable tbl, int categoryLevelNumber)
        {
            // open template file
            var templatePath = StoreSettings.Current.FolderUploadsMapPath;
            var PRDxmltemplatepath = templatePath.Substring(0, templatePath.IndexOf("Portals")) + @"DesktopModules\NBright\NBrightBuyExcelImport\Templates\PRD_template.xml";
            var PRD_MODEL_xmltemplatepath = templatePath.Substring(0, templatePath.IndexOf("Portals")) + @"DesktopModules\NBright\NBrightBuyExcelImport\Templates\PRD_MODEL_template.xml";
            var PRDLANGxmltemplatepath = templatePath.Substring(0, templatePath.IndexOf("Portals")) + @"DesktopModules\NBright\NBrightBuyExcelImport\Templates\PRDLANG_template.xml";
            var PRDLANG_MODEL_xmltemplatepath = templatePath.Substring(0, templatePath.IndexOf("Portals")) + @"DesktopModules\NBright\NBrightBuyExcelImport\Templates\PRDLANG_MODEL_template.xml";

            var CATEGORYxmltemplatepath = templatePath.Substring(0, templatePath.IndexOf("Portals")) + @"DesktopModules\NBright\NBrightBuyExcelImport\Templates\CATEGORY_template.xml";
            var CATEGORYLANGxmltemplatepath = templatePath.Substring(0, templatePath.IndexOf("Portals")) + @"DesktopModules\NBright\NBrightBuyExcelImport\Templates\CATEGORYLANG_template.xml";
            var CATXREFxmltemplatepath = templatePath.Substring(0, templatePath.IndexOf("Portals")) + @"DesktopModules\NBright\NBrightBuyExcelImport\Templates\CATXREF_template.xml";

            string PRDxmltemplate; string PRDLANGxmltemplate; string CATEGORYxmltemplate; string CATEGORYLANGxmltemplate; string PRD_MODEL_xmltemplate;
            string PRDLANG_MODEL_xmltemplate; string CATXREFxmltemplate;
            // var xmlTemplateFile = new XmlDocument();
            if (System.IO.File.Exists(PRDxmltemplatepath)) { PRDxmltemplate = File.ReadAllText(PRDxmltemplatepath, System.Text.Encoding.GetEncoding("utf-8")); }
            else { PRDxmltemplate = ""; }
            if (System.IO.File.Exists(PRDLANGxmltemplatepath)) { PRDLANGxmltemplate = File.ReadAllText(PRDLANGxmltemplatepath, System.Text.Encoding.GetEncoding("utf-8")); }
            else { PRDLANGxmltemplate = ""; }

            if (System.IO.File.Exists(PRD_MODEL_xmltemplatepath)) { PRD_MODEL_xmltemplate = File.ReadAllText(PRD_MODEL_xmltemplatepath, System.Text.Encoding.GetEncoding("utf-8")); }
            else { PRD_MODEL_xmltemplate = ""; }
            if (System.IO.File.Exists(PRDLANG_MODEL_xmltemplatepath)) { PRDLANG_MODEL_xmltemplate = File.ReadAllText(PRDLANG_MODEL_xmltemplatepath, System.Text.Encoding.GetEncoding("utf-8")); }
            else { PRDLANG_MODEL_xmltemplate = ""; }

            if (System.IO.File.Exists(CATEGORYxmltemplatepath)) { CATEGORYxmltemplate = File.ReadAllText(CATEGORYxmltemplatepath, System.Text.Encoding.GetEncoding("utf-8")); }
            else { CATEGORYxmltemplate = ""; }
            if (System.IO.File.Exists(CATEGORYLANGxmltemplatepath)) { CATEGORYLANGxmltemplate = File.ReadAllText(CATEGORYLANGxmltemplatepath, System.Text.Encoding.GetEncoding("utf-8")); }
            else { CATEGORYLANGxmltemplate = ""; }
            if (System.IO.File.Exists(CATXREFxmltemplatepath)) { CATXREFxmltemplate = File.ReadAllText(CATXREFxmltemplatepath, System.Text.Encoding.GetEncoding("utf-8")); }
            else { CATXREFxmltemplate = ""; }


            var langList = DnnUtils.GetCultureCodeList(PortalId);
            var id = 1000;
            //langList[0] = "en-US";

            // 1) trasformo il csv in datable, estendo il datatable con n colonne in più che contengono gli id delle categorie
            // CICLO UNA VOLTA SUL DT E CREO LE CATEGORIE
            // CICLO tante volte quante sono le lingue e creo le categorie in lingua

            // 2)CICLO UNA VOLTA SUL DT E CREO IL SINGOLO PRODOTTO
            // PER OGNI PRODOTTO PER TANTE QUANTE SONO LE LINGUE CREO I CAMPI IN LINGUA
            // AL CICLO SUCCESSIVO VERIFICO SE IL COD RIMANE UGUALE è UN MODELLO ALTRIMENTI UN NUOVO PRODOTTO



            string finalXML = @"<root>";

            string articleref = ""; string modelRef = ""; string modelID = ""; string image1name = ""; string currentPRD = ""; string currentPRDMODEL = "";
            string currentPRDLANG = ""; string currentPRDLANGMODEL = "";
            string currentCATEGORY = ""; string currentCATEGORYLANG = ""; string currentCATEGORYname = "";

            //estendo il datatable aggiungendo gli id delle categorie poi ciclo su tutto e aggiungo gli id per ogni categoria

            for (int i = 1; i <= categoryLevelNumber; i++) // aggiungo colonne sul dt per inserire gli id delle categorie
            {
                tbl.Columns.Add("CategoryID_" + i.ToString());
            }
            Dictionary<string, int> Catdictionary = new Dictionary<string, int>();
            
            var firstlang = "";

            foreach (DataRow row in tbl.Rows) // CICLO SU DT
            {
                firstlang = langList[0]; // per creare le categorie prendo la prima lista

                for (int i = 1; i <= categoryLevelNumber; i++) // for each category level
                {
                    string namefield = "CATEGORY" + i.ToString() + "_" + firstlang;
                    currentCATEGORYname = row[namefield].ToString();
                    if (!Catdictionary.ContainsKey(currentCATEGORYname)) // check if the categori isn't in the Dictionary
                    {
                        id++;
                        // se la categoria è nuova la creo e creo le sueversioni in lingua
                        Catdictionary.Add(currentCATEGORYname, id); //aggiungo al dizionario
                                                                    //aggiungo l'id al datatable
                                                                    //creo la categoria
                        currentCATEGORY = CATEGORYxmltemplate;
                        currentCATEGORY = currentCATEGORY.Replace("{{itemCATEGORY.itemid}}", id.ToString());

                        string fatherCATEGORYname = row[namefield].ToString();
                        string fatherID = Catdictionary[fatherCATEGORYname].ToString();

                        if (i != 1) // se è una sotto categoria cioè se non è padre vado a settare {itemCATEGORY.parentitemid nell xml
                        {
                            string fathernamefield = "CATEGORY" + (i - 1).ToString() + "_" + firstlang;
                            var fathercolumname = row[fathernamefield].ToString();
                            var fatherIDsub = Catdictionary[fathercolumname].ToString();
                            //guardo la categoria padre, estraggo l'id e lo metto nel parent
                            currentCATEGORY = currentCATEGORY.Replace("{{itemCATEGORY.parentitemid}}", fatherIDsub);
                        }
                        else { currentCATEGORY = currentCATEGORY.Replace("{{itemCATEGORY.parentitemid}}", "0"); }
                        finalXML += currentCATEGORY;

                        var CatID_CATEGORYLANG_ParentID = id.ToString(); // store the current id of the main category for use it on the category lang

                        id++;
                        foreach (var lang in langList)
                        { //per ogni lingua apro il template e lo copio nel file finale
                            
                            currentCATEGORYLANG = CATEGORYLANGxmltemplate;
                            currentCATEGORYLANG = currentCATEGORYLANG.Replace("{{itemCATEGORYLANG.itemid}}", id.ToString());
                            currentCATEGORYLANG = currentCATEGORYLANG.Replace("{{itemCATEGORYLANG.lang}}", lang);
                            currentCATEGORYLANG = currentCATEGORYLANG.Replace("{{itemCATEGORYLANG.txtcategoryname}}", currentCATEGORYname);
                            // se è una sottocategoria inserisco il itemCATEGORYLANG.parentitemid
                            currentCATEGORYLANG = currentCATEGORYLANG.Replace("{{itemCATEGORYLANG.parentitemid}}", fatherID);

                            finalXML += currentCATEGORYLANG;
                            id++;
                        }

                    }

                    // aggiungo l'id corrente a fianco della categoria nella colonna degli id
                    row["CategoryID_" + i.ToString()] = Catdictionary[currentCATEGORYname];

                }

            }

            // adesso ho creato le categorie xml e inserisco i prodotti basandomi sulle colonne del dt contenenti gli id di categoria

            for (int index = 0; index < tbl.Rows.Count; index++)
            {
                DataRow row = tbl.Rows[index];

                /////////////////////////////////////////////////////////
                /////////////////// PRD ////////////////////////////////
                /////////////////// PRD ////////////////////////////////
                articleref = row["ARTICLEREF"].ToString();
                var beforearticleref = articleref;
                var nextarticleref = articleref;
                modelRef = row["MODELREF"].ToString();
                modelID = id.ToString();
                image1name = articleref;
                
                if (index != 0) //first round
                { beforearticleref = tbl.Rows[index - 1]["ARTICLEREF"].ToString(); }
                else beforearticleref = "-1";

                if (index < tbl.Rows.Count-1)//last round
                { nextarticleref = tbl.Rows[index + 1]["ARTICLEREF"].ToString(); }
                else nextarticleref = "-1";

                var currentPRD_id = "";

                if (articleref != beforearticleref) // IF ref different from the ref before it's a new article
                {

                    currentPRD = PRDxmltemplate;

                    //PRD
                    id++;
                    currentPRD = currentPRD.Replace("{{itemPRD.id}}", id.ToString());
                    currentPRD_id = id.ToString();
                    currentPRD = currentPRD.Replace("{{itemPRD.txtproductref}}", articleref);
                    currentPRD = currentPRD.Replace("{{itemPRD.image1name}}", image1name);

                    // for each product I create a model

                    currentPRDMODEL = PRD_MODEL_xmltemplate;
                    currentPRDMODEL = currentPRDMODEL.Replace("{{modelPRD.txtmodulref}}", modelRef);
                    currentPRDMODEL = currentPRDMODEL.Replace("{{modelPRD.txtmodulid}}", modelID);
                    //replace the model
                    currentPRD = currentPRD.Replace(@"</models>", currentPRDMODEL + @"</models>");


                    // CATXREF
                    // if is a product I add CATXREF to final xml 
                    // associate the product to the max level category
                    id++;
                    var currentCATXREF = CATXREFxmltemplate;
                    currentCATXREF = currentCATXREF.Replace("{{itemCATXREF.id}}", id.ToString());
                    var t = "CATEGORYID_" + categoryLevelNumber.ToString();
                    var CurrentCategoryID = row[t].ToString();
                    currentCATXREF = currentCATXREF.Replace("{{itemCurrentCategoryID.id}}", CurrentCategoryID);
                    currentCATXREF = currentCATXREF.Replace("{{itemCurrentProductID.id}}", currentPRD_id);

                    finalXML += currentCATXREF;

                }
                else // is a model
                {
                    // populate template
                    currentPRDMODEL = PRD_MODEL_xmltemplate;
                    currentPRDMODEL = currentPRDMODEL.Replace("{{modelPRD.txtmodulref}}", modelRef);
                    currentPRDMODEL = currentPRDMODEL.Replace("{{modelPRD.txtmodulid}}", modelID);
                    //replace the model
                    currentPRD = currentPRD.Replace(@"</models>", currentPRDMODEL + @"</models>");
                }

                // if next row different ref is a new article, so I write
                if ((articleref != nextarticleref))  finalXML += currentPRD;

                /////////////////// FINE PRD ////////////////////////////////
                /////////////////// FINE PRD ////////////////////////////////


                /////////////////// PRDLANG ////////////////////////////////
                /////////////////// PRDLANG ////////////////////////////////

                foreach (var lang in langList) // add one PRDLANG for each language
                {
                    var description = row["DESCRIPTION_" + lang].ToString() + " " +  row["DESCRIPTION2_" + lang].ToString();
                    var um = row["UM"].ToString();

                    if (articleref != beforearticleref)  // if article coderef different from article code line before is a new product else is a model
                    {

                        currentPRDLANG = PRDLANGxmltemplate;
                        currentPRDLANG = currentPRDLANG.Replace("{{itemPRDLANG.parentitemid}}", currentPRD_id);
                        id = id + 1;
                        currentPRDLANG = currentPRDLANG.Replace("{{itemPRDLANG.itemid}}", id.ToString());
                        currentPRDLANG = currentPRDLANG.Replace("{{itemPRDLANG.name}}", description);
                        currentPRDLANG = currentPRDLANG.Replace("{{itemPRDLANG.summary}}", description);
                        currentPRDLANG = currentPRDLANG.Replace("{{itemPRDLANG.txtmodelname}}", description);
                        currentPRDLANG = currentPRDLANG.Replace("{{itemPRDLANG.lang}}", lang);

                        //CUSTOM FIELD
                        currentPRDLANG = currentPRDLANG.Replace("{{itemPRDLANG.misureunit}}", um);

                        currentPRDLANGMODEL = PRDLANG_MODEL_xmltemplate;
                        currentPRDLANGMODEL = currentPRDLANGMODEL.Replace("{{modelPRDLANG.txtmodelname}}", modelRef);
                        currentPRDLANGMODEL = currentPRDLANGMODEL.Replace("{{modelPRDLANG.txtextra}}", description);

                        //replace inside the model tag , before the </models>
                        currentPRDLANG = currentPRDLANG.Replace(@"</models>", currentPRDLANGMODEL + @"</models>");
                        
                    }
                    else //is a model
                    {
                        // populate model template
                        currentPRDLANGMODEL = PRDLANG_MODEL_xmltemplate;
                        currentPRDLANGMODEL = currentPRDLANGMODEL.Replace("{{modelPRDLANG.txtmodelname}}", modelRef);
                        currentPRDLANGMODEL = currentPRDLANGMODEL.Replace("{{modelPRDLANG.txtextra}}", description);
                        //replace inside the model tag , before the </models>
                        currentPRDLANG = currentPRDLANG.Replace(@"</models>", currentPRDLANGMODEL + @"</models>");
                    }


                    // if next row different ref is a new article, so I write
                    if ((articleref != nextarticleref)) finalXML += currentPRDLANG;

                }//languagelist
                 /////////////////// END PRDLANG ////////////////////////////////
                 /////////////////// END PRDLANG ////////////////////////////////

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

        private bool ContainDataRowInDataTable(DataTable T, DataRow R)
        {
            foreach (DataRow item in T.Rows)
            {
                if (Enumerable.SequenceEqual(item.ItemArray, R.ItemArray))
                    return true;
            }
            return false;
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
