using System;

namespace BuggyNotes.Api.Auth
{
	public class JwtOptions
	{
		public string Issuer { get; set; } = "BuggyNotes";
		public string Audience { get; set; } = "BuggyNotesAudience";
		public string Secret { get; set; } = "THIS_IS_WEAK_AND_FOR_DEMO_ONLY_CHANGE_ME";
		public int ExpiryMinutes { get; set; } = 60;
	}
}
