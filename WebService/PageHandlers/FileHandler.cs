#region imports

using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using FPacker;
using FPacker.IO;
using FPacker.Models;
using FPacker.Utils;
using static System.Net.WebRequestMethods;
using File = System.IO.File;
#endregion

namespace WebService.PageHandlers;
// This class is responsable for generating all the HTML and javascript the the application homepage

internal class FileHandler : AbstractPageHandler
{
    /// <summary>
    ///     Get the file and safe it.
    /// </summary>
    /// <param name="response">not used</param>
    /// <param name="uri">raw URI tokenized by '/'</param>
    /// <returns>HTML page + javascript</returns>
    public override byte[] HandleRequest(HttpListenerRequest request, HttpListenerResponse response, string[] uri)
    {
        var rnd = new Random();
        //Settings settings = new Settings();
        var _params = GetParams(uri);
        var key = "7289B39D-8BF4-43E8-982A-CFFA518E04BD";
        _params.TryGetValue("key", out key);
        /*if (!Statics.keyHandler.isKeyValid(key))
        {
            //response.StatusCode = 500;
            //return BuildHTML("invalid key");
            settings.RVMat = false;
            settings.P3D = false;
            settings.Config = false;
        }
        */
        foreach (string header in request.Headers)
        {
            Console.WriteLine($"{header}:{request.Headers[header]}");
        }
        foreach (var param in _params)
        {
            Console.WriteLine($"got param: {param}");
        }

        var Boundary = Statics.GetBoundary(request.ContentType);

        MemoryStream ms = new MemoryStream();
        var result = Statics.GetStream(request.ContentEncoding, Boundary, request.InputStream, out ms);
        if (ms.ToArray().Length == 0)
        {
            response.StatusCode = 500;
            return BuildHTML("invalid zip file");
        }

        var prefix = "test";

        var factory = new PboFactory("GayPorn").WithObfuscatedIncludes();

        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        foreach (var file in zip.Entries)
        {
            /*
             * fix later
             * should use using but closing stream too early
             */
            var stream = file.Open();
            var fileMs = new MemoryStream();
            stream.CopyTo(fileMs);
            fileMs.Position = 0;
            factory = factory.WithEntry(new PBOEntry(file.FullName, fileMs,
                (int)PackingTypeFlags.Compressed), true);
        }
        

        //using var fs = File.OpenRead(outFile);
        //var fileName = Path.GetFileName(outFile);
        var fileName = "test.pbo";
        var bytes = factory.Build().ToArray();
        File.WriteAllBytes(@"archive.pbo", bytes);

        response.ContentLength64 = bytes.Length;
        response.SendChunked = false;
        response.ContentType = System.Net.Mime.MediaTypeNames.Application.Octet;
        response.AddHeader("Content-disposition", $"attachment; filename={fileName}");
        byte[] buffer = new byte[64 * 1024];
        int read;
        using var bw = new BinaryWriter(response.OutputStream);
        bw.Write(bytes, 0, bytes.Length);
        bw.Flush();
        bw.Close();

        response.StatusCode = (int)HttpStatusCode.OK;
        response.StatusDescription = "OK";
        var staticPage = "ok";
        return BuildHTML(staticPage);
    }
}