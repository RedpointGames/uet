FROM ghcr.io/redpointgames/uet/wine:9.0.0

USER root

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

ENTRYPOINT [ "/usr/bin/uet" ]