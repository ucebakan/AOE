#include "reader.hpp"
#include <thread>
#include <mutex>
#include <condition_variable>
#ifdef PC_UI_TEST
#include <fstream>
#include <filesystem>
#endif

namespace {
constexpr UINT sampleMessage = WM_APP + 1;
constexpr UINT exitResultMessage = WM_APP + 2;
constexpr int exitButtonId = 100;
HWND exitButton = nullptr;
bool pendingReady = false, exitQueued = false;
std::wstring pendingError;
std::optional<uint64_t> displayed;
std::optional<uint64_t> pending;
std::mutex mutex;
std::condition_variable wake;
bool stopping = false;
HFONT font = nullptr;
#ifdef UNITY_SUITE
std::thread suiteWorker;
bool suiteOverlay=false;
#endif
void PaintBadge(HDC dc, RECT rect, std::optional<uint64_t> value) {
    const auto tone = pc::Color(value);
    COLORREF background = tone == pc::Tone::Missing ? RGB(232,232,232) : tone == pc::Tone::Alert ? RGB(220,0,0) : RGB(255,255,255);
    COLORREF foreground = tone == pc::Tone::Missing ? RGB(85,85,85) : tone == pc::Tone::Alert ? RGB(255,255,255) : RGB(0,0,0);
    #ifdef UNITY_SUITE
    if(!suiteOverlay){background=tone==pc::Tone::Alert?RGB(156,52,80):RGB(32,36,48);foreground=RGB(234,237,246);}
#endif
    auto brush = CreateSolidBrush(background); FillRect(dc, &rect, brush); DeleteObject(brush);
    auto old = SelectObject(dc, font); SetBkMode(dc, TRANSPARENT); SetTextColor(dc, foreground);
    auto text = L"Player : " + (value ? std::to_wstring(*value) : std::wstring(L"--"));
    DrawTextW(dc, text.c_str(), -1, &rect, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS);
    SelectObject(dc, old);
}
void Worker(HWND window) {
    pc::ProcessReader reader;
    ULONGLONG nextSearch = 0;
    for (;;) {
        bool requestExit = false;
        { std::lock_guard lock(mutex); if (stopping) break; requestExit = exitQueued; exitQueued = false; }
        auto now = GetTickCount64();
        if (!reader.Connected() && now >= nextSearch) { reader.Open(); nextSearch = now + 2000; }
        if (requestExit && !reader.RequestExit()) {
            { std::lock_guard lock(mutex); pendingError = reader.ExitError(); }
            PostMessageW(window, exitResultMessage, 0, 0);
        }
        auto value = reader.Poll();
        const bool ready = value.has_value() && reader.CanExit();
        { std::lock_guard lock(mutex); if (stopping) break; pending = value; pendingReady = ready; }
        PostMessageW(window, sampleMessage, 0, 0);
        std::unique_lock lock(mutex);
        wake.wait_for(lock, std::chrono::milliseconds(300), [] { return stopping || exitQueued; });
        if (stopping) break;
    }
}
LRESULT CALLBACK WindowProc(HWND window, UINT message, WPARAM wp, LPARAM lp) {
    switch (message) {
    case WM_CREATE: {
        UINT dpi = GetDpiForWindow(window);
        exitButton = CreateWindowExW(0,L"BUTTON",L"Exit",WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_OWNERDRAW,
            MulDiv(136,dpi,96),0,MulDiv(64,dpi,96),MulDiv(36,dpi,96),window,reinterpret_cast<HMENU>(static_cast<INT_PTR>(exitButtonId)),
            reinterpret_cast<LPCREATESTRUCTW>(lp)->hInstance,nullptr);
        if (!exitButton) return -1;
        EnableWindow(exitButton,FALSE);
        return 0;
    }
    case WM_COMMAND:
        if (LOWORD(wp) == exitButtonId && HIWORD(wp) == BN_CLICKED) {
            { std::lock_guard lock(mutex); if (!pendingReady || exitQueued) return 0; pendingReady = false; exitQueued = true; }
            EnableWindow(exitButton,FALSE); wake.notify_all();
        }
        return 0;
    case exitResultMessage: {
        std::wstring error;
        { std::lock_guard lock(mutex); error.swap(pendingError); }
        if (!error.empty()) MessageBoxW(window,error.c_str(),L"PlayerCounter",MB_OK | MB_ICONWARNING);
        return 0;
    }
    case WM_DRAWITEM: {
        auto item = reinterpret_cast<DRAWITEMSTRUCT*>(lp);
        if (item->CtlID != exitButtonId) break;
        bool disabled = (item->itemState & ODS_DISABLED) != 0;
        auto brush = CreateSolidBrush(disabled ? RGB(132,38,38) : (item->itemState & ODS_SELECTED) ? RGB(160,0,0) : RGB(220,0,0));
        FillRect(item->hDC,&item->rcItem,brush); DeleteObject(brush);
        auto old = SelectObject(item->hDC,font); SetBkMode(item->hDC,TRANSPARENT);
        SetTextColor(item->hDC,disabled ? RGB(200,180,180) : RGB(255,255,255));
        DrawTextW(item->hDC,L"Exit",-1,&item->rcItem,DT_CENTER | DT_VCENTER | DT_SINGLELINE);
        if (item->itemState & ODS_FOCUS) { RECT focus=item->rcItem; InflateRect(&focus,-3,-3); DrawFocusRect(item->hDC,&focus); }
        SelectObject(item->hDC,old); return TRUE;
    }
    case sampleMessage: {
        std::lock_guard lock(mutex);
        EnableWindow(exitButton,pendingReady && !exitQueued);
        if (displayed != pending) {
            displayed = pending;
            auto title = L"Player : " + (displayed ? std::to_wstring(*displayed) : std::wstring(L"--"));
            SetWindowTextW(window, title.c_str());
            InvalidateRect(window, nullptr, FALSE);
        }
        return 0;
    }
    case WM_ERASEBKGND: return 1;
    case WM_PRINTCLIENT: {
        RECT rect{};GetClientRect(window,&rect);
#ifdef UNITY_SUITE
        rect.right=std::max(0L,rect.right-MulDiv(suiteOverlay?64:140,GetDpiForWindow(window),96));
#else
        rect.right=MulDiv(136,GetDpiForWindow(window),96);
#endif
        PaintBadge(reinterpret_cast<HDC>(wp),rect,displayed);return 0;
    }
    case WM_PAINT: {
        PAINTSTRUCT paint{}; HDC dc = BeginPaint(window, &paint);
        RECT rect{}; GetClientRect(window, &rect);
        #ifdef UNITY_SUITE
        rect.right=std::max(0L,rect.right-MulDiv(suiteOverlay?64:140,GetDpiForWindow(window),96));
#else
        rect.right = MulDiv(136,GetDpiForWindow(window),96);
#endif
        PaintBadge(dc,rect,displayed);
        EndPaint(window, &paint); return 0;
    }
    #ifdef UNITY_SUITE
    case WM_SIZE: {RECT r{};GetClientRect(window,&r);int button=MulDiv(suiteOverlay?64:140,GetDpiForWindow(window),96);SetWindowPos(exitButton,nullptr,std::max(0L,r.right-button),0,button,r.bottom,SWP_NOZORDER);InvalidateRect(window,nullptr,FALSE);return 0;}

#endif
    case WM_LBUTTONDOWN: ReleaseCapture(); SendMessageW(window, WM_NCLBUTTONDOWN, HTCAPTION, 0); return 0;
    case WM_CONTEXTMENU: {
        POINT point{}; GetCursorPos(&point);
        HMENU menu = CreatePopupMenu(); AppendMenuW(menu, MF_STRING, 1, L"PlayerCounter'ı kapat");
        SetForegroundWindow(window);
        auto command = TrackPopupMenu(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON, point.x, point.y, 0, window, nullptr);
        DestroyMenu(menu); if (command == 1) DestroyWindow(window); return 0;
    }
    case WM_DPICHANGED: {
        UINT dpi = HIWORD(wp);
        if (font) DeleteObject(font);
        font = CreateFontW(-MulDiv(11, dpi, 72),0,0,0,FW_BOLD,FALSE,FALSE,FALSE,DEFAULT_CHARSET,0,0,CLEARTYPE_QUALITY,0,L"Segoe UI");
        auto rect = reinterpret_cast<RECT*>(lp);
        SetWindowPos(window, HWND_TOPMOST, rect->left, rect->top, MulDiv(200,dpi,96), MulDiv(36,dpi,96), SWP_NOACTIVATE);
        SetWindowPos(exitButton,nullptr,MulDiv(136,dpi,96),0,MulDiv(64,dpi,96),MulDiv(36,dpi,96),SWP_NOZORDER | SWP_NOACTIVATE);
        InvalidateRect(window, nullptr, FALSE); return 0;
    }
    case WM_DESTROY:
        { std::lock_guard lock(mutex); stopping = true; }
        wake.notify_all();
#ifdef UNITY_SUITE
        if(suiteWorker.joinable())suiteWorker.join();if(font){DeleteObject(font);font=nullptr;}return 0;
#else
        PostQuitMessage(0); return 0;
#endif
    }
    return DefWindowProcW(window, message, wp, lp);
}
#ifdef PC_UI_TEST
// Compiled only into the test executable. No Worker is started and no game is opened.
int UiTest(HWND window) {
    RECT size{}; GetClientRect(window,&size);
    int width=size.right,height=size.bottom,split=MulDiv(136,GetDpiForWindow(window),96);
    BITMAPINFO info{}; info.bmiHeader.biSize=sizeof(BITMAPINFOHEADER);
    info.bmiHeader.biWidth=width; info.bmiHeader.biHeight=-height*3; info.bmiHeader.biPlanes=1; info.bmiHeader.biBitCount=32;
    void* pixels=nullptr; HDC dc=CreateCompatibleDC(nullptr);
    HBITMAP bitmap=CreateDIBSection(dc,&info,DIB_RGB_COLORS,&pixels,nullptr,0);
    if(!dc || !bitmap || !pixels) return 1;
    auto old=SelectObject(dc,bitmap);
    bool good=!IsWindowEnabled(exitButton) && !exitQueued;
    const std::array<std::optional<uint64_t>,3> values{4,5,std::nullopt};
    const std::array<COLORREF,3> colors{RGB(255,255,255),RGB(220,0,0),RGB(232,232,232)};
    for(int i=0;i<3;++i) {
        RECT badge{0,height*i,split,height*(i+1)}; PaintBadge(dc,badge,values[i]);
        DRAWITEMSTRUCT item{}; item.CtlID=exitButtonId; item.hDC=dc; item.rcItem={split,height*i,width,height*(i+1)};
        item.itemState=i==2 ? ODS_DISABLED : 0;
        SendMessageW(window,WM_DRAWITEM,exitButtonId,reinterpret_cast<LPARAM>(&item));
        good=good && GetPixel(dc,2,height*i+2)==colors[i];
        good=good && GetPixel(dc,split+2,height*i+2)==(i==2 ? RGB(132,38,38) : RGB(220,0,0));
    }
    pendingReady=true; EnableWindow(exitButton,TRUE);
    SendMessageW(window,WM_COMMAND,MAKEWPARAM(exitButtonId,BN_CLICKED),reinterpret_cast<LPARAM>(exitButton));
    good=good && exitQueued && !pendingReady && !IsWindowEnabled(exitButton);
    exitQueued=false;
    SendMessageW(window,WM_COMMAND,MAKEWPARAM(exitButtonId,BN_CLICKED),reinterpret_cast<LPARAM>(exitButton));
    good=good && !exitQueued;
    wchar_t module[MAX_PATH]{}; GetModuleFileNameW(nullptr,module,MAX_PATH);
    auto directory=std::filesystem::path(module).parent_path();
    GdiFlush();
    BITMAPFILEHEADER header{}; header.bfType=0x4D42; header.bfOffBits=sizeof(header)+sizeof(info.bmiHeader);
    DWORD bytes=static_cast<DWORD>(width*height*3*4); header.bfSize=header.bfOffBits+bytes;
    std::ofstream out(directory/L"PlayerCounter-ui.bmp",std::ios::binary);
    out.write(reinterpret_cast<const char*>(&header),sizeof(header));
    out.write(reinterpret_cast<const char*>(&info.bmiHeader),sizeof(info.bmiHeader));
    out.write(static_cast<const char*>(pixels),bytes); good=good && out.good();
    SelectObject(dc,old); DeleteObject(bitmap); DeleteDC(dc);
    return good ? 0 : 1;
}
#endif
}
int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR, int) {
    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    UINT dpi = GetDpiForSystem();
    font = CreateFontW(-MulDiv(11,dpi,72),0,0,0,FW_BOLD,FALSE,FALSE,FALSE,DEFAULT_CHARSET,0,0,CLEARTYPE_QUALITY,0,L"Segoe UI");
    WNDCLASSW cls{}; cls.hInstance = instance; cls.lpfnWndProc = WindowProc;
    cls.lpszClassName = L"PlayerCounterBadge"; cls.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    if (!RegisterClassW(&cls)) { DeleteObject(font); return 1; }
    HWND window = CreateWindowExW(WS_EX_TOPMOST | WS_EX_TOOLWINDOW, cls.lpszClassName, L"Player : --", WS_POPUP,
        40,40,MulDiv(200,dpi,96),MulDiv(36,dpi,96),nullptr,nullptr,instance,nullptr);
    if (!window) { DeleteObject(font); return 1; }
#ifdef PC_UI_TEST
    const int result=UiTest(window); DestroyWindow(window); DeleteObject(font); return result;
#else
    ShowWindow(window, SW_SHOWNOACTIVATE);
    std::thread worker(Worker, window);
    MSG message{};
    while (GetMessageW(&message, nullptr, 0, 0) > 0) { TranslateMessage(&message); DispatchMessageW(&message); }
    { std::lock_guard lock(mutex); stopping = true; }
    wake.notify_all(); worker.join(); DeleteObject(font); return 0;
#endif
}

