@ECHO OFF
REM ---------------------------------------------------------
REM InstallScalar.bat
REM
REM Description: Main logic for installing Scalar and supporting
REM              Components.
REM ---------------------------------------------------------

SET SCALAR_DISTRIBUTION_ROOT=%~dp0
SET GIT_INSTALLER_EXE=##GIT_INSTALLER_EXE_PLACEHOLDER##
SET SCALAR_INSTALLER_EXE=##SCALAR_INSTALLER_EXE_PLACEHOLDER##

IF "%1"=="" (SET "LOG_BASE=%TEMP%\scalar-install-logs") ELSE (SET "LOG_BASE=%1")
SET GIT_INSTALLER_LOG=%LOG_BASE%\install-git.log
SET SCALAR_INSTALLER_LOG=%LOG_BASE%\install-scalar.log

ECHO Scalar distribution root: %SCALAR_DISTRIBUTION_ROOT%
ECHO Git installer exe: %GIT_INSTALLER_EXE%
ECHO Scalar installer exe: %SCALAR_INSTALLER_EXE%
ECHO.
ECHO Git installer log: %GIT_INSTALLER_LOG%
ECHO Scalar installer log: %SCALAR_INSTALLER_LOG%

REM Clean logs from previous installations
IF EXIST "%LOG_BASE%" (
    ECHO.
    ECHO ==============================
    rmdir /s /q %LOG_BASE%
    ECHO Cleaning previous installation logs
)
mkdir %LOG_BASE%

REM Install Git
ECHO.
ECHO ==============================
ECHO Installing Git for Windows for Scalar
%SCALAR_DISTRIBUTION_ROOT%\Git\%GIT_INSTALLER_EXE% /DIR="C:\Program Files\Git" /NOICONS /COMPONENTS="ext,ext\shellhere,ext\guihere,assoc,assoc_sh" /GROUP="Git" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /ALLOWDOWNGRADE=1 /LOG="%GIT_INSTALLER_LOG%" || EXIT /B 1

REM Install Scalar
ECHO.
ECHO ==============================
ECHO Installing Scalar
%SCALAR_DISTRIBUTION_ROOT%\Scalar\%SCALAR_INSTALLER_EXE% /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG="%SCALAR_INSTALLER_LOG%" || EXIT /B 1

REM Run the post install script (if any)
IF EXIST "%SCALAR_DISTRIBUTION_ROOT%\Scripts\PostInstall.bat" (
    ECHO.
    ECHO ==============================
    ECHO Running post install script
    START "%SCALAR_DISTRIBUTION_ROOT%\Scripts\PostInstall.bat"
)

REM Installation Complete!
ECHO.
ECHO ==============================
ECHO Installation Complete!!!
