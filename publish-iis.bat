@echo off
echo ==========================================
echo  LMSApi Backend - IIS Publish Script
echo ==========================================
echo.

REM Clean previous publish
echo [1/4] Cleaning previous build...
dotnet clean LMS.Api.csproj -c Release
if %errorlevel% neq 0 (
    echo ERROR: dotnet clean failed
    exit /b 1
)

REM Publish
echo [2/4] Publishing to publish\iis-output...
dotnet publish LMS.Api.csproj -c Release -r win-x64 --self-contained false -o publish\iis-output
if %errorlevel% neq 0 (
    echo ERROR: dotnet publish failed
    exit /b 1
)

echo [3/4] Verifying publish output...
if not exist publish\iis-output\LMS.Api.dll (
    echo ERROR: LMS.Api.dll not found in publish output
    exit /b 1
)
if not exist publish\iis-output\LMS.Api.exe (
    echo ERROR: LMS.Api.exe not found in publish output
    exit /b 1
)
if not exist publish\iis-output\web.config (
    echo WARNING: web.config not found in publish output
)
if not exist publish\iis-output\appsettings.json (
    echo ERROR: appsettings.json not found in publish output
    exit /b 1
)

echo [4/4] Done!
echo.
echo ==========================================
echo  Backend published to: publish\iis-output
echo ==========================================
echo.
echo Next steps for IIS deployment:
echo  1. Copy the 'publish\iis-output' folder to your IIS server (e.g. C:\inetpub\wwwroot\WUPortal\WUStudentPortalAPI)
echo  2. In IIS Manager, right-click the folder and select 'Convert to Application'
echo  3. Create a dedicated Application Pool (.NET CLR Version: 'No Managed Code', Managed Pipeline: Integrated)
echo  4. Ensure the ASP.NET Core Hosting Bundle (AspNetCoreModuleV2) is installed on Windows Server
echo  5. Update appsettings.json with production database connection strings and credentials
echo  6. Restart/recycle the application pool
echo.
