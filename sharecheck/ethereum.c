#include <Windows.h>
#include <stdio.h>
#include <stdlib.h>

#include "shareddefs.h"
#include "cryptopp_sha3.h"
#include "endian.h"
#include "fnv.h"


#define ETHASH_ACCESSES 64
#define ETHASH_MIX_BYTES 128
#define NODE_WORDS (64/4) // 16
#define MIX_WORDS (ETHASH_MIX_BYTES/4) // 32
#define MIX_NODES (MIX_WORDS / NODE_WORDS) //  32/16 = 2


typedef union node {
	uint8_t bytes[NODE_WORDS * 4];
	uint32_t words[NODE_WORDS];
	uint64_t double_words[NODE_WORDS / 2];

#if defined(_M_X64) && ENABLE_SSE
	__m128i xmm[NODE_WORDS / 4];
#endif

#if defined(_M_X64) && ENABLE_AVX
	__m256i ymm[NODE_WORDS / 8];
#endif
} node;


typedef struct ethash_return_value {
	ethash_h256_t result;
	ethash_h256_t mix_hash;
	BOOL success;
} ethash_return_value_t;


#define MAX_NUM_EPOCHS	2048
ethash_h256_t epochHashes[MAX_NUM_EPOCHS];

uchar* DAGbuffer = NULL;
ulong64 DAGsize = 0;
int epochDAGloaded = -1;

CRITICAL_SECTION cs;


void hexToMyHex64(const char* in, char* out)
{
	int len = (int)strlen(in);
	int k = 63;
	for (int i = len - 1; i >= 0 && k >= 0; --i, --k)
		out[k] = in[i];
	for (; k >= 0; --k)
		out[k] = '0';
	out[64] = 0;
}


void hexToMyHex16(const char* in, char* out)
{
	int len = (int)strlen(in);
	int k = 15;
	for (int i = len - 1; i >= 0 && k >= 0; --i, --k)
		out[k] = in[i];
	for (; k >= 0; --k)
		out[k] = '0';
	out[16] = 0;
}


void bin2hex2(const uchar *p, size_t len, char *s)
{
	size_t i;
	if (!s) return;

	for (i = 0; i < len; i++)
		sprintf_s(s + (i * 2), 3, "%02x", (unsigned int)p[i]);
}


int hex2bin(unsigned char *p, const char *hexstr, size_t len)
{
	char hex_byte[3];
	char *ep;

	hex_byte[2] = '\0';

	while (*hexstr && len) {
		if (!hexstr[1]) {
			//printf("hex2bin str truncated");
			return 0;
		}
		hex_byte[0] = hexstr[0];
		hex_byte[1] = hexstr[1];
		*p = (unsigned char)strtol(hex_byte, &ep, 16);
		if (*ep) {
			//printf("hex2bin failed on '%s'", hex_byte);
			return 0;
		}
		p++;
		hexstr += 2;
		len--;
	}

	return (len == 0 && *hexstr == 0) ? 1 : 0;
}


const double truediffone = 26959535291011309493156476344723991336010898738574164086137773096960.0;
const double bits192 = 6277101735386680763835789423207666416102355444464034512896.0;
const double bits128 = 340282366920938463463374607431768211456.0;
const double bits64 = 18446744073709551616.0;

#define le64toh(x) (x)


double le256todouble(const void *target)
{
	uint64_t *data64;
	double dcut64;

	data64 = (uint64_t *)((unsigned char *)target + 24);
	dcut64 = le64toh(*data64) * bits192;

	data64 = (uint64_t *)((unsigned char *)target + 16);
	dcut64 += le64toh(*data64) * bits128;

	data64 = (uint64_t *)((unsigned char *)target + 8);
	dcut64 += le64toh(*data64) * bits64;

	data64 = (uint64_t *)target;
	dcut64 += le64toh(*data64);

	return dcut64;
}


double share_diff(uchar *hash)
{
	double s64;
	uchar hash_end[32];

	for (int i = 0; i < 32; i++)
		hash_end[31 - i] = hash[i];

	s64 = le256todouble(hash_end);

	if (s64 <= 0) return 0;

	return truediffone / s64;
}


static __inline void be32enc(void *pp, uint32_t x)
{
	uint8_t *p = (uint8_t *)pp;
	p[3] = x & 0xff;
	p[2] = (x >> 8) & 0xff;
	p[1] = (x >> 16) & 0xff;
	p[0] = (x >> 24) & 0xff;
}


static __inline void be64enc(void *pp, uint64_t x)
{
	uint8_t *p = (uint8_t *)pp;
	p[7] = x & 0xff;
	p[6] = (x >> 8) & 0xff;
	p[5] = (x >> 16) & 0xff;
	p[4] = (x >> 24) & 0xff;
	p[3] = (x >> 32) & 0xff;
	p[2] = (x >> 40) & 0xff;
	p[1] = (x >> 48) & 0xff;
	p[0] = (x >> 56) & 0xff;
}


