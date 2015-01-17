using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using taiyuanhitech.TGFCSpiderman.CommonLib;

namespace taiyuanhitech.TGFCSpiderman.Persistence
{
    class RunningInfoRepository : IRunningInfoRepository
    {
        public Task SaveAsync(RunningInfo running)
        {
            using (var db = new TgfcDbContext())
            {
                if (running.Id == 0)
                {
                    db.RunningInfoes.Add(running);
                }
                else
                {
                    db.RunningInfoes.Attach(running);
                    var entry = db.Entry(running);
                    entry.State = EntityState.Modified;
                    entry.Property(e => e.InitialEntryPointUrl).IsModified = false;
                    entry.Property(e => e.InitialExpirationDate).IsModified = false;
                    entry.Property(e => e.Mode).IsModified = false;
                    entry.Property(e => e.StartTime).IsModified = false;
                }
                return db.SaveChangesAsync();
            }
        }

        public Task<RunningInfo> GetLastUncompletedAsync()
        {
            using (var db = new TgfcDbContext())
            {
                db.Database.Log = s => Debug.WriteLine(s);
                return (from running in db.RunningInfoes
                             where running.IsCompleted == false
                             orderby running.StartTime descending
                             select running).FirstOrDefaultAsync();
            }
        }
    }
}
