param
(
    [String]
    $SlnPath = "Elite.sln"
)

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal -ArgumentList $identity
if(-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator))
{
    throw "Please run this script with administrative privileges."
}

Write-Warning @"
Running this script will perform the following actions on your PC:
1. Modify the dependencies of the project files to point to the directory of your XboxDevices app.
2. Compile Elite.sln. It is expected that the projects were not modified from how they were packeged with the solution.
3. Compile the ElitePaddlesServiceHost which must be run simultaneously with ElitePaddles and acts as an HTTP listener for SendInput commands (http://localhost:8642/EliteService)
4. Register the above url for the active user. Existing registration will be removed as it is assumed to be stale state.
5. Generate certificate to sign the appx package. The user will be prompted for passwords to create the certs, and then again to use them (4 prompts).
6. Add the certificate to the root store and sign the appx package.
7. Deploy the appx package.
"@

Write-Host "Press any key to continue ..."
$x = $host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

if (Get-AppxPackage -Name "ElitePaddles")
{
    throw "ElitePaddles app already installed. Please remove it if you wish to proceed."
}

if(-not ($package = Get-AppxPackage -Name Microsoft.XboxDevices))
{
    throw "Failed to find the Microsoft.XboxDevices appx package. Please install the Xbox Accessories app from the Windows Store."
}
$xboxDevicesLocation = $package.InstallLocation

# Verify that Elite.sln exists and find its directory from the provided parameters
if(-not ($SlnPath.EndsWith("Elite.sln") -and (Test-Path $SlnPath -PathType Leaf)))
{
    throw "Parameter SlnPath does not resolve to 'Elite.sln'.";
}
$slnDir = $SlnPath | Resolve-Path | Split-Path

# Verify that build dependency exists
$msbuildLocation = gci "C:\Program Files*\MSBuild\14.0\Bin\MSBuild.exe" | Select -First 1 | Resolve-Path | Convert-Path
if(-not (Test-Path $msbuildLocation -PathType Leaf))
{
    throw "Could not find MSBuild.exe in . Please make sure you have the Microsoft Build Tools installed (https://www.microsoft.com/en-us/download/details.aspx?id=48159)."
}

# Verify that installutil exists to install the service
$installUtilLocation = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\installutil.exe"
if(-not (Test-Path $installUtilLocation -PathType Leaf))
{
    throw "Could not find Installutil.exe in C:\Windows\Microsoft.NET\Framework\v4.0.30319\. Please make sure the necessary version of the .NET Framework is installed."
}

#Verify that the certificate creation and signing tools exists
$makeCertLocation = "C:\Program Files (x86)\Windows Kits\10\bin\x86\makecert.exe"
$pvk2pfxLocation = "C:\Program Files (x86)\Windows Kits\10\bin\x86\pvk2pfx.exe"
$signtoolLocation = "C:\Program Files (x86)\Windows Kits\10\bin\x86\signtool.exe"
if(-not ((Test-Path $makeCertLocation -PathType Leaf) -and (Test-Path $pvk2pfxLocation -PathType Leaf) -and (Test-Path $signtoolLocation)))
{
    throw "Could not find makecert.exe, pvk2pfx.exe, or signtool.exe in C:\Program Files (x86)\Windows Kits\10\bin\x86\. Please make sure you have the Windows 10 SDK installed (https://dev.windows.com/en-us/downloads/windows-10-sdk)."
}

# Assume the csproj files exists and change their dependencies to point to the Xbox Accessories app directory
$eliteUiCsprojLocation = $slnDir + "\EliteUi\EliteUi.csproj"
if ($xboxDevicesLocation)
{
    $xml = [xml](Get-Content -Path $eliteUiCsprojLocation)
    $xml.Project.ItemGroup.Reference | ? { $_.HintPath -and $_.HintPath.Contains("XboxDevices") } | % { $file = $_.HintPath | Split-Path -Leaf; $_.HintPath = $xboxDevicesLocation + "\" + $file }
    $xml.Save($eliteUiCsprojLocation)
}

# Build the solution
& $msbuildLocation $SlnPath /m:4 /t:Rebuild "/p:Configuration=Release;OutDir=.\Out\;Platform=x64;AppxPackageSigningEnabled=false"

# Set up app package paths and make sure the installation script was generated
$eliteLocation = $slnDir + "\EliteUi\Out\EliteUi\AppPackages\EliteUi_1.0.0.0_x64_Test\"
$eliteAppxLocation = $slnDir + "\EliteUi\Out\EliteUi\AppPackages\EliteUi_1.0.0.0_x64_Test\EliteUi_1.0.0.0_x64.appx"
$eliteAppxAddLocation = $slnDir + "\EliteUi\Out\EliteUi\AppPackages\EliteUi_1.0.0.0_x64_Test\Add-AppDevPackage.ps1"
if(-not ((Test-Path $eliteAppxLocation -PathType Leaf) -and (Test-Path $eliteAppxAddLocation -PathType Leaf)))
{
    throw "EliteUi_1.0.0.0_x64.appx and Add-AppDevPackage.ps1 not in expected build location $eliteLocation."
}

# Set listener URI Reservation
netsh http add urlacl url=http://+:8642/EliteService user=$env:userdomain\$env:username

$tempPath = [System.IO.Path]::GetTempPath()
$tempDir = $tempPath + [Guid]::NewGuid()
md $tempDir
push-location
cd $tempDir

# Generate certs
try
{
& $makeCertLocation -sv ElitePaddles_TestKey.pvk -n "cn=ElitePaddlesPublisher" ElitePaddles_TestKey.cer -b 12/25/2015 -e 12/25/2025 -r
& $pvk2pfxLocation -pvk ElitePaddles_TestKey.pvk -spc ElitePaddles_TestKey.cer -pfx ElitePaddles_TestKey.pfx -po "ElitePaddles_TestKeyPw"
$pfxPath = Get-ChildItem ElitePaddles_TestKey.pfx | Convert-Path
$cerPath = Get-ChildItem ElitePaddles_TestKey.cer | Convert-Path
}
finally
{
    Pop-Location
}

# Copy certs to root store and application directory
Import-PfxCertificate -CertStoreLocation Cert:\LocalMachine\Root -FilePath $pfxPath -Password (ConvertTo-SecureString "ElitePaddles_TestKeyPw" -AsPlainText -Force)
Copy-item -path $pfxPath -Destination .
Copy-item -Path $cerPath -Destination $eliteLocation -Force

# Sign the package
& $signtoolLocation sign /fd SHA256 /a /f $pfxPath /p "ElitePaddles_TestKeyPw" $eliteAppxLocation

# Install the package
Invoke-Expression "& '$eliteAppxAddLocation'"
