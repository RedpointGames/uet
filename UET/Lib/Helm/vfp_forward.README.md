## Patching vfp filter class for Windows 11 clients

This patch is not yet automatically handled by RKM / `uet cluster start`. On Windows 11 machines, you need to apply the registry keys in the `vfp_forward.reg` file by running in an Administrator prompt:

```ps1
$ConfirmPreference = "None"
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Start-Process powershell -ArgumentList "-NoProfile -File `"$PSCommandPath`"" -Verb RunAs
    exit
}
Set-ExecutionPolicy -ExecutionPolicy bypass
Install-Module -Name NtObjectManager
Start-Service -Name TrustedInstaller
$parent = Get-NtProcess -ServiceName TrustedInstaller
$proc = New-Win32Process cmd.exe -CreationFlags NewConsole -ParentProcess $parent
$ConfirmPreference = "High"
```

Once the command prompt running as `TrustedInstaller` appears, run `regedit.exe`

Once the Registry Editor is open, import the `vfp_forward.reg` file and reboot the machine. This will switch the Azure VFP Switch Extension from filtering mode to forwarding mode; the latter mode being necessary for Calico to route packets correctly for containers on Windows.