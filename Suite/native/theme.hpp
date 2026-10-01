// Included inside AOE's private namespace, after the Suite palette constants.
void PaintSuiteControl(HWND window,HDC dc,bool tabs){
    RECT r{};GetClientRect(window,&r);auto brush=CreateSolidBrush(Background);FillRect(dc,&r,brush);DeleteObject(brush);
    auto old=SelectObject(dc,reinterpret_cast<HFONT>(SendMessageW(window,WM_GETFONT,0,0)));SetBkMode(dc,TRANSPARENT);
    if(tabs){
        for(int i=0;i<TabCtrl_GetItemCount(window);++i){
            RECT item{};TabCtrl_GetItemRect(window,i,&item);bool selected=i==TabCtrl_GetCurSel(window);
            auto fill=CreateSolidBrush(selected?Card:Background);FillRect(dc,&item,fill);DeleteObject(fill);
            wchar_t text[256]{};TCITEMW tab{};tab.mask=TCIF_TEXT;tab.pszText=text;tab.cchTextMax=256;TabCtrl_GetItem(window,i,&tab);
            SetTextColor(dc,selected?Accent:Muted);DrawTextW(dc,text,-1,&item,DT_CENTER|DT_VCENTER|DT_SINGLELINE);
        }
    }else{
        const auto style=GetWindowLongPtrW(window,GWL_STYLE)&BS_TYPEMASK;
        bool check=style==BS_AUTOCHECKBOX||style==BS_CHECKBOX;
        bool enabled=IsWindowEnabled(window)!=FALSE;auto state=SendMessageW(window,BM_GETSTATE,0,0);
        wchar_t text[512]{};GetWindowTextW(window,text,512);RECT label=r;
        if(check){
            int side=MulDiv(14,GetDpiForWindow(window),96);RECT box{r.left+2,(r.bottom-side)/2,r.left+2+side,(r.bottom+side)/2};
            auto fill=CreateSolidBrush(SendMessageW(window,BM_GETCHECK,0,0)==BST_CHECKED?Accent:Card);FillRect(dc,&box,fill);DeleteObject(fill);
            auto edge=CreateSolidBrush(Muted);FrameRect(dc,&box,edge);DeleteObject(edge);
            if(SendMessageW(window,BM_GETCHECK,0,0)==BST_CHECKED){SetTextColor(dc,Background);DrawTextW(dc,L"✓",-1,&box,DT_CENTER|DT_VCENTER|DT_SINGLELINE);}
            label.left=box.right+7;
        }else{
            auto fill=CreateSolidBrush((state&BST_PUSHED)?Accent:Card);FillRect(dc,&r,fill);DeleteObject(fill);
            auto edge=CreateSolidBrush(enabled?Accent:Muted);FrameRect(dc,&r,edge);DeleteObject(edge);InflateRect(&label,-6,-2);
        }
        SetTextColor(dc,enabled?Ink:Muted);DrawTextW(dc,text,-1,&label,DT_VCENTER|DT_SINGLELINE|DT_END_ELLIPSIS|(check?DT_LEFT:DT_CENTER));
        if((state&BST_FOCUS)&&!(SendMessageW(window,WM_QUERYUISTATE,0,0)&UISF_HIDEFOCUS)){RECT focus=r;InflateRect(&focus,-3,-3);DrawFocusRect(dc,&focus);}
    }
    SelectObject(dc,old);
}
LRESULT CALLBACK SuiteControlProc(HWND window,UINT message,WPARAM w,LPARAM l,UINT_PTR id,DWORD_PTR kind){
    if(message==WM_ERASEBKGND)return 1;
    if(message==WM_PAINT){PAINTSTRUCT ps{};HDC dc=BeginPaint(window,&ps);PaintSuiteControl(window,dc,kind==2);EndPaint(window,&ps);return 0;}
    if(message==WM_PRINTCLIENT){PaintSuiteControl(window,reinterpret_cast<HDC>(w),kind==2);return 0;}
    if(message==WM_NCDESTROY)RemoveWindowSubclass(window,SuiteControlProc,id);
    auto result=DefSubclassProc(window,message,w,l);
    if(message==BM_SETCHECK||message==WM_ENABLE||message==WM_SETFOCUS||message==WM_KILLFOCUS)InvalidateRect(window,nullptr,FALSE);
    return result;
}
void ThemeSuiteControl(HWND control){
    wchar_t type[64]{};GetClassNameW(control,type,64);
    if(wcscmp(type,L"Button")==0)SetWindowSubclass(control,SuiteControlProc,1,1);
    if(wcscmp(type,WC_TABCONTROLW)==0)SetWindowSubclass(control,SuiteControlProc,1,2);
    if(wcscmp(type,WC_LISTVIEWW)==0){ListView_SetBkColor(control,Background);ListView_SetTextBkColor(control,Background);ListView_SetTextColor(control,Ink);}
}
