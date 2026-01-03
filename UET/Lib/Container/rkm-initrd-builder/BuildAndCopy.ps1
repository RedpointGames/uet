param([switch] $SkipDotNet, [switch] $OnlyDotNet)

if (!$SkipDotNet) {
    Push-Location $PSScriptRoot\..\..\..\uet
    try {
        dotnet publish -c Release -r linux-x64
        if ($LastExitCode -ne 0) { exit $LastExitCode }

        Copy-Item -Force ".\bin\Release\net9.0\linux-x64\publish\uet" "$PSScriptRoot\files\usr\bin\uet-bootstrap"
    } finally {
        Pop-Location
    }
}

if ($OnlyDotNet) {
    exit 0
}

Push-Location $PSScriptRoot
try {
    docker build . -f .\final.Dockerfile --tag ghcr.io/redpointgames/uet/uet-pxeboot-server:test
    if ($LastExitCode -ne 0) { exit $LastExitCode }

    $ContainerId = $(docker run --rm --detach --entrypoint /bin/sleep ghcr.io/redpointgames/uet/uet-pxeboot-server:test 3600)
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