using System.Collections.Generic;

namespace BeMyArms.Product
{
    public enum ReportCategory
    {
        Cheating,
        Abuse,
        Griefing,
        OffensiveName,
        Spam,
        Other
    }

    public enum ReportStatus
    {
        Open,
        Reviewing,
        Actioned,
        Dismissed
    }

    public class Report
    {
        public string Id;
        public string ReporterId;
        public string TargetId;
        public ReportCategory Category;
        public string Note;
        public double CreatedAt;
        public ReportStatus Status = ReportStatus.Open;
    }

    /// <summary>
    /// Reporting/moderation seams. The provider (and its policy) is deliberately not chosen; Product only
    /// establishes submit/list/resolve plus a client-side block list.
    /// </summary>
    public interface IModerationProvider
    {
        Report Submit(string reporterId, string targetId, ReportCategory category, string note, double now);
        IReadOnlyList<Report> ReportsByStatus(ReportStatus status);
        void Resolve(string reportId, ReportStatus status);
    }

    public class InMemoryModerationProvider : IModerationProvider
    {
        readonly List<Report> _reports = new List<Report>();
        int _next;

        public Report Submit(string reporterId, string targetId, ReportCategory category, string note, double now)
        {
            if (string.IsNullOrEmpty(targetId)) return null;
            _next++;
            var report = new Report
            {
                Id = $"report-{_next:0000}",
                ReporterId = reporterId,
                TargetId = targetId,
                Category = category,
                Note = note ?? "",
                CreatedAt = now,
                Status = ReportStatus.Open
            };
            _reports.Add(report);
            return report;
        }

        public IReadOnlyList<Report> ReportsByStatus(ReportStatus status)
        {
            var list = new List<Report>();
            for (int i = 0; i < _reports.Count; i++)
                if (_reports[i].Status == status) list.Add(_reports[i]);
            return list;
        }

        public void Resolve(string reportId, ReportStatus status)
        {
            for (int i = 0; i < _reports.Count; i++)
                if (_reports[i].Id == reportId) { _reports[i].Status = status; return; }
        }
    }
}
