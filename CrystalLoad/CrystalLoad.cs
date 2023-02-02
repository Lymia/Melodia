namespace CrystalLoad;

using System;

using SDL2;

class CrystalLoad {
    private static void ErrorMsgBox(string msg) {
        SDL.SDL_ShowSimpleMessageBox(SDL.SDL_MessageBoxFlags.SDL_MESSAGEBOX_ERROR, "CrystalLoad Error", msg, IntPtr.Zero);
        throw new Exception(msg);
    }

    static void Main(string[] args)
    {
        if (args.Length != 2) 
            ErrorMsgBox("Not enough arguments passed to CrystalLoad Main!");

        Console.WriteLine("CrystalLoad by AuroraAmissa");
        Console.WriteLine();
        Console.WriteLine($"Target .dll to load: {args[0]}");
        Console.WriteLine($"Path to Crystal Project: {args[1]}");
        Console.WriteLine();
    }
}