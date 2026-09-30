#include "reader.hpp"
#include <thread>
#include <mutex>
#include <condition_variable>

namespace {
constexpr UINT sampleMessage = WM_APP + 1;
std::optional<uint64_t> displayed;
std::optional<uint64_t> pending;
std::mutex mutex;
std::condition_variable wake;
bool stopping = false;
HFONT font = nullptr;
void Worker(HWND window) {
    pc::ProcessReader reader;
    ULONGLONG nextSearch = 0;
    for (;;) {
        { std::lock_guard lock(mutex); if (stopping) break; }
        auto now = GetTickCount64();
        if (!reader.Connected() && now >= nextSearch) { reader.Open(); nextSearch = now + 2000; }
        auto value = reader.Poll();
        { std::lock_guard lock(mutex); if (stopping) break; pending = value; }
        PostMessageW(window, sampleMessage, 0, 0);
        std::unique_lock lock(mutex);
        if (wake.wait_for(lock, std::chrono::milliseconds(300), [] { return stopping; })) break;
    }
}
LRESULT CALLBACK WindowProc(HWND window, UINT message, WPARAM wp, LPARAM lp) {
    switch (message) {
    case sampleMessage: {
        std::lock_guard lock(mutex);
        if (displayed != pending) {
            displayed = pending;
            auto title = L"Player : " + (displayed ? std::to_wstring(*displayed) : std::wstring(L"--"));
            SetWindowTextW(window, title.c_str());
            InvalidateRect(window, nullptr, FALSE);
        }
        return 0;
    }
    case WM_ERASEBKGND: return 1;
    case WM_PAINT: {
        PAINTSTRUCT paint{}; HDC dc = BeginPaint(window, &paint);
        RECT rect{}; GetClientRect(window, &rect);
        const auto tone = pc::Color(displayed);
        COLORREF background = tone == pc::Tone::Missing ? RGB(232,232,232) : tone == pc::Tone::Alert ? RGB(220,0,0) : RGB(255,255,255);
        COLORREF foreground = tone == pc::Tone::Missing ? RGB(85,85,85) : tone == pc::Tone::Alert ? RGB(255,255,255) : RGB(0,0,0);
        auto brush = CreateSolidBrush(background); FillRect(dc, &rect, brush); DeleteObject(brush);
        auto old = SelectObject(dc, font); SetBkMode(dc, TRANSPARENT); SetTextColor(dc, foreground);
        auto text = L"Player : " + (displayed ? std::to_wstring(*displayed) : std::wstring(L"--"));
        DrawTextW(dc, text.c_str(), -1, &rect, DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS);
        SelectObject(dc, old); EndPaint(window, &paint); return 0;
    }
    case WM_LBUTTONDOWN: ReleaseCapture(); SendMessageW(window, WM_NCLBUTTONDOWN, HTCAPTION, 0); return 0;
    case WM_CONTEXTMENU: {
        POINT point{}; GetCursorPos(&point);
        HMENU menu = CreatePopupMenu(); AppendMenuW(menu, MF_STRING, 1, L"Exit");
        SetForegroundWindow(window);
        auto command = TrackPopupMenu(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON, point.x, point.y, 0, window, nullptr);
        DestroyMenu(menu); if (command == 1) DestroyWindow(window); return 0;
    }
    case WM_DPICHANGED: {
        UINT dpi = HIWORD(wp);
        if (font) DeleteObject(font);
        font = CreateFontW(-MulDiv(11, dpi, 72),0,0,0,FW_BOLD,FALSE,FALSE,FALSE,DEFAULT_CHARSET,0,0,CLEARTYPE_QUALITY,0,L"Segoe UI");
        auto rect = reinterpret_cast<RECT*>(lp);
        SetWindowPos(window, HWND_TOPMOST, rect->left, rect->top, MulDiv(136,dpi,96), MulDiv(36,dpi,96), SWP_NOACTIVATE);
        InvalidateRect(window, nullptr, FALSE); return 0;
    }
    case WM_DESTROY:
        { std::lock_guard lock(mutex); stopping = true; }
        wake.notify_all(); PostQuitMessage(0); return 0;
    }
    return DefWindowProcW(window, message, wp, lp);
}
}
int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR, int) {
    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    UINT dpi = GetDpiForSystem();
    font = CreateFontW(-MulDiv(11,dpi,72),0,0,0,FW_BOLD,FALSE,FALSE,FALSE,DEFAULT_CHARSET,0,0,CLEARTYPE_QUALITY,0,L"Segoe UI");
    WNDCLASSW cls{}; cls.hInstance = instance; cls.lpfnWndProc = WindowProc;
    cls.lpszClassName = L"PlayerCounterBadge"; cls.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    if (!RegisterClassW(&cls)) { DeleteObject(font); return 1; }
    HWND window = CreateWindowExW(WS_EX_TOPMOST | WS_EX_TOOLWINDOW, cls.lpszClassName, L"Player : --", WS_POPUP,
        40,40,MulDiv(136,dpi,96),MulDiv(36,dpi,96),nullptr,nullptr,instance,nullptr);
    if (!window) { DeleteObject(font); return 1; }
    ShowWindow(window, SW_SHOWNOACTIVATE);
    std::thread worker(Worker, window);
    MSG message{};
    while (GetMessageW(&message, nullptr, 0, 0) > 0) { TranslateMessage(&message); DispatchMessageW(&message); }
    { std::lock_guard lock(mutex); stopping = true; }
    wake.notify_all(); worker.join(); DeleteObject(font); return 0;
}
