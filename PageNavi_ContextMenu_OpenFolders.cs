//#################################################################################################################################################
// ESS - PageNavi_ContextMenu_OpenFolders
//#################################################################################################################################################
// Erweiterung des Kontextmenüs im Seitennavigator zum schnellen Öffnen der Verzeichnisse $(P), $(DOC) und $(IMG)
// EPLAN GmbH & Co. KG
//#################################################################################################################################################
//#################################################################################################################################################

using System.IO;
using System.Windows.Forms;
using Eplan.EplApi.ApplicationFramework;
using Eplan.EplApi.Gui;
using Eplan.EplApi.Base;
using Eplan.EplApi.Scripting;

//[C#]
public class PageNavi_ContextMenu_OpenFolders
{
    #region define global variables
    public static ISOCode.Language global_GuiLanguage = new Languages().GuiLanguage.GetNumber();
    #endregion

    [DeclareAction("OpenFolder")]
    public void XOpenFolder(string FolderName)
    {
        if (FolderName != string.Empty)
        {
            if (FolderName.StartsWith("$("))
            {
                FolderName = Eplan.EplApi.Base.PathMap.SubstitutePath(FolderName);
            }

            DirectoryInfo oDI = new DirectoryInfo(FolderName);
            if (oDI.Exists)
            {
                //Exportfile öffnen
                System.Diagnostics.ProcessStartInfo proc = new System.Diagnostics.ProcessStartInfo();
                proc.FileName = "explorer.exe";
                proc.Arguments = FolderName;
                System.Diagnostics.Process.Start(proc);
            }
        }
    }

    [DeclareMenu()]
    public void CreateMenu()
    {

        #region language-depending menu-texts
        MultiLangString oMLSMenuText1 = new MultiLangString();
        oMLSMenuText1.AddString(ISOCode.Language.L_de_DE, "$(P)-Verzeichnis öffnen...");
        oMLSMenuText1.AddString(ISOCode.Language.L_en_US, "Open $(P) folder...");
        string _sGui_MenuText1 = oMLSMenuText1.GetStringToDisplay(global_GuiLanguage);
        if (String.IsNullOrEmpty(_sGui_MenuText1))
        {
            //if actual GUI-language is not defined in multi-language-string, use en_US-text-version
            _sGui_MenuText1 = "Open $(P) folder...";
        }

        MultiLangString oMLSMenuText2 = new MultiLangString();
        oMLSMenuText2.AddString(ISOCode.Language.L_de_DE, "$(DOC)-Verzeichnis öffnen...");
        oMLSMenuText2.AddString(ISOCode.Language.L_en_US, "Open $(DOC) folder...");
        string _sGui_MenuText2 = oMLSMenuText2.GetStringToDisplay(global_GuiLanguage);
        if (String.IsNullOrEmpty(_sGui_MenuText2))
        {
            //if actual GUI-language is not defined in multi-language-string, use en_US-text-version
            _sGui_MenuText2 = "Open $(DOC) folder...";
        }

        MultiLangString oMLSMenuText3 = new MultiLangString();
        oMLSMenuText3.AddString(ISOCode.Language.L_de_DE, "$(IMG)-Verzeichnis öffnen...");
        oMLSMenuText3.AddString(ISOCode.Language.L_en_US, "Open $(IMG) folder...");
        string _sGui_MenuText3 = oMLSMenuText3.GetStringToDisplay(global_GuiLanguage);
        if (String.IsNullOrEmpty(_sGui_MenuText3))
        {
            //if actual GUI-language is not defined in multi-language-string, use en_US-text-version
            _sGui_MenuText3 = "Open $(IMG) folder...";
        }
        #endregion

        #region expan context menues
        //expand context-menu in page-navigator (tree-view)
        ContextMenuLocation oCtxLoc = new ContextMenuLocation();     
        oCtxLoc.DialogName = "PmPageObjectTreeDialog";
        oCtxLoc.ContextMenuName = "1007";

        Eplan.EplApi.Gui.ContextMenu oCTXMnu = new Eplan.EplApi.Gui.ContextMenu();
        oCTXMnu.AddMenuItem(oCtxLoc, _sGui_MenuText1, "OpenFolder /FolderName:$(PROJECTPATH)", true, false);
        oCTXMnu.AddMenuItem(oCtxLoc, _sGui_MenuText2, "OpenFolder /FolderName:$(DOC)", false, false);
        oCTXMnu.AddMenuItem(oCtxLoc, _sGui_MenuText3, "OpenFolder /FolderName:$(IMG)", false, false);
        #endregion
    }
   
}
