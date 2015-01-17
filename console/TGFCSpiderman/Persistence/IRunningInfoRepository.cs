using System.Threading.Tasks;
using taiyuanhitech.TGFCSpiderman.CommonLib;

namespace taiyuanhitech.TGFCSpiderman.Persistence
{
    public interface IRunningInfoRepository
    {
        Task SaveAsync(RunningInfo running);
        Task<RunningInfo> GetLastUncompletedAsync();
    }
}
