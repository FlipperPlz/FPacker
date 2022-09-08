using System.Security.Cryptography;
using System.Text;
using FPacker.Models;

namespace FPacker.Utils; 

public static class ObfuscationTools {
    private static readonly string[] IllegalWindowsFilenames = new[] {
        "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3",
        "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8",
        "LPT9"
    };

    private static readonly string[] RVExtensions = new[] {
        ".c", ".rvmat", ".ogg", ".p3d", ".sqf", ".rtm", ".layout", ".edds", ".paa", ".jpg", ".ptc", ".anm", ".xob",
        ".fnt", ".xyz"
    };

    private static readonly string[] WindowsFolderGUIDs = new[] {
        "{DE61D971-5EBC-4F02-A3A9-6C82895E5C04}", "{724EF170-A42D-4FEF-9F26-B60E846FBA4F}",
        "{A520A1A4-1780-4FF6-BD18-167343C5AF16}", "{A305CE99-F527-492B-8B1A-7E76FA98D6E4}",
        "{9E52AB10-F80D-49DF-ACB8-4330F5687855}", "{DF7266AC-9274-4867-8D55-3BD661DE872D}",
        "{D0384E7D-BAC3-4797-8F14-CBA229B392B5}", "{C1BAE2D0-10DF-4334-BEDD-7AA20B227A9D}",
        "{0139D44E-6AFE-49F2-8690-3DAFCAE6FFB8}", "{A4115719-D62E-491D-AA7C-E74B8BE3B067}",
        "{82A5EA35-D9CD-47C5-9629-E15D2F714E6E}", "{B94237E7-57AC-4347-9151-B08C6C32D1F7}",
        "{0AC0837C-BBF8-452A-850D-79D08E667CA7}", "{4BFEFB45-347D-4006-A5BE-AC0CB0567192}",
        "{6F0CD92B-2E97-45D1-88FF-B0D186B8DEDD}", "{56784854-C6CB-462B-8169-88E350ACB882}",
        "{82A74AEB-AEB4-465C-A014-D097EE346D63}", "{2B0F765D-C0E9-4171-908E-08A611B84FF6}",
        "{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}", "{FDD39AD0-238F-46AF-ADB4-6C85480369C7}",
        "{374DE290-123F-4565-9164-39C4925E467B}", "{1777F761-68AD-4D8A-87BD-30B759FA33DD}",
        "{FD228CB7-AE11-4AE3-864C-16F3910AB8FE}", "{CAC52C1A-B53D-4EDC-92D7-6B2E8AC19434}",
        "{054FAE61-4DD8-4787-80B6-090220C4B700}", "{D9DC8A3B-B784-432E-A781-5A1130A75963}",
        "{4D9F7874-4E0C-4904-967B-40B0D20C3E4B}", "{352481E8-33BE-4251-BA85-6007CAEDCF9D}",
        "{BFB9D5E0-C6A9-404C-B2B2-AE6DB6AF4968}", "{F1B32785-6FBA-4FCF-9D55-7B8E7F157091}",
        "{2A00375E-224C-49DE-B8D1-440DF7EF3DDC}", "{4BD8D571-6D19-48D3-BE97-422220080E43}",
        "{C5ABBF53-E17F-4121-8900-86626FC2C973}", "{D20BEEC4-5CA8-4905-AE3B-BF251EA09B53}",
        "{31C0DD25-9439-4F12-BF41-7FF4EDA38722}", "{2C36C0AA-5812-4B87-BFD0-4CD0DFB19B39}",
        "{69D2CF90-FC33-4FB7-9A0C-EBB0F0FCB43C}", "{33E28130-4E1E-4676-835A-98395C3BC3BB}",
        "{DE92C1C7-837F-4F69-A3BB-86E631204A23}", "{76FC4E2D-D6AD-4519-A663-37BD56068185}",
        "{9274BD8D-CFD1-41C3-B35E-B13F55A758F4}", "{5E6C858F-0E22-4760-9AFE-EA3317B67173}",
        "{62AB5D82-FDC1-4DC3-A9DD-070D1D495D97}", "{905E63B6-C1BF-494E-B29C-65B732D3D21A}",
        "{F7F1ED05-9F6D-47A2-AAAE-29D317C6F066}", "{6365D5A7-0F0D-45E5-87F6-0DA56B6A4F7D}",
        "{DE974D24-D9C6-4D3E-BF91-F4455120B917}", "{6D809377-6AF0-444B-8957-A3773F02200E}",
        "{7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E}", "{A77F5D77-2E2B-44C3-A6A2-ABA601054A51}",
        "{DFDF76A2-C82A-4D63-906A-5644AC457385}", "{C4AA340D-F20F-4863-AFEF-F87EF2E6BA25}",
        "{ED4824AF-DCE4-45A8-81E2-FC7965083634}", "{3D644C9B-1FB8-4F30-9B45-F670235F79C0}",
        "{DEBF2536-E1A8-4C59-B6A2-414586476AEA}", "{3214FAB5-9757-4298-BB61-92A9DEAA44FF}",
        "{B6EBFB86-6907-413C-9AF7-4FC2ABF07CC5}", "{2400183A-6185-49FB-A2D8-4A392A602BA3}",
        "{52A4F021-7B75-48A9-9F6B-4B87A210BC8F}", "{AE50C081-EBD2-438A-8655-8A092E34987A}",
        "{BD85E001-112E-431E-983B-7B15AC09FFF1}", "{B7534046-3ECB-4C18-BE4E-64CD4CB7D6AC}",
        "{8AD10C31-2ADB-4296-A8F7-E4701232C972}", "{3EB685DB-65F9-4CF6-A03A-E3EF65729F3D}",
        "{B250C668-F57D-4EE1-A63C-290EE7D1AA1F}", "{C4900540-2379-4C75-844B-64E6FAF8716B}",
        "{15CA69B3-30EE-49C1-ACE1-6B5EC372AFB5}", "{859EAD94-2E85-48AD-A71A-0969CB56A6CD}",
        "{4C5C32FF-BB9D-43B0-B5B4-2D72E54EAAA4}", "{7D1D3A04-DEBB-4115-95CF-2F29DA2920DA}",
        "{EE32E446-31CA-4ABA-814F-A5EBD2FD6D5E}", "{98EC0E18-2098-4D44-8644-66979315A281}",
        "{190337D1-B8CA-4121-A639-6D472D16972A}", "{8983036C-27C0-404B-8F08-102D10DCFD74}",
        "{7B396E54-9EC5-4300-BE0A-2482EBAE1A26}", "{A75D362E-50FC-4FB7-AC2C-A8BEAA314493}",
        "{625B53C3-AB48-4EC1-BA1F-A1EF4146FC19}", "{B97D20BB-F46A-4C97-BA10-5E3608430854}",
        "{43668BF8-C14E-49B2-97C9-747784D784B7}", "{289A9A43-BE44-4057-A41B-587A76D7E7F9}",
        "{0F214138-B1D3-4A90-BBA9-27CBC0C5389A}", "{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}",
        "{D65231B0-B2F1-4857-A4CE-A8E7C6EA7D27}", "{A63293E8-664E-48DB-A079-DF759E0509F7}",
        "{5B3749AD-B49F-49C1-83EB-15370FBD4882}", "{0762D272-C50A-4BB0-A382-697DCD729B80}",
        "{F3CE0F7C-4901-4ACC-8648-D5D44B04EF8F}", "{18989B1D-99B5-455B-841C-AB7C74E4DDFC}",
        "{F38BF404-1D43-42F2-9305-67DE0B28FC23}"
    };

