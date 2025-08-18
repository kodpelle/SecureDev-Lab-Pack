using Microsoft.EntityFrameworkCore;
using BuggyNotes.Api.Models;

namespace BuggyNotes.Api.Data
{
    public class AppDb : DbContext
    {
        public AppDb(DbContextOptions<AppDb> options) : base(options) {}
        public DbSet<Note> Notes => Set<Note>();
        public DbSet<User> Users => Set<User>();
    }
}
