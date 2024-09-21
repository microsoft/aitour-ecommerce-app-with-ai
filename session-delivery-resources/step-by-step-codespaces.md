# Demo Step-by-step - CodeSpaces

## Session CodeSpaces

The main goal of this document is to provide a detailed guide for setting up and running the demo project using GitHub Codespaces, including implementing AI search with Semantic Kernel and improving the response message.

***Important**: We strongly advise to also watch the recorded demo videos to get better insights on how to run the demo.*

### Setup CodeSpace

1. Clone the repository.
2. Create a new CodeSpace in the repository.
3. You should have Azure OpenAI Keys and Endpoints with the following models deployed:
   - GPT4o
   - Ada002
4. Check the [requirements and preparation steps](/train-the-trainer/RequirementsAndPreparation.md) to add the LLMs keys to the projects.

### Run Solution in CodeSpaces

1. Start by running both projects: Products and Store.
2. Run first Products.
   - Select C# and Default Configuration.
   - From VSCode Web, change the port from Private to Public.
   - Enable popup if needed.
   - Test the URL with the suffix [/api/product], e.g., `https://fluffy-waffle-6444gxrpqq2x5v7-5228.app.github.dev/api/product`.
3. With Products project running, run also the project Store.
   - Select C# and Default Configuration.
   - From VSCode Web, change the port from Private to Public.
   - The Product page should be ready to be used.
   - Select Products and see the list of products.
   - Test the Current Search: Keyword mode.
     - Search "Camping", 4 results.
     - Search "something for rainy days", 0 results.
4. Stop both projects.

### Implement AI Search

1. Add a better search experience using GPT model and an Embedding Model.
2. Use Semantic Kernel to orchestrate this.
3. Add NuGet packages:
   - `Microsoft.SemanticKernel`
   - `Microsoft.SemanticKernel.Plugins.Memory`
   - `System.Linq.Async`
   - Use the commands:

     ```bash
     dotnet add package Microsoft.SemanticKernel --version 1.16.0
     dotnet add package Microsoft.SemanticKernel.Plugins.Memory --version 1.16.0-alpha
     dotnet add package System.Linq.Async --version 6.0.1
     ```

4. Explain the process of a vector search.
5. Include hidden file `.\src\Products\Memory\MemoryContext.cs`.
6. Edit the `.\src\Products\Products.csproj` and delete the lines:

   ```xml
   <ItemGroup>
     <Compile Remove="Memory\**" />
     <Content Remove="Memory\**" />
     <EmbeddedResource Remove="Memory\**" />
     <None Remove="Memory\**" />
   </ItemGroup>
   ```

7. Build the Products project.
8. Add User Secrets with the Azure OpenAI Keys:

   ```bash
   dotnet user-secrets init
   dotnet user-secrets set "AZURE_OPENAI_MODEL" "gpt-4o"
   dotnet user-secrets set "AZURE_OPENAI_ENDPOINT" "<>.openai.azure.com/"
   dotnet user-secrets set "AZURE_OPENAI_APIKEY" "Key"
   dotnet user-secrets set "AZURE_OPENAI_ADA02" "text-embedding-ada-002"
   ```

9. Open the file `MemoryContext.cs` and explain the file:

   - `#pragma warning disabled` due to Semantic Kernel experimental features.
   - `InitMemoryContext()` method creates the ChatCompletion Service and the Embedding Generation Support and initializes chat history.
   - `FillProductsAsync()` creates a vector database in memory with the current list of products.
   - `Search()` performs the search in the VectorMemory, and if a product is found, returns the product details from the Product DB.

10. Add `MemoryContext` to `app.Services`, uncomment Code, and uncomment `app.InitSemanticMemory()`.
11. Enable the Endpoint for AI Search in `.\src\Products\Endpoints\ProductEndpoints.cs` by uncommenting the AI Search Endpoint.
12. Update FrontEnd to use the new endpoint `.\src\Store\Services\ProductService.cs`.
13. Run both projects again.
14. Test Search:
    - Search "Camping", 1 result.
    - Search "something for rainy days", 1 result.

### Define a System Prompt

1. The current logic shows a product result if found, and if not found, asks the question to the GPT model. Test this with these sentences:
   - "Hi, my name is Bruno, can you help me with math operations?"
   - "What is my name?"

2. Update the `InitMemoryContext()` code:

   ```csharp
   // create chat history
   _chatHistory = new ChatHistory();
   _chatHistory.AddSystemMessage("You are a useful assistant. You always reply with a short and funny message. If you don't know an answer, you say 'I don't know that.' You only answer questions related to outdoor camping products. For any other type of questions, explain to the user that you only answer outdoor camping products questions. Do not store memory of the chat conversation.");
   ```

3. Test these sentences again and see the difference in the response:
   - "Hi, my name is Bruno, can you help me with math operations?"
   - "What is my name?"

### Improve the Final Response Message

1. In the `Search()` function, add this code before the return:

   ```csharp
   // let's improve the response message
   var prompt = @$"You are an intelligent assistant helping Contoso Inc clients with their search about outdoor product. 
   Generate a catchy and friendly message using the following information:
   - User Question: {search}
   - Found Product Name: {firstProduct.Name}
   Include the found product information in the response to the user question.";
   _chatHistory.AddUserMessage(prompt);
   var resultPrompt = await _chat.GetChatMessageContentsAsync(_chatHistory);
   responseText = resultPrompt[1](https://microsoft.sharepoint.com/teams/AI-Tour-FY25/Shared%20Documents/GS-BRK-ecommerce-app-with-ai/GS-BRK-ecommerce-app-with-ai%20-%20Demo%20Step-by-step%20-%20CodeSpaces.docx?web=1).Content;
   ```

2. Explain the process to change and improve the prompt, like adding the product description and price. Prompty is here to help!
3. Add the NuGet package: `Microsoft.SemanticKernel.Prompty`
   - Use the command:

     ```bash
     dotnet add package Microsoft.SemanticKernel.Prompty --version 1.16.0-alpha
     ```

4. Copy the supporting prompty files from `.\srcDemo\Products\` to `.\src\Products\`.
   - ***Note**: The `.env` file should be previously completed with the Azure OpenAI information.*

5. Install the Prompty extension (if it is not installed).
6. Open the file `aisearchresponse.prompty`.
7. Run the prompt.
8. Add changes to the prompt to get a better response.
9. Change the improve message code to this one:

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

1. Open the solution `.\src BackUp\02 Aspire\eShopLite-Aspire.sln`.
2. Run, using AppHost as StartUp project.
3. Perform a search.
4. Perform a general review of the traces, telemetry, and more.