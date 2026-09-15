using System.Net;

namespace ErrorHandling.Test;

public class InitialConnectionTest
{
    [Fact]
    public async Task HealthTest()
    {
        await using WebApplicationFactory<Program> factory = new();
        
        using HttpClient client = factory.CreateClient();
        
        var response = await client.GetAsync("/health");
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
