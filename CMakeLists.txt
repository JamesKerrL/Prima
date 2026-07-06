cmake_minimum_required(VERSION 3.20)
project(Prima LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

if(NOT CMAKE_BUILD_TYPE AND NOT CMAKE_CONFIGURATION_TYPES)
    set(CMAKE_BUILD_TYPE Release CACHE STRING "Build type" FORCE)
endif()

# Where the native shared library is emitted. The .NET projects copy the DLL
# from here into their output (see Directory.Build.props). Kept under build/ so
# it stays gitignored.
set(PRIMA_NATIVE_BIN_DIR "${CMAKE_SOURCE_DIR}/build/native/bin"
    CACHE PATH "Directory for the prima_c shared library")

# Direct3D 11 render backend (Windows only). MinGW-w64 supplies the headers and
# import libs, so this defaults ON for any Windows build.
if(WIN32)
    option(PRIMA_ENABLE_D3D11 "Build the Direct3D 11 render backend" ON)
else()
    set(PRIMA_ENABLE_D3D11 OFF)
endif()

enable_testing()

add_subdirectory(engine)
add_subdirectory(interop)

option(PRIMA_BUILD_TESTS "Build the C++ engine tests" ON)
if(PRIMA_BUILD_TESTS)
    add_subdirectory(tests/engine)
endif()
