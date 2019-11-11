@ECHO OFF
CALL %~dp0\InitializeEnvironment.bat || EXIT /b 10

IF "%1"=="" (SET "Configuration=Debug") ELSE (SET "Configuration=%1")

SETLOCAL
SET PATH=C:\Program Files\Scalar;C:\Program Files\Git\cmd;%PATH%

IF "%2"=="--local" GOTO :testLocal

:testPath
ECHO **************************
ECHO Testing Scalar on the PATH
ECHO **************************
ECHO %%PATH%%:
ECHO %PATH%
ECHO Scalar:
where scalar
ECHO Scalar.Service:
where scalar.service
ECHO Git:
where git

dotnet run %SCALAR_SRCDIR%\Scalar.FunctionalTests --configuration $Configuration -- %2 %3 %4 %5 %6 %7 %8
GOTO :endTests

:testLocal
SET SCALAR_EXEC_PATH=%SCALAR_OUTPUTDIR%\Scalar\bin\%Configuration%\netcoreapp3.0\win-x64\publish\scalar.exe
SET SERVICE_EXEC_PATH=%SCALAR_OUTPUTDIR%\Scalar.Service\bin\%Configuration%\netcoreapp3.0\win-x64\publish\scalar.service.exe
ECHO ********************************
ECHO Testing Scalar from build output
ECHO ********************************
ECHO Scalar: %SCALAR_EXEC_PATH%
ECHO Scalar.Service: %SERVICE_EXEC_PATH%

dotnet run %SCALAR_SRCDIR%\Scalar.FunctionalTests --configuration $Configuration -- --scalar=%SCALAR_EXEC_PATH% --service=%SERVICE_EXEC_PATH% %3 %4 %5 %6 %7 %8
GOTO :endTests

:endTests
SET error=%errorlevel%
CALL %SCALAR_SCRIPTSDIR%\StopAllServices.bat

EXIT /b %error%
