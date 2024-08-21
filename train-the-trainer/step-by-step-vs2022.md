# Demo Step-by-step - VS2022

## Session VS2022

The main goal of this document is to provide a detailed guide for setting up and running the demo project using Visual Studio 2022, including implementing AI search with Semantic Kernel and improving the response message.

***Important**: We strongly advise to also watch the recorded demo videos to get better insights on how to run the demo.*

### Setup environment

- Requires Windows 11, Visual Studio 2022, .NET 8, and .NET Aspire workload installed.
- Azure OpenAI Keys and Endpoints with the following models deployed:
  - GPT4o
  - Ada002
- Check the [requirements and preparation steps](/train-the-trainer/RequirementsAndPreparation.md) to add the LLMs keys to the projects.

### Run Solution

- Run the project and show Current Search: Keyword mode
- Search "Camping", show 4 results
- Search "something for rainy days", 0 results

### Implement AI Search

- Time to add a better search experience using GPT model and an Embedding Model. Semantic Kernel will orchestrate this.

- Add Nuget packages:
  - `Microsoft.SemanticKernel`
  - `Microsoft.SemanticKernel.Plugins.Memory`
  - `System.Linq.Async`

- Explain the process of a vector search.

- Include hidden file `.\src\Products\Memory\MemoryContext.cs`.

- Explain the file:

  - `#pragma warning disabled` due to Semantic Kernel experimental features
  - `InitMemoryContext()` method creates the ChatCompletion Service and the Embedding Generation Support and initializes chat history
  - `FillProductsAsync()` creates a vector database in memory with the current list of products
  - `Search()` performs the search in the VectorMemory, and if a product is found, returns the product details from the Product DB

- Add `MemoryContext` to `app.Services`, uncomment Code, and uncomment `app.InitSemanticMemory()`.

- Enable the Endpoint for AI Search in `.\src\Products\Endpoints\ProductEndpoints.cs` by uncommenting the AI Search Endpoint.

- Update FrontEnd to use the new endpoint. In the file `.\src\Store\Services\ProductService.cs`.

- Run the project and show Current Search: Keyword mode
  - Search "Camping", show 1 result
  - Search "something for rainy days", show 1 result

### Define a System Prompt

The current logic shows a product result if found, and if not found, asks the question to the GPT model. Test this with these sentences:

- "Hi, my name is Bruno, can you help me with math operations?"
- "What is my name?"

It looks like the chat implementation opens the GPT model to answer all questions. Let us fix this with a system prompt message. In `InitMemoryContext()`, update this code:

    ```csharp
    // create chat history
    _chatHistory = new ChatHistory();
    _chatHistory.AddSystemMessage("You are a useful assistant. You always reply with a short and funny message. If you do not know an answer, you say 'I don't know that.' You only answer questions related to outdoor camping products. For any other type of questions, explain to the user that you only answer outdoor camping products questions. Do not store memory of the chat conversation.");
    ```

- Ask the questions again. The Search should not respond to questions not related to Outdoor Camping Products.

### Improve the final response message

Now let us improve the response message. In the `Search()` function, add this code before the return:

    ```csharp
    // let's improve the response message
    var prompt = @$"You are an intelligent assistant helping Contoso Inc clients with their search about outdoor product. 
    Generate a catchy and friendly message using the following information:
    - User Question: {search}
    - Found Product Name: {firstProduct.Name}
    Include the found product information in the response to the user question.";
    _chatHistory.AddUserMessage(prompt);
    var resultPrompt = await _chat.GetChatMessageContentsAsync(_chatHistory);
    responseText = resultPrompt[1](https://microsoft.sharepoint.com/teams/AI-Tour-FY25/Shared%20Documents/GS-BRK-ecommerce-app-with-ai/GS-BRK-ecommerce-app-with-ai%20-%20Demo%20Step-by-step%20-%20VS2022.docx?web=1).Content;
    ```

- Explain the process to change and improve the prompt, like adding the product description and price. Prompty is here to help!

- Add the NuGet package: `Microsoft.SemanticKernel.Prompty`. 

- Copy the supporting prompty files from `.\srcDemo\Products\` to `.\src\Products\`. 
  - ***Note**: The `.env` file should be previously completed with the Azure OpenAI information.*

- Switch to VSCode, open the Solution folder with VSCode. Open the file `aisearchresponse.prompty`.

- Run the prompt in VSCode.

- Add changes to the prompt to get a better response.

- Switch back to Visual Studio.

Change the improve message code to this one:

    ```csharp
    // let's improve the response message
    KernelArguments kernelArguments = new()
    {
      { "productid", $"{firstProduct.Id.ToString()}" },
      { "productname", $"{firstProduct.Name}" },
      { "productdescription", $"{firstProduct.Description}" },
      { "productprice", $"{firstProduct.Price}" },
      { "question", $"{search}" }
    };
    var prompty = _kernel.CreateFunctionFromPromptyFile("aisearchresponse.prompty");
    responseText = await prompty.InvokeAsync<string>(_kernel, kernelArguments);
    ```

### Aspire

- Open the solution `.\src BackUp\02 Aspire\eShopLite-Aspire.sln`.

- Run, using AppHost as StartUp project.

- Perform a search.

- Perform a general review of the traces, telemetry, and more.