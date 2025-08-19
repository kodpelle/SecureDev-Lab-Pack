namespace BuggyNotes.Api.Auth;
{
public record RegisterDto(string UserName, string Password);
public record LoginDto(string UserName, string Password);
}