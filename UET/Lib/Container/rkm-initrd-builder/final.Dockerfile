FROM ghcr.io/redpointgames/uet/buildroot-prebuilt-base:latest

COPY files /build/files
COPY setup.sh /build/setup.sh

RUN make