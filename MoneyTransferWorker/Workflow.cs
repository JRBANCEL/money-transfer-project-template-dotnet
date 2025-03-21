// @@@SNIPSTART money-transfer-project-template-dotnet-workflow

using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Temporalio.MoneyTransferProject.MoneyTransferWorker;
using Temporalio.MoneyTransferProject.BankingService.Exceptions;
using Temporalio.Workflows;
using Temporalio.Common;
using Temporalio.Exceptions;

public class TargetInput
{
    public string ProjectId { get; init; }
    
    [JsonInclude]
    private string Target { get; init; }
    
    //private PaymentDetails() {}
    
    public static TargetInput New<T>(string projectId, T target) where T : ITarget
    {
        return new TargetInput
            {
                ProjectId = projectId, 
                Target = JsonSerializer.Serialize(target)
            };
    }
    
    public T GetTarget<T>() where T : class
    {
        return JsonSerializer.Deserialize<T>(Target) ?? throw new InvalidOperationException();
    }
}

[Workflow]
public class MoneyTransferWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(TargetInput input)
    {
        Console.WriteLine("Projects: {0}", Registry.Projects.Count);
        var project = Registry.Projects.Single(p => p.Id == input.ProjectId);

        // Retry policy
        var retryPolicy = new RetryPolicy
        {
            InitialInterval = TimeSpan.FromSeconds(1),
            MaximumInterval = TimeSpan.FromSeconds(100),
            BackoffCoefficient = 2,
            MaximumAttempts = 3,
            NonRetryableErrorTypes = new[] { "InvalidAccountException", "InsufficientFundsException" }
        };

        // Call the Project.Rollout IOrchestration here??
        // Get TargetLister and Rollout fields using reflection
        var targetListerField = project.GetType().GetProperty(nameof(Project<ITarget>.TargetLister));
        var rolloutField = project.GetType().GetProperty(nameof(Project<ITarget>.Rollout));

        if (targetListerField == null || rolloutField == null)
        {
            throw new InvalidOperationException("Project is missing required properties.");
        }

        // Get the generic type of Project<TTarget>
        Type targetType = targetListerField.PropertyType.GenericTypeArguments[0];

        // Deserialize TargetInput.Target to the correct type
        var getTargetMethod = typeof(TargetInput).GetMethod(nameof(TargetInput.GetTarget))!.MakeGenericMethod(targetType);
        var target = getTargetMethod.Invoke(input, null);
        if (target == null)
        {
            throw new InvalidOperationException("Failed to deserialize target.");
        }

        // Get the list of IOrchestration<TTarget>
        var rolloutList = (IEnumerable)rolloutField.GetValue(project);
        if (rolloutList == null)
        {
            throw new InvalidOperationException("Rollout list is null.");
        }

        // Invoke Run(target) for each orchestration
        foreach (var orchestration in rolloutList)
        {
            var runMethod = orchestration.GetType().GetMethod("RunAsync");
            if (runMethod == null)
            {
                throw new InvalidOperationException("IOrchestration does not have a Run method.");
            }

            runMethod.Invoke(orchestration, new object[] { target });
        }

        return "";
    }
}