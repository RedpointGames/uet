FROM --platform=$BUILDPLATFORM ubuntu:latest AS build
RUN apt-get update && apt-get install -y curl unzip
RUN curl -Lo docker.zip https://download.docker.com/win/static/stable/x86_64/docker-28.3.3.zip && \
    unzip docker.zip

FROM mcr.microsoft.com/oss/kubernetes/windows-host-process-containers-base-image:v1.0.0
ENTRYPOINT ["dockerd.exe", "--containerd", "\\\\.\\pipe\\containerd-containerd", "--exec-opt", "isolation=process"]
COPY --from=build docker .