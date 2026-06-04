// ProjectAIAgent.Core/Agents/DocumentationAgent.cs
namespace ProjectAIAgent.Core.Agents;
using Microsoft.Extensions.Logging;

public class DocumentationAgent : BaseAgent
{
    public override string Name => "Documentation";
    public override string Role => "Documentation";
    protected override string PromptResourceName => "documentation.txt";
    
    public DocumentationAgent(
        ILogger<DocumentationAgent> logger,
        IServiceProvider serviceProvider) 
        : base(logger, serviceProvider)
    {
    }
}

// ProjectAIAgent.Core/Agents/ContextAgent.cs

public class ContextAgent : BaseAgent
{
    public override string Name => "Context";
    public override string Role => "Context";
    protected override string PromptResourceName => "context.txt";
    
    public ContextAgent(
        ILogger<ContextAgent> logger,
        IServiceProvider serviceProvider) 
        : base(logger, serviceProvider)
    {
    }
}