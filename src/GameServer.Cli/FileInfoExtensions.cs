using HtmlAgilityPack;

namespace GameServer.Cli;

public static class HttpClientExtensions
{
    public static async Task GetFileAsync(this HttpClient httpClient, string url, FileInfo destination, CancellationToken cancellationToken = default)
    {
        using var source = await httpClient.GetStreamAsync(url, cancellationToken);
        using var destinationStream = new FileStream(destination.FullName, FileMode.Create, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(destinationStream, cancellationToken);
    }

    public static async Task GetFileIfNotExistsAsync(this HttpClient httpClient, string url, FileInfo destination, CancellationToken cancellationToken = default)
    {
        if (!destination.Exists)
        {
            await httpClient.GetFileAsync(url, destination, cancellationToken);
        }
    }

    public static async Task<HtmlDocument> ReadAsHtmlAsync(this HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = new HtmlDocument();
        document.LoadHtml(html);
        return document;
    }
}

public static class FileInfoExtensions
{
    public static async Task Text(this FileInfo fileInfo, string text)
    {
        await System.IO.File.WriteAllTextAsync(fileInfo.FullName, text);
    }

    public static FileInfo File(this DirectoryInfo directory, string path) => new FileInfo(Path.Combine(directory.FullName, path));

    public static FileInfo File(this CreateServerRequest request, string path) => request.Location.File(path);
}