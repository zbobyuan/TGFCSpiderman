using System.Threading.Tasks;
using SQLite;
using taiyuanhitech.TGFCSpiderman.CommonLib;

namespace taiyuanhitech.TGFCSpiderman.Persistence
{
    class RunningInfoRepository : IRunningInfoRepository
    {
        const string DbName = "tgfc.sqlite";
        public Task SaveAsync(RunningInfo running)
        {
            var conn = new SQLiteAsyncConnection(DbName, true);
            return running.Id == 0 ? conn.InsertAsync(running) : conn.UpdateAsync(running);
        }

        public Task<RunningInfo> GetLastUncompletedAsync()
        {
            var conn = new SQLiteAsyncConnection(DbName, true);
            return (from running in conn.Table<RunningInfo>()
                    where running.IsCompleted == false
                    orderby running.StartTime descending
                    select running).FirstOrDefaultAsync();
        }
    }
}
