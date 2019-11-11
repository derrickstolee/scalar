@ECHO OFF
CALL %~dp0\..\InitializeEnvironment.bat || EXIT /b 10

IF "%1"=="" GOTO USAGE
IF "%2"=="" GOTO USAGE

SETLOCAL enableextensions
SET Configuration=%1
SET SCALAR_STAGEDIR=%2

REM Prepare the staging directories for functional tests
IF EXIST %SCALAR_STAGEDIR% (
  rmdir /s /q %SCALAR_STAGEDIR%
  mkdir %SCALAR_STAGEDIR%
)

REM Publish tests to the build drop
dotnet publish %SCALAR_SRCDIR%\Scalar.FunctionalTests --configuration %Configuration% --runtime win-x64 --output %SCALAR_STAGEDIR% || EXIT /B 1
GOTO END

:USAGE
echo "ERROR: Usage: CreateFTDrop.bat [configuration] [build drop root directory]"
EXIT /b 1

:END
EXIT 0
