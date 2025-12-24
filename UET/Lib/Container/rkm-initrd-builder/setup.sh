#!/bin/bash

set -e
set -x

if [ "$TARGET_DIR" == "" ]; then
  echo "TARGET_DIR not set!"
  exit 1
fi

echo '#!/bin/bash' > $TARGET_DIR/usr/sbin/swapoff
chmod a+x $TARGET_DIR/usr/sbin/swapoff

touch $TARGET_DIR/opt/no_pivot_root

cat >$TARGET_DIR/usr/lib/systemd/system/rkm-linux.target <<EOF
[Unit]
Description=RKM Minimal Linux Target
Requires=basic.target network.target
Wants=dbus.service rkm-shell.service rkm-install.service
Conflicts=multi-user.target rescue.service rescue.target
After=multi-user.target rescue.service rescue.target
AllowIsolate=yes
EOF

cat >$TARGET_DIR/usr/lib/systemd/system/rkm-install.service <<EOF
[Unit]
Description=RKM Install

[Service]
Type=oneshot
ExecStart=/usr/bin/uet upgrade --then cluster start --auto-upgrade
StandardInput=tty
StandardOutput=tty
TTYPath=/dev/tty1

[Install]
WantedBy=rkm-linux.target
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
DNS=1.1.1.1
DNS=1.0.0.1
DNS=8.8.8.8
DNS=8.8.4.4
EOF