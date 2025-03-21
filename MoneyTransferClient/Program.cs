// @@@SNIPSTART money-transfer-project-template-dotnet-start-workflow
// This file is designated to run the workflow

using Temporalio.Api.Update.V1;
using Temporalio.MoneyTransferProject.MoneyTransferWorker;
using Temporalio.Client;

// Connect to the Temporal server
var client = await TemporalClient.ConnectAsync(new("localhost:7233") { Namespace = "default" });

Console.WriteLine(Registry.Projects.Count);

var workflowId = $"pay-invoice-{Guid.NewGuid()}";

try
{
    // Start the workflow
    var handle = await client.StartWorkflowAsync(
        (MoneyTransferWorkflow wf) => wf.RunAsync(TargetInput.New(
            Registry.Projects[0].Id,
            new ScaleUnit{
                Name = "MyScaleUnit",
                Version = "0.0.1",
                Clusters = new List<Cluster>{
                    new Cluster
                    {
                        ArmID = "wefwe",
                        ResourceGroup = "wsfw"
                    }
                }
        })),
        new(id: workflowId, taskQueue: "MONEY_TRANSFER_TASK_QUEUE"));

    Console.WriteLine($"Started Workflow {workflowId}");

    // Await the result of the workflow
    var result = await handle.GetResultAsync();
    Console.WriteLine($"Workflow result: {result}");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Workflow execution failed: {ex.Message}");
}
// @@@SNIPEND