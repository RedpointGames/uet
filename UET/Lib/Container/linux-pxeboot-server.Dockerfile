FROM ghcr.io/redpointgames/uet/buildroot-prebuilt-base:latest AS static-source

FROM ubuntu:latest

RUN apt-get update && \
    apt-get install -y wget apt-transport-https software-properties-common && \
    bash -c 'source /etc/os-release && wget -q https://packages.microsoft.com/config/ubuntu/$VERSION_ID/packages-microsoft-prod.deb' && \
    dpkg -i packages-microsoft-prod.deb && \
    rm packages-microsoft-prod.deb && \
    apt-get update && \
    apt-get install -y powershell
    
ARG UET_TARGET_FRAMEWORK

WORKDIR /srv

ADD UET/uet/bin/Release/${UET_TARGET_FRAMEWORK}/linux-x64/publish/uet /usr/bin/uet
RUN chmod a+x /usr/bin/uet

COPY --from=static-source /static /static
COPY UET/uet/bin/Release/${UET_TARGET_FRAMEWORK}/linux-x64/publish/uet /static/uet
COPY UET/uet/bin/Release/${UET_TARGET_FRAMEWORK}/win-x64/publish/uet.exe /static/uet.exe

ENTRYPOINT [ "/usr/bin/uet" ]