// App member methods: Suite layout is separate from the standalone research workspace.
HWND suiteNotice=nullptr;
int suiteHeight=700;
bool suiteLayoutBusy=false;
bool suiteCloseStarted=false;
struct SuiteHeader { RECT title{}, subtitle{}, badge{}; int bottom=0; };
SuiteHeader MeasureSuiteHeader(){
    // The managed shell already supplies the page title and description.
    SuiteHeader header;header.bottom=ScaleUi(20,dpi);return header;
}
void PaintSuiteHeader(HDC){}
int SuiteTextHeight(HWND control,int width,int minimum=24){
    if(!control)return 0;
    int length=GetWindowTextLengthW(control);std::wstring text(size_t(length)+1,L'\0');GetWindowTextW(control,text.data(),length+1);
    HDC dc=GetDC(control);auto font=reinterpret_cast<HFONT>(SendMessageW(control,WM_GETFONT,0,0));auto old=SelectObject(dc,font);
    RECT measure{0,0,std::max(1,width),0};DrawTextW(dc,text.c_str(),-1,&measure,DT_CALCRECT|DT_WORDBREAK|DT_NOPREFIX);
    SelectObject(dc,old);ReleaseDC(control,dc);return std::max(ScaleUi(minimum,dpi),int(measure.bottom)+ScaleUi(6,dpi));
}
void LayoutSuite(){
    if(!window||playControls.empty()||suiteLayoutBusy)return;suiteLayoutBusy=true;
    RECT client{};GetClientRect(window,&client);int pad=ScaleUi(20,dpi),gap=ScaleUi(10,dpi),width=std::max(1,int(client.right)-2*pad),y=MeasureSuiteHeader().bottom+gap;
    auto place=[&](HWND h,int height){Place(h,{pad,y,width,height});y+=height+gap;};
    ShowWindow(tabs,SW_HIDE);ShowWindow(playControls[0],SW_HIDE);
    place(playStatus,SuiteTextHeight(playStatus,width,24));
    place(playControls[2],SuiteTextHeight(playControls[2],width));
    Place(initialCalls,{pad,y,std::min(width,ScaleUi(180,dpi)),ScaleUi(34,dpi)});Place(GetDlgItem(window,InitialSpin),{pad+std::min(width,ScaleUi(180,dpi))-ScaleUi(20,dpi),y,ScaleUi(20,dpi),ScaleUi(34,dpi)});y+=ScaleUi(44,dpi);
    for(int id:{HideAoeVisual})place(GetDlgItem(window,id),SuiteTextHeight(GetDlgItem(window,id),width-ScaleUi(24,dpi),30));
    ShowWindow(GetDlgItem(window,StartWindows),SW_HIDE);ShowWindow(GetDlgItem(window,MinimizeArm),SW_HIDE);
    ShowWindow(visualStatus,SW_HIDE);
    // Action row wraps using each button's measured caption, never truncating its text.
    int x=pad,rowHeight=ScaleUi(40,dpi);int actionIds[]={SaveSettings,AttachNow,DetachNow,ResetExperiment,ExportExperiment};
    for(int id:actionIds){HWND h=GetDlgItem(window,id);wchar_t text[128]{};GetWindowTextW(h,text,128);HDC dc=GetDC(h);auto old=SelectObject(dc,font);SIZE size{};GetTextExtentPoint32W(dc,text,lstrlenW(text),&size);SelectObject(dc,old);ReleaseDC(h,dc);
        int w=std::min(width,std::max(ScaleUi(140,dpi),int(size.cx)+ScaleUi(32,dpi)));if(x>pad&&x+w>pad+width){x=pad;y+=rowHeight+gap;}Place(h,{x,y,w,rowHeight});x+=w+gap;}
    y+=rowHeight+gap;
    place(GetDlgItem(window,ArmNow),SuiteTextHeight(GetDlgItem(window,ArmNow),width-ScaleUi(24,dpi),34));
    if(suiteNotice)place(suiteNotice,SuiteTextHeight(suiteNotice,width,24));
    suiteHeight=y+ScaleUi(20,dpi);suiteLayoutBusy=false;
}
