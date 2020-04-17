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
            var timeOutInMilliseconds = 3000;
            try
            {
                using var cts = new CancellationTokenSource(timeOutInMilliseconds);
                var totalLengthOfResources = await GetTotalLengthOfResources(resourceUris, cts.Token);
                Console.WriteLine($"Total length of resources: {totalLengthOfResources}");
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine($"{nameof(GetTotalLengthOfResources)} didn't complete within {timeOutInMilliseconds / 1000.0} seconds and was cancelled.");
            }
        }

        private static async Task<long> GetTotalLengthOfResources(IEnumerable<Uri> resourceUris, CancellationToken cancellationToken = default)
        {
            var httpClient = new HttpClient();
            var fetchResourceLengths = await Task.WhenAll(resourceUris.Select(uri => GetResourceLength(httpClient, uri, cancellationToken)));
            return fetchResourceLengths.Sum(l => l ?? 0);

            static async Task<long?> GetResourceLength(HttpClient client, Uri resourceUri, CancellationToken cancellationToken)
            {
                var response = await client.GetAsync(resourceUri, cancellationToken);
                // using HTTP GET here as was specified in the test. You can also do a HEAD request if the server support it. Sample code shown below.
                //var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, resourceUri), cancellationToken);
                return response.Content.Headers.ContentLength;
            }
        }

    }
}
