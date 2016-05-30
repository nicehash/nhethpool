#include <stdint.h>
#include <stdlib.h>

#ifdef __cplusplus
extern "C" {
#endif

	//struct ethash_h256;
	typedef struct ethash_h256 { uint8_t b[32]; } ethash_h256_t;

	void SHA3_256(struct ethash_h256 const* ret, uint8_t const* data, size_t size);
	void SHA3_512(uint8_t* const ret, uint8_t const* data, size_t size);

#ifdef __cplusplus
}
#endif