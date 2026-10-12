using DataApiBuilder.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

# region SQL Server
var sqlPassword = builder.AddParameter("SqlPassword", secret: true);

var sql = builder.AddSqlServer("sql", password: sqlPassword)
    .WithEndpoint(targetPort: 1433, port: 1433)
    .WithDataVolume("sql-data-volume")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerName("sql-container");

var databaseName = "LudamoInvoiceDb";

var database = sql.AddDatabase(databaseName)
    .WithCreationScript(DatabaseScript.CreateDatabaseScript(databaseName));
#endregion

#region Data API Builder
var dab = builder.AddDataAPIBuilder("dab-source", ["../DataApiBuilder.App/dab-config.json"])
    .WithImageTag("latest")
    .WithReference(database)
    .WithHttpEndpoint(targetPort: 5000, port: 5000);
#endregion

#region AI Agent
// var modelEndpoint = builder.AddParameter("ModelEndpoint");
// var modelApiKey = builder.AddParameter("ModelApiKey", secret: true);
// var modelDeploymentName = builder.AddParameter("ModelDeploymentName");

var dabUri = dab.Resource.UriExpression;

var ollama = builder.AddOllama("ollama")
    .WithDataVolume("ollama-data-volume")
    .WithOpenWebUI();
var ollamaEndpoint = ollama.Resource.UriExpression;
var ollamaModel = ollama.AddModel("llama3.2");

var aiAgent = builder.AddProject<Projects.DataApiBuilder_AiAgent>("ai-agent")
    //.WithEnvironment("AzureFoundry__ApiKey", modelApiKey)
    //.WithEnvironment("AzureFoundry__Endpoint", modelEndpoint)
    //.WithEnvironment("AzureFoundry__DeploymentName", modelDeploymentName)
    .WithEnvironment("DabMcp__Endpoint", $"{dabUri}/mcp")
    .WithEnvironment("Ollama__Endpoint", ollamaEndpoint)
    .WithEnvironment("Ollama__Model", ollamaModel.Resource.ModelName)
    .WaitFor(dab);
#endregion

#region UI
builder.AddProject<Projects.DataApiBuilder_UI>("ui")
    .WithReference(aiAgent)
    .WithEnvironment("Dab__Endpoint", dabUri)
    .WaitFor(aiAgent);
#endregion

builder.Build().Run();

