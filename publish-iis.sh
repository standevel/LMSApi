#!/bin/bash

echo "=========================================="
echo " LMSApi Backend - IIS Publish Script"
echo "=========================================="
echo ""

# Clean previous publish
echo "[1/4] Cleaning previous build..."
dotnet clean LMS.Api.csproj -c Release > /dev/null 2>&1
if [ $? -ne 0 ]; then
    echo "ERROR: dotnet clean failed"
    exit 1
fi

# Publish
echo "[2/4] Publishing to publish/iis-output..."
dotnet publish LMS.Api.csproj -c Release -r win-x64 --self-contained false -o publish/iis-output > /dev/null 2>&1
if [ $? -ne 0 ]; then
    echo "ERROR: dotnet publish failed"
    exit 1
fi

echo "[3/4] Verifying publish output..."
if [ ! -f "publish/iis-output/LMS.Api.dll" ]; then
    echo "ERROR: LMS.Api.dll not found in publish output"
    exit 1
fi
if [ ! -f "publish/iis-output/LMS.Api.exe" ]; then
    echo "ERROR: LMS.Api.exe not found in publish output"
    exit 1
fi
if [ ! -f "publish/iis-output/web.config" ]; then
    echo "WARNING: web.config not found in publish output"
fi
if [ ! -f "publish/iis-output/appsettings.json" ]; then
    echo "ERROR: appsettings.json not found in publish output"
    exit 1
fi

echo "[4/4] Done!"
echo ""
echo "=========================================="
echo " Backend published to: publish/iis-output"
echo "=========================================="
echo ""
echo "Next steps for IIS deployment:"
echo " 1. Copy the 'publish/iis-output' folder to your IIS server"
echo " 2. Create an Application Pool (Integrated Pipeline, .NET No-Code)"
echo " 3. Create a site/virtual directory pointing to the publish folder"
echo " 4. Ensure web.config is present in the deploy directory"
echo " 5. Update appsettings.json with production connection strings"
echo "    (see appsettings.Production.json.template for reference)"
echo " 6. Restart the application pool"
echo ""
