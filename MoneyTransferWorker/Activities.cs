// @@@SNIPSTART money-transfer-project-template-dotnet-withdraw-activity

using System.Reflection;
using System.Text;
using Temporalio.Common;
using Temporalio.Workflows;

namespace Temporalio.MoneyTransferProject.MoneyTransferWorker;
using Temporalio.Activities;
using Temporalio.Exceptions;

public record MyMetadata(string A, string[] B);

//public class BankingActivities
//{
//    [Activity]
//    public static async Task<string> WithdrawAsync(PaymentDetails details)
//    {
//        Console.WriteLine("Account: {0}", details.Account);
//        Console.WriteLine("Metadata.A: {0}", details.GetMetadata<MyMetadata>().A);
//        Console.WriteLine("Metadata.B: [{0}]", string.Join(", ", details.GetMetadata<MyMetadata>().B));
//        return "";
//    }
//}
// @@@SNIPEND

public interface ITarget
{
    public string Name { get; set; }
    
    public string Version { get; set; }
}

public class Cluster
{
    public string ArmID { get; set; }
    
    public string ResourceGroup { get; set; }
}

public class ScaleUnit : ITarget
{
    public string Name { get; set; }
    
    public string Version { get; set; }
    
    public List<Cluster> Clusters { get; set; }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"Name: {Name}");
        sb.AppendLine($"Version: {Version}");
        sb.AppendLine("Clusters:");
        foreach (var cluster in Clusters)
        {
            sb.AppendLine($"\tArmID: {cluster.ArmID}");
        }

        return sb.ToString();
    }
}

public interface ITargetLister<TTarget>
{
    IEnumerable<TTarget> ListTargets();
}

public class ScaleUnitLister : ITargetLister<ScaleUnit>
{
    public IEnumerable<ScaleUnit> ListTargets()
    {
        return new[]
        {
            new ScaleUnit
            {
                Name = "Foo",
                Version = "0.0.0",
                Clusters =
                [
                    new Cluster
                    {
                        ArmID = "MyArmID",
                        ResourceGroup = "MyResourceGroup"
                    }
                ]
            }
        };
    }
}

public interface IOrchestration<TTarget>
{
    public Task RunAsync(TTarget target);
}

public class ScaleUnitOrchestration : IOrchestration<ScaleUnit>
{
    public async Task RunAsync(ScaleUnit target)
    {
        Console.WriteLine("-> ScaleUnitOrchestration");
        Console.WriteLine(target.ToString());
        await Workflow.ExecuteActivityAsync(
            () => ScaleUnitActivities.RunAsync(new RunInput(target)),
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(5), RetryPolicy = new RetryPolicy() }
        );
    }
}

public record RunInput(ScaleUnit ScaleUnit);

public record RunOutput();

public class ScaleUnitActivities
{
    [Activity]
    public static async Task<RunOutput> RunAsync(RunInput input)
    {
        Console.WriteLine("--- Activity:\n{0}", input.ScaleUnit);
        return new RunOutput();
    }
}

public class OtherTarget : ITarget
{
    public string Name { get; set; }
    public string Version { get; set; }
}

public class ClusterOrchestration : IOrchestration<OtherTarget>
{
    public Task RunAsync(OtherTarget target)
    {
        throw new NotImplementedException();
    }
}

public class Project
{
    public required string Id;
}

public class Project<TTarget> : Project where TTarget : ITarget
{
    public required ITargetLister<TTarget> TargetLister { get; set; }
    
    public required List<IOrchestration<TTarget>> Rollout { get; set; }
}

public static class Registry
{
       public static readonly List<Project> Projects = [];

    static Registry()
    {
        // Automatically register all projects in the assembly
        foreach (
            var project in typeof(Registry)
                .Assembly.GetTypes()
                .Where(type =>
                    {
                        //Console.WriteLine(type.FullName);
                        return type.IsClass; // && type.IsSubclassOf(typeof(Project))
                    }
                )
                .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
                .SelectMany(field =>
                {
                    
                    //if (field.FieldType == typeof(List<ProjectDefinition>))
                    //{
                    //    return (List<ProjectDefinition>)field.GetValue(null)!;
                    //}
                    if (field.FieldType == typeof(Project))
                    {
                        return new List<Project>
                        {
                            (Project)field.GetValue(null)!,
                        };
                    }

                    return [];
                })
        )
        {
            Console.WriteLine($"Registering project Id={project.Id}");
            Projects.Add(project);
        }
    } 
}

public static class MyTestProject
{
    public static readonly Project Project = new Project<ScaleUnit>
    {
        Id = "MyTestProject",
        TargetLister = new ScaleUnitLister(),
        Rollout = [new ScaleUnitOrchestration()]
    };
}