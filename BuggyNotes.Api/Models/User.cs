namespace BuggyNotes.Api.Models
{
    public class User
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        // *Deliberately weak for Day 1 (fix Day 2)
        public string Password { get; set; } = string.Empty;
    }
}