    public static string GetRandomIllegalFilename() {
        return IllegalWindowsFilenames.OrderBy(n => Guid.NewGuid()).ToArray()[new Random().Next(IllegalWindowsFilenames.Length)];
    }
    public static string GetRandomFolderGUID() {
        return WindowsFolderGUIDs.OrderBy(n => Guid.NewGuid()).ToArray()[new Random().Next(WindowsFolderGUIDs.Length)];
    }
    public static string GetRandomRVExtension() {
        return RVExtensions.OrderBy(n => Guid.NewGuid()).ToArray()[new Random().Next(RVExtensions.Length)];
    }
    
    public static string GenerateObfuscatedPath(string parent = "", string? extension = null) {
              var pathBuilder = new StringBuilder();
        extension ??= GetRandomRVExtension();
        if (parent != "") pathBuilder.Append(parent).Append('\\');

        var obfBuilder = new StringBuilder();
        obfBuilder.Append(RandomString(includeSpaces: true)).Append('.').Append(GetRandomFolderGUID()).Append("\\\\\\\\\\").Append(RandomString(includeSpaces: true)).Append('\\').Append("../../../////..//..//..//..//..\\..\\\\\\\\\\..\\..\\..\\").Append(GetRandomIllegalFilename()).Append(extension);
        obfBuilder.Append(RandomString()).Append(extension);
        return pathBuilder.Append(RandomizeStringCase(obfBuilder.ToString())).ToString();
    }
    
