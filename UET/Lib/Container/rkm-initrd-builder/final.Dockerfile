FROM ghcr.io/redpointgames/uet/buildroot-prebuilt-base:latest

COPY files /build/files
COPY setup.sh /build/setup.sh

RUN make

# --mount=type=cache,target=/build/buildroot/output --mount=type=cache,target=/root/.buildroot-ccache 

# mkdir /tmp/mytpm1
# swtpm_setup --tpm-state dir:///tmp/mytpm1 --tpm2 --createek
# swtpm socket --tpmstate dir=/tmp/mytpm1 \
#   --ctrl type=unixio,path=/tmp/mytpm1/swtpm-sock \
#   --tpm2 \
#   --log level=20

# qemu-system-x86_64 -kernel output/images/bzImage -initrd output/images/rootfs.cpio -append "console=ttyS0 quiet systemd.show_status=true" -serial mon:stdio -m 8G -net bridge,br=virbr0 -net nic,model=virtio -chardev socket,id=chrtpm,path=/tmp/mytpm1/swtpm-sock -tpmdev emulator,id=tpm0,chardev=chrtpm -device tpm-tis,tpmdev=tpm0