param()

function Test-PendingReboot
{
    if ($global:IsWindows) {
        if (Get-ChildItem "HKLM:\Software\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending" -EA Ignore) { 
            Write-Host "Detected pending reboot from component servicing"
            return $true 
        }
        if (Get-Item "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired" -EA Ignore) { 
            Write-Host "Detected pending reboot from Windows Update"
            return $true 
        }
        try { 
            $util = [wmiclass]"\\.\root\ccm\clientsdk:CCM_ClientUtilities"
            $status = $util.DetermineIfRebootPending()
            if(($null -ne $status) -and $status.RebootPending){
                Write-Host "Detected pending reboot from CCM client SDK utilities"
                return $true
            }
        } catch {}
    }
    return $false
}

$UEFSService = Get-Service -Name "UEFS Service" -ErrorAction SilentlyContinue
if ($null -ne $UEFSService) {
    if ($UEFSService.Status -eq "Running") {
        Write-Host "Stopping existing machine-level UEFS service..."
        $UEFSService | Stop-Service -Force
    }

    Write-Host "Removing existing machine-level UEFS service..."
    sc.exe delete "UEFS Service"
}

if ((Test-Path "C:\ProgramData\chocolatey\bin\choco.exe") -and
    (Test-Path "C:\ProgramData\chocolatey\bin\uefs.exe")) {
    Write-Host "Uninstalling existing machine-level UEFS..."
    C:\ProgramData\chocolatey\bin\choco.exe uninstall -y uefs
}

$IsVersion2Installed = (Test-Path "C:\Program Files (x86)\WinFsp\SxS")
$IsVersion1Installed = (!$IsVersion2Installed -and (Test-Path "C:\Program Files (x86)\WinFsp"))

if ($IsVersion1Installed) {
    # WinFsp 1.x series is installed. We have to uninstall it first.
    if ((Test-Path "C:\ProgramData\chocolatey\bin\choco.exe") -and
        ((C:\ProgramData\chocolatey\bin\choco.exe list -l | Out-String).Contains("winfsp"))) {
        Write-Host "Uninstalling existing machine-level WinFsp..."
        C:\ProgramData\chocolatey\bin\choco.exe uninstall -y winfsp

        if (Test-PendingReboot) {
            Write-Host "Computer is pending reboot after WinFsp was uninstalled, restarting now..."
            Restart-Computer -Force
        }
    }
}

$RequireVersion2Installation = $false
$TargetVersion = "2.0.23075"
$TargetVersionDownload = "v2.0/winfsp-2.0.23075.msi"
if ($IsVersion2Installed) {
    $CurrentVersion = (Get-Item "C:\Program Files (x86)\WinFsp\bin\winfsp-x64.dll").VersionInfo.FileVersion
    if (!$CurrentVersion.StartsWith($TargetVersion)) {
        # We need to upgrade WinFsp.
        $RequireVersion2Installation = $true
    }
} else {
    # Version 2 isn't installed, this is a fresh install.
    $RequireVersion2Installation = $true
}

if ($RequireVersion2Installation) {
    Write-Host "Downloading WinFsp installer..."
    Invoke-WebRequest -UseBasicParsing -Uri "https://github.com/winfsp/winfsp/releases/download/$TargetVersionDownload" -OutFile "winfsp-$TargetVersion.msi"

    Write-Host "Running WinFsp installer..."
    $DataStamp = get-date -Format yyyyMMddTHHmmss
    $LogFile = '{0}-{1}.log' -f "winfsp-$TargetVersion.msi",$DataStamp
    $MSIArguments = @(
        "/i"
        ('"{0}"' -f "winfsp-$TargetVersion.msi")
        "/qn"
        "/norestart"
        "/L*v"
        $logFile
    )
    Start-Process "msiexec.exe" -ArgumentList $MSIArguments -Wait -NoNewWindow 

    Write-Host "Logs from installation are displayed below:"
    Get-Content -Path $LogFile

    if ((Get-Content -Path $LogFile -Raw).Contains("If you just uninstalled an older version of WinFsp please restart your computer.")) {
        Write-Host "Computer is pending reboot after WinFsp was uninstalled, restarting now..."
        Restart-Computer -Force
    }
    if ((Get-Content -Path $LogFile -Raw).Contains("Installation failed")) {
        exit 1
    }

    if (Test-PendingReboot) {
        Write-Host "Computer is pending reboot after WinFsp was installed, restarting now..."
        Restart-Computer -Force
    }
}

Write-Host "Directory appears as:"
Get-ChildItem .

Write-Host "Checking if we need to make uefs.exe available system-wide..."
$ExistingUEFS = Get-Item -ErrorAction SilentlyContinue -Path "$env:ProgramFiles\UEFS"
if ($null -ne $ExistingUEFS -and $ExistingUEFS.LinkType -eq "SymbolicLink") {
    # Remove-Item is bugged in PS 5 when targeting a reparse point that no longer
    # points to a valid location.
    [System.IO.Directory]::Delete("C:\Program Files\UEFS")
}
$ExistingUEFS = Get-Item -ErrorAction SilentlyContinue -Path "$env:ProgramFiles\UEFS"
if ($null -eq $ExistingUEFS) {
    Write-Host "Linking $env:ProgramFiles\UEFS to $((Get-Location).Path) so that uefs.exe is available to other programs."
    New-Item -ItemType SymbolicLink -Path "$env:ProgramFiles\UEFS" -Target "$((Get-Location).Path)"
}
$MachinePath = [Environment]::GetEnvironmentVariable("PATH", "Machine")
if (!$MachinePath.Contains("$env:ProgramFiles\UEFS")) {
    Write-Host "Adding $env:ProgramFiles\UEFS to system-wide PATH..."
    [Environment]::SetEnvironmentVariable("PATH", $MachinePath + [IO.Path]::PathSeparator + "$env:ProgramFiles\UEFS", "Machine")
}

Write-Host "Starting UEFS daemon..."
.\uefs-daemon.exe

Write-Host "UEFS daemon exited with exit code: $LASTEXITCODE"
exit $LASTEXITCODE
