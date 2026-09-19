using System.Collections.Generic;

namespace BeMyArms.M5
{
    public enum M5ReportCategory
    {
        Cheating,
        Abuse,
        Griefing,
        OffensiveName,
        Spam,
        Other
    }

    public enum M5ReportStatus
    {
        Open,
        Reviewing,
        Actioned,
        Dismissed
    }

    public class M5Report
    {
        public string Id;
        public string ReporterId;
        public string TargetId;
        public M5ReportCategory Category;
        public string Note;
        public double CreatedAt;
        public M5ReportStatus Status = M5ReportStatus.Open;
    }

    /// <summary>
    /// Reporting/moderation seams. The provider (and its policy) is deliberately not chosen; M5 only
    /// establishes submit/list/resolve plus a client-side block list.
    /// </summary>
    public interface IM5ModerationProvider
    {
        M5Report Submit(string reporterId, string targetId, M5ReportCategory category, string note, double now);
        IReadOnlyList<M5Report> ReportsByStatus(M5ReportStatus status);
        void Resolve(string reportId, M5ReportStatus status);
    }

    public class M5InMemoryModerationProvider : IM5ModerationProvider
    {
        readonly List<M5Report> _reports = new List<M5Report>();
        int _next;

        public M5Report Submit(string reporterId, string targetId, M5ReportCategory category, string note, double now)
        {
            if (string.IsNullOrEmpty(targetId)) return null;
            _next++;
            var report = new M5Report
            {
                Id = $"report-{_next:0000}",
                ReporterId = reporterId,
                TargetId = targetId,
                Category = category,
                Note = note ?? "",
                CreatedAt = now,
                Status = M5ReportStatus.Open
            };
            _reports.Add(report);
            return report;
        }

        public IReadOnlyList<M5Report> ReportsByStatus(M5ReportStatus status)
        {
            var list = new List<M5Report>();
            for (int i = 0; i < _reports.Count; i++)
                if (_reports[i].Status == status) list.Add(_reports[i]);
            return list;
        }

        public void Resolve(string reportId, M5ReportStatus status)
        {
            for (int i = 0; i < _reports.Count; i++)
                if (_reports[i].Id == reportId) { _reports[i].Status = status; return; }
        }
    }
}
