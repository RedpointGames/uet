# Install common dependencies
FROM ubuntu:noble AS common-deps

RUN apt update
RUN apt install -y sed make binutils build-essential diffutils gcc g++ patch gzip bzip2 perl tar cpio unzip rsync file bc findutils gawk wget libncurses-dev curl git gcc-mingw-w64-i686-posix gcc-mingw-w64-x86-64-posix

# Build iPXE, which doesn't need to change
FROM common-deps AS build-ipxe

RUN git clone https://github.com/ipxe/ipxe.git /build/ipxe
WORKDIR /build/ipxe
COPY ipxe-config.h src/config/local/general.h
RUN cd src && \
    make clean && \
    make bin-x86_64-efi/ipxe.efi && \
    mkdir -pv /static && \
    mv bin-x86_64-efi/ipxe.efi /static/ipxe.efi

# Build wimboot, which doesn't need to change
FROM common-deps AS build-wimboot

RUN git clone https://github.com/ipxe/wimboot /build/wimboot
WORKDIR /build/wimboot
RUN cd src && \
    make clean && \
    make wimboot && \
    mkdir -pv /static && \
    mv wimboot /static/wimboot

# Build buildroot, for vmlinuz and initrd.
FROM common-deps AS build-buildroot

RUN mkdir /build
WORKDIR /build

RUN curl -L -o buildroot.tar.xz https://buildroot.org/downloads/buildroot-2025.11.tar.xz
RUN tar -xf buildroot.tar.xz && mv buildroot-2025.11 buildroot

WORKDIR /build/buildroot
COPY xserver_xorg-server.mk package/x11r7/xserver_xorg-server/xserver_xorg-server.mk
RUN sed -i 's/EFIBOOTMGR_VERSION = 18/EFIBOOTMGR_VERSION = 0a85e9b/g' package/efibootmgr/efibootmgr.mk
RUN echo 'sha256  05c621b1c08f3fdade8ddd4403240eb528705cee9e65d1bce937b0dc43c4fee9  efibootmgr-0a85e9b.tar.gz' >> package/efibootmgr/efibootmgr.hash
COPY buildroot.config .config
RUN mkdir output

COPY br2-external /build/br2-external
COPY kernel.config /build/kernel.config
COPY boot-image.png /build/boot-image.png
COPY setup.sh /build/setup.sh
COPY files /build/files
RUN chmod a+x /build/setup.sh

ENV BR2_EXTERNAL=/build/br2-external

RUN make syncconfig
RUN make
RUN mkdir /static
RUN cp output/images/rootfs.cpio.zst /static/initrd && \
    cp output/images/bzImage /static/vmlinuz

# Final image just has static builds all together.
FROM scratch

COPY --from=build-ipxe /static/ipxe.efi /static/ipxe.efi
COPY --from=build-wimboot /static/wimboot /static/wimboot
COPY --from=build-buildroot /static/vmlinuz /static/vmlinuz
COPY --from=build-buildroot /static/initrd /static/initrd
COPY background.png /static/background.png