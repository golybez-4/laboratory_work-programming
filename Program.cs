using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

class Program
{
    const string InputFile = "input.txt";
    const string OutputFile = "Program_output.txt";
    const int RunSizeMB = 500;          // максимальний розмір серії
    const int BufferSize = 1024 * 1024; // 1 MB буфер
    static Random rnd = new Random();

    static void Main()
    {
        // 1. Генерація вхідного файлу (~50 МБ)
        Console.WriteLine("Генерація вхідного файлу...");
        GenerateInputFile(InputFile, 50); //  50 М

        // 2. Створення початкових серій
        Console.WriteLine("Створення початкових серій...");
        var runs = CreateInitialRuns(InputFile, RunSizeMB);

        // 3. Злиття серій
        Console.WriteLine("Злиття серій...");
        MergeRuns(runs, OutputFile);

        // 4. Видалення тимчасових файлів
        foreach (var r in runs) File.Delete(r);

        Console.WriteLine($"Сортування завершено. Результат: {OutputFile}");
    }

    //  Генерація вхідних даних 
    static void GenerateInputFile(string filename, int sizeMB)
    {
        using var fs = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);
        using var writer = new StreamWriter(fs, Encoding.UTF8);

        long targetBytes = sizeMB * 1024L * 1024L;
        long written = 0;

        while (written < targetBytes)
        {
            char key = (char)('A' + rnd.Next(26));
            string randStr = RandomString(rnd.Next(1, 46));
            string phone = "+380" + rnd.Next(100_000_000, 1_000_000_000).ToString("D9");
            string line = $"{key} {randStr} {phone}";
            writer.WriteLine(line);
            written += Encoding.UTF8.GetByteCount(line) + 2; // +2 через \r\n
        }
    }

    static string RandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        char[] buffer = new char[length];
        for (int i = 0; i < length; i++)
            buffer[i] = chars[rnd.Next(chars.Length)];
        return new string(buffer);
    }

    // Створення початкових серій 
    static List<string> CreateInitialRuns(string inputFile, int runSizeMB)
    {
        List<string> runFiles = new List<string>();
        long runSizeBytes = runSizeMB * 1024L * 1024L;

        using var fs = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
        using var reader = new StreamReader(fs, Encoding.UTF8);

        while (!reader.EndOfStream)
        {
            List<string> lines = new List<string>();
            long bytesRead = 0;

            while (!reader.EndOfStream && bytesRead < runSizeBytes)
            {
                string? line = reader.ReadLine();
                if (line == null) break;
                lines.Add(line);
                bytesRead += Encoding.UTF8.GetByteCount(line) + 2;
            }

            // Сортування серії за спаданням ключа (перша літера)
            lines.Sort((a, b) =>
            {
                char ka = a.Length > 0 ? a[0] : '\0';
                char kb = b.Length > 0 ? b[0] : '\0';
                return kb.CompareTo(ka); // спадання
            });

            string runFile = $"run_{runFiles.Count}.tmp";
            File.WriteAllLines(runFile, lines, Encoding.UTF8);
            runFiles.Add(runFile);
        }
        return runFiles;
    }

    // Багатошляхове злиття 
    class RunReader
    {
        public StreamReader Reader;
        public string? CurrentLine;
        public RunReader(string path)
        {
            Reader = new StreamReader(path, Encoding.UTF8);
            CurrentLine = Reader.ReadLine();
        }
        public bool Advance()
        {
            if (Reader.EndOfStream) { CurrentLine = null; return false; }
            CurrentLine = Reader.ReadLine();
            return CurrentLine != null;
        }
    }

    static void MergeRuns(List<string> runFiles, string outputFile)
    {
        var readers = new List<RunReader>();
        foreach (var r in runFiles)
            readers.Add(new RunReader(r));

        using var writer = new StreamWriter(outputFile, false, Encoding.UTF8, BufferSize);

        // Пріоритетна черга для багатошляхового злиття
        var comparer = Comparer<(char key, int idx)>.Create((a, b) =>
        {
            int cmp = b.key.CompareTo(a.key); // спадання
            return cmp != 0 ? cmp : a.idx.CompareTo(b.idx);
        });

        var pq = new SortedSet<(char key, int idx)>(comparer);

        for (int i = 0; i < readers.Count; i++)
        {
            if (readers[i].CurrentLine != null)
                pq.Add((readers[i].CurrentLine[0], i));
        }

        while (pq.Count > 0)
        {
            var top = pq.Min;
            pq.Remove(top);
            int idx = top.idx;
            writer.WriteLine(readers[idx].CurrentLine);

            if (readers[idx].Advance())
                pq.Add((readers[idx].CurrentLine![0], idx));
        }

        foreach (var r in readers) r.Reader.Dispose();
    }
}

