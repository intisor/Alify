using System.Net;
using System.Text.Json;
using Alify.Core.Features.Analysis.Engine.Gemini;
using Xunit;

namespace Alify.Core.Tests;

public class OpenAiCompatibleModelClientTests
{
    private class TestHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? CapturedRequest { get; private set; }
        public string? CapturedRequestBody { get; private set; }
        public HttpResponseMessage ResponseToReturn { get; set; } = new(HttpStatusCode.OK);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedRequest = request;
            if (request.Content != null)
            {
                CapturedRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return ResponseToReturn;
        }
    }

    [Fact]
    public async Task GenerateContentAsync_SendsValidOpenAiFormat_AndReturnsContent()
    {
        var handler = new TestHttpMessageHandler
        {
            ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{
                    ""choices"": [
                        {
                            ""message"": {
                                ""role"": ""assistant"",
                                ""content"": ""{\""status\"": \""completed\""}""
                            }
                        }
                    ]
                }")
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new OpenAiCompatibleModelClient(
            httpClient,
            apiKey: "sk-or-v1-testkey123",
            modelName: "meta-llama/llama-3.3-70b-instruct:free",
            endpointUrl: OpenAiCompatibleModelClient.DefaultOpenRouterEndpoint
        );

        var result = await client.GenerateContentAsync("Analyze this lyric");

        Assert.Equal("{\"status\": \"completed\"}", result);
        Assert.NotNull(handler.CapturedRequest);
        Assert.Equal("Bearer", handler.CapturedRequest!.Headers.Authorization?.Scheme);
        Assert.Equal("sk-or-v1-testkey123", handler.CapturedRequest.Headers.Authorization?.Parameter);
        Assert.True(handler.CapturedRequest.Headers.Contains("HTTP-Referer"));
        Assert.True(handler.CapturedRequest.Headers.Contains("X-Title"));

        // Verify JSON payload
        Assert.NotNull(handler.CapturedRequestBody);
        using var doc = JsonDocument.Parse(handler.CapturedRequestBody);
        Assert.Equal("meta-llama/llama-3.3-70b-instruct:free", doc.RootElement.GetProperty("model").GetString());
        var messages = doc.RootElement.GetProperty("messages");
        Assert.Equal(1, messages.GetArrayLength());
        Assert.Equal("user", messages[0].GetProperty("role").GetString());
        Assert.Equal("Analyze this lyric", messages[0].GetProperty("content").GetString());
    }

    [Fact]
    public async Task GenerateContentAsync_WithGroqEndpoint_DoesNotAddOpenRouterHeaders()
    {
        var handler = new TestHttpMessageHandler
        {
            ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{
                    ""choices"": [
                        {
                            ""message"": {
                                ""content"": ""Groq result""
                            }
                        }
                    ]
                }")
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new OpenAiCompatibleModelClient(
            httpClient,
            apiKey: "gsk_groqkey123",
            modelName: "llama-3.3-70b-versatile",
            endpointUrl: OpenAiCompatibleModelClient.DefaultGroqEndpoint
        );

        var result = await client.GenerateContentAsync("Test prompt");

        Assert.Equal("Groq result", result);
        Assert.NotNull(handler.CapturedRequest);
        Assert.Equal("Bearer", handler.CapturedRequest!.Headers.Authorization?.Scheme);
        Assert.Equal("gsk_groqkey123", handler.CapturedRequest.Headers.Authorization?.Parameter);
        Assert.False(handler.CapturedRequest.Headers.Contains("HTTP-Referer"));
    }

    [Fact]
    public async Task GenerateContentAsync_WithLocalOllama_WorksWithoutApiKey()
    {
        var handler = new TestHttpMessageHandler
        {
            ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{
                    ""choices"": [
                        {
                            ""message"": {
                                ""content"": ""Ollama local answer""
                            }
                        }
                    ]
                }")
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new OpenAiCompatibleModelClient(
            httpClient,
            apiKey: null,
            modelName: "llama3",
            endpointUrl: OpenAiCompatibleModelClient.DefaultOllamaEndpoint
        );

        var result = await client.GenerateContentAsync("Offline prompt");

        Assert.Equal("Ollama local answer", result);
        Assert.NotNull(handler.CapturedRequest);
        Assert.Null(handler.CapturedRequest!.Headers.Authorization);
    }
}
