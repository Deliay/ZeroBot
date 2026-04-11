using System.Diagnostics.CodeAnalysis;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;

namespace ZeroBot.AI.Skills;

public class SkillManager(IServiceProvider sp)
{
    [Experimental("MAAI001")] public IEnumerable<AgentSkill> Providers => sp
        .GetServices<ISkillProvider>()
        .SelectMany(p => p.GetSkills());
}