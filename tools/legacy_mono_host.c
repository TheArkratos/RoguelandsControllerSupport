/* Standalone embedding host; deliberately does not link Unity or system Mono. */
#include <dlfcn.h>
#include <stdio.h>
#include <stdlib.h>
#include <sys/resource.h>

typedef struct _MonoDomain MonoDomain;
typedef struct _MonoAssembly MonoAssembly;

int main(int argc, char **argv)
{
    void *mono;
    void (*set_path)(const char *);
    MonoDomain *(*init)(const char *, const char *);
    MonoAssembly *(*open_assembly)(MonoDomain *, const char *);
    int (*exec)(MonoDomain *, MonoAssembly *, int, char **);
    void (*cleanup)(MonoDomain *);
    const char *(*runtime_info)(void);
    MonoDomain *domain;
    MonoAssembly *assembly;
    int result;
    struct rlimit no_core = {0, 0};
    setbuf(stdout, NULL);
    setbuf(stderr, NULL);
    printf("HOST_BITS %u\n", (unsigned)(sizeof(void *) * 8));
    if (setrlimit(RLIMIT_CORE, &no_core) != 0) {
        perror("FAIL disabling core dumps");
        return 2;
    }
    if (argc < 5) {
        fprintf(stderr, "Usage: host libmono.so managed-dir harness.exe [harness arguments]\n");
        return 2;
    }
    printf("MONO_LOAD_BEGIN\n");
    mono = dlopen(argv[1], RTLD_NOW | RTLD_GLOBAL);
    if (!mono) {
        fprintf(stderr, "FAIL dlopen exact game Mono: %s\n", dlerror());
        return 3;
    }
#define BIND(variable, symbol) do { \
    *(void **)(&(variable)) = dlsym(mono, (symbol)); \
    if (!(variable)) { fprintf(stderr, "FAIL missing Mono C API %s: %s\n", (symbol), dlerror()); return 4; } \
} while (0)
    BIND(set_path, "mono_set_assemblies_path");
    BIND(init, "mono_jit_init_version");
    BIND(open_assembly, "mono_domain_assembly_open");
    BIND(exec, "mono_jit_exec");
    BIND(cleanup, "mono_jit_cleanup");
    *(void **)(&runtime_info) = dlsym(mono, "mono_get_runtime_build_info");
    printf("MONO_LIBRARY %s\n", argv[1]);
    if (runtime_info) printf("MONO_RUNTIME %s\n", runtime_info());
    set_path(argv[2]);
    domain = init("controller-native-smoke", "v2.0.50727");
    if (!domain) { fprintf(stderr, "FAIL mono_jit_init_version returned NULL\n"); return 5; }
    assembly = open_assembly(domain, argv[3]);
    if (!assembly) {
        fprintf(stderr, "FAIL mono_domain_assembly_open returned NULL for harness\n");
        cleanup(domain);
        return 6;
    }
    result = exec(domain, assembly, argc - 3, argv + 3);
    printf("MONO_EXEC_EXIT %d\n", result);
    cleanup(domain);
    /* Mono owns process-global state until exit; do not dlclose it. */
    return result;
}
