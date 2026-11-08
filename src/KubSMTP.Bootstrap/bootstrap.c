#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <commctrl.h>
#include <shlobj.h>
#include <bcrypt.h>
#include <compressapi.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <wchar.h>

/* The bootstrap has no .NET dependency. Cabinet, BCrypt, and the GUI are Windows APIs. */
#define PROGRESS_MSG (WM_APP+1)
#define STARTED_MSG (WM_APP+2)
#define HIDE_MSG (WM_APP+3)
#define DONE_MSG (WM_APP+4)
#define LIMIT_PATH 32768
static HWND window,bar,label,cancelButton;
static volatile LONG cancelled,childStarted;
static DWORD resultCode;
static int german;
static wchar_t executable[LIMIT_PATH],cache[LIMIT_PATH],marker[LIMIT_PATH];
static wchar_t failure[512];
static const wchar_t *arguments;
static uint64_t payloadOffset,payloadLength;
static BYTE payloadDigest[32];
static FILE *trace;
static DWORD uiTestDelay;
#define T(de,en) (german?(de):(en))

static void event(const char *name,unsigned value){if(trace){fprintf(trace,"%s %u\n",name,value);fflush(trace);}}
static int read_exact(HANDLE file,void *data,DWORD size){DWORD got;return ReadFile(file,data,size,&got,NULL)&&got==size;}
static int write_exact(HANDLE file,const void *data,DWORD size){DWORD put;return WriteFile(file,data,size,&put,NULL)&&put==size;}
static int u32(HANDLE file,uint32_t *v){return read_exact(file,v,4);}
static int u64(HANDLE file,uint64_t *v){return read_exact(file,v,8);}
static void fail(const wchar_t *message){wcsncpy(failure,message,511);failure[511]=0;}
static int mkdirs(wchar_t *path){
 for(wchar_t *p=path+3;*p;p++)if(*p==L'\\'){wchar_t ch=*p;*p=0;DWORD a=GetFileAttributesW(path);if(a!=INVALID_FILE_ATTRIBUTES&&(a&FILE_ATTRIBUTE_REPARSE_POINT)){*p=ch;return 0;}if(!CreateDirectoryW(path,NULL)&&GetLastError()!=ERROR_ALREADY_EXISTS){*p=ch;return 0;}*p=ch;}
 if(!CreateDirectoryW(path,NULL)&&GetLastError()!=ERROR_ALREADY_EXISTS)return 0;
 DWORD a=GetFileAttributesW(path);return a!=INVALID_FILE_ATTRIBUTES&&!(a&FILE_ATTRIBUTE_REPARSE_POINT);
}
static int ready(void){
 BYTE digest[32];HANDLE file=CreateFileW(marker,GENERIC_READ,FILE_SHARE_READ,NULL,OPEN_EXISTING,0,NULL);
 if(file==INVALID_HANDLE_VALUE)return 0;int ok=read_exact(file,digest,32)&&!memcmp(digest,payloadDigest,32);CloseHandle(file);
 const wchar_t *required[]={L"KubSMTP.App.exe",L"hostfxr.dll",L"coreclr.dll",L"Microsoft.ui.xaml.dll",L"KubSMTP.App.pri"};
 wchar_t path[LIMIT_PATH];if(!ok)return 0;
 for(unsigned i=0;i<sizeof(required)/sizeof(required[0]);i++)if(_snwprintf(path,LIMIT_PATH,L"%ls\\%ls",cache,required[i])<0||GetFileAttributesW(path)==INVALID_FILE_ATTRIBUTES)return 0;
 return 1;
}
static int safe_name(const wchar_t *name){
 if(!*name||*name==L'\\'||*name==L'/')return 0;
 for(const wchar_t *p=name;*p;p++)if(*p==L':'||*p==L'/'||*p<32||(*p==L'.'&&(p==name||p[-1]==L'\\')&&(p[1]==0||p[1]==L'\\'||p[1]==L'.')))return 0;
 return 1;
}
static int extract(void){
 int ok=0;HANDLE input=INVALID_HANDLE_VALUE,output=INVALID_HANDLE_VALUE;DECOMPRESSOR_HANDLE decoder=NULL;
 BCRYPT_ALG_HANDLE algorithm=NULL;BCRYPT_HASH_HANDLE hash=NULL;
 BYTE *compressed=NULL,*plain=NULL;uint32_t count;uint64_t total,done=0;char magic[8];
 input=CreateFileW(executable,GENERIC_READ,FILE_SHARE_READ,NULL,OPEN_EXISTING,FILE_ATTRIBUTE_NORMAL,NULL);
 if(input==INVALID_HANDLE_VALUE)goto end;
 LARGE_INTEGER at;at.QuadPart=payloadOffset;if(!SetFilePointerEx(input,at,NULL,FILE_BEGIN))goto end;
 if(!read_exact(input,magic,8)||memcmp(magic,"KUBPKG1\0",8)||!u32(input,&count)||!u64(input,&total)||count>10000||total>2ULL*1024*1024*1024)goto end;
 if(!CreateDecompressor(COMPRESS_ALGORITHM_XPRESS_HUFF,NULL,&decoder)||BCryptOpenAlgorithmProvider(&algorithm,BCRYPT_SHA256_ALGORITHM,NULL,0)<0)goto end;
 compressed=malloc(2*1024*1024);plain=malloc(1024*1024);if(!compressed||!plain)goto end;
 for(uint32_t i=0;i<count;i++){
  uint32_t nameLength,chunks;uint64_t size,written=0;BYTE expected[32],actual[32];char utf8[2049];wchar_t name[2049],path[LIMIT_PATH],parent[LIMIT_PATH];
  if(InterlockedCompareExchange(&cancelled,0,0))goto end;
  if(!u32(input,&nameLength)||nameLength==0||nameLength>2048||!u64(input,&size)||!read_exact(input,expected,32)||!u32(input,&chunks)||chunks>2048||!read_exact(input,utf8,nameLength))goto end;
  utf8[nameLength]=0;int n=MultiByteToWideChar(CP_UTF8,MB_ERR_INVALID_CHARS,utf8,nameLength,name,2048);if(!n)goto end;name[n]=0;if(!safe_name(name))goto end;
  if(_snwprintf(path,LIMIT_PATH,L"%ls\\%ls",cache,name)<0)goto end;
  wcscpy(parent,path);wchar_t *slash=wcsrchr(parent,L'\\');if(!slash)goto end;*slash=0;if(!mkdirs(parent))goto end;
  DWORD attributes=GetFileAttributesW(path);if(attributes!=INVALID_FILE_ATTRIBUTES&&(attributes&FILE_ATTRIBUTE_REPARSE_POINT))goto end;
  output=CreateFileW(path,GENERIC_WRITE,0,NULL,CREATE_ALWAYS,FILE_ATTRIBUTE_NORMAL,NULL);if(output==INVALID_HANDLE_VALUE)goto end;
  if(BCryptCreateHash(algorithm,&hash,NULL,0,NULL,0,0)<0)goto end;
  for(uint32_t c=0;c<chunks;c++){
   uint32_t raw,packed;SIZE_T decoded;
   if(InterlockedCompareExchange(&cancelled,0,0))goto end;
   if(!u32(input,&raw)||!u32(input,&packed)||raw==0||raw>1024*1024||packed>2*1024*1024)goto end;
   if(packed){if(!read_exact(input,compressed,packed)||!Decompress(decoder,compressed,packed,plain,raw,&decoded)||decoded!=raw)goto end;}
   else if(!read_exact(input,plain,raw))goto end;
   if(written+raw>size||done+raw>total||!write_exact(output,plain,raw)||BCryptHashData(hash,plain,raw,0)<0)goto end;
   written+=raw;done+=raw;unsigned progress=total?(unsigned)(done*10000/total):10000;
   PostMessageW(window,PROGRESS_MSG,progress,0);
   /* Explicit diagnostics-only throttling makes cancellation/GUI tests reproducible. */
   if(uiTestDelay)Sleep(uiTestDelay);
  }
  if(written!=size||BCryptFinishHash(hash,actual,32,0)<0||memcmp(actual,expected,32))goto end;
  BCryptDestroyHash(hash);hash=NULL;CloseHandle(output);output=INVALID_HANDLE_VALUE;
 }
 LARGE_INTEGER pos,zero;zero.QuadPart=0;if(done!=total||!SetFilePointerEx(input,zero,&pos,FILE_CURRENT)||(uint64_t)pos.QuadPart!=payloadOffset+payloadLength)goto end;
 wchar_t temporary[LIMIT_PATH];_snwprintf(temporary,LIMIT_PATH,L"%ls.tmp",marker);
 output=CreateFileW(temporary,GENERIC_WRITE,0,NULL,CREATE_ALWAYS,FILE_ATTRIBUTE_NORMAL,NULL);if(output==INVALID_HANDLE_VALUE)goto end;
 if(!write_exact(output,payloadDigest,32)||!FlushFileBuffers(output))goto end;CloseHandle(output);output=INVALID_HANDLE_VALUE;
 if(!MoveFileExW(temporary,marker,MOVEFILE_REPLACE_EXISTING|MOVEFILE_WRITE_THROUGH))goto end;
 ok=1;
end:
 if(hash)BCryptDestroyHash(hash);if(algorithm)BCryptCloseAlgorithmProvider(algorithm,0);if(decoder)CloseDecompressor(decoder);
 if(input!=INVALID_HANDLE_VALUE)CloseHandle(input);if(output!=INVALID_HANDLE_VALUE)CloseHandle(output);free(compressed);free(plain);
 return ok;
}
static BOOL CALLBACK visible_window(HWND candidate,LPARAM pid){DWORD owner;GetWindowThreadProcessId(candidate,&owner);return !(owner==(DWORD)pid&&IsWindowVisible(candidate)&&GetWindow(candidate,GW_OWNER)==NULL);}
static DWORD WINAPI run(void *unused){
 wchar_t mutexName[100],hex[65];for(int i=0;i<32;i++)swprintf(hex+i*2,3,L"%02x",payloadDigest[i]);
 _snwprintf(mutexName,100,L"Local\\KubSMTP.Extract.%ls",hex);
 HANDLE mutex=CreateMutexW(NULL,FALSE,mutexName);if(!mutex){fail(T(L"Vorbereitung konnte nicht gestartet werden.",L"Could not prepare the application."));goto end;}
 DWORD locked;while((locked=WaitForSingleObject(mutex,100))==WAIT_TIMEOUT){if(InterlockedCompareExchange(&cancelled,0,0)){CloseHandle(mutex);goto end;}}
 if(locked!=WAIT_OBJECT_0&&locked!=WAIT_ABANDONED){CloseHandle(mutex);fail(T(L"Vorbereitung konnte nicht gesperrt werden.",L"Could not lock the application cache."));goto end;}
 if(!ready()){
  event("extract-start",0);if(!mkdirs(cache)||!extract()){ReleaseMutex(mutex);CloseHandle(mutex);if(!cancelled)fail(T(L"Entpacken fehlgeschlagen. Bitte freien Speicherplatz und Schreibrechte prüfen.",L"Extraction failed. Please check free space and write permissions."));goto end;}
  event("extract-complete",10000);
 }else event("cache-hit",1);
 ReleaseMutex(mutex);CloseHandle(mutex);
 if(cancelled)goto end;
 wchar_t app[LIMIT_PATH];_snwprintf(app,LIMIT_PATH,L"%ls\\KubSMTP.App.exe",cache);
 size_t length=wcslen(app)+wcslen(arguments)+5;wchar_t *command=calloc(length,sizeof(wchar_t));if(!command)goto end;
 _snwprintf(command,length,L"\"%ls\" %ls",app,arguments);
 STARTUPINFOW startup={0};PROCESS_INFORMATION process={0};startup.cb=sizeof(startup);
 if(!CreateProcessW(app,command,NULL,NULL,FALSE,0,NULL,cache,&startup,&process)){free(command);fail(T(L"KubSMTP konnte nicht gestartet werden.",L"KubSMTP could not be started."));goto end;}
 free(command);CloseHandle(process.hThread);InterlockedExchange(&childStarted,1);PostMessageW(window,STARTED_MSG,0,0);event("child-start",process.dwProcessId);
 while(WaitForSingleObject(process.hProcess,50)==WAIT_TIMEOUT){if(!EnumWindows(visible_window,(LPARAM)process.dwProcessId)){PostMessageW(window,HIDE_MSG,0,0);event("main-window",1);break;}}
 WaitForSingleObject(process.hProcess,INFINITE);GetExitCodeProcess(process.hProcess,&resultCode);CloseHandle(process.hProcess);event("child-exit",resultCode);
end:
 if(failure[0])resultCode=1;else if(cancelled&&!childStarted)resultCode=ERROR_CANCELLED;
 PostMessageW(window,DONE_MSG,0,0);return 0;
}
static LRESULT CALLBACK procedure(HWND hwnd,UINT message,WPARAM w,LPARAM l){
 switch(message){
 case WM_CREATE:{
  HFONT font=(HFONT)GetStockObject(DEFAULT_GUI_FONT);
  label=CreateWindowW(L"STATIC",T(L"KubSMTP wird für den ersten Start vorbereitet …",L"Preparing KubSMTP for its first start …"),WS_CHILD|WS_VISIBLE,20,18,420,38,hwnd,NULL,NULL,NULL);
  bar=CreateWindowExW(0,PROGRESS_CLASSW,NULL,WS_CHILD|WS_VISIBLE,20,62,420,22,hwnd,NULL,NULL,NULL);SendMessageW(bar,PBM_SETRANGE32,0,10000);
  cancelButton=CreateWindowW(L"BUTTON",T(L"Abbrechen",L"Cancel"),WS_CHILD|WS_VISIBLE|WS_TABSTOP,340,100,100,28,hwnd,(HMENU)1,NULL,NULL);
  SendMessageW(label,WM_SETFONT,(WPARAM)font,TRUE);SendMessageW(cancelButton,WM_SETFONT,(WPARAM)font,TRUE);return 0;}
 case PROGRESS_MSG:{wchar_t text[160];_snwprintf(text,160,T(L"Programmdateien werden entpackt … %u %%",L"Extracting application files … %u %%"),(unsigned)w/100);SetWindowTextW(label,text);SendMessageW(bar,PBM_SETPOS,w,0);event("progress",(unsigned)w);return 0;}
 case STARTED_MSG:EnableWindow(cancelButton,FALSE);SetWindowTextW(label,T(L"KubSMTP wird gestartet …",L"Starting KubSMTP …"));return 0;
 case HIDE_MSG:ShowWindow(hwnd,SW_HIDE);return 0;
 case WM_COMMAND:if(LOWORD(w)!=1&&LOWORD(w)!=IDCANCEL)break;/* fall through */
 case WM_CLOSE:if(childStarted)ShowWindow(hwnd,SW_HIDE);else{InterlockedExchange(&cancelled,1);EnableWindow(cancelButton,FALSE);SetWindowTextW(label,T(L"Wird abgebrochen …",L"Cancelling …"));}return 0;
 case DONE_MSG:if(failure[0])MessageBoxW(hwnd,failure,L"KubSMTP",MB_OK|MB_ICONERROR);DestroyWindow(hwnd);return 0;
 case WM_DESTROY:PostQuitMessage(0);return 0;
 }
 return DefWindowProcW(hwnd,message,w,l);
}
int WINAPI wWinMain(HINSTANCE instance,HINSTANCE previous,LPWSTR cmd,int show){
 german=PRIMARYLANGID(GetUserDefaultUILanguage())==LANG_GERMAN;arguments=cmd;
 wchar_t tracePath[LIMIT_PATH];if(GetEnvironmentVariableW(L"KUBSMTP_PORTABLE_TRACE",tracePath,LIMIT_PATH))trace=_wfopen(tracePath,L"wb");
 wchar_t delay[16];if(trace&&GetEnvironmentVariableW(L"KUBSMTP_PORTABLE_UI_TEST_DELAY_MS",delay,16)){uiTestDelay=wcstoul(delay,NULL,10);if(uiTestDelay>100)uiTestDelay=100;}
 GetModuleFileNameW(NULL,executable,LIMIT_PATH);HANDLE input=CreateFileW(executable,GENERIC_READ,FILE_SHARE_READ,NULL,OPEN_EXISTING,0,NULL);
 BYTE footer[56];LARGE_INTEGER size,position;position.QuadPart=-56;
 if(input==INVALID_HANDLE_VALUE||!GetFileSizeEx(input,&size)||!SetFilePointerEx(input,position,NULL,FILE_END)||!read_exact(input,footer,56)||memcmp(footer,"KUBEND1\0",8)){MessageBoxW(NULL,L"Invalid portable package.",L"KubSMTP",MB_OK|MB_ICONERROR);return 1;}
 CloseHandle(input);memcpy(&payloadOffset,footer+8,8);memcpy(&payloadLength,footer+16,8);memcpy(payloadDigest,footer+24,32);
 if(payloadOffset>(uint64_t)size.QuadPart||payloadLength>(uint64_t)size.QuadPart||payloadOffset+payloadLength!=(uint64_t)size.QuadPart-56)return 1;
 wchar_t base[LIMIT_PATH],hex[65];if(!GetEnvironmentVariableW(L"KUBSMTP_PORTABLE_CACHE",base,LIMIT_PATH)){PWSTR folder;if(FAILED(SHGetKnownFolderPath(&FOLDERID_LocalAppData,0,NULL,&folder)))return 1;_snwprintf(base,LIMIT_PATH,L"%ls\\KubSMTP\\Runtime",folder);CoTaskMemFree(folder);}
 for(int i=0;i<32;i++)swprintf(hex+i*2,3,L"%02x",payloadDigest[i]);if(_snwprintf(cache,LIMIT_PATH,L"%ls\\%ls",base,hex)<0)return 1;
 if(_snwprintf(marker,LIMIT_PATH,L"%ls\\.ready",cache)<0)return 1;
 INITCOMMONCONTROLSEX controls={sizeof(controls),ICC_PROGRESS_CLASS};InitCommonControlsEx(&controls);
 WNDCLASSW cls={0};cls.lpfnWndProc=procedure;cls.hInstance=instance;cls.hCursor=LoadCursor(NULL,IDC_ARROW);cls.hIcon=LoadIconW(instance,MAKEINTRESOURCEW(1));cls.hbrBackground=(HBRUSH)(COLOR_WINDOW+1);cls.lpszClassName=L"KubSMTP.PortableStartup";RegisterClassW(&cls);
 RECT rectangle={0,0,460,148};AdjustWindowRect(&rectangle,WS_OVERLAPPED|WS_CAPTION|WS_SYSMENU,FALSE);int width=rectangle.right-rectangle.left,height=rectangle.bottom-rectangle.top;
 window=CreateWindowW(cls.lpszClassName,T(L"KubSMTP – Startvorbereitung",L"KubSMTP – Preparing startup"),WS_OVERLAPPED|WS_CAPTION|WS_SYSMENU,(GetSystemMetrics(SM_CXSCREEN)-width)/2,(GetSystemMetrics(SM_CYSCREEN)-height)/2,width,height,NULL,NULL,instance,NULL);
 if(!window)return 1;int cached=ready();if(!cached){ShowWindow(window,SW_SHOW);UpdateWindow(window);event("progress-visible",IsWindowVisible(window)?1:0);}
 HANDLE worker=CreateThread(NULL,0,run,NULL,0,NULL);if(!worker)return 1;
 MSG message;while(GetMessageW(&message,NULL,0,0)>0){if(!IsDialogMessageW(window,&message)){TranslateMessage(&message);DispatchMessageW(&message);}}
 WaitForSingleObject(worker,INFINITE);CloseHandle(worker);if(trace)fclose(trace);return (int)resultCode;
}
