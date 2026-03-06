using System.Collections.Generic;
namespace ZeroBot.Endfield.Api.Skland.Endfield.Models;
public readonly record struct EndfieldSettlement(
    string id,
    int level,
    string remainMoney,
    string officerCharIds,
    string name
);

public readonly record struct EndfieldCollection(
    string levelId,
    int puzzleCount,
    int trchestCount,
    int pieceCount,
    int blackboxCount
);

public readonly record struct EndfieldDomainData(
    string domainId,
    int level,
    List<EndfieldSettlement> settlements,
    string moneyMgr,
    List<EndfieldCollection> collections,
    object factory, // Can be null, so use object
    string name
);
