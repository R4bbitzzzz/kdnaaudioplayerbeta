﻿using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using System;

namespace musicplayC
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .LogToTrace();

    }
}