FROM ubuntu:latest

# note: We also install iptables here because this image is used by RKM for running flannel.
RUN apt-get update && \
    apt-get install -y wget apt-transport-https software-properties-common && \
    wget https://github.com/PowerShell/PowerShell/releases/download/v7.5.6/powershell_7.5.6-1.deb_amd64.deb && \
    apt-get install -y ./powershell_7.5.6-1.deb_amd64.deb && \
    rm powershell_7.5.6-1.deb_amd64.deb && \
    apt-get update && \
    apt-get install -y iptables
    
ARG UET_TARGET_FRAMEWORK

WORKDIR /srv

ADD UET/uet/bin/Release/${UET_TARGET_FRAMEWORK}/linux-x64/publish/uet /usr/bin/uet
RUN chmod a+x /usr/bin/uet

ENTRYPOINT [ "/usr/bin/uet" ]