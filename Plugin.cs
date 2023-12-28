using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using ClassicUO.Host;
using CUO_API;

sealed unsafe class Plugin
{
    public static List<Plugin> Plugins { get; } = new List<Plugin>();


    private readonly CuoCustomServer _server;

    public Plugin(CuoCustomServer server)
    {
        _server = server;
    }

    public void Load(string pluginPath)
    {
        if (!File.Exists(pluginPath))
            return;

        var asm = Assembly.LoadFile(pluginPath);
        var type = asm.GetType("Assistant.Engine");

        if (type == null)
        {
            return;
        }

        var meth = type.GetMethod(
            "Install",
            BindingFlags.Public | BindingFlags.Static
        );

        if (meth == null)
        {
            return;
        }

        var clientVersion_TODO = 117466370;
        var hwnd_TODO = IntPtr.Zero;
        var sdlWnd_TODO = IntPtr.Zero;

        _recv = OnPluginRecv;
        _send = OnPluginSend;
        _recv_new = OnPluginRecv_new;
        _send_new = OnPluginSend_new;
        _getPacketLength = GetPacketLength;
        _getPlayerPosition = GetPlayerPosition;
        _castSpell = CastSpell;
        _getStaticImage = GetStaticImage;
        _getUoFilePath = GetUOFilePath;
        _requestMove = RequestMove;
        _setTitle = SetWindowTitle;
        _get_static_data = GetStaticData;
        _get_tile_data = GetTileData;
        _get_cliloc = GetCliloc;

        var header = new PluginHeader()
        {
            ClientVersion = clientVersion_TODO,
            Recv = Marshal.GetFunctionPointerForDelegate(_recv),
            Send = Marshal.GetFunctionPointerForDelegate(_send),
            GetPacketLength = Marshal.GetFunctionPointerForDelegate(_getPacketLength),
            GetPlayerPosition = Marshal.GetFunctionPointerForDelegate(_getPlayerPosition),
            CastSpell = Marshal.GetFunctionPointerForDelegate(_castSpell),
            GetStaticImage = Marshal.GetFunctionPointerForDelegate(_getStaticImage),
            HWND = hwnd_TODO,
            GetUOFilePath = Marshal.GetFunctionPointerForDelegate(_getUoFilePath),
            RequestMove = Marshal.GetFunctionPointerForDelegate(_requestMove),
            SetTitle = Marshal.GetFunctionPointerForDelegate(_setTitle),
            Recv_new = Marshal.GetFunctionPointerForDelegate(_recv_new),
            Send_new = Marshal.GetFunctionPointerForDelegate(_send_new),
            SDL_Window = sdlWnd_TODO,
            GetStaticData = Marshal.GetFunctionPointerForDelegate(_get_static_data),
            GetTileData = Marshal.GetFunctionPointerForDelegate(_get_tile_data),
            GetCliloc = Marshal.GetFunctionPointerForDelegate(_get_cliloc)
        };

        meth.Invoke(null, new object[] { (IntPtr)(&header) });

        if (header.OnRecv != IntPtr.Zero)
        {
            _onRecv = Marshal.GetDelegateForFunctionPointer<OnPacketSendRecv>(header.OnRecv);
        }

        if (header.OnSend != IntPtr.Zero)
        {
            _onSend = Marshal.GetDelegateForFunctionPointer<OnPacketSendRecv>(header.OnSend);
        }

        if (header.OnHotkeyPressed != IntPtr.Zero)
        {
            _onHotkeyPressed = Marshal.GetDelegateForFunctionPointer<OnHotkey>(
                header.OnHotkeyPressed
            );
        }

        if (header.OnMouse != IntPtr.Zero)
        {
            _onMouse = Marshal.GetDelegateForFunctionPointer<OnMouse>(header.OnMouse);
        }

        if (header.OnPlayerPositionChanged != IntPtr.Zero)
        {
            _onUpdatePlayerPosition =
                Marshal.GetDelegateForFunctionPointer<OnUpdatePlayerPosition>(
                    header.OnPlayerPositionChanged
                );
        }

        if (header.OnClientClosing != IntPtr.Zero)
        {
            _onClientClose = Marshal.GetDelegateForFunctionPointer<OnClientClose>(
                header.OnClientClosing
            );
        }

        if (header.OnInitialize != IntPtr.Zero)
        {
            _onInitialize = Marshal.GetDelegateForFunctionPointer<OnInitialize>(
                header.OnInitialize
            );
        }

        if (header.OnConnected != IntPtr.Zero)
        {
            _onConnected = Marshal.GetDelegateForFunctionPointer<OnConnected>(
                header.OnConnected
            );
        }

        if (header.OnDisconnected != IntPtr.Zero)
        {
            _onDisconnected = Marshal.GetDelegateForFunctionPointer<OnDisconnected>(
                header.OnDisconnected
            );
        }

        if (header.OnFocusGained != IntPtr.Zero)
        {
            _onFocusGained = Marshal.GetDelegateForFunctionPointer<OnFocusGained>(
                header.OnFocusGained
            );
        }

        if (header.OnFocusLost != IntPtr.Zero)
        {
            _onFocusLost = Marshal.GetDelegateForFunctionPointer<OnFocusLost>(
                header.OnFocusLost
            );
        }

        if (header.Tick != IntPtr.Zero)
        {
            _tick = Marshal.GetDelegateForFunctionPointer<OnTick>(header.Tick);
        }

        if (header.OnRecv_new != IntPtr.Zero)
        {
            _onRecv_new = Marshal.GetDelegateForFunctionPointer<OnPacketSendRecv_new>(
                header.OnRecv_new
            );
        }

        if (header.OnSend_new != IntPtr.Zero)
        {
            _onSend_new = Marshal.GetDelegateForFunctionPointer<OnPacketSendRecv_new>(
                header.OnSend_new
            );
        }

        if (header.OnDrawCmdList != IntPtr.Zero)
        {
            _draw_cmd_list = Marshal.GetDelegateForFunctionPointer<OnDrawCmdList>(
                header.OnDrawCmdList
            );
        }

        if (header.OnWndProc != IntPtr.Zero)
        {
            _on_wnd_proc = Marshal.GetDelegateForFunctionPointer<OnWndProc>(header.OnWndProc);
        }

        _onInitialize?.Invoke();

        Plugins.Add(this);
    }


