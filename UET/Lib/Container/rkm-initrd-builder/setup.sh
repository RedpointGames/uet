#!/bin/bash

set -e
set -x

if [ "$TARGET_DIR" == "" ]; then
  echo "TARGET_DIR not set!"
  exit 1
fi