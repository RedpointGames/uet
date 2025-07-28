FROM mcr.microsoft.com/oss/kubernetes/windows-host-process-containers-base-image:v1.0.0

COPY uet.exe uet.exe
COPY pwsh pwsh

ENTRYPOINT [".\\pwsh\\pwsh.exe", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';", "-ExecutionPolicy", "Bypass"]