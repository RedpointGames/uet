param()

cd $PSScriptRoot\..\..\..\uet
dotnet publish -c Release -r linux-x64
if ($LastExitCode -ne 0) { exit $LastExitCode }

Copy-Item -Force ".\bin\Release\net9.0\linux-x64\publish\uet" "$PSScriptRoot\files\usr\bin\uet-bootstrap"

cd $PSScriptRoot

docker build . -f .\final.Dockerfile --tag ghcr.io/redpointgames/uet/uet-pxeboot-server:test
if ($LastExitCode -ne 0) { exit $LastExitCode }

$ContainerId = $(docker run --rm --detach --entrypoint /bin/sleep ghcr.io/redpointgames/uet/uet-pxeboot-server:test 3600)
$ContainerId = $ContainerId.Trim()

docker cp "${ContainerId}:/build/buildroot/output/images/bzImage" static/bzImage
if ($LastExitCode -ne 0) { exit $LastExitCode }
docker cp "${ContainerId}:/build/buildroot/output/images/rootfs.cpio" static/rootfs.cpio
if ($LastExitCode -ne 0) { exit $LastExitCode }

docker stop -t 0 $ContainerId
if ($LastExitCode -ne 0) { exit $LastExitCode }