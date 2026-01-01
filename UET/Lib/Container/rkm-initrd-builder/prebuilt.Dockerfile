FROM ubuntu:noble

RUN apt update
RUN apt install -y sed make binutils build-essential diffutils gcc g++ patch gzip bzip2 perl tar cpio unzip rsync file bc findutils gawk wget libncurses-dev curl

RUN mkdir /build
WORKDIR /build

RUN curl -L -o buildroot.tar.xz https://buildroot.org/downloads/buildroot-2025.11.tar.xz
RUN tar -xf buildroot.tar.xz && mv buildroot-2025.11 buildroot

WORKDIR /build/buildroot
COPY buildroot.config .config
RUN mkdir output

COPY kernel.config /build/kernel.config

RUN make syncconfig
RUN bash -c 'echo -e "\n.PHONY: redpoint-target-finalize\nredpoint-target-finalize: \$(PACKAGES) \$(TARGET_DIR) host-finalize\n"' >> Makefile
RUN make redpoint-target-finalize