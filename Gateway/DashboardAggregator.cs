using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Ocelot.Middleware;
using Ocelot.Multiplexer;

namespace Gateway;

/// <summary>
/// Merges the housing-dashboard and feedback-dashboard downstream responses into one flat
/// JSON object (their top-level keys don't overlap). If either downstream returns a non-200,
/// that response is passed straight through instead of a half-built aggregate.
/// </summary>
public class DashboardAggregator : IDefinedAggregator
{
    public async Task<DownstreamResponse> Aggregate(List<HttpContext> responses)
    {
        var merged = new JsonObject();

        foreach (var context in responses)
        {
            var downstream = context.Items.DownstreamResponse();
            if (downstream is null)
            {
                continue;
            }

            var body = await downstream.Content.ReadAsStringAsync();

            if (downstream.StatusCode != HttpStatusCode.OK)
            {
                return new DownstreamResponse(
                    new StringContent(body, Encoding.UTF8, "application/json"),
                    downstream.StatusCode,
                    new List<KeyValuePair<string, IEnumerable<string>>>(),
                    downstream.ReasonPhrase);
            }

            if (JsonNode.Parse(body) is JsonObject obj)
            {
                foreach (var pair in obj)
                {
                    merged[pair.Key] = pair.Value?.DeepClone();
                }
            }
        }

        return new DownstreamResponse(
            new StringContent(merged.ToJsonString(), Encoding.UTF8, "application/json"),
            HttpStatusCode.OK,
            new List<KeyValuePair<string, IEnumerable<string>>>(),
            "OK");
    }
}
