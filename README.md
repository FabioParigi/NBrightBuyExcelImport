NBrightBuyExcelImport : Import CSV file to https://nbrightbuy.codeplex.com/

<h1>REQUIREMENT</h1>
Dnn > V.6
Framework 4
nBSTORE V3 https://nbrightbuy.codeplex.com/
Visual Studio 2015

<h1>INSTALLATION</h1>
On the /Installation Directory you find NBrightBuyExcelImport_1.0.0_Install.zip
On DNN Host Extention install the zip file NBrightBuyExcelImport_1.0.0_Install.zip
You ll find the installed plugin on NB-Store admin area on Utilities Menu
Unzip the NBrightBuyExcelImport or copy all the source code under \DesktopModules\NBright\NBrightBuyExcelImport

<img src='http://i.imgur.com/YjvRxH3.png' border='0' alt="" />
<HR/>
<img src='http://i.imgur.com/kEYbGbl.png' border='0' alt="" />


<h1>NOTE</h1>
The import  plugin is under develop, I have tested it on 3/4 catalogues.
Please look the demo csv file I have incuded, THE COLUM NAMES MUST BE THE SAME

For custom field in the CSV demo you find a UM field, I use it like a text box field in PRDLANG
<img src='http://i.imgur.com/A6yWMWJ.png' border='0' alt="" />


<h1>SUGGESTION</h1>



Install the source version, open it in visual studio and debug the first import, you can easly modify the source import based on your CSV file
<hr>
<strong>Very important!!! Test the import in a empty Nbstore db, in this way you can easly clean the db and reimport all the time you need. When your products/categories are correct, export the xml and import it on the production db</strong>
<hr>
For any question contact me on GitHub
Fabio
