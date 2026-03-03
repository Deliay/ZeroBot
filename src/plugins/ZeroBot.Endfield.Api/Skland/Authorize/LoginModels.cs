namespace ZeroBot.Endfield.Api.Skland.Authorize;

public readonly record struct LoginQrCodeResponse(string scanId, string scanUrl);
public readonly record struct LoginScanStatusResponse(string scanCode);

public readonly record struct ScanTokenToOAuthTokenRequest(string scanCode);
public readonly record struct ScanTokenToOAuthTokenResponse(string token, string hgId);

public readonly record struct OAuthGrantRequest(string oAuthToken, string appCode = "4ca99fa6b56cc2ba", int type = 0);
public readonly record struct OAuthGrantResponse(string uid, string code);

public readonly record struct CredentialRequest(string code, int kind = 1);
public readonly record struct CredentialResponse(string token, string cred);

public readonly record struct ZonAiRefreshTokenResponse(string token);
