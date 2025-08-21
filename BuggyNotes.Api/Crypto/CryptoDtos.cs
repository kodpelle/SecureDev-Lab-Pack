namespace BuggyNotes.Api.Crypto;

public record AesEncryptRequest(string Plaintext, string? Base64Key);
public record AesEncryptResponse(string Base64Key, string Base64Nonce, string Base64Ciphertext, string Base64Tag);
public record AesDecryptRequest(string Base64Key, string Base64Nonce, string Base64Ciphertext, string Base64Tag);
public record AesDecryptResponse(string Plaintext);

public record HashRequest(string Password, int Iterations = 100_000);
public record HashResponse(string Algorithm, string HashBase64, int Iterations, long ElapsedMs);

public record VerifyRequest(string Password, string HashBase64, int Iterations = 100_000);
public record VerifyResponse(bool Verified, long ElapsedMs);