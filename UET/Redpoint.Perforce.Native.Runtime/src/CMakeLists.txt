cmake_minimum_required(VERSION 3.20.0)

project(Redpoint.Perforce.Native.Runtime)

add_library(
    Redpoint.Perforce.Native.Runtime
    MODULE
    ${CMAKE_CURRENT_SOURCE_DIR}/src/sync.cpp
)

set(LINK_FOLDER "")
set(LINK_SUFFIX "")
set(ARCHIVE_SUFFIX "")
set(DOWNLOAD_URL "")
if(WIN32)
    set(LINK_SUFFIX ".lib")
    set(ARCHIVE_SUFFIX ".zip")
    if(CMAKE_SIZE_OF_VOID_P EQUAL 4)
        set(LINK_FOLDER "win-x86")
        set(DOWNLOAD_URL "https://cdist2.perforce.com/perforce/r23.2/bin.ntx86/p4api_vs2022_static.zip")
    else()
        set(LINK_FOLDER "win-x64")
        set(DOWNLOAD_URL "https://cdist2.perforce.com/perforce/r23.2/bin.ntx64/p4api_vs2022_static.zip")
    endif()
elseif(APPLE)
    set(LINK_SUFFIX ".a")
    set(LINK_FOLDER "osx")
    set(ARCHIVE_SUFFIX ".tgz")
    set(DOWNLOAD_URL "https://cdist2.perforce.com/perforce/r23.2/bin.macosx12u/p4api.tgz")
else()
    set(LINK_SUFFIX ".a")
    set(LINK_FOLDER "linux-x64")
    set(ARCHIVE_SUFFIX ".tgz")
    set(DOWNLOAD_URL "https://cdist2.perforce.com/perforce/r23.2/bin.linux26x86_64/p4api.tgz")
endif()

if (EXISTS ${CMAKE_BINARY_DIR}/p4${LINK_FOLDER}${ARCHIVE_SUFFIX})
else()
    file(
        DOWNLOAD 
        ${DOWNLOAD_URL} 
        ${CMAKE_BINARY_DIR}/p4${LINK_FOLDER}${ARCHIVE_SUFFIX}
        SHOW_PROGRESS
    )
endif()
if (EXISTS ${CMAKE_BINARY_DIR}/p4${LINK_FOLDER})
else()
    file(
        ARCHIVE_EXTRACT 
        INPUT ${CMAKE_BINARY_DIR}/p4${LINK_FOLDER}${ARCHIVE_SUFFIX}
        DESTINATION ${CMAKE_BINARY_DIR}/p4${LINK_FOLDER}
        VERBOSE
    )
endif()
file(
    GLOB
    P4PATH_ROOT
    LIST_DIRECTORIES true
    ${CMAKE_BINARY_DIR}/p4${LINK_FOLDER}/*
)
set(P4PATH_LIB ${P4PATH_ROOT}/lib)
set(P4PATH_INCLUDE ${P4PATH_ROOT}/include)

target_include_directories(
    Redpoint.Perforce.Native.Runtime
    PRIVATE
    ${P4PATH_INCLUDE}/p4
)

target_link_libraries(
    Redpoint.Perforce.Native.Runtime
    ${P4PATH_LIB}/libp4api${LINK_SUFFIX}
)
if(WIN32)
    if (EXISTS ${CMAKE_BINARY_DIR}/openssl.zip)
    else()
        file(
            DOWNLOAD 
            "https://indy.fulgan.com/SSL/LinkLibs/openssl-1.0.2u-x64_86-win64_LinkLibs.zip"
            ${CMAKE_BINARY_DIR}/openssl.zip
            SHOW_PROGRESS
        )
    endif()
    if (EXISTS ${CMAKE_BINARY_DIR}/openssl)
    else()
        file(
            ARCHIVE_EXTRACT 
            INPUT ${CMAKE_BINARY_DIR}/openssl.zip
            DESTINATION ${CMAKE_BINARY_DIR}/openssl
            VERBOSE
        )
    endif()
    target_link_libraries(
        Redpoint.Perforce.Native.Runtime
        ${CMAKE_BINARY_DIR}/openssl/libeay32.lib
        ${CMAKE_BINARY_DIR}/openssl/ssleay32.lib
        ws2_32.lib
        crypt32.lib
    )
else()
    target_link_libraries(
        Redpoint.Perforce.Native.Runtime
        ssl
        crypto
    )
endif()