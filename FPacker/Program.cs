using FPacker.Builders;

public static class Program {

    public static void Main(string[] arguments) {
        arguments = new[] { @"C:\Users\developer\Desktop\MinimalTestMod" };
        
        var pbo = new PboBuilder("MinimalTestMod").WithEntryBuilder(e => {
            e.WithoutBinarizedConfigs();
            e.WithConfigProtection();
            e.WithRelocatedConfigs();
            e.WithRenamedScripts();
            return e.FromDirectory(arguments[0]);
        }).Build();
        File.WriteAllBytes(@"C:\Users\developer\Desktop\OmegaManager\servers\0\@TestMod\Addons\MinimalTestMod.pbo", pbo.ToArray());
    }
}