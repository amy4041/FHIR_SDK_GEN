// K1 freezes the already-versioned compiler-only contract until K4.
// This historical metadata is emitted only by the compiler-contract build;
// the deployed SDK never grants the generator production friend access.
#if MYFHIRSDK_LEGACY_COMPILER_CONTRACT
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("MyFhirSdk.Generated.CompilationValidation")]
#endif
