# Install common dependencies
FROM ubuntu:noble AS common-deps

RUN apt update
RUN apt install -y sed make binutils build-essential diffutils gcc g++ patch gzip bzip2 perl tar cpio unzip rsync file bc findutils gawk wget libncurses-dev curl git

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

# Prebuild buildroot so that we can quickly build the final image
# with UET later.
FROM common-deps

RUN mkdir /build
WORKDIR /build

RUN curl -L -o buildroot.tar.xz https://buildroot.org/downloads/buildroot-2025.11.tar.xz
RUN tar -xf buildroot.tar.xz && mv buildroot-2025.11 buildroot

WORKDIR /build/buildroot
COPY buildroot.config .config
RUN mkdir output

COPY kernel.config /build/kernel.config
COPY boot-image.png /build/boot-image.png

RUN make syncconfig
RUN bash -c 'echo -e "\n.PHONY: redpoint-target-finalize\nredpoint-target-finalize: \$(PACKAGES) \$(TARGET_DIR) host-finalize\n"' >> Makefile
RUN make redpoint-target-finalize

COPY --from=build-ipxe /static/ipxe.efi /static/ipxe.efi
COPY --from=build-wimboot /static/wimboot /static/wimboot
COPY background.png /static/background.png