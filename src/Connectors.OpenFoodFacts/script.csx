// Custom code for the Open Food Facts connector - implements Microsoft's "Script : ScriptBase" contract.
// Docs: https://learn.microsoft.com/en-us/connectors/custom-connectors/write-code
//
// - GetProductByBarcode: adds the User-Agent Open Food Facts requires, turns "status != 1"
//   into a 404 and flattens the response to the connector's Product schema.
// - GetProductImage: fetches an image URL through the connector, so the code app can show
//   images its content security policy would otherwise block.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

public class Script : ScriptBase
{
    private const string UserAgent = "TALXIS-Lab-WarehousePicking/1.0 (+https://github.com/TALXIS/lab)";

    public override async Task<HttpResponseMessage> ExecuteAsync()
    {
        switch (this.Context.OperationId)
        {
            case "GetProductByBarcode":
                return await GetProductByBarcode().ConfigureAwait(false);
            case "GetProductImage":
                return await GetProductImage().ConfigureAwait(false);
            default:
                return await this.Context.SendAsync(this.Context.Request, this.CancellationToken)
                    .ConfigureAwait(false);
        }
    }

    private async Task<HttpResponseMessage> GetProductByBarcode()
    {
        this.Context.Request.Headers.Remove("User-Agent");
        this.Context.Request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

        var response = await this.Context.SendAsync(this.Context.Request, this.CancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return response;
        }

        var body = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        if (body.Value<int?>("status") != 1)
        {
            return Message(HttpStatusCode.NotFound, "No product found for this barcode");
        }

        var product = body["product"] as JObject ?? new JObject();
        var result = new JObject
        {
            ["ean"] = body.Value<string>("code"),
            ["name"] = product.Value<string>("product_name"),
            ["brand"] = product.Value<string>("brands"),
            ["quantity"] = product.Value<string>("quantity"),
            ["imageUrl"] = product.Value<string>("image_url"),
        };

        response.Content = CreateJsonContent(result.ToString());
        return response;
    }

    private async Task<HttpResponseMessage> GetProductImage()
    {
        var imageUrl = GetQueryParameter(this.Context.Request.RequestUri, "imageUrl");
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return Message(HttpStatusCode.BadRequest, "imageUrl query parameter is required");
        }

        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(imageUrl));
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

        return await this.Context.SendAsync(request, this.CancellationToken).ConfigureAwait(false);
    }

    // System.Web isn't available to connector code, so the query string is parsed by hand.
    private static string GetQueryParameter(Uri requestUri, string name)
    {
        foreach (var pair in requestUri.Query.TrimStart('?').Split('&'))
        {
            var parts = pair.Split(new[] { '=' }, 2);
            if (parts.Length == 2 && Uri.UnescapeDataString(parts[0]) == name)
            {
                return Uri.UnescapeDataString(parts[1].Replace('+', ' '));
            }
        }
        return null;
    }

    private static HttpResponseMessage Message(HttpStatusCode status, string message)
    {
        var response = new HttpResponseMessage(status);
        response.Content = CreateJsonContent(new JObject { ["message"] = message }.ToString());
        return response;
    }
}
