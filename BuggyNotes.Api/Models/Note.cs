
namespace BuggyNotes.Api.Models	
{
	public class Note
	{
		public int Id { get; set; }
		public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
		public string OwnerId { get; set; } = "demo-user";
    }

}
