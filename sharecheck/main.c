#include <Windows.h>
#include "ethereum.h"


BOOL WINAPI DllMain(
	HINSTANCE hinstDLL,
	DWORD fdwReason,
	LPVOID lpReserved) 
{
	switch (fdwReason)
	{
	case DLL_PROCESS_ATTACH:
		ethInit();
		break;

	case DLL_THREAD_ATTACH:
		break;

	case DLL_THREAD_DETACH:
		break;

	case DLL_PROCESS_DETACH:
		ethUninit();
		break;
	}
	return TRUE; 
}
