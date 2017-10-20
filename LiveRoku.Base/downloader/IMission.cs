using System;
namespace LiveRoku.Base{
    public interface IMission {
        string Subject { get; }
        string VideoObjectName { get; }
        string XMLObjectName { get; }
        DateTime BeginTime { get; }
        DateTime EndTime { get; }
        long RecordSize { get; }
    }
}
