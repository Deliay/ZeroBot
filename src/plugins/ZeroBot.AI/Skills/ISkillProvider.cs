using System.Diagnostics.CodeAnalysis;
using Microsoft.Agents.AI;

namespace ZeroBot.AI.Skills;

public interface ISkillProvider
{
    [Experimental("MAAI001")]
    IEnumerable<AgentSkill> GetSkills();
}