    public static string GenerateSimpleObfuscatedPath(out string fileName, string parent = "") {
        var pathBuilder = new StringBuilder();
        if (parent != "") pathBuilder.Append(parent).Append('\\');

        var obfBuilder = new StringBuilder();
        fileName = $"{RandomString(16,allowableChars: "!@#$%^&*()<>.<~:;?+=-_", includeSpaces: true)}{GetRandomRVExtension()}";
        obfBuilder.Append(fileName);
        return pathBuilder.Append(RandomizeStringCase(obfBuilder.ToString())).ToString();
    }

    public static PBOEntry GenerateJunkEntry(string parent = "") =>
        new PBOEntry(GenerateObfuscatedPath(parent), new MemoryStream(new byte[] { }), (int)PackingTypeFlags.Compressed);


    public static MemoryStream GenerateIncludeText(string including, string? prefix = null) {
        var b = new StringBuilder();
        b.Append("/*\n");
        if (prefix is null or "") {
            b.Append($"#pragma \"{GenerateObfuscatedPath()}\"\n");
            b.Append("*/\n");
            b.Append($"#include \"{including}\"\n");
        }
        else {
            b.Append($"#pragma \"{prefix}\\{GenerateObfuscatedPath()}\"\n");
            b.Append("*/\n");
            b.Append($"#include \"{prefix}\\{including}\"\n");
        }
        return new MemoryStream(Encoding.UTF8.GetBytes(b.ToString()));
    }
    
    public static string RandomizeStringCase(string someString) {
        var randomizer = new Random();
        var final =
            someString.Select(x => randomizer.Next() % 2 == 0 ? 
                (char.IsUpper(x) ? x.ToString().ToLower().First() : x.ToString().ToUpper().First()) : x);
        return new string(final.ToArray()); 
    }
    
    public static string RandomString(int stringLength = 8, string? allowableChars = null, bool includeSpaces = false, bool includeNumbers = true) {
        // ReSharper disable once StringLiteralTypo
        if (allowableChars is not null && allowableChars != string.Empty) allowableChars = @"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        if (includeNumbers) allowableChars += "0123456789";
        if (includeSpaces) allowableChars += " ";

        
        var rnd = new byte[stringLength];
        using (var rng = new RNGCryptoServiceProvider()) rng.GetBytes(rnd);
        
        var allowable = (allowableChars ?? "abcdefghijklmnopqrstuvwxyz").ToCharArray();
        var l = allowable.Length;
        var chars = new char[stringLength];
        for (var i = 0; i < stringLength; i++)
            chars[i] = allowable[rnd[i] % l];

        var generatedString = new string(chars);
        
        //if (randoms.Contains(generatedString)) return RandomString(stringLength, allowableChars, includeNumbers);
        //randoms.Add(generatedString);
        //Console.Write(".");
        return generatedString;
    }
}