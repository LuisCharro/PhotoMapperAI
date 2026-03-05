using System.Net;
using System.Reflection;
using PhotoMapperAI.Services.AI;

namespace PhotoMapperAI.Tests.Services.AI;

public class OllamaClientTests
{
    [Fact]
    public async Task EnsureSuccessOrThrowAsync_TooManyRequests_ThrowsQuotaExceeded()
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("""{"error":"weekly usage limit reached"}""")
        };

        var method = typeof(OllamaClient).GetMethod(
            "EnsureSuccessOrThrowAsync",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var task = (Task?)method!.Invoke(null, new object[] { response, "gemini-3-flash-preview:cloud", "/api/chat" });
        Assert.NotNull(task);

        var exception = await Assert.ThrowsAsync<OllamaQuotaExceededException>(async () => await task!);
        Assert.Contains("HTTP 429", exception.Message);
    }
}
