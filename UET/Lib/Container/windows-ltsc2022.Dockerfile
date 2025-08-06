FROM --platform=$BUILDPLATFORM ubuntu:latest AS build

RUN apt-get update && apt-get install -y curl unzip
RUN curl -Lo pwsh.zip https://github.com/PowerShell/PowerShell/releases/download/v7.5.2/PowerShell-7.5.2-win-x64.zip && \
    mkdir pwsh && \
    cd pwsh && \
    unzip ../pwsh.zip && \
    cd ..

ARG UET_TARGET_FRAMEWORK

COPY UET/uet/bin/Release/${UET_TARGET_FRAMEWORK}/windows-x64/publish/uet/uet.exe uet.exe

FROM mcr.microsoft.com/windows/servercore:ltsc2022
ENTRYPOINT [".\\pwsh\\pwsh.exe", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';", "-ExecutionPolicy", "Bypass"]
COPY --from=build pwsh pwsh/
COPY --from=build uet.exe .

