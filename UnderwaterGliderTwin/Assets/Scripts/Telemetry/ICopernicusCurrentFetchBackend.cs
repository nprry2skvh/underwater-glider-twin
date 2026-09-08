using System;

namespace UnderwaterGliderTwin.Telemetry
{
    public interface ICopernicusCurrentFetchBackend
    {
        void Run(CopernicusCurrentRequest request, string requestPath, string responsePath, Action<string> onCompleted, Action<string> onFailure, Action<string> onProgress);
    }
}
