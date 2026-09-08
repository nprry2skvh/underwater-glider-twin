using System;

namespace UnderwaterGliderTwin.Telemetry
{
    public static class SimulationRuntimeRegistry
    {
        public static event Action<SimulationRuntimeSession> ActiveChanged;

        public static SimulationRuntimeSession Active { get; private set; }

        public static void SetActive(SimulationRuntimeSession session)
        {
            if (ReferenceEquals(Active, session))
            {
                return;
            }

            Active = session;
            ActiveChanged?.Invoke(Active);
        }
    }
}
