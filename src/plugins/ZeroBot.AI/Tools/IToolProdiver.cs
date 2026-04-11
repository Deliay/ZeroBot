using Microsoft.Extensions.AI;

namespace ZeroBot.AI.Tools;

public interface IToolProdiver
{
    public IEnumerable<AITool> GetTools();
}