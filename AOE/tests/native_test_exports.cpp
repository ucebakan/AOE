// Read-only test entry points for a managed/Python host. No executable manifest
// needs to be weakened to run unit checks in a non-elevated build environment.
#ifdef AOE_LOCATOR_TEST_LIBRARY
int main(int,char**);
extern "C" __declspec(dllexport) int RunTests(){char name[]="locator-tests";char current[]="--current-image";char* args[]={name,current};return main(2,args);}
#else
int wmain(int,wchar_t**);
extern "C" __declspec(dllexport) int RunTests(){wchar_t name[]=L"tracer-tests";wchar_t* args[]={name};return wmain(1,args);}
#endif
