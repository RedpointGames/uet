param([switch] $SkipDotNet, [switch] $OnlyDotNet)

if (!$SkipDotNet) {
    Push-Location $PSScriptRoot\..\..\..\uet
    try {
        dotnet publish -c Release -r linux-x64
        if ($LastExitCode -ne 0) { exit $LastExitCode }
        dotnet publish -c Release -r win-x64
        if ($LastExitCode -ne 0) { exit $LastExitCode }

        Copy-Item -Force ".\bin\Release\net10.0\linux-x64\publish\uet" "$PSScriptRoot\static"
        Copy-Item -Force ".\bin\Release\net10.0\win-x64\publish\uet.exe" "$PSScriptRoot\static"
    } finally {
        Pop-Location
    }
}

if ($OnlyDotNet) {
    exit 0
}

Push-Location $PSScriptRoot
try {
    docker build . -f .\copy.Dockerfile --tag copy-buildroot
    if ($LastExitCode -ne 0) { exit $LastExitCode }

    $ContainerId = $(docker run --rm --detach copy-buildroot)
    $ContainerId = $ContainerId.Trim()

    docker cp "${ContainerId}:/static/vmlinuz" static/vmlinuz
    if ($LastExitCode -ne 0) { exit $LastExitCode }
    docker cp "${ContainerId}:/static/initrd" static/initrd
    if ($LastExitCode -ne 0) { exit $LastExitCode }
    docker cp "${ContainerId}:/static/ipxe.efi" static/ipxe.efi
    if ($LastExitCode -ne 0) { exit $LastExitCode }
    docker cp "${ContainerId}:/static/wimboot" static/wimboot
    if ($LastExitCode -ne 0) { exit $LastExitCode }
    docker cp "${ContainerId}:/static/background.png" static/background.png
    if ($LastExitCode -ne 0) { exit $LastExitCode }

    docker stop -t 0 $ContainerId
    if ($LastExitCode -ne 0) { exit $LastExitCode }
} finally {
    Pop-Location
}