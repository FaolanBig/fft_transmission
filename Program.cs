/*
    <one line to give the program's name and a brief idea of what it does.>
    Copyright (C) 2025  Carl Öttinger

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using MathNet.Numerics.IntegralTransforms;
using BigLog;
using MathNet.Numerics;


namespace fft_transmission
{
    class Program
    {
        const int SampleRate = 44100;
        const double ToneDuration = 0.1;   // 100 ms
        const double SilenceDuration = 0.05; // 50 ms
        const int BitsPerBlock = 32;       // 4 Bytes
        const double BaseFreq = 500.0;
        const double FreqStep = 75.0; // old 20.0, 50.0 works best

        static Logger logger = new Logger()
        {
            LogToFile = true,
            TimeStampPrefix = "[fftt] <",
            PrePrefix = "> ==> ",
            FileName = "fftt.log",
            LogToTerminal = true,
            ColorLevelPrefix = true,
            /*PrefixInf = "[INFO] ",
            PrefixSuc = "[SUCCESS] ",
            PrefixWar = "[WARNING] ",
            PrefixErr = "[ERROR] ",
            PrefixCustom = "[CUSTOM] "*/
            PrefixInf = "[INF] ",
            PrefixSuc = "[SUC] ",
            PrefixWar = "[WAR] ",
            PrefixErr = "[ERR] ",
            PrefixCustom = "[CTM] "
        };

        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                /*Console.WriteLine("Usage:\n" +
                    "fftt encode <input-file>\n" +
                    "fftt decode <audio-file>");*/
                logger.Err("invalid arguments");
                logger.Inf("usage:");
                logger.Inf("    fftt encode <input-file>");
                logger.Inf("    fftt decode <audio-file>");
                return;
            }

            if (args[0] == "encode") 
            {
                if (!File.Exists(args[1])) { logger.Err("input file not found at " + Path.GetFullPath(args[1])); }
                else
                {
                    logger.Inf("starting engine");
                    Encode(args[1]);
                }
            }
            else if (args[0] == "decode") 
            {
                if (!File.Exists(args[1])) { logger.Err("input file not found at " + Path.GetFullPath(args[1])); }
                else
                {
                    logger.Inf("starting engine");
                    Decode(args[1]);
                }
            }
            else
            {
                logger.Err("invalid arguments");
                logger.Inf("usage:");
                logger.Inf("    fftt encode <input-file>");
                logger.Inf("    fftt decode <audio-file>");
                return;
            }
            logger.Inf("bye from fftt :)");
            logger.Inf("closing");
        }

        static void Encode(string inputPath)
        {
            logger.Inf("encoding file");
            logger.Inf("opening and reading input file at " + inputPath);
            byte[] data = File.ReadAllBytes(inputPath);
            logger.Suc("read " + new FileInfo(inputPath).Length + " bytes");
            var samples = new List<short>();

            logger.Inf("processing");
            for (int i = 0; i < data.Length; i += 4)
            {
                byte[] block = data.Skip(i).Take(4).ToArray();
                if (block.Length < 4)
                    block = block.Concat(new byte[4 - block.Length]).ToArray();

                samples.AddRange(EncodeBlock(block));
            }
            logger.Suc("building complete, writing output file");
            WriteWav(samples.ToArray(), "encoded.wav");
            logger.Suc("wrote " + samples.Count * sizeof(short) + " bytes");
            //Console.WriteLine("Encoded to encoded.wav");
            logger.Suc($"encoded to encoded.wav at {Path.GetFullPath("encoded.wav")}");
        }

        static IEnumerable<short> EncodeBlock(byte[] block)
        {
            //logger.Inf("encoding block: " + BitConverter.ToString(block)); // might comment out for less resource usage
            int samplesCount = (int)(SampleRate * ToneDuration);
            double[] buffer = new double[samplesCount];

            // 32 bits
            for (int bitIndex = 0; bitIndex < 32; bitIndex++)
            {
                int byteIndex = bitIndex / 8;
                int bitPos = bitIndex % 8;
                bool isOne = ((block[byteIndex] >> bitPos) & 1) == 1;
                if (!isOne) continue;

                double freq = BaseFreq + bitIndex * FreqStep;
                for (int s = 0; s < samplesCount; s++)
                    buffer[s] += Math.Sin(2 * Math.PI * freq * s / SampleRate);
            }

            // Normalisieren
            double max = buffer.Max(Math.Abs);
            if (max > 0)
                for (int s = 0; s < buffer.Length; s++)
                    buffer[s] = buffer[s] / max * short.MaxValue;

            // Umwandeln in short[]
            List<short> result = buffer.Select(v => (short)v).ToList();

            // 50ms Stille anhängen
            int silenceSamples = (int)(SampleRate * SilenceDuration);
            result.AddRange(new short[silenceSamples]);

            return result;
        }

        static void Decode(string wavPath)
        {
            logger.Inf("decoding file");
            logger.Inf("opening and reading wav file at " + Path.GetFullPath(wavPath));
            var samples = ReadWav(wavPath, out int channels, out int sampleRate);
            int blockSamples = (int)(SampleRate * (ToneDuration + SilenceDuration));
            int toneSamples = (int)(SampleRate * ToneDuration);
            logger.Suc("loaded " + samples.Length * sizeof(short) + " bytes");
            logger.Suc("found: " + samples.Length + " samples, " + channels + " channels, " + sampleRate + " Hz");
            List<byte> output = new();

            for (int pos = 0; pos + blockSamples <= samples.Length; pos += blockSamples)
            {
                double[] toneBlock = samples.Skip(pos).Take(toneSamples).Select(s => s / 32768.0).ToArray();
                byte[] decoded = DecodeBlock(toneBlock);
                output.AddRange(decoded);
            }
            logger.Suc("decoded " + output.Count + " bytes");
            logger.Inf("writing output file");
            File.WriteAllBytes("decoded.bin", output.ToArray());
            //logger.Suc($"wrote {output.Count} bytes ({output.Count / 4.0} blocks à 4 bytes)");
            //logger.Suc($"wrote {output.Count} bytes ({output.Count / decodedBlockSize} blocks)");
            logger.Suc($"wrote {output.Count} bytes");
            //Console.WriteLine("Decoded to decoded.bin");
            logger.Suc($"decoded to decoded.bin at {Path.GetFullPath("decoded.bin")}");
        }

        static byte[] DecodeBlock(double[] samples)
        {
            // FFT vorbereiten (auf nächste 2er-Potenz auffüllen)
            int fftSize = 1;
            while (fftSize < samples.Length) fftSize *= 2;

            Complex[] fft = samples.Select(v => new Complex(v, 0)).ToArray();
            Array.Resize(ref fft, fftSize);

            Fourier.Forward(fft, FourierOptions.Matlab);

            double[] magnitudes = fft.Take(fftSize / 2).Select(c => c.Magnitude).ToArray();
            byte[] bytes = new byte[4];

            for (int bitIndex = 0; bitIndex < 32; bitIndex++)
            {
                double freq = BaseFreq + bitIndex * FreqStep;
                int idx = (int)(freq / SampleRate * fftSize);
                double amp = magnitudes[idx];
                bool isOne = amp > 50; // Schwellenwert
                if (isOne)
                    bytes[bitIndex / 8] |= (byte)(1 << (bitIndex % 8));
            }

            return bytes;
        }

        static void WriteWav(short[] samples, string path)
        {
            using var fs = new FileStream(path, FileMode.Create);
            using var bw = new BinaryWriter(fs);

            int byteRate = SampleRate * 2;
            int subchunk2Size = samples.Length * 2;

            // Header
            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + subchunk2Size);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            bw.Write(16);
            bw.Write((short)1); // PCM
            bw.Write((short)1); // Mono
            bw.Write(SampleRate);
            bw.Write(byteRate);
            bw.Write((short)2); // BlockAlign
            bw.Write((short)16); // Bits per sample
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(subchunk2Size);

            foreach (short s in samples)
                bw.Write(s);
        }

        static double[] ReadWav(string path, out int channels, out int sampleRate)
        {
            using var br = new BinaryReader(File.OpenRead(path));

            // Header lesen
            string riff = new string(br.ReadChars(4)); // "RIFF"
            int fileSize = br.ReadInt32();
            string wave = new string(br.ReadChars(4)); // "WAVE"

            channels = 1;
            sampleRate = 44100;
            short bitsPerSample = 16;
            byte[] dataBytes = Array.Empty<byte>();

            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                string chunkId = new string(br.ReadChars(4));
                int chunkSize = br.ReadInt32();

                if (chunkId == "fmt ")
                {
                    short audioFormat = br.ReadInt16();
                    channels = br.ReadInt16();
                    sampleRate = br.ReadInt32();
                    int byteRate = br.ReadInt32();
                    short blockAlign = br.ReadInt16();
                    bitsPerSample = br.ReadInt16();
                    br.BaseStream.Seek(chunkSize - 16, SeekOrigin.Current);
                }
                else if (chunkId == "data")
                {
                    dataBytes = br.ReadBytes(chunkSize);
                    break; // fertig
                }
                else
                {
                    // Unbekannten Chunk überspringen
                    br.BaseStream.Seek(chunkSize, SeekOrigin.Current);
                }
            }

            if (dataBytes.Length == 0)
                throw new Exception("Kein 'data'-Chunk in WAV-Datei gefunden.");

            int bytesPerSample = bitsPerSample / 8;
            int sampleCount = dataBytes.Length / bytesPerSample;
            double[] samples = new double[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                short sample = BitConverter.ToInt16(dataBytes, i * 2);
                samples[i] = sample;
            }

            return samples;
        }

    }

}
