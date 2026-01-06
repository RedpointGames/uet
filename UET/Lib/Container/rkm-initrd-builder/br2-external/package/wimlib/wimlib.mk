################################################################################
#
# wimlib
#
################################################################################

WIMLIB_VERSION = 1.14.4
WIMLIB_SOURCE = wimlib-1.14.4.tar.gz
WIMLIB_SITE = https://wimlib.net/downloads
WIMLIB_LICENSE = GPL3
WIMLIB_DEPENDENCIES = ntfs-3g libfuse3

#define WIMLIB_CONFIGURE_CMDS
#	:
#endef

#define WIMLIB_BUILD_CMDS
#	$(TARGET_MAKE_ENV) EXTRA_CFLAGS="-I $(HOST_DIR)/include/freetype2 -I $(HOST_DIR)/include" EXTRA_LDFLAGS="-L $(HOST_DIR)/lib" make $(WIMLIB_MAKE_OPTS) -C $(@D)
#endef

#define WIMLIB_INSTALL_TARGET_CMDS
#	strip $(@D)/fbtextdemo
#	$(INSTALL) -D -m 755 $(@D)/fbtextdemo $(TARGET_DIR)/usr/bin
#endef

$(eval $(autotools-package))
