ARG VERSION_DOTNET=9.0

FROM mcr.microsoft.com/dotnet/sdk:${VERSION_DOTNET}

ARG VERSION_NODE=22.x
ARG VERSION_PWSH=7.4.6

ENV PATH="/root/.dotnet/tools:${PATH}"
RUN apt-get update && \
    apt-get install -y ca-certificates curl gnupg && \
    mkdir -p /etc/apt/keyrings && \
    (curl -fsSL https://deb.nodesource.com/gpgkey/nodesource-repo.gpg.key | gpg --dearmor -o /etc/apt/keyrings/nodesource.gpg) && \
    (echo "deb [signed-by=/etc/apt/keyrings/nodesource.gpg] https://deb.nodesource.com/node_${VERSION_NODE} nodistro main" | tee /etc/apt/sources.list.d/nodesource.list) && \
    apt-get update && \
    apt-get install -y nodejs && \
    corepack enable && \
    dotnet tool install --global powershell --version ${VERSION_PWSH}