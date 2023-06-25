#include <os/log.h>

extern void redpoint_os_log(os_log_t log, int log_type, const char* message) {
    os_log_with_type(log, log_type, "%{public}s", message);
}