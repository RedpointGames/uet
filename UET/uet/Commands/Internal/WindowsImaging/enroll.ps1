param([string] $ImagePath, [string] $ImageIndex, [string] $ProvisioningPackagePath)

function Start-RecoveryShell() {
    Write-Host "error: This machine has a unknown disk or partition layout. It either already has an operating system installed, has too many disks attached, or the previous partitioning for UET boot failed. Reset the primary (and only) disk on the machine, and then reboot."
    powershell.exe

    # Restart the computer after the recovery shell.
    Restart-Computer -Force

    # Prevent exiting from this process until the machine reboots.
    Start-Sleep -Seconds 10000
}

function Provision-Windows() {
    # Remove any existing disk.
    if (Test-Path W:\Windows.vhdx) {
        Remove-Item -Force -path W:\Windows.vhdx
    }

    # Create and attach the disk.
    $TargetSize = [Math]::Round(((Get-Volume -DriveLetter W).Size / 1024 / 1024) - (25 * 1024))
    $Script = @"
create vdisk file="W:\Windows.vhdx" type=fixed maximum=$TargetSize
attach vdisk
clean
create partition primary
format quick fs=ntfs
assign letter=c
exit
"@
    Set-Content -Path "$env:TEMP\vdisksetup.txt" -Value $Script
    diskpart /s "$env:TEMP\vdisksetup.txt"
    if ($LastExitCode -ne 0) {
        Start-RecoveryShell
        exit 1
    }

    # Wait for network stable.
    while (!(Test-Path $ImagePath)) {
        Start-Sleep -Seconds 1
    }

    # Apply image.
    if (Test-Path S:\EFI) {
        Remove-Item -Recurse -Force S:\EFI
    }
    dism /Apply-Image /ImageFile:$ImagePath /Index:$ImageIndex /ApplyDir:C:\
    if (Test-Path S:\EFI) {
        Remove-Item -Recurse -Force S:\EFI
    }

    # Install Hyper-V and Containers features.
    dism /Image:C:\ /Enable-Feature /FeatureName:Containers /FeatureName:Microsoft-Hyper-V /All /ScratchDir:W:\

    # Apply provisioning package.
    dism /Image=C:\ /Add-ProvisioningPackage /PackagePath:$ProvisioningPackagePath

    # Set up bootloader.
    Remove-Item -Recurse -Force S:\EFI
    bcdboot C:\Windows

    # todo: determine target computer name
    $TargetComputerName = "testunattend"

    # Set up unattend.xml to set computer name.
    New-Item -ItemType Directory -Path C:\Windows\Panther
    Set-Content -Path "C:\Windows\Panther\unattend.xml" -Value `
@"
<?xml version="1.0" encoding="utf-8"?>
<unattend xmlns="urn:schemas-microsoft-com:unattend">
    <settings pass="specialize">
        <component name="Microsoft-Windows-Shell-Setup" processorArchitecture="amd64"
            publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS"
            xmlns:wcm="http://schemas.microsoft.com/WMIConfig/2002/State"
            xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
            <ComputerName>$TargetComputerName</ComputerName>
        </component>
    </settings>
    <settings pass="oobeSystem">
        <component name="Microsoft-Windows-Shell-Setup" processorArchitecture="amd64"
            publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS"
            xmlns:wcm="http://schemas.microsoft.com/WMIConfig/2002/State"
            xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
            <OOBE>
                <HideEULAPage>true</HideEULAPage>
                <SkipMachineOOBE>true</SkipMachineOOBE>
                <SkipUserOOBE>true</SkipUserOOBE>
            </OOBE>
        </component>
    </settings>
</unattend>
"@

    # todo: copy ipxe and related files to local disk EFI
    # todo: set order to boot ipxe by default
    # todo: set once-off override to boot into windows.

    Restart-Computer
}

function Continue-Setup() {
    # Provision Windows.
    Provision-Windows
    exit 0
}

function Start-Enroll() {
    $Disks = Get-Disk | Where-Object { $_.BusType -ne "USB" }
    if (($Disks | Measure-Object).Count -ne 1) {
        Write-Host "Incorrect number of disks."
        Start-RecoveryShell
        exit 1
    }

    $Disk = $Disks[0]

    if ($Disk.PartitionStyle -eq "RAW") {
        Initialize-Disk -PartitionStyle GPT -Number $Disk.Number
    }

    $Partitions = Get-Partition -DiskNumber $Disk.Number -ErrorAction SilentlyContinue
    if ($Partitions.Length -eq 3) {
        $Volume = Get-Volume -Partition $Partitions[2] -ErrorAction SilentlyContinue
        if ($null -eq $Volume) {
            Write-Host "Unable to access volume 3."
            Start-RecoveryShell
            exit 1
        }

        if ($Volume.FileSystemLabel -ne "UETBootDisk") {
            Write-Host "Volume 3 does not have friendly name 'UETBootDisk'."
            Start-RecoveryShell
            exit 1
        }

        # We have a UET boot disk, and this machine is configured to be managed by UET. Exit now
        # and let the next script handle installing Windows to a VHDX if needed.
        Write-Host "Detected that this machine is already set up for UET."
        Set-Partition -DiskNumber $Disk.Number -PartitionNumber 1 -NewDriveLetter "S"
        Set-Partition -DiskNumber $Disk.Number -PartitionNumber 3 -NewDriveLetter "W"
        Continue-Setup
        exit 0
    } elseif ($Partitions.Length -ne 0) {
        Write-Host "Partition count is not equal to 0."
        Start-RecoveryShell
        exit 1
    }

    Write-Host "Detected that this machine does not have any existing partition setup."
    $Confirmation = Read-Host "Do you want to format the disk and enroll this machine to be automatically managed by UET?"
    if ($Confirmation -ne "y") {
        Start-RecoveryShell
        exit 1
    }

    $EFI = New-Partition -DiskNumber $Disk.Number -Size 260MB -DriveLetter "S" -GptType "{c12a7328-f81f-11d2-ba4b-00a0c93ec93b}"
    $EFI | Format-Volume -FileSystem FAT32 -NewFileSystemLabel "System" -Force
    $MSR = New-Partition -DiskNumber $Disk.Number -Size 16MB -GptType "{e3c9e316-0b5c-4db8-817d-f92df00215ae}"
    $UETBootDisk = New-Partition -DiskNumber $Disk.Number -UseMaximumSize -DriveLetter "W"
    $UETBootDisk | Format-Volume -FileSystem NTFS -NewFileSystemLabel "UETBootDisk" -Force
    Continue-Setup
    exit 0
}

if (!(Test-Path $ImagePath)) 
{
    Write-Host "Missing file at '$ImagePath'! Can not continue."
    Start-RecoveryShell
    exit 1
}

if (!(Test-Path $ProvisioningPackagePath)) 
{
    Write-Host "Missing file at '$ProvisioningPackagePath'! Can not continue."
    Start-RecoveryShell
    exit 1
}

Start-Enroll