using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using CondoAgents.Agents.Orchestration;
using CondoAgents.Api;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CondoAgents.Tests.Api;

public sealed class RunEndpointTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static WebApplicationFactory<Program> Factory(string runsDir)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Development").UseSetting("Runs:Path", runsDir));

    [Fact]
    public async Task A_run_is_created_and_readable_with_its_trajectory()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            using var factory = Factory(dir.FullName);
            using var client = factory.CreateClient();

            var created = await client.PostAsJsonAsync("/buildings/jardins-plateau/runs", new StartRunRequest("What are the monthly fees?"), Ct);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var run = await created.Content.ReadFromJsonAsync<AgentRun>(RunJson.Options, Ct);

            var fetched = await client.GetAsync(new Uri($"/runs/{run!.Id}", UriKind.Relative), Ct);
            var json = await fetched.Content.ReadAsStringAsync(Ct);

            Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
            Assert.Equal("Completed", JsonDocument.Parse(json).RootElement.GetProperty("status").GetString());
            Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("events").GetArrayLength() > 0);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task A_malformed_building_id_is_a_bad_request_not_an_open_scope()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            using var factory = Factory(dir.FullName);
            using var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync("/buildings/Jardins%20Plateau/runs", new StartRunRequest("fees"), Ct);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Approvals_are_decided_over_http()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            using var factory = Factory(dir.FullName);
            using var client = factory.CreateClient();

            var created = await client.PostAsJsonAsync("/buildings/jardins-plateau/runs", new StartRunRequest("Please contact the syndicate about the roof contract."), Ct);
            var run = (await created.Content.ReadFromJsonAsync<AgentRun>(RunJson.Options, Ct))!;
            Assert.Equal(RunStatus.AwaitingApproval, run.Status);

            var decided = await client.PostAsJsonAsync($"/runs/{run.Id}/approvals/{run.Approvals[0].Id}", new DecisionRequest(true, "buyer"), Ct);
            var again = await client.PostAsJsonAsync($"/runs/{run.Id}/approvals/{run.Approvals[0].Id}", new DecisionRequest(true, "buyer"), Ct);

            Assert.Equal(HttpStatusCode.OK, decided.StatusCode);
            Assert.Equal(RunStatus.Completed, (await decided.Content.ReadFromJsonAsync<AgentRun>(RunJson.Options, Ct))!.Status);
            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}