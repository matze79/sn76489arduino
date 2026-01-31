using System;
using System.IO;
using System.IO.Ports;

namespace VGMPlayer
{
    public class SerialSender : IDisposable
    {
        private readonly SerialPort _serialPort;

        public SerialSender()
        {
            // Standardwerte
            string portName = "COM3";
            int baudRate = 115200;

            // serial.ini einlesen
            try
            {
                if (File.Exists("serial.ini"))
                {
                    foreach (var line in File.ReadAllLines("serial.ini"))
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith(";"))
                            continue;

                        if (trimmed.StartsWith("Port=", StringComparison.OrdinalIgnoreCase))
                            portName = trimmed.Substring("Port=".Length).Trim();

                        if (trimmed.StartsWith("BaudRate=", StringComparison.OrdinalIgnoreCase))
                        {
                            if (int.TryParse(trimmed.Substring("BaudRate=".Length).Trim(), out int br))
                                baudRate = br;
                        }
                    }
                }
                else
                {
                    Console.WriteLine("serial.ini nicht gefunden – Standardwerte werden verwendet.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Einlesen der serial.ini: {ex.Message}");
            }

            // SerialPort initialisieren
            _serialPort = new SerialPort
            {
                PortName = portName,
                BaudRate = baudRate,
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One
            };

            _serialPort.Open();

            Console.WriteLine($"Serial port geöffnet: {portName} @ {baudRate} Baud");
        }

        public void Send(byte b)
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Write(new[] { b }, 0, 1);
            }
        }

        public void Dispose()
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
                Console.WriteLine("Serial port geschlossen.");
            }
        }
    }
}
