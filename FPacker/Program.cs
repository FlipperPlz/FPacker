// See https://aka.ms/new-console-template for more information

using FPacker;
using FPacker.Models;

public static class Program {

    public static void Main(string[] arguments) {
        arguments = new[] { @"C:\Users\developer\Desktop\HellfireCore" };
        var factory = new PboFactory("TestMod").WithObfuscatedIncludes();

        foreach (var file in new DirectoryInfo(arguments[0]).EnumerateFiles("*", SearchOption.AllDirectories)) {
            factory = factory.WithEntry(new PBOEntry(
                Path.GetRelativePath(arguments[0], file.FullName),
                file.OpenRead(),
                (int)PackingTypeFlags.Uncompressed), true);
        }

        var v = factory.Build();
        File.WriteAllBytes(@"C:\Users\developer\Desktop\OmegaManager\servers\0\@TestMod\Addons\TestMod.pbo", v.ToArray());
    }
}