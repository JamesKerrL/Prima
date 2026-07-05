# prima_engine: the pure C++ core (STL only). No UI, no interop concerns.
add_library(prima_engine STATIC
    src/canvas.cpp
)

target_include_directories(prima_engine PUBLIC include)
target_compile_features(prima_engine PUBLIC cxx_std_17)

# Needed so the static engine can be linked into the prima_c shared library.
set_target_properties(prima_engine PROPERTIES POSITION_INDEPENDENT_CODE ON)
