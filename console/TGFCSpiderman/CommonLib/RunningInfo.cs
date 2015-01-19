using System;
using SQLite;

namespace taiyuanhitech.TGFCSpiderman.CommonLib
{
    public sealed class RunningInfo
    {
        public enum RunningMode
        {
            Single,
            Cycle,
        }
        [PrimaryKey, AutoIncrement]
        public long Id { get; set; }
        public string InitialEntryPointUrl { get; set; }
        public string CurrentEntryPointUrl { get; set; }
        public DateTime InitialExpirationDate { get; set; }
        public DateTime CurrentExpirationDate { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime LastSavedTime { get; set; }
        public RunningMode Mode { get; set; }
    }
}