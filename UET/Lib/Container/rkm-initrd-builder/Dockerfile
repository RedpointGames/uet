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

COPY files /build/files
COPY setup.sh /build/setup.sh
COPY kernel.config /build/kernel.config

RUN make syncconfig
RUN make

# --mount=type=cache,target=/build/buildroot/output --mount=type=cache,target=/root/.buildroot-ccache 

# qemu-system-x86_64 -kernel output/images/bzImage -initrd output/images/rootfs.cpio -append "console=ttyS0 quiet systemd.show_status=true" -serial mon:stdio -m 1G -nic user,model=virtio

# qemu-system-x86_64 -kernel output/images/bzImage -initrd output/images/rootfs.cpio -append "console=ttyS0 quiet systemd.show_status=true" -serial mon:stdio -m 1G -net bridge,br=virbr0 -net nic,model=virtio