#define ETHEREUM_REVISION	23
#define ETHASH_DAG_MAGIC_NUM_SIZE 8
#define ETHASH_EPOCH_LENGTH 30000U

#define READ_CHUNCK_SIZE_MB 32
#define MEMORY_DAG_REFRESH_INTERVAL 60 // seconds

#define ENABLE_AVX 0
#define ENABLE_SSE 0

typedef unsigned __int64 ulong64;
typedef __int64 long64;
typedef unsigned char uchar;
typedef int long32;
typedef unsigned int ulong32;

#define DLLEXPORT __declspec(dllexport)
#define DLLIMPORT __declspec(dllimport)

#ifdef _USRDLL
#define DLL DLLEXPORT
#else
#define DLL DLLIMPORT
#endif
