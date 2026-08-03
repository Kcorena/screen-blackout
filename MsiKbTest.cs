using System;

public static class MsiKbTest
{
    public static int Main(string[] args)
    {
        var paths = MsiKb.MsiKeyboard.FindDevicePaths("vid_1462&pid_1601");
        Console.WriteLine("found " + paths.Count + " device path(s)");
        foreach (var p in paths) Console.WriteLine("  " + p);
        if (paths.Count == 0) { Console.WriteLine("NO DEVICE"); return 1; }

        if (args.Length == 0) { Console.WriteLine("usage: MsiKbTest off|on|flash"); return 2; }
        string cmd = args[0].ToLower();
        bool ok = false;
        if (cmd == "off")
        {
            ok = MsiKb.MsiKeyboard.TryTurnOff();
            Console.WriteLine("turn OFF -> " + ok);
        }
        else if (cmd == "on" || cmd == "flash")
        {
            ok = MsiKb.MsiKeyboard.TryTurnOn();
            Console.WriteLine("turn ON (load flash) -> " + ok);
        }
        return ok ? 0 : 3;
    }
}
