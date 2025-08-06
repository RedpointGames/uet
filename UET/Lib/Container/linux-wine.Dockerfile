FROM ghcr.io/redpointgames/uet/wine:9.0.0

RUN apt-get update && apt-get install -y curl && \
    curl -O pwsh.deb https://github.com/PowerShell/PowerShell/releases/download/v7.5.2/powershell_7.5.2-1.deb_amd64.deb && \
    dpkg -i pwsh.deb && \
    apt-get install -y -f && \
    rm pwsh.deb

ARG UET_TARGET_FRAMEWORK

USER root

WORKDIR /srv

ADD UET/uet/bin/Release/${UET_TARGET_FRAMEWORK}/linux-x64/publish/uet /usr/bin/uet
RUN chmod a+x /usr/bin/uet

ENTRYPOINT [ "/usr/bin/uet" ]