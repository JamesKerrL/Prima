# Try to use system-installed GTest first; fall back to FetchContent if needed
find_package(GTest QUIET)
if(NOT GTest_FOUND)
    include(FetchContent)
    FetchContent_Declare(
        googletest
        URL https://github.com/google/googletest/archive/refs/tags/v1.15.2.tar.gz
        DOWNLOAD_EXTRACT_TIMESTAMP TRUE
    )
    FetchContent_MakeAvailable(googletest)
endif()

add_executable(prima_engine_tests
    canvas_test.cpp
    flood_fill_test.cpp
    renderer_test.cpp
    color_test.cpp
    color_wheel_test.cpp
    image_io_test.cpp
    brush_math_test.cpp
    dab_emitter_test.cpp
    brush_engine_test.cpp
)
# D3D11 links and the PRIMA_HAS_D3D11 define propagate from prima_engine's
# PUBLIC usage requirements.
if(PRIMA_ENABLE_D3D11)
    target_sources(prima_engine_tests PRIVATE d3d11_renderer_test.cpp)
endif()

target_link_libraries(prima_engine_tests PRIVATE prima_engine GTest::gtest_main)

include(GoogleTest)
gtest_discover_tests(prima_engine_tests)
