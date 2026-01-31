using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

// VGM Player written by
// !Shawty!/DS in 2017
// NOTE: Updated by matze79 to support both old (v1.01) and newer (v1.50) VGM files
// Device clock speed must be set manually by user!

namespace VGMPlayer
{
    public class VgmFile
    {
        private SerialSender _serialSender = new SerialSender();

        private VgmFileHeader _header = new VgmFileHeader();
        private byte[] _chipData;

        private int _dataPointer = 0;
        private int _delayCounter = 0;

        private byte barMax = 32;
        private byte barSpeed = 0;
        private byte barSpeedMax = 192;

        public bool SongLooping { get; private set; }
        public int DelayCounter { get { return _delayCounter; } }
        public byte LastByteSent { get; private set; }

        public byte Tone3Volume { get; private set; }
        public byte Tone2Volume { get; private set; }
        public byte Tone1Volume { get; private set; }
        public byte NoiseVolume { get; private set; }

        public byte Tone3Bar { get; private set; }
        public byte Tone2Bar { get; private set; }
        public byte Tone1Bar { get; private set; }

        public void Load(string fileName)
        {
            if (!File.Exists(fileName))
                throw new FileNotFoundException("VGM/VGZ file not found", fileName);

            Console.WriteLine($"[INFO] Lade Datei: {Path.GetFileName(fileName)}");

            // --- NEU: VGZ-Unterstützung ---
            Stream fileStream;
            if (fileName.EndsWith(".vgz", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("[INFO] VGZ-Datei erkannt, dekomprimiere...");
                fileStream = new MemoryStream(DecompressGzFile(fileName));
            }
            else
            {
                fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            }

            using (BinaryReader reader = new BinaryReader(fileStream))
            {
                LoadHeader(reader);
                LoadChipData(reader);
            }

            SongLooping = false;

            // Info über Takt
            Console.WriteLine($"[INFO] PSG Clock from file: {_header.Sn76489Clock} Hz");
            if (_header.Sn76489Clock == 3579545)
                Console.WriteLine("⚠️ Bitte Gerät auf 3.58 MHz einstellen");
            else if (_header.Sn76489Clock == 4000000)
                Console.WriteLine("⚠️ Bitte Gerät auf 4.00 MHz einstellen");
            else
                Console.WriteLine("⚠️ Unbekannte PSG-Frequenz, Standard = 3.58 MHz");
        }

        private byte[] DecompressGzFile(string fileName)
        {
            using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            using (GZipStream gz = new GZipStream(fs, CompressionMode.Decompress))
            using (MemoryStream ms = new MemoryStream())
            {
                gz.CopyTo(ms);
                Console.WriteLine($"[INFO] Dekomprimiert: {ms.Length:N0} Bytes");
                return ms.ToArray();
            }
        }

        public void PlayNext()
        {
            if (barSpeed == 0)
            {
                Tone3Bar--; if (Tone3Bar < 1) Tone3Bar = 1; if (Tone3Bar > barMax) Tone3Bar = barMax;
                Tone2Bar--; if (Tone2Bar < 1) Tone2Bar = 1; if (Tone2Bar > barMax) Tone2Bar = barMax;
                Tone1Bar--; if (Tone1Bar < 1) Tone1Bar = 1; if (Tone1Bar > barMax) Tone1Bar = barMax;
                barSpeed = barSpeedMax;
            }
            else
            {
                barSpeed--;
            }

            if (_delayCounter > 0)
            {
                _delayCounter--;
                return;
            }

            byte currentDataByte = _chipData[_dataPointer];

            switch (currentDataByte)
            {
                case 0x61:
                    int delayVal = (_chipData[_dataPointer + 2] << 8) + _chipData[_dataPointer + 1];
                    _delayCounter = delayVal;
                    _dataPointer += 3;
                    break;

                case 0x62:
                    _delayCounter = 735; // 1/60 Sekunde
                    _dataPointer++;
                    break;

                case 0x63:
                    _delayCounter = 882; // 1/50 Sekunde
                    _dataPointer++;
                    break;

                case 0x50:
                    byte chipByte = _chipData[_dataPointer + 1];
                    _dataPointer += 2;
                    _serialSender.Send(chipByte);

                    if ((chipByte & 0x90) == 0x90) Tone3Volume = (byte)(chipByte & 0x0F);
                    if ((chipByte & 0xB0) == 0xB0) Tone2Volume = (byte)(chipByte & 0x0F);
                    if ((chipByte & 0xD0) == 0xD0) Tone1Volume = (byte)(chipByte & 0x0F);
                    if ((chipByte & 0xF0) == 0xF0) NoiseVolume = (byte)(chipByte & 0x0F);

                    if ((chipByte & 0x80) == 0x80) Tone3Bar = barMax;
                    if ((chipByte & 0xA0) == 0xA0) Tone2Bar = barMax;
                    if ((chipByte & 0xC0) == 0xC0) Tone1Bar = barMax;

                    LastByteSent = chipByte;
                    break;

                case 0x66:
                    Console.WriteLine("[INFO] End of song reached.");
                    SongLooping = false;
                    _dataPointer = 0;
                    return;

                default:
                    _dataPointer++;
                    if (_dataPointer > (_chipData.Length - 1))
                    {
                        _dataPointer = 0;
                    }
                    break;
            }
        }

        private void LoadHeader(BinaryReader reader)
        {
            byte[] magicBytes = reader.ReadBytes(4);
            string magic = Encoding.Default.GetString(magicBytes).Trim();

            if (magic != "Vgm")
            {
                throw new ApplicationException("Specified file is NOT a VGM file");
            }

            _header.VgmMagic = magic;

            _header.EofOffset = reader.ReadUInt32() + 4;
            _header.Version = reader.ReadUInt32();
            _header.Sn76489Clock = reader.ReadUInt32();
            _header.Ym2413Clock = reader.ReadUInt32();
            _header.Gd3Offset = reader.ReadUInt32();
            _header.TotalSamples = reader.ReadUInt32();
            _header.LoopOffset = reader.ReadUInt32();
            _header.LoopOffset = reader.ReadUInt32();
            _header.Rate = reader.ReadUInt32();
            _header.SnFb = reader.ReadUInt16();
            _header.Snw = reader.ReadByte();
            _header.Reserved = reader.ReadByte();
            _header.Ym2612Clock = reader.ReadUInt32();
            _header.Ym2151Clock = reader.ReadUInt32();

            if (_header.Version < 0x150)
            {
                _header.VgmDataOffset = 0x40;
            }
            else
            {
                long currentFilePointer = reader.BaseStream.Position;
                _header.VgmDataOffset = reader.ReadUInt32();
                if (_header.VgmDataOffset == 0)
                    _header.VgmDataOffset = 0x40;
                else
                    _header.VgmDataOffset = (uint)(currentFilePointer + _header.VgmDataOffset);

                var reserved = reader.ReadUInt32();
                reserved = reader.ReadUInt32();
            }
        }

        private void LoadChipData(BinaryReader reader)
        {
            List<byte> result = new List<byte>();
            reader.BaseStream.Seek(_header.VgmDataOffset, SeekOrigin.Begin);
            var dataSize = _header.EofOffset - _header.VgmDataOffset;
            result.AddRange(reader.ReadBytes((int)dataSize));
            _chipData = result.ToArray();
        }
    }
}
