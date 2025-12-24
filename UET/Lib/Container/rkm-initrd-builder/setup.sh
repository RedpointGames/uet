#!/bin/bash

set -e
set -x

if [ "$TARGET_DIR" == "" ]; then
  echo "TARGET_DIR not set!"
  exit 1
fi

cat >$TARGET_DIR/usr/lib/systemd/system/rkm-linux.target <<EOF
[Unit]
Description=RKM Minimal Linux Target
Requires=basic.target network.target
Wants=dbus.service rkm-shell.service
Conflicts=multi-user.target rescue.service rescue.target
After=multi-user.target rescue.service rescue.target
AllowIsolate=yes
EOF

cat >$TARGET_DIR/usr/lib/systemd/system/rkm-shell.service <<EOF
[Unit]
Description=RKM Shell

[Service]
Type=simple
ExecStart=/usr/bin/bash
ExecStop=/bin/kill -HUP \${MAINPID}
StandardInput=tty
StandardOutput=tty
TTYPath=/dev/ttyS0
Restart=always
RestartSec=2

[Install]
WantedBy=rkm-linux.target
EOF

cat >$TARGET_DIR/etc/systemd/network/ethernet.network <<EOF
[Match]
Kind=!*
Type=ether

[Network]
DHCP=yes
EOF