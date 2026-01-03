#!/bin/bash

set -e
set -x

if [ "$TARGET_DIR" == "" ]; then
  echo "TARGET_DIR not set!"
  exit 1
fi

echo '#!/bin/bash' > $TARGET_DIR/usr/sbin/swapoff
chmod a+x $TARGET_DIR/usr/sbin/swapoff

touch $TARGET_DIR/rkm-initrd

cat >$TARGET_DIR/usr/lib/systemd/system/rkm-initrd.target <<EOF
[Unit]
Description=RKM Initrd Target
Requires=basic.target network.target
Wants=dbus.service rkm-provision-client.service systemd-networkd.service
Conflicts=multi-user.target rescue.service rescue.target
After=multi-user.target rescue.service rescue.target systemd-networkd.service
AllowIsolate=yes
EOF

cat >$TARGET_DIR/usr/lib/systemd/system/rkm-provision-client.service <<EOF
[Unit]
Description=RKM Provision Client

[Service]
Type=simple
ExecStart=/usr/bin/uet-bootstrap internal pxeboot provision-client
StandardInput=tty
StandardOutput=tty
Environment="DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp/dotnet-bundle"
Environment="GRPC_PIPE_PATH_USER=/tmp/.grpc"
Restart=always
RestartSec=2

[Install]
WantedBy=rkm-initrd.target
EOF

cat >$TARGET_DIR/etc/systemd/network/ethernet.network <<EOF
[Match]
Kind=!*
Type=ether

[Network]
DHCP=yes
DNS=1.1.1.1
DNS=1.0.0.1
DNS=8.8.8.8
DNS=8.8.4.4

[DHCP]
ClientIdentifier=mac
EOF