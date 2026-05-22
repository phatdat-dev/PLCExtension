internal static class Printt
{
    public static void Default(object? obj) => Console.WriteLine(StringPrintColor.Default(obj));
    public static void Black(object? obj) => Console.WriteLine(StringPrintColor.Black(obj));
    public static void Red(object? obj) => Console.WriteLine(StringPrintColor.Red(obj));
    public static void Green(object? obj) => Console.WriteLine(StringPrintColor.Green(obj));
    public static void Yellow(object? obj) => Console.WriteLine(StringPrintColor.Yellow(obj));
    public static void Blue(object? obj) => Console.WriteLine(StringPrintColor.Blue(obj));
    public static void Magenta(object? obj) => Console.WriteLine(StringPrintColor.Magenta(obj));
    public static void Cyan(object? obj) => Console.WriteLine(StringPrintColor.Cyan(obj));
    public static void White(object? obj) => Console.WriteLine(StringPrintColor.White(obj));
    public static void Reset(object? obj) => Console.WriteLine(StringPrintColor.Reset(obj));
}

internal static class StringPrintColor
{
    public static string Default(object? obj) => $"{obj}";
    public static string Black(object? obj) => $"\x1B[30m{obj}\x1B[0m";
    public static string Red(object? obj) => $"\x1B[31m{obj}\x1B[0m";
    public static string Green(object? obj) => $"\x1B[32m{obj}\x1B[0m";
    public static string Yellow(object? obj) => $"\x1B[33m{obj}\x1B[0m";
    public static string Blue(object? obj) => $"\x1B[34m{obj}\x1B[0m";
    public static string Magenta(object? obj) => $"\x1B[35m{obj}\x1B[0m";
    public static string Cyan(object? obj) => $"\x1B[36m{obj}\x1B[0m";
    public static string White(object? obj) => $"\x1B[37m{obj}\x1B[0m";
    public static string Reset(object? obj) => $"\x1B[38m{obj}\x1B[0m";
}