void readDAG(uint64_t full_size, const long32 index, uchar* buffer128)
{
	EnterCriticalSection(&cs);

	if (DAGbuffer == NULL || full_size != (DAGsize - ETHASH_DAG_MAGIC_NUM_SIZE))
	{
		LeaveCriticalSection(&cs);
		return;
	}

	// read quads directly from memory
	long64* start = (long64*)(DAGbuffer + ETHASH_DAG_MAGIC_NUM_SIZE + index);
	for (long64 i = 0; i != 16; ++i)
		((long64*)buffer128)[i] = start[i];

	LeaveCriticalSection(&cs);
}


ulong64 getDAGsize()
{
	ulong64 outval = 0;

	EnterCriticalSection(&cs);
	if (DAGbuffer != NULL)
		outval = DAGsize;
	LeaveCriticalSection(&cs);

	return outval;
}


BOOL ethash_hash(
	ethash_return_value_t* ret,
	ethash_h256_t const header_hash,
	uint64_t const nonce
	)
{
	uint64_t full_size = getDAGsize();
	if (full_size == 0) return FALSE;
	
	full_size -= ETHASH_DAG_MAGIC_NUM_SIZE;

	if (full_size % MIX_WORDS != 0) 
	{
		return FALSE;
	}

	// pack hash and nonce together into first 40 bytes of s_mix
	node s_mix[MIX_NODES + 1];
	memcpy(s_mix[0].bytes, &header_hash, 32);
	fix_endian64(s_mix[0].double_words[4], nonce);

	// compute sha3-512 hash and replicate across mix
	SHA3_512(s_mix->bytes, s_mix->bytes, 40);
	fix_endian_arr32(s_mix[0].words, 16);

	node* const mix = s_mix + 1;
	for (uint32_t w = 0; w != MIX_WORDS; ++w)
		mix->words[w] = s_mix[0].words[w % NODE_WORDS];

	unsigned const page_size = sizeof(uint32_t) * MIX_WORDS;
	unsigned const num_full_pages = (unsigned)(full_size / page_size);

	for (unsigned i = 0; i != ETHASH_ACCESSES; ++i) 
	{
		uint32_t const index = fnv_hash(s_mix->words[0] ^ i, mix->words[i % MIX_WORDS]) % num_full_pages;

		node/* const**/ dag_node[2];

		//dag_node = &full_nodes[MIX_NODES * index + n];
		readDAG(full_size, (MIX_NODES * index) * sizeof(node), (uchar*)dag_node);

		for (unsigned n = 0; n != MIX_NODES; ++n) 
		{
#if defined(_M_X64) && ENABLE_AVX
			{
				__m256i fnv_prime = _mm256_set1_epi32(FNV_PRIME);
				__m256i ymm0 = _mm256_mullo_epi32(fnv_prime, mix[n].ymm[0]);
				__m256i ymm1 = _mm256_mullo_epi32(fnv_prime, mix[n].ymm[1]);
				mix[n].ymm[0] = _mm256_xor_si256(ymm0, dag_node[n].ymm[0]);
				mix[n].ymm[1] = _mm256_xor_si256(ymm1, dag_node[n].ymm[1]);
			}
#else
#if defined(_M_X64) && ENABLE_SSE
			{
				__m128i fnv_prime = _mm_set1_epi32(FNV_PRIME);
				__m128i xmm0 = _mm_mullo_epi32(fnv_prime, mix[n].xmm[0]);
				__m128i xmm1 = _mm_mullo_epi32(fnv_prime, mix[n].xmm[1]);
				__m128i xmm2 = _mm_mullo_epi32(fnv_prime, mix[n].xmm[2]);
				__m128i xmm3 = _mm_mullo_epi32(fnv_prime, mix[n].xmm[3]);
				mix[n].xmm[0] = _mm_xor_si128(xmm0, dag_node[n].xmm[0]);
				mix[n].xmm[1] = _mm_xor_si128(xmm1, dag_node[n].xmm[1]);
				mix[n].xmm[2] = _mm_xor_si128(xmm2, dag_node[n].xmm[2]);
				mix[n].xmm[3] = _mm_xor_si128(xmm3, dag_node[n].xmm[3]);
			}
#else
			{
				for (unsigned w = 0; w != NODE_WORDS; ++w) 
				{
					mix[n].words[w] = fnv_hash(mix[n].words[w], dag_node[n].words[w]);
				}
			}
#endif
#endif
		}
	}

	// compress mix
	for (uint32_t w = 0; w != MIX_WORDS; w += 4) 
	{
		uint32_t reduction = mix->words[w + 0];
		reduction = reduction * FNV_PRIME ^ mix->words[w + 1];
		reduction = reduction * FNV_PRIME ^ mix->words[w + 2];
		reduction = reduction * FNV_PRIME ^ mix->words[w + 3];
		mix->words[w / 4] = reduction;
	}

	fix_endian_arr32(mix->words, MIX_WORDS / 4);
	memcpy(&ret->mix_hash, mix->bytes, 32);
	// final Keccak hash
	SHA3_256(&ret->result, s_mix->bytes, 64 + 32); // Keccak-256(s + compressed_mix)
	return TRUE;
}


