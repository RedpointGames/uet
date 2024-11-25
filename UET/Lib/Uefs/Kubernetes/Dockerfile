FROM mcr.microsoft.com/oss/kubernetes/windows-host-process-containers-base-image:v1.0.0

ADD UET/Lib/Uefs/Kubernetes/run-service.ps1 ./run-service.ps1
ADD UET/Redpoint.Uefs.Daemon/bin/Release/${UET_TARGET_FRAMEWORK}/win-x64/publish/uefs-daemon.exe ./uefs-daemon.exe
ADD UET/Redpoint.Uefs.Client/bin/Release/${UET_TARGET_FRAMEWORK}/win-x64/publish/uefs.exe ./uefs.exe

ENV PATH="C:\Windows\system32;C:\Windows;C:\WINDOWS\System32\WindowsPowerShell\v1.0\;"

ENTRYPOINT ["powershell.exe", "-ExecutionPolicy", "Bypass", "./run-service.ps1"]
