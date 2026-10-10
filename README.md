# DataApiBuilder AI Agent PoC

This repository contains a minimal local proof of concept for an AI agent that accepts natural-language requests and routes them through a DAB MCP-backed Invoice query flow.

## Project layout

- `.AppHost` starts SQL Server and Data API Builder.
- `.App` contains the DAB configuration and SQL seed script.
- `.AiAgent` is a minimal ASP.NET Core API that exposes a POST endpoint with a `text` property.
- `.AiAgent.Tests` validates the invoice-only query guard.

## Run locally

1. Start the local app host:
   ```bash
   dotnet run --project DataApiBuilder.AppHost
   ```

2. Run the SQL seed against the local SQL instance if needed:
   ```sql
   -- use the contents of DataApiBuilder.App/seed-invoices.sql
   ```

3. Start the AI API:
   ```bash
   dotnet run --project DataApiBuilder.AiAgent
   ```

4. Send a request:
   ```bash
   curl -X POST http://localhost:5000/api/query \
     -H "Content-Type: application/json" \
     -d '{"text":"give me all invoices with status paid and amount greater than 1000"}'
   ```

## Environment variables for Azure AI Foundry

Set the following before starting the AI API:

```bash
export AzureFoundry__Endpoint="https://<your-resource>.services.ai.azure.com/api/projects/<project-name>"
export AzureFoundry__ApiKey="<your-api-key>"
export AzureFoundry__DeploymentName="<your-model-deployment>"
export DabMcp__Endpoint="<dab-mcp-endpoint>"
```

## Guardrail

The AI layer is intentionally limited to invoice-related queries that can be resolved by the DAB MCP server. Unsupported requests are rejected by the planner before the model is given a chance to answer.

## Structured report responses

Every successful `POST /api/query` response uses the following JSON envelope:

- `contractVersion`: current response contract version (`1.0`).
- `answer`: a short plain-text summary.
- `report`: either `null` or a typed `table` or `comparison` report.
- `tools`: MCP tools used by the agent.
- `usage`: input, output, and total token counts when the model supplies them.

The report executor discovers scalar Invoice fields from the DAB GraphQL schema and validates model-selected columns before it retrieves rows. New scalar columns are therefore eligible for generic table and comparison reports without an application code change. Identifier and relationship fields are excluded by default.

For predictable token use and UI responses, source reads are capped at 200 records and reports show up to 25 rows. Comparisons support a numeric metric grouped by an eligible scalar field. Table and comparison reports support up to three validated filters using `eq`, `neq`, `gt`, `gte`, `lt`, and `lte`; range operators are limited to numeric fields.
