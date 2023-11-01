using GraphQL;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using TeslaSolarCharger.GridPriceProvider.Data;
using TeslaSolarCharger.GridPriceProvider.Data.Options;
using TeslaSolarCharger.GridPriceProvider.Services.Interfaces;

namespace TeslaSolarCharger.GridPriceProvider.Services;

public class TibberService : IPriceDataService
{
    private readonly HttpClient _client;
    private readonly GraphQLHttpClientOptions _graphQLHttpClientOptions;
    private readonly TibberOptions _options;
    private readonly IGraphQLJsonSerializer _graphQLJsonSerializer;

    public TibberService(
        HttpClient client,
        IGraphQLJsonSerializer graphQLJsonSerializer,
        IOptions<TibberOptions> options
        )
    {
        _client = client;
        _options = options.Value;
        _graphQLHttpClientOptions = new GraphQLHttpClientOptions { EndPoint = new Uri(_options.BaseUrl) };
        _graphQLJsonSerializer = graphQLJsonSerializer;
    }

    public async Task<IEnumerable<Price>> GetPriceData(DateTimeOffset from, DateTimeOffset to)
    {
        var fetch = (int)Math.Ceiling((to - from).TotalHours) + 1;
        var request = new GraphQLHttpRequest
        {
            Query = @"
query PriceData($after: String, $first: Int) {
    viewer {
        homes {
            currentSubscription {
                priceInfo {
                    range(resolution: HOURLY, after: $after, first: $first) {
                        nodes {
                            total
                            startsAt
                        }
                    }
                    current {
                        total
                        startsAt
                        level
                    }
                }
            }
        }
    }
}
",
            OperationName = "PriceData",
            Variables = new
            {
                after = Convert.ToBase64String(Encoding.UTF8.GetBytes(from.AddHours(-1).ToString("o"))),
                first = fetch
            }
        };
        var graphQLHttpResponse = await SendRequest(request);
        var priceInfo = graphQLHttpResponse
            .Data
            .Viewer
            .Homes
            .First()
            .CurrentSubscription
            .PriceInfo;

        var prices = priceInfo
            .Range
            .Nodes
            .Select(x => new Price
            {
                ValidFrom = x.StartsAt,
                ValidTo = x.StartsAt.AddHours(1),
                Value = x.Total
            })
            .ToList();

        var count = prices.Count();
        // The Tibber range API only returns historical and does not include the current price
        // This will add the current price to the list if it should be in there but isn't
        if (count + 1 == fetch
            && priceInfo.Current.StartsAt >= from.AddHours(-1)
            && priceInfo.Current.StartsAt < to
            && !prices.Any(x => x.ValidFrom == priceInfo.Current.StartsAt)
            )
        {
            prices.Add(new Price
            {
                ValidFrom = priceInfo.Current.StartsAt,
                ValidTo = priceInfo.Current.StartsAt.AddHours(1),
                Value = priceInfo.Current.Total
            });
        }
        else if (count != fetch)
        {
            throw new Exception($"Mismatch of requested price info from Tibber API (expected: {fetch}, actual: {count})");
        }

        return prices;
    }

    private async Task<GraphQLHttpResponse<ResponseType>> SendRequest(GraphQLHttpRequest request)
    {
        using var httpRequestMessage = request.ToHttpRequestMessage(_graphQLHttpClientOptions, _graphQLJsonSerializer);
        using var httpResponseMessage = await _client.SendAsync(httpRequestMessage);
        var contentStream = await httpResponseMessage.Content.ReadAsStreamAsync();
        if (httpResponseMessage.IsSuccessStatusCode)
        {
            var graphQLResponse = await JsonSerializer.DeserializeAsync<GraphQLResponse<ResponseType>>(contentStream, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            if (graphQLResponse == null)
            {
                throw new Exception($"Deserialization of Tibber API response failed");
            }
            var graphQLHttpResponse = graphQLResponse.ToGraphQLHttpResponse(httpResponseMessage.Headers, httpResponseMessage.StatusCode);
            if (graphQLHttpResponse.Errors?.Any() ?? false)
            {
                var errorMessages = string.Join(", ", graphQLHttpResponse.Errors.Select(x => x.Message));
                throw new HttpRequestException($"Failed to call Tibber API: {errorMessages}");
            }
            return graphQLHttpResponse;
        }

        string content = null;
        if (contentStream != null)
        {
            using var sr = new StreamReader(contentStream);
            content = await sr.ReadToEndAsync();
        }

        throw new GraphQLHttpRequestException(httpResponseMessage.StatusCode, httpResponseMessage.Headers, content);
    }

    private class ResponseType
    {
        public Viewer Viewer { get; set; }
    }

    private class Viewer
    {
        public List<Home> Homes { get; set; }
    }

    private class Home
    {
        public Subscription CurrentSubscription { get; set; }
    }

    private class Subscription
    {
        public PriceInfo PriceInfo { get; set; }
    }

    private class PriceInfo
    {
        public RangeInfo Range { get; set; }
        public Node Current { get; set; }
    }

    private class RangeInfo
    {
        public List<Node> Nodes { get; set; }
    }

    private class Node
    {
        public decimal Total { get; set; }
        public DateTimeOffset StartsAt { get; set; }
    }
}
