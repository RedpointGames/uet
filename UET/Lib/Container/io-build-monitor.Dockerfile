ARG VERSION_DOTNET=10.0

FROM mcr.microsoft.com/dotnet/sdk:${VERSION_DOTNET} AS builder

ARG VERSION_NODE=22.x
ARG VERSION_PWSH=7.4.6

ENV PATH="/root/.dotnet/tools:${PATH}"
RUN apt-get update && \
    apt-get install -y apt-transport-https ca-certificates curl gnupg && \
    mkdir -p /etc/apt/keyrings && \
    (curl -fsSL https://deb.nodesource.com/gpgkey/nodesource-repo.gpg.key | gpg --dearmor -o /etc/apt/keyrings/nodesource.gpg) && \
    (curl https://packages.cloud.google.com/apt/doc/apt-key.gpg | gpg --dearmor -o /usr/share/keyrings/cloud.google.gpg) && \
    (echo "deb [signed-by=/etc/apt/keyrings/nodesource.gpg] https://deb.nodesource.com/node_${VERSION_NODE} nodistro main" | tee /etc/apt/sources.list.d/nodesource.list) && \
    (echo "deb [signed-by=/usr/share/keyrings/cloud.google.gpg] https://packages.cloud.google.com/apt cloud-sdk main" | tee -a /etc/apt/sources.list.d/google-cloud-sdk.list) && \
    apt-get update && \
    apt-get install -y nodejs google-cloud-cli build-essential && \
    corepack enable && \
    dotnet tool install --global powershell --version ${VERSION_PWSH}

COPY UET /build/UET

RUN dotnet publish \
    -c Release \
    -r linux-x64 \
    -o /srv \
    /build/UET/Redpoint.IoBuildMonitor/Redpoint.IoBuildMonitor.csproj

FROM mcr.microsoft.com/dotnet/aspnet:${VERSION_DOTNET}

EXPOSE 8080
WORKDIR /srv
COPY --link --from=builder /srv .
USER $APP_UID

ENTRYPOINT ["./Redpoint.IoBuildMonitor"]