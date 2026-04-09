namespace PapersCli.Cli.Sources;

public static class HttpRetryHandler
{
    public static async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpClient client,
        HttpRequestMessage request,
        int maxRetries = 3,
        int delayMs = 1000,
        CancellationToken cancellationToken = default)
    {
        HttpResponseMessage? response = null;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            if (attempt > 0)
            {
                await Task.Delay(delayMs, cancellationToken);
                request = await CloneRequestAsync(request);
            }

            try
            {
                response = await client.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode || !IsTransient(response.StatusCode))
                    return response;
            }
            catch (HttpRequestException) when (attempt < maxRetries)
            {
                continue;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < maxRetries)
            {
                continue;
            }
        }

        return response ?? throw new HttpRequestException("Request failed after all retries.");
    }

    public static async Task<Stream> DownloadWithRetryAsync(
        HttpClient client,
        string url,
        int maxRetries = 3,
        int delayMs = 1000,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var response = await SendWithRetryAsync(client, request, maxRetries, delayMs, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    private static bool IsTransient(System.Net.HttpStatusCode statusCode) =>
        statusCode is System.Net.HttpStatusCode.RequestTimeout
            or System.Net.HttpStatusCode.TooManyRequests
            or System.Net.HttpStatusCode.InternalServerError
            or System.Net.HttpStatusCode.BadGateway
            or System.Net.HttpStatusCode.ServiceUnavailable
            or System.Net.HttpStatusCode.GatewayTimeout;

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        if (request.Content is not null)
        {
            var content = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(content);
            foreach (var header in request.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        return clone;
    }
}