// error codes:
// 0 - all OK
// 1 - invalid seedhash
// 2 - DAG file does not exist
// 3 - DAG file is 0
// 4 - unable to allocate memory for DAG, not enough mem?
// 5 - failed to read DAG file
int DLL loadDAGFile(const char* seedhash, const char* folder)
{
	char seedHashHexFull[64 + 1];
	char seedHashHexPart[16 + 1];
	char fullPath[MAX_PATH];
	ethash_h256_t bseed;

	hexToMyHex64(seedhash, seedHashHexFull);
	hex2bin(bseed.b, seedHashHexFull, 32);

	// verify if seedhash is valid
	int i = 0;
	for (i = 0; i != MAX_NUM_EPOCHS; ++i)
	{
		if (!memcmp(&bseed, &epochHashes[i], 32))
			break;
	}
	if (i == MAX_NUM_EPOCHS)
	{
		return 1;
	}

	memcpy(seedHashHexPart, seedHashHexFull, 16);
	seedHashHexPart[16] = 0;

	sprintf_s(fullPath, sizeof(fullPath) - 1, "%sfull-R%d-%s", folder, ETHEREUM_REVISION, seedHashHexPart);

	EnterCriticalSection(&cs);

	if (epochDAGloaded == i)
	{
		LeaveCriticalSection(&cs);
		return 0; // already loaded
	}

	FILE* f;
	fopen_s(&f, fullPath, "rb");
	if (f == NULL)
	{
		LeaveCriticalSection(&cs);
		return 2;
	}

	fseek(f, 0, SEEK_END);
	ulong32 size = ftell(f);
	fseek(f, 0, SEEK_SET);

	if (size == 0)
	{
		LeaveCriticalSection(&cs);
		fclose(f);
		return 3;
	}

	if (DAGbuffer != NULL)
	{
		free(DAGbuffer);
		DAGbuffer = NULL;
		DAGsize = 0;
		epochDAGloaded = -1;
	}

	DAGbuffer = (uchar*)malloc(size);
	if (DAGbuffer == NULL)
	{
		fclose(f);
		LeaveCriticalSection(&cs);
		return 4;
	}

	ulong32 read = (ulong32)fread(DAGbuffer, 1, size, f);
	if (read != size)
	{
		free(DAGbuffer);
		DAGbuffer = NULL;
		DAGsize = 0;
		fclose(f);
		LeaveCriticalSection(&cs);
		return 5;
	}

	DAGsize = size;
	epochDAGloaded = i;
	LeaveCriticalSection(&cs);

	fclose(f);
	return 0;
}


void DLL unloadDAGFile()
{
	EnterCriticalSection(&cs);

	if (DAGbuffer == NULL)
	{
		LeaveCriticalSection(&cs);
		return;
	}

	free(DAGbuffer);
	DAGbuffer = NULL;
	DAGsize = 0;
	epochDAGloaded = -1;
	
	LeaveCriticalSection(&cs);
}


double DLL getHashDiff(const char* target)
{
	char hexHash[64 + 1];
	uchar hash[32];
	hexToMyHex64(target, hexHash);
	hex2bin((unsigned char*)hash, hexHash, 32);

	return share_diff(hash);
}


void diff_to_target(uint32_t *target, double diff)
{
	uint64_t m;
	int k;

	for (k = 6; k > 0 && diff > 1.0; k--)
		diff /= 4294967296.0;
	m = (uint64_t)(4294901760.0 / diff);
	if (m == 0 && k == 6)
		memset(target, 0xff, 32);
	else {
		memset(target, 0, 32);
		target[k] = (uint32_t)m;
		target[k + 1] = (uint32_t)(m >> 32);
	}
}


void DLL diffToTarget(const double diff, char* target)
{
	ethash_h256_t targetH, targetR;

	diff_to_target((uint32_t *)&targetH.b, diff);

	for (int i = 0; i < 32; i++)
		targetR.b[31 - i] = targetH.b[i];

	bin2hex2(targetR.b, 32, target);
}


double DLL getShareDiff(const char* headerhash, const char* nonce, char* mixhash)
{
	char hexHash[64 + 1];
	char hexNonce[16 + 1];
	ethash_h256_t hashH;
	uint64_t nonceH;
	BOOL res;

	hexToMyHex64(headerhash, hexHash);
	hex2bin(hashH.b, hexHash, 32);
	hexToMyHex16(nonce, hexNonce);
	hex2bin((uchar*)&nonceH, hexNonce, 8);
	nonceH = ethash_swap_u64(nonceH);

	ethash_return_value_t ret;
	res = ethash_hash(&ret, hashH, nonceH);
	if (!res) return 0;

	bin2hex2(ret.mix_hash.b, 32, mixhash);
	
	return share_diff(ret.result.b);
}


void ethInit()
{
	memset(&epochHashes[0], 0, 32);
	for (int i = 0; i != MAX_NUM_EPOCHS - 1; ++i)
		SHA3_256(&epochHashes[i + 1], (uint8_t*)&epochHashes[i], 32);

	InitializeCriticalSection(&cs);
}


void ethUninit()
{
	unloadDAGFile();

	DeleteCriticalSection(&cs);
}