using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using taiyuanhitech.TGFCSpiderman.CommonLib;

namespace taiyuanhitech.TGFCSpiderman.Persistence
{
    public class TgfcDbContext : DbContext
    {
        public TgfcDbContext()
        {
            Database.SetInitializer<TgfcDbContext>(null);
        }

        public DbSet<Post> Posts { get; set; }
        public DbSet<Revision> Revisions { get; set; }
        public DbSet<RunningInfo> RunningInfoes { get; set; } 

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Ignore<PostWithThreadTitle>();
            modelBuilder.Entity<Post>().Property(p => p.Id).HasDatabaseGeneratedOption(DatabaseGeneratedOption.None);
        }
    }
}