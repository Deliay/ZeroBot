using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Agents.AI;

namespace ZeroBot.AI.Skills;

public class TrpgSkill : ISkillProvider
{
    private Dictionary<string, Dictionary<string, string>> playerStatus = []; 
    
    [Experimental("MAAI001")]
    public IEnumerable<AgentSkill> GetSkills()
    {
        yield return new AgentInlineSkill(
            name: SkillStrings.TrpgStatusSkillName,
            description: SkillStrings.TrpgStatusSkillDescription,
            instructions: SkillStrings.TrpgStatusInstructions)
            .AddScript("write_status", (string player, string attribute, string value) =>
            {
                if (!playerStatus.TryGetValue(player, out var status))
                {
                    status = playerStatus[player] = new Dictionary<string, string>();
                }
                status[attribute] = value;

                return JsonSerializer.Serialize(new { status = "ok" });
            })
            .AddScript("read_status", (string player, string attribute) =>
            {
                if (!playerStatus.TryGetValue(player, out var status))
                {
                    status = playerStatus[player] = new Dictionary<string, string>();
                }

                return status.TryGetValue(attribute, out var value) 
                    ? JsonSerializer.Serialize(new { status = "ok", value }) 
                    : JsonSerializer.Serialize(new { status = "attribute not exists" });
            })
            .AddScript("all_status", (string player) =>
            {
                if (!playerStatus.TryGetValue(player, out var status))
                {
                    status = playerStatus[player] = new Dictionary<string, string>();
                }

                return JsonSerializer.Serialize(new { status = "ok", values = status }) ;
            });
    }
}