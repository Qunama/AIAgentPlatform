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