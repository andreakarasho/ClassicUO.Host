using System;
using System.IO;
using System.Reflection;

sealed unsafe class PluginLoader
{
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

        var header = new PluginHeader();

        meth.Invoke(null, new object[] { (IntPtr)(&header) });
    }
}

sealed class Plugin
{

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