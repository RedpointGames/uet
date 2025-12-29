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
RUN make target-finalize

# At this point we have a target directory that is ready for it's final file copy to be done.
#RUN make

# --mount=type=cache,target=/build/buildroot/output --mount=type=cache,target=/root/.buildroot-ccache 

# mkdir /tmp/mytpm1
# swtpm_setup --tpm-state dir:///tmp/mytpm1 --tpm2 --createek
# swtpm socket --tpmstate dir=/tmp/mytpm1 \
#   --ctrl type=unixio,path=/tmp/mytpm1/swtpm-sock \
#   --tpm2 \
#   --log level=20

# qemu-system-x86_64 -kernel output/images/bzImage -initrd output/images/rootfs.cpio -append "console=ttyS0 quiet systemd.show_status=true" -serial mon:stdio -m 8G -net bridge,br=virbr0 -net nic,model=virtio -chardev socket,id=chrtpm,path=/tmp/mytpm1/swtpm-sock -tpmdev emulator,id=tpm0,chardev=chrtpm -device tpm-tis,tpmdev=tpm0