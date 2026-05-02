using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Purview;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oasf.Sample.Agent.Middleware;
using Oasf.Sample.Agent.Services;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<OasfGovernanceService>();
builder.Services.AddSingleton<OasfGovernanceMiddleware>();

var host = builder.Build();
var logger = host.Services.GetRequiredService<ILogger<Program>>();
var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
var governance = host.Services.GetRequiredService<OasfGovernanceService>();
var oasfMiddleware = host.Services.GetRequiredService<OasfGovernanceMiddleware>();

try
{
    await governance.InitializeAsync();
}
catch (Exception ex)
{
    logger.LogCritical(ex, "OASF governance initialization failed.");
    return 1;
}

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";
var endpoint = Environment.GetEnvironmentVariable("OPENAI_ENDPOINT");
var tenantId = Environment.GetEnvironmentVariable("PURVIEW_TENANT_ID");

if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.WriteLine("Set OPENAI_API_KEY to run the interactive sample agent.");
    return 1;
}

var credential = new ApiKeyCredential(apiKey);
var openAiClient = string.IsNullOrWhiteSpace(endpoint)
    ? new OpenAIClient(credential)
    : new OpenAIClient(credential, new OpenAIClientOptions { Endpoint = new Uri(endpoint) });

var chatClient = openAiClient.GetChatClient(model);
var baseAgent = chatClient.AsAIAgent(
    $"{governance.Record.Name}:{governance.Record.Version}",
    "OnboardingAssistant",
    $"""
        You are an HR Onboarding Assistant for {governance.Record.Name}.
        You help employees with onboarding tasks and policy questions.

        GOVERNANCE CONTEXT:
        - Sensitivity tier: {governance.Policy.SensitivityTier}
        - Dependency ceiling: {governance.Policy.DependencySensitivityCeiling}
        - Compliance frameworks: {string.Join(", ", governance.Policy.ComplianceFrameworks)}
        """,
    null,
    null,
    loggerFactory,
    host.Services);

var purviewSettings = new PurviewSettings(governance.Record.Name)
{
    TenantId = tenantId,
    AppVersion = governance.Record.Version,
    IgnoreExceptions = true,
    BlockedPromptMessage = "Prompt blocked by Purview policy.",
    BlockedResponseMessage = "Response blocked by Purview policy."
};

var agent = baseAgent
    .AsBuilder()
    .WithPurview(new DefaultAzureCredential(), purviewSettings, logger)
    .Build(host.Services);

logger.LogInformation(
    "Agent runtime ready for {Name} v{Version} (tier={Tier}, ceiling={Ceiling}).",
    governance.Record.Name,
    governance.Record.Version,
    governance.Policy.SensitivityTier,
    governance.Policy.DependencySensitivityCeiling);

var consumerId = Environment.GetEnvironmentVariable("CONSUMER_ID") ?? "anonymous";

Console.WriteLine();
Console.WriteLine("OASF-Governed Onboarding Agent");
Console.WriteLine($"Asset: {governance.Record.Name} v{governance.Record.Version}");
Console.WriteLine($"Model: {model}");
Console.WriteLine($"Consumer: {consumerId}");
Console.WriteLine("Type a message (or 'quit' to exit):");
Console.WriteLine();

while (true)
{
    Console.Write("You> ");
    var input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input))
        continue;

    if (input.Equals("quit", StringComparison.OrdinalIgnoreCase))
        break;

    if (!oasfMiddleware.IsInvocationAllowed(consumerId))
    {
        Console.WriteLine($"\nAgent> Access denied for consumer '{consumerId}'.\n");
        continue;
    }

    try
    {
        var response = await agent.RunAsync(input);
        Console.WriteLine($"\nAgent> {response.Text}\n");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Agent execution failed.");
        Console.WriteLine($"\n[Error: {ex.Message}]\n");
    }
}

logger.LogInformation("Agent shutting down.");
return 0;
