include(FetchContent)
FetchContent_Declare(
    googletest
    URL https://github.com/google/googletest/archive/refs/tags/v1.15.2.tar.gz
    DOWNLOAD_EXTRACT_TIMESTAMP TRUE
)
FetchContent_MakeAvailable(googletest)

add_executable(prima_engine_tests
    canvas_test.cpp
    renderer_test.cpp
    color_test.cpp
    color_wheel_test.cpp
    image_io_test.cpp
)
target_link_libraries(prima_engine_tests PRIVATE prima_engine GTest::gtest_main)

include(GoogleTest)
gtest_discover_tests(prima_engine_tests)
