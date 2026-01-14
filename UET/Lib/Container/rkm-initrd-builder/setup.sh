#!/bin/bash

set -e
set -x

if [ "$TARGET_DIR" == "" ]; then
  echo "TARGET_DIR not set!"
  exit 1
fi

chmod a-x $TARGET_DIR/usr/lib/systemd/system/*.service
chmod a-x $TARGET_DIR/usr/lib/systemd/system/*.target
chmod a-x $TARGET_DIR/usr/lib/systemd/system/*.mount
chmod a-x $TARGET_DIR/usr/lib/systemd/network/ethernet.network
chmod a-x $TARGET_DIR/usr/share/background-x11*
chmod a-x $TARGET_DIR/rkm-initrd

chmod u=rw,go= $TARGET_DIR/etc/ssh/ssh_host_*
chmod a-x $TARGET_DIR/etc/ssh/sshd_config
chmod a-x $TARGET_DIR/etc/motd