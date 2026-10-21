; FHZIPA INJECT - instalador
;
; A interface usa as cores do aplicativo: o fundo escuro, o vermelho da
; marca e o mesmo tom de texto. O aviso sobre os aplicativos no iPhone fica
; na primeira tela, antes de qualquer instalação.
;
; Os botões são desenhados pelo projeto, com a forma e a cor da marca, e
; entram no lugar dos botões do sistema, que sairiam cinza claro sobre o
; fundo escuro. Os botões do sistema continuam sendo os que levam a página
; para a frente, só que escondidos fora da vista.

Unicode true
RequestExecutionLevel user

Name "FHZIPA INJECT"
Icon "payload\app.ico"
UninstallIcon "payload\app.ico"
InstallDir "$LOCALAPPDATA\Programs\FHZIPA INJECT"

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "x64.nsh"

!ifndef APP_VERSION
  !define APP_VERSION "1.0.0"
!endif

!define APP_NAME "FHZIPA INJECT"
!define APP_PUBLISHER "FHZIPA INJECT"
!define APP_DIR "FHZIPA INJECT"
!define APP_REGKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\FHZIPA INJECT"
!define APP_WEB "https://github.com/fhzipa/fhzipa-inject"

; Os arquivos ficam ao lado do script, na pasta payload.
!define APP_EXE "fhzipa-inject.exe"
!define APP_ICON "payload\app.ico"
!define APP_PAYLOAD "payload\fhzipa-inject.exe"
!define APP_PORTRAIT "portrait.bmp"

; As cores do aplicativo, no formato do Windows (RGB).
!define CLR_BG "0x00080B10"
!define CLR_SURFACE "0x0018212B"
!define CLR_BORDER "0x002A3542"
!define CLR_TEXT "0x00F5F7FA"
!define CLR_MUTED "0x00A6B2C2"
!define CLR_DIM "0x00788596"
!define CLR_RED "0x00FF2343"

; Onde os botões ficam na tela, em unidades de diálogo.
!define BTN_Y 178u
!define BTN_W 105u
!define BTN_H 20u
!define BTN_CANCELAR_X 120u
!define BTN_PRINCIPAL_X 215u

!define MUI_ABORTWARNING
!define MUI_ICON "${APP_ICON}"
!define MUI_UNICON "${APP_ICON}"
!define MUI_BGCOLOR "${CLR_BG}"
!define MUI_TEXTCOLOR "${CLR_TEXT}"
!define MUI_WELCOMEFINISHPAGE_BITMAP "${APP_PORTRAIT}"

!define MUI_WELCOMEPAGE_TITLE "FHZIPA INJECT"
!define MUI_WELCOMEPAGE_TEXT "⚠ Aviso: a instalação de aplicativos no iPhone$\r$\n$\r$\nO FHZIPA INJECT assina os aplicativos com a sua conta Apple ID e os instala no aparelho. Aplicativos assinados assim se comportam como qualquer app da loja: a assinatura expira em 7 dias e o aplicativo precisa ser reinstalado. Um iPhone costuma aceitar 3 aplicativos assinados por conta.$\r$\n$\r$\nA Apple pode revogar a assinatura a qualquer momento, e instalar aplicativos modificados viola os Termos de Serviço da App Store."

!define MUI_FINISHPAGE_TITLE "Instalação concluída"
!define MUI_FINISHPAGE_TEXT "O FHZIPA INJECT já está instalado neste computador e pronto para uso.$\r$\n$\r$\nPara instalar aplicativos no iPhone, conecte o aparelho por USB e abra o aplicativo."

!define MUI_PAGE_CUSTOMFUNCTION_SHOW customizeWelcome
!insertmacro MUI_PAGE_WELCOME

!define MUI_PAGE_CUSTOMFUNCTION_SHOW customizeInstall
!insertmacro MUI_PAGE_INSTFILES

!define MUI_PAGE_CUSTOMFUNCTION_SHOW customizeFinish
!define MUI_PAGE_CUSTOMFUNCTION_LEAVE customizeFinishLeave
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_LANGUAGE "PortugueseBR"

Var PageText
Var BtnProximo
Var BtnSair

Function .onInit
  System::Call 'SetProcessDPIAware()i.r0'

  InitPluginsDir
  File /oname=$PLUGINSDIR\portrait.bmp "portrait.bmp"

  SetRegView 64
  WriteRegStr HKCU "${APP_REGKEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKCU "${APP_REGKEY}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "${APP_REGKEY}" "Publisher" "${APP_PUBLISHER}"
  WriteRegStr HKCU "${APP_REGKEY}" "InstallLocation" "$LOCALAPPDATA\Programs\${APP_DIR}"
  WriteRegStr HKCU "${APP_REGKEY}" "URLInfoAbout" "${APP_WEB}"
  WriteRegStr HKCU "${APP_REGKEY}" "UninstallString" '"$INSTDIR\uninstall.exe"'

  SetShellVarContext current
FunctionEnd

; ------------------------------------------------------------------ ;
; Os botões do sistema, com o texto certo                            ;
; ------------------------------------------------------------------ ;

!macro BotoesDaPagina TEXTO_PROXIMO TEXTO_SAIR
  GetDlgItem $0 $HWNDPARENT 1
  Pop $BtnProximo
  ${NSD_SetText} $BtnProximo "${TEXTO_PROXIMO}"

  GetDlgItem $0 $HWNDPARENT 2
  Pop $BtnSair
  ${NSD_SetText} $BtnSair "${TEXTO_SAIR}"
!macroend

; ------------------------------------------------------------------ ;
; A tela de boas vindas                                              ;
; ------------------------------------------------------------------ ;

