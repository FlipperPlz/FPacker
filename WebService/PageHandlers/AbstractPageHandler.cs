#region imports

using System;
using System.Globalization;
using System.Net;
using System.Text;

#endregion

namespace WebService.PageHandlers;

/// <summary>
///     base class for all the request handlers
/// </summary>
internal abstract class AbstractPageHandler
{
    /// <summary>
    /// </summary>
    /// <param name="response">response to be sent to client</param>
    /// <param name="uri">tokenized request URI</param>
    /// <returns>response body</returns>
    public abstract byte[] HandleRequest(HttpListenerRequest request, HttpListenerResponse response, string[] uri);

    /// <summary>
    ///     boilerplate HTML wraping for all the response streams
    /// </summary>
    /// <param name="content"></param>
    /// <returns></returns>
    internal static byte[] BuildHTML(string content) =>
        Encoding.UTF8.GetBytes(
            $"<!doctype html>{Environment.NewLine}<head><title>FPacker</title></head>{Environment.NewLine}<body>{Environment.NewLine}{content}</body>{Environment.NewLine}</html>{Environment.NewLine}");

    /// <summary>
    ///     Get all params from uri
    /// </summary>
    /// <param name="uri"></param>
    /// <returns>Dictionary<string, string></returns>    
    internal static Dictionary<string, string> GetParams(string[] uri) => uri.Last().Split('?').Last().Split('&')
        .Select(part => part.Split('='))
        .Where(part => part.Length == 2)
        .ToDictionary(sp => sp[0], sp => sp[1]);

    /// <summary>
    ///     Get all post data
    /// </summary>
    /// <param name="uri"></param>
    /// <returns>string</returns>   
    public static string GetRequestPostData(HttpListenerRequest request)
    {
        if (!request.HasEntityBody) return "";
        using (System.IO.Stream body = request.InputStream) // here we have data
        using (var reader = new System.IO.StreamReader(body, request.ContentEncoding))
            return reader.ReadToEnd();
    }
}