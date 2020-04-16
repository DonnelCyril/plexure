using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Plexure.Exercise1
{
    class Program
    {
        static async Task Main()
        {
            var resourceUris = new[] { "1", "2", "3" }.Select(id => new Uri($"http://localhost:8888/resource/{id}"));
            var timeOutInMilliseconds = 1000;
            try
            {
                using var cts = new CancellationTokenSource(timeOutInMilliseconds);
                var resources = await FetchResources(resourceUris, cts.Token);
                foreach (var result in resources.Select((r, idx) => $"Resource {idx + 1}:\n{r}\n"))
                {
                    Console.WriteLine(result);
                }

            }
            catch (TaskCanceledException)
            {
                Console.WriteLine($"{nameof(FetchResources)} didn't complete within {timeOutInMilliseconds / 1000.0} seconds and was cancelled.");
            }
        }

        private static async Task<IEnumerable<string>> FetchResources(IEnumerable<Uri> resourceUris, CancellationToken cancellationToken = default)
        {
            var httpClient = new HttpClient();
            var fetchResources = await Task.WhenAll(resourceUris.Select(uri => FetchResource(httpClient, uri, cancellationToken)));
            return fetchResources;

            static async Task<string> FetchResource(HttpClient client, Uri resourceUri, CancellationToken cancellationToken)
            {
                var response = await client.GetAsync(resourceUri, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return await response.Content.ReadAsStringAsync();
            }
        }

    }
}
