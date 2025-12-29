FROM ghcr.io/redpointgames/uet/buildroot-prebuilt-base:latest AS source

FROM busybox
COPY --from=source /static /static
ENTRYPOINT [ "/bin/sleep", "3600" ]