Function customizeWelcome
  ; A janela da página fica atrás do diálogo, a barra de botões dela
  ; também precisa do fundo escuro.
  SetCtlColors $HWNDPARENT "" "${CLR_BG}"

  ; O aviso num cartão, com o tom do aplicativo.
  SetCtlColors $mui.WelcomePage.Text "${CLR_MUTED}" "${CLR_SURFACE}"
  CreateFont $0 "Segoe UI" "9" "400"
  SendMessage $mui.WelcomePage.Text ${WM_SETFONT} $0 0
  Pop $0

  ; O título da marca, maior e na cor da marca.
  SetCtlColors $mui.WelcomePage.Title "${CLR_TEXT}" "${CLR_BG}"
  CreateFont $0 "Segoe UI" "17" "700"
  SendMessage $mui.WelcomePage.Title ${WM_SETFONT} $0 0
  Pop $0



  !insertmacro BotoesDaPagina "Instalar" "Cancelar"
FunctionEnd

; ------------------------------------------------------------------ ;
; A instalação                                                        ;
; ------------------------------------------------------------------ ;

Function customizeInstall
  ; A janela da página também fica no escuro.
  SetCtlColors $HWNDPARENT "" "${CLR_BG}"

  ; O texto da página, no tom do aplicativo.
  GetDlgItem $0 $mui.InstFilesPage 1006
  SetCtlColors $0 "${CLR_MUTED}" "${CLR_BG}"
  CreateFont $0 "Segoe UI" "9" "400"
  SendMessage $0 ${WM_SETFONT} $0 0
  Pop $0

  ; A barra de progresso, no tema escuro.
  GetDlgItem $0 $mui.InstFilesPage 1004
  System::Call 'user32::SetWindowTheme(i $0, w "DarkMode_Explorer", w " ")i.r0'

  ; A lista de detalhes e o botão dela saem, a tela fica limpa.
  GetDlgItem $0 $mui.InstFilesPage 1016
  ShowWindow $0 ${SW_HIDE}
  GetDlgItem $0 $mui.InstFilesPage 1027
  ShowWindow $0 ${SW_HIDE}
FunctionEnd

; ------------------------------------------------------------------ ;
; A tela do fim                                                       ;
; ------------------------------------------------------------------ ;

Function customizeFinish
  ; A janela da página também fica no escuro.
  SetCtlColors $HWNDPARENT "" "${CLR_BG}"

  ; O aplicativo abre junto com a conclusão, no tom da marca.
  ${NSD_CreateLabel} 120u 158u 195u 12u "Abrir o ${APP_NAME} ao concluir"
  Pop $PageText
  SetCtlColors $PageText "${CLR_RED}" "${CLR_BG}"
  CreateFont $0 "Segoe UI" "9" "700"
  SendMessage $PageText ${WM_SETFONT} $0 0
  Pop $0


  !insertmacro BotoesDaPagina "Concluir" "Cancelar"
FunctionEnd

Function customizeFinishLeave
  ; O aplicativo abre quando o instalador termina.
  Exec "$INSTDIR\${APP_EXE}"
FunctionEnd

; ------------------------------------------------------------------ ;
; A instalação propriamente dita                                     ;
; ------------------------------------------------------------------ ;

Section "Instalar o ${APP_NAME}" SecMain
  SetOutPath "$INSTDIR"
  File "${APP_PAYLOAD}"

  ; O WebView2 é o motor da interface do aplicativo.
  DetailPrint "Verificando o WebView2..."
  IfFileExists "$TEMP\webview2.exe" 0 +2
    Delete "$TEMP\webview2.exe"

  ReadRegStr $0 HKLM "SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9E7E4C5}" "pv"
  ${If} $0 == ""
    ReadRegStr $0 HKCU "SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9E7E4C5}" "pv"
  ${EndIf}

  ${If} $0 == ""
    DetailPrint "Baixando o WebView2..."
    NSISdl::download /TIMEOUT=300 "https://go.microsoft.com/fwlink/p/?LinkId=2124703" "$TEMP\webview2.exe"
    Pop $1
    ${If} $1 == "success"
      DetailPrint "Instalando o WebView2..."
      ExecWait '"$TEMP\webview2.exe" /silent /install' $0
      Delete "$TEMP\webview2.exe"
    ${EndIf}
  ${EndIf}

  DetailPrint "Criando os atalhos..."
  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\Desinstalar.lnk" "$INSTDIR\uninstall.exe"
  CreateShortcut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"

  DetailPrint "Registrando o desinstalador..."
  WriteUninstaller "$INSTDIR\uninstall.exe"

  WriteRegStr HKCU "${APP_REGKEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKCU "${APP_REGKEY}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "${APP_REGKEY}" "Publisher" "${APP_PUBLISHER}"
  WriteRegStr HKCU "${APP_REGKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${APP_REGKEY}" "UninstallString" '"$INSTDIR\uninstall.exe"'
  WriteRegStr HKCU "${APP_REGKEY}" "URLInfoAbout" "${APP_WEB}"
  WriteRegDWORD HKCU "${APP_REGKEY}" "NoModify" 1
  WriteRegDWORD HKCU "${APP_REGKEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  Delete "$INSTDIR\${APP_EXE}"
  Delete "$INSTDIR\uninstall.exe"
  RMDir "$INSTDIR"
  RMDir /r "$SMPROGRAMS\${APP_NAME}"
  Delete "$DESKTOP\${APP_NAME}.lnk"
  DeleteRegKey HKCU "${APP_REGKEY}"
SectionEnd
