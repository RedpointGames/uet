#include "error.h"
#include "p4libs.h"
#include "clientapi.h"

#if WIN32
#define DLLEXPORT __declspec(dllexport)
#else
#define DLLEXPORT 
#endif

DLLEXPORT void PerforceNative_sync()
{
	ClientUser ui;
	ClientApi client;
	StrBuf msg;
	Error e;

	P4Libraries::Initialize(P4LIBRARIES_INIT_ALL, &e);

	if (e.Test())
	{
		return;
	}

	client.Init(&e);

	if (e.Test())
	{
		return;
	}
}