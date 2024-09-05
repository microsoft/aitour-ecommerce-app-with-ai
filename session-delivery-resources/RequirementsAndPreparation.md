# Requirements and Preparation

The current session demo can be run in 2 scenarios:
- Windows 11 with Visual Studio 2022
- CodeSpaces for non-windows environments

We advise to run the demo in a Windows environment. Both scenarios have a detailed set of demos in video mode, and a step-by-step document that describes the main actions and talking points for the demos.

***Note:** We strongly advise to watch the demos and practice the session before the delivery date.*

## Preparation

The main source code is located in `.\src\`.

There are also backup projects in:

- `.\src BackUp\00 initial\`: This is the initial state for the demo. You can copy this to the main source code location `.\src\`, to start a session from scratch.

- `.\src BackUp\01 AISearch Code Complete\`: This is the complete solution with the AI Search fully implemented.

- `.\src BackUp\02 Aspire\`: This is the complete solution with the AI Search implementation, and also managed and orchestrated with Aspire.

## Requirements

We only need to set the necessary variables to access the GPT and ADA models in the start demo solution, and in the Aspire complete solution. Follow these steps:

Navigate to this folder `..\src\Products\`
Run the commands:

```bash
dotnet user-secrets init
dotnet user-secrets set "AZURE_OPENAI_MODEL" "gpt-4o"
dotnet user-secrets set "AZURE_OPENAI_ENDPOINT" "https://< end point >.openai.azure.com/"
dotnet user-secrets set "AZURE_OPENAI_APIKEY" "< Api Key >"
dotnet user-secrets set "AZURE_OPENAI_ADA02" "text-embedding-ada-002"
```

Navigate to the folder of the Products projects in the Aspire demo `..\src BackUp\02 Aspire\Products\`
Run the same commands

## Demo using Windows Environment

- Windows 11
- .NET 8
- Visual Studio 2022 and Visual Studio Code
- Keys and Endpoints for GPT-4o and ADA002

## Demo using Codespaces

- GitHub Account, fork the main demo repo
- Create a CodeSpace from the repo
- Keys and Endpoints for GPT-4o and ADA002
- Optional, use VSCode as client for the Codespace