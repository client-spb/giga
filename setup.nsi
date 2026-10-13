!define APP_NAME "Giga3DApp"
!define APP_VERSION "1.0.0"
!define PUBLISH_DIR "Giga3DApp\bin\Publish\Win64"

Name "${APP_NAME}"
OutFile "Giga3DApp-Setup.exe"
InstallDir "$PROGRAMFILES64\Giga3DApp"
RequestExecutionLevel admin
SetCompressor lzma

Page directory
Page instfiles

Section "Install"
    SetOutPath "$INSTDIR"
    File /r "${PUBLISH_DIR}\*.*"

    CreateDirectory "$SMPROGRAMS\${APP_NAME}"
    CreateShortCut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\Giga3DApp.exe"
    CreateShortCut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\Giga3DApp.exe"

    WriteUninstaller "$INSTDIR\uninstall.exe"
SectionEnd

Section "Uninstall"
    Delete "$DESKTOP\${APP_NAME}.lnk"
    Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
    RMDir "$SMPROGRAMS\${APP_NAME}"
    RMDir /r "$INSTDIR"
SectionEnd
