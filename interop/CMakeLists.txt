# prima_c: the C ABI shared library the C# layer P/Invokes. Thin shim over
# prima_engine — no logic of its own.
add_library(prima_c SHARED
    src/prima_c.cpp
)

target_include_directories(prima_c PUBLIC include)
target_link_libraries(prima_c PRIVATE prima_engine)
target_compile_definitions(prima_c PRIVATE PRIMA_C_BUILD)

set_target_properties(prima_c PROPERTIES
    PREFIX ""                                      # emit prima_c.dll, not libprima_c.dll
    RUNTIME_OUTPUT_DIRECTORY "${PRIMA_NATIVE_BIN_DIR}"
    LIBRARY_OUTPUT_DIRECTORY "${PRIMA_NATIVE_BIN_DIR}"
)

# Statically fold in the MinGW runtimes so the DLL has no external gcc/stdc++/
# winpthread dependencies and can be copied next to the .NET output on its own.
if(MINGW)
    target_link_options(prima_c PRIVATE -static -static-libgcc -static-libstdc++)
endif()
