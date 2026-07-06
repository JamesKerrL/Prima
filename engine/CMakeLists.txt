# prima_engine: the pure C++ core (STL only). No UI, no interop concerns.
add_library(prima_engine STATIC
    src/canvas.cpp
    src/flood_fill.cpp
    src/software_renderer.cpp
    src/color.cpp
    src/color_wheel.cpp
    src/image_io.cpp
    src/dab_emitter.cpp
    src/brush_engine.cpp
)

target_include_directories(prima_engine PUBLIC include PRIVATE third_party)
target_compile_features(prima_engine PUBLIC cxx_std_17)

# Needed so the static engine can be linked into the prima_c shared library.
set_target_properties(prima_engine PROPERTIES POSITION_INDEPENDENT_CODE ON)

# Direct3D 11 backend (Windows only; see PRIMA_ENABLE_D3D11 in the root
# CMakeLists). PUBLIC so consumers (prima_c, tests) inherit the PRIMA_HAS_D3D11
# define and the system import libs. dxguid is required for IID/__uuidof
# linkage under GCC.
if(PRIMA_ENABLE_D3D11)
    target_sources(prima_engine PRIVATE src/d3d11_renderer.cpp)
    target_compile_definitions(prima_engine PUBLIC PRIMA_HAS_D3D11)
    target_link_libraries(prima_engine PUBLIC d3d11 dxgi dxguid)
endif()