    short GetPacketLength(int packetId)
    {
        // get from cuo
        return _server.GetPackeLen(Guid.Empty);
    }

    void CastSpell(int index)
    {
        // get from cuo
    }

    bool OnPluginRecv(ref byte[] data, ref int length)
    {
        // get from cuo

        return true;
    }

    bool OnPluginSend(ref byte[] data, ref int length)
    {
        // get from cuo

        return true;
    }

    bool OnPluginRecv_new(IntPtr buffer, ref int length)
    {
        // get from cuo

        return true;
    }

    bool OnPluginSend_new(IntPtr buffer, ref int length)
    {
        // get from cuo

        return true;
    }

    string GetUOFilePath()
    {
        // get from cuo
        return "";
    }

    void SetWindowTitle(string str)
    {
        // get from cuo
    }

    bool GetStaticData(
        int index,
        ref ulong flags,
        ref byte weight,
        ref byte layer,
        ref int count,
        ref ushort animid,
        ref ushort lightidx,
        ref byte height,
        ref string name
    )
    {
        // get from cuo

        return true;
    }

    bool GetTileData(
        int index,
        ref ulong flags,
        ref ushort textid,
        ref string name
    )
    {
        // get from cuo

        return true;
    }

    bool GetCliloc(int cliloc, string args, bool capitalize, out string buffer)
    {
        // get from cuo

        buffer = "";

        return buffer != null;
    }

    void GetStaticImage(ushort g, ref CUO_API.ArtInfo info)
    {

    }

    bool RequestMove(int dir, bool run)
    {
    // get from cuo

    return true;
    }

    bool GetPlayerPosition(out int x, out int y, out int z)
    {
    // get from cuo

    x = y = z = 0;

    return false;
    }

    void Tick()
    {
        // get from cuo
    }

    bool ProcessRecvPacket(byte[] data, ref int length)
    {
        var result = true;
        if (_onRecv_new != null)
        {
            result = _onRecv_new(data, ref length);
        }
        else if (_onRecv != null)
        {
            result = _onRecv(ref data, ref length);
        }
        
        // get from cuo
        
        return result;
    }

    bool ProcessSendPacket(ref Span<byte> message)
    {
        var result = true;
        if (_onSend_new != null)
        {
            var tmp = message.ToArray();
            var length = tmp.Length;
            result = _onSend_new(tmp, ref length);
            message = message.Slice(0, length);
            tmp.AsSpan(0, length).CopyTo(message);
        }
        else if (_onSend != null)
        {
            var tmp = message.ToArray();
            var length = tmp.Length;
            result = _onSend(ref tmp, ref length);
            message = message.Slice(0, length);
            tmp.AsSpan(0, length).CopyTo(message);
        }

        // get from cuo

        return result;
    }

    void OnClosing()
    {
        // get from cuo
        _onClientClose?.Invoke();

        Plugins.Remove(this);
    }

    void OnFocusGained()
    {
        _onFocusGained?.Invoke();
    }

    void OnFocusLost()
    {
        _onFocusLost?.Invoke();
    }

    void OnConnected()
    {
        _onConnected?.Invoke();
    }

    void OnDisconnected()
    {
        _onDisconnected?.Invoke();
    }

    bool ProcessHotkeys(int key, int mod, bool ispressed)
    {
        var result = _onHotkeyPressed?.Invoke(key, mod, ispressed) ?? false;

        return result;
    }

    void ProcessMouse(int button, int wheel)
    {
        _onMouse?.Invoke(button, wheel);
    }


    int ProcessWndProc(void* e)
    {
        var result = _on_wnd_proc(e);

        return result;
    }

    void UpdatePlayerPosition(int x, int y, int z)
    {
        try
        {
            _onUpdatePlayerPosition?.Invoke(x, y, z);
        }
        catch
        {
        }
    }



    [MarshalAs(UnmanagedType.FunctionPtr)]
    private OnCastSpell _castSpell;

