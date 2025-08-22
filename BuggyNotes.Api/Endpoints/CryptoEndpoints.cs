using BuggyNotes.Api.Crypto;

namespace BuggyNotes.Api.Endpoints;

public class CryptoEndpoints : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/crypto");

        grp.MapPost("/aes/gcm/encrypt", (AesEncryptRequest req)
            => Results.Ok(CryptoService.AesGcmEncrypt(req.Plaintext, req.Base64Key)));

        grp.MapPost("/aes/gcm/decrypt", (AesDecryptRequest req)
            => Results.Ok(CryptoService.AesGcmDecrypt(req.Base64Key, req.Base64Nonce, req.Base64Ciphertext, req.Base64Tag)));

        grp.MapPost("/aes/cbc-bug/encrypt", (AesEncryptRequest req)
            => Results.Ok(CryptoService.AesCbcInsecureEncrypt(req.Plaintext, req.Base64Key)));

        grp.MapPost("/hash/pbkdf2", (HashRequest req)
            => Results.Ok(CryptoService.HashPasswordPbkdf2(req.Password, req.Iterations)));

        grp.MapPost("/hash/verify", (VerifyRequest req)
            => Results.Ok(CryptoService.VerifyPasswordPbkdf2(req.Password, req.HashBase64, req.Iterations)));

        grp.MapPost("/hash/sha256-bug", (HashRequest req)
            => Results.Ok(CryptoService.HashPasswordSha256(req.Password)));
    }
}

