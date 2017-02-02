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
4. Generate certificate to sign the appx package. The user will be prompted for passwords to create the certs, and then again to use them (4 prompts).
5. Add the certificate to the root store and sign the appx package.
6. Deploy the appx package.
"@

Write-Host "Press any key to continue ..."
$x = $host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

if (Get-AppxPackage -Name "ElitePaddles")
{
    throw "ElitePaddles app already installed. Please remove it if you wish to proceed."
}

$systemType = (gwmi win32_computersystem).SystemType
if($systemType.StartsWith("x64"))
{
	$procArch = "x64"
}
else
{
	$procArch = "x86"
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
$slnDir = $SlnPath | Resolve-Path | Split-Path | Convert-Path

# Verify that build dependency exists
$msbuildLocation = gci "C:\Program Files*\MSBuild\14.0\Bin\MSBuild.exe" | Select -First 1 | Resolve-Path | Convert-Path
if(-not (Test-Path $msbuildLocation -PathType Leaf))
{
    throw "Could not find MSBuild.exe in $msbuildLocation. Please make sure you have the Microsoft Build Tools installed (https://www.microsoft.com/en-us/download/details.aspx?id=48159)."
}

# Verify that installutil exists to install the service
$installUtilLocation = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\installutil.exe"
if(-not (Test-Path $installUtilLocation -PathType Leaf))
{
    throw "Could not find Installutil.exe in C:\Windows\Microsoft.NET\Framework\v4.0.30319\. Please make sure the necessary version of the .NET Framework is installed."
}

#Verify that the certificate creation and signing tools exists
$signtoolLocation = gci "C:\Program Files*\Windows Kits\10\bin\x86\signtool.exe" | Select -First 1 | Resolve-Path | Convert-Path
if(-not ((Test-Path $makeCertLocation -PathType Leaf)))
{
    throw "Could not find signtool.exe in C:\Program Files*\Windows Kits\10\bin\x86\. Please make sure you have the Windows 10 SDK installed (https://dev.windows.com/en-us/downloads/windows-10-sdk)."
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
& $msbuildLocation $SlnPath /m:4 /t:Rebuild "/p:Configuration=Release;OutDir=.\Out\;Platform=${procArch};AppxPackageSigningEnabled=false"

# Set up app package paths and make sure the installation script was generated
$eliteLocation = $slnDir + "\EliteUi\Out\EliteUi\AppPackages\EliteUi_1.0.0.0_${procArch}_Test\"
$eliteAppxLocation = $slnDir + "\EliteUi\Out\EliteUi\AppPackages\EliteUi_1.0.0.0_${procArch}_Test\EliteUi_1.0.0.0_${procArch}.appx"
$eliteAppxAddLocation = $slnDir + "\EliteUi\Out\EliteUi\AppPackages\EliteUi_1.0.0.0_${procArch}_Test\Add-AppDevPackage.ps1"
if(-not ((Test-Path $eliteAppxLocation -PathType Leaf) -and (Test-Path $eliteAppxAddLocation -PathType Leaf)))
{
    throw "EliteUi_1.0.0.0_x64.appx and Add-AppDevPackage.ps1 not in expected build location $eliteLocation."
}

<#$tempPath = [System.IO.Path]::GetTempPath()
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
#Erase old versions of the cert
gci cert:\localmachine\root | ? { $_.Subject -eq "CN=ElitePaddlesPublisher" } | Remove-Item

# Copy certs to root store and application directory
Import-PfxCertificate -CertStoreLocation Cert:\LocalMachine\Root -FilePath $pfxPath -Password (ConvertTo-SecureString "ElitePaddles_TestKeyPw" -AsPlainText -Force)
Copy-item -path $pfxPath -Destination .
Copy-item -Path $cerPath -Destination $eliteLocation -Force#>

$fp = "ElitePaddlesAppCert.pfx"

powershell.exe -sta -command {
$subject = "CN=ElitePaddlesPublisher"
$pw = (ConvertTo-SecureString "Temp123" -AsPlainText -Force)
$fp = "ElitePaddlesAppCert.pfx"
gci cert:\currentuser\my | ? { $_.Subject -eq $subject } | Remove-Item
gci cert:\localmachine\root | ? { $_.Subject -eq $subject } | Remove-Item

$subjectEnc = New-Object -ComObject X509Enrollment.CX500DistinguishedName
$subjectEnc.Encode($subject, 0)
$ids = New-Object -ComObject X509Enrollment.CObjectIDs
"1.3.6.1.5.5.7.3.3","1.3.6.1.4.1.311.10.3.13" | % { $id = New-Object -ComObject X509Enrollment.CObjectID; $id.InitializeFromValue(([Security.Cryptography.Oid]$_).Value); $ids.Add($id) }

$eku = New-Object -ComObject X509Enrollment.CX509ExtensionEnhancedKeyUsage
$eku.InitializeEncode($ids)

$algId = New-Object -ComObject X509Enrollment.CObjectId
$algId.InitializeFromValue(([Security.Cryptography.Oid]"RSA").Value)
$pk = New-Object -ComObject X509Enrollment.CX509PrivateKey
$pk.ProviderName = "Microsoft Enhanced Cryptographic Provider v1.0"
$pk.KeySpec = 2 #or 1 for Exchange
$pk.Length = 2048
$pk.MachineContext = $false #false for CU, true for LM
$pk.ExportPolicy = 1; #exportable
$pk.Algorithm = $algId
$pk.Create()
$cert = New-Object -ComObject X509Enrollment.CX509CertificateRequestCertificate
$cert.InitializeFromPrivateKey(1,$pk,"")
$cert.Subject = $subjectEnc
$cert.Issuer = $subjectEnc
$cert.NotBefore = [DateTime]::Now
$cert.NotAfter = [DateTime]::Now.AddYears(5)
$cert.X509Extensions.Add($eku)
$sigAlg = New-Object -ComObject X509Enrollment.CObjectId
$sigAlg.InitializeFromValue(([Security.Cryptography.Oid]"SHA256").Value)
$cert.SignatureInformation.HashAlgorithm = $sigAlg
$cert.Encode()
$certReq = New-Object -ComObject X509Enrollment.CX509enrollment
$certReq.InitializeFromRequest($cert)
$certReq.CertificateFriendlyName = ""
$end = $certReq.CreateRequest(1)
$certReq.InstallResponse(2,$end,1,"")
$data = $certReq.CreatePFX([Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($pw)),0,1)
Set-Content -Path $fp -Value ([Convert]::FromBase64String($data)) -Encoding Byte
[Byte[]]$bytes = [Convert]::FromBase64String($end)
New-Object Security.Cryptography.X509Certificates.X509Certificate2 @(,$bytes)

Import-PfxCertificate -Password (ConvertTo-SecureString "Temp123" -Force -AsPlainText) -FilePath $fp -CertStoreLocation cert:\Localmachine\root
}

# Sign the package
& $signtoolLocation sign /debug /fd SHA256 /a /f $fp /p "Temp123" $eliteAppxLocation

# Install the package
Invoke-Expression "& '$eliteAppxAddLocation'"