    [MarshalAs(UnmanagedType.FunctionPtr)]
    private OnDrawCmdList _draw_cmd_list;

    [MarshalAs(UnmanagedType.FunctionPtr)]
    private OnGetCliloc _get_cliloc;

    [MarshalAs(UnmanagedType.FunctionPtr)]
    private OnGetStaticData _get_static_data;

    [MarshalAs(UnmanagedType.FunctionPtr)]
    private OnGetTileData _get_tile_data;

    [MarshalAs(UnmanagedType.FunctionPtr)]
    private OnGetPacketLength _getPacketLength;

    [MarshalAs(UnmanagedType.FunctionPtr)]
    private OnGetPlayerPosition _getPlayerPosition;

    [MarshalAs(UnmanagedType.FunctionPtr)]
    private OnGetStaticImage _getStaticImage;

    [MarshalAs(UnmanagedType.FunctionPtr)]
    private OnGetUOFilePath _getUoFilePath;

    [MarshalAs(UnmanagedType.FunctionPtr)]
    private OnWndProc _on_wnd_proc;

    [MarshalAs(UnmanagedType.FunctionPtr)]
    private OnClientClose _onClientClose;

    [MarshalAs(UnmanagedType.FunctionPtr)]
    private OnConnected _onConnected;

    [MarshalAs(UnmanagedType.FunctionPtr)]
    private OnDisconnected _onDisconnected;

    [MarshalAs(UnmanagedType.FunctionPtr)]
    private OnFocusGained _onFocusGained;

    [MarshalAs(UnmanagedType.FunctionPtr)]
    private OnFocusLost _onFocusLost;

    [MarshalAs(UnmanagedType.FunctionPtr)]
    private OnHotkey _onHotkeyPressed;

    [MarshalAs(UnmanagedType.FunctionPtr)]
    private OnInitialize _onInitialize;

    [MarshalAs(UnmanagedType.FunctionPtr)]
    private OnMouse _onMouse;

    [MarshalAs(UnmanagedType.FunctionPtr)]
    private OnPacketSendRecv_new _onRecv_new,
        _onSend_new;

    [MarshalAs(UnmanagedType.FunctionPtr)]
    private OnUpdatePlayerPosition _onUpdatePlayerPosition;

    [MarshalAs(UnmanagedType.FunctionPtr)]
    private OnPacketSendRecv _recv,
        _send,
        _onRecv,
        _onSend;

    [MarshalAs(UnmanagedType.FunctionPtr)]
    private OnPacketSendRecv_new_intptr _recv_new,
        _send_new;

    [MarshalAs(UnmanagedType.FunctionPtr)]
    private RequestMove _requestMove;
    private readonly Dictionary<IntPtr, object> _resources =
        new Dictionary<IntPtr, object>();

    [MarshalAs(UnmanagedType.FunctionPtr)]
    private OnSetTitle _setTitle;

    [MarshalAs(UnmanagedType.FunctionPtr)]
    private OnTick _tick;





    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void OnInstall(void* header);

    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool OnPacketSendRecv_new(byte[] data, ref int length);

    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool OnPacketSendRecv_new_intptr(IntPtr data, ref int length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int OnDrawCmdList([Out] out IntPtr cmdlist, ref int size);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int OnWndProc(void* ev);

    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool OnGetStaticData(
        int index,
        ref ulong flags,
        ref byte weight,
        ref byte layer,
        ref int count,
        ref ushort animid,
        ref ushort lightidx,
        ref byte height,
        ref string name
    );

    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool OnGetTileData(
        int index,
        ref ulong flags,
        ref ushort textid,
        ref string name
    );

    [return: MarshalAs(UnmanagedType.I1)]
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool OnGetCliloc(
        int cliloc,
        [MarshalAs(UnmanagedType.LPStr)] string args,
        bool capitalize,
        [Out][MarshalAs(UnmanagedType.LPStr)] out string buffer
    );
}

struct PluginHeader
{
    public int ClientVersion;
    public IntPtr HWND;
    public IntPtr OnRecv;
    public IntPtr OnSend;
    public IntPtr OnHotkeyPressed;
    public IntPtr OnMouse;
    public IntPtr OnPlayerPositionChanged;
    public IntPtr OnClientClosing;
    public IntPtr OnInitialize;
    public IntPtr OnConnected;
    public IntPtr OnDisconnected;
    public IntPtr OnFocusGained;
    public IntPtr OnFocusLost;
    public IntPtr GetUOFilePath;
    public IntPtr Recv;
    public IntPtr Send;
    public IntPtr GetPacketLength;
    public IntPtr GetPlayerPosition;
    public IntPtr CastSpell;
    public IntPtr GetStaticImage;
    public IntPtr Tick;
    public IntPtr RequestMove;
    public IntPtr SetTitle;

    public IntPtr OnRecv_new,
        OnSend_new,
        Recv_new,
        Send_new;

    public IntPtr OnDrawCmdList;
    public IntPtr SDL_Window;
    public IntPtr OnWndProc;
    public IntPtr GetStaticData;
    public IntPtr GetTileData;
    public IntPtr GetCliloc;
}
