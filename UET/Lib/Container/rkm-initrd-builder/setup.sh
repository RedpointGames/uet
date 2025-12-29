#!/bin/bash

set -e
set -x

if [ "$TARGET_DIR" == "" ]; then
  echo "TARGET_DIR not set!"
  exit 1
fi

chmod a-x $TARGET_DIR/usr/lib/systemd/system/*.service
chmod a-x $TARGET_DIR/usr/lib/systemd/system/*.target
