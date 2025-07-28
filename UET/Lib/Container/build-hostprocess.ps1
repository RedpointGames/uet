param($UetPath, $Tag1, $Tag2, $OutputType)

Push-Location $PSScriptPath

if (Test-Path pwsh) {
    Remove-Item -Path pwsh -Force -Recurse
}
if (Test-Path pwsh.zip) {
    Remove-Item -Path pwsh.zip -Force
}

Invoke-WebRequest -OutFile pwsh.zip -Uri "https://github.com/PowerShell/PowerShell/releases/download/v7.5.2/PowerShell-7.5.2-win-x64.zip" -UseBasicParsing
Expand-Archive -Path pwsh.zip -DestinationPath pwsh

Copy-Item $UetPath uet.exe

if (!(Test-Path buildx.exe)) {
    Invoke-WebRequest -OutFile buildx.exe -Uri "https://github.com/docker/buildx/releases/download/v0.26.1/buildx-v0.26.1.windows-amd64.exe" -UseBasicParsing
}

.\buildx.exe rm img-builder-hostprocess

.\buildx.exe create --name img-builder-hostprocess --use --platform windows/amd64
if ($LastExitCode -ne 0) { exit $LastExitCode }

try {
    .\buildx.exe build --platform windows/amd64 --output=type=$OutputType -f hostprocess.Dockerfile -t $Tag1 .
    if ($LastExitCode -ne 0) { exit $LastExitCode }
    .\buildx.exe build --platform windows/amd64 --output=type=$OutputType -f hostprocess.Dockerfile -t $Tag2 .
    if ($LastExitCode -ne 0) { exit $LastExitCode }
} finally {
    .\buildx.exe rm img-builder-hostprocess
}