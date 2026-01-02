FROM ghcr.io/redpointgames/uet/buildroot-prebuilt-base:latest

COPY files /build/files
COPY setup.sh /build/setup.sh

RUN make

RUN cp output/images/rootfs.cpio /static/initrd && \
    cp output/images/bzImage /static/vmlinuz
