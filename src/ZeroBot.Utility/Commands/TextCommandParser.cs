using ZeroBot.Abstraction.Bot;

namespace ZeroBot.Utility.Commands;

public class TextCommandParser(char prefix, char[] argumentSplitters)
{
    public IEnumerable<ITextCommand> Parse(string text)
    {
        return Parse(prefix, argumentSplitters, text);
    }
    
    public static IEnumerable<ITextCommand> Parse(char prefix, char[] argumentSplitters, string text)
    {
        if (text.Length == 0) yield break;
        var currentPos = 0;
        foreach (var rawIncomingCommand in ReadEntireIncomingCommands(text))
        {
            yield return ParseIncomingCommand(rawIncomingCommand);
        }
        yield break;

        ITextCommand ParseIncomingCommand(string raw)
        {
            var splitResult = raw.Split(argumentSplitters);
            
            return new TextCommand(splitResult[0], splitResult.Skip(1).ToArray());
        }
        IEnumerable<string> ReadEntireIncomingCommands(string raw)
        {
            HashSet<char> prefixSet = [prefix];
            Until(prefixSet);
            while (currentPos < text.Length)
            {
                var rawCmd = Until([prefix]);
                if (rawCmd.Length == 0) break;
                yield return rawCmd;
            }
        }
        string Until(HashSet<char> cs)
        {
            if (currentPos >= text.Length) return "";
            var start = currentPos;
            while (currentPos < text.Length)
            {
                if (cs.Contains(text[currentPos])) return text[start..currentPos++];
                
                currentPos++;
            }
            return text[start..currentPos];
        }
    }
}
