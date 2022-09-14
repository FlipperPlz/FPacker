using FPacker.Builders;

public static class Program {

    public static void Main(string[] arguments) {
        arguments = new[] { @"C:\Users\developer\Desktop\TestMod" };
        
        var pbo = new PboBuilder("TestMod").WithEntryBuilder(e => {
            e.WithRelocatedConfigs();
            e.WithRelocatedScripts();
            e.WithConfigProtection();
            e.WithoutBinarizedConfigs();
            e.WithJunkFiles();
            return e.FromDirectory(arguments[0]);
        }).Build();
        File.WriteAllBytes(@"C:\Users\developer\Desktop\OmegaManager\servers\0\@TestMod\Addons\TestMod.pbo", pbo.ToArray());
    }
}