#ifdef UNITY_SUITE
extern "C" __declspec(dllexport) HWND __cdecl CreateTool(HWND parent,const wchar_t*,int preview){
    suiteOverlay=parent==nullptr;stopping=false;pendingReady=false;exitQueued=false;displayed.reset();pending.reset();pendingError.clear();
    auto instance=GetModuleHandleW(nullptr);UINT dpi=parent?GetDpiForWindow(parent):GetDpiForSystem();
    font=CreateFontW(-MulDiv(suiteOverlay?11:16,dpi,72),0,0,0,FW_BOLD,FALSE,FALSE,FALSE,DEFAULT_CHARSET,0,0,CLEARTYPE_QUALITY,0,L"Segoe UI");
    WNDCLASSW cls{};cls.hInstance=instance;cls.lpfnWndProc=WindowProc;cls.lpszClassName=L"UnitySuiteCounter";cls.hCursor=LoadCursorW(nullptr,IDC_ARROW);RegisterClassW(&cls);
    HWND window=CreateWindowExW(suiteOverlay?(WS_EX_TOPMOST|WS_EX_TOOLWINDOW|WS_EX_NOACTIVATE):WS_EX_CONTROLPARENT,cls.lpszClassName,L"PlayerCounter",suiteOverlay?WS_POPUP:(WS_CHILD|WS_VISIBLE),suiteOverlay?40:0,suiteOverlay?40:0,suiteOverlay?MulDiv(200,dpi,96):600,suiteOverlay?MulDiv(36,dpi,96):80,parent,nullptr,instance,nullptr);
    if(window&&suiteOverlay)ShowWindow(window,SW_SHOWNOACTIVATE);
    if(window&&!preview)suiteWorker=std::thread(Worker,window);return window;
}
extern "C" __declspec(dllexport) HWND __cdecl CreateOverlay(int preview){return CreateTool(nullptr,L"",preview);}
extern "C" __declspec(dllexport) int __cdecl CloseTool(HWND window){if(IsWindow(window))DestroyWindow(window);return 1;}
#endif
