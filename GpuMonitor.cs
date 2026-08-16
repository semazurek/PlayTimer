using LibreHardwareMonitor.Hardware;
using System;
using System.Runtime.Remoting.Lifetime;

namespace PT2
{
    public class GpuMonitor
    {
        private Computer computer;

        public GpuMonitor()
        {
            computer = new Computer
            {
                IsGpuEnabled = true // Zezwala na odczyt sensorów z kart graficznych
            };

            try
            {
                computer.Open();
            }
            catch
            {
                // W razie braku uprawnień
            }
        }

        public bool IsAnyGpuUsageAbove(float threshold)
        {
            bool isAbove = false;

            foreach (IHardware hardware in computer.Hardware)
            {
                // Aktualizujemy sensory dla danej karty
                hardware.Update();

                foreach (ISensor sensor in hardware.Sensors)
                {
                    // Szukamy sensorów typu "Load" (Obciążenie) odpowiadających za rdzeń karty
                    if (sensor.SensorType == SensorType.Load && sensor.Value.HasValue)
                    {
                        if (sensor.Name.Contains("Core") || sensor.Name.Contains("GPU"))
                        {
                            if (sensor.Value.Value > threshold)
                            {
                                isAbove = true;
                                break;
                            }
                        }
                    }
                }
                if (isAbove) break;
            }
            return isAbove;
        }

        public void Close()
        {
            try { computer.Close(); } catch { }
        }
    }
}