using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TelegramLauncher.ViewModels;

namespace TelegramLauncher.Services
{
    public class SorterService
    {
        public async Task<UndoJournal> CopyTdataAsync(IReadOnlyList<SorterClientVM> clients, string targetRoot, IProgress<(int done, int total, string current)> progress, CancellationToken ct)
        {
            var journal = new UndoJournal();

            var list = clients.Where(c => c.TDataExists).ToList();
            int total = list.Count;
            int done = 0;

            foreach (var c in list)
            {
                ct.ThrowIfCancellationRequested();
                var srcTdata = Path.Combine(c.SourceFolder, "tdata");
                var dstClientDir = Path.Combine(targetRoot, c.Id);
                var dstTdata = Path.Combine(dstClientDir, "tdata");

                Directory.CreateDirectory(dstClientDir);
                journal.TryAddDir(dstClientDir);

                await CopyDirectoryAsync(srcTdata, dstTdata, journal, ct);

                done++;
                progress?.Report((done, total, c.Name));
            }

            return journal;
        }

        private static async Task CopyDirectoryAsync(string src, string dst, UndoJournal journal, CancellationToken ct)
        {
            Directory.CreateDirectory(dst);
            journal.TryAddDir(dst);

            foreach (var dir in Directory.EnumerateDirectories(src, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                var rel = Path.GetRelativePath(src, dir);
                var nd = Path.Combine(dst, rel);
                Directory.CreateDirectory(nd);
                journal.TryAddDir(nd);
                await Task.Yield();
            }

            foreach (var file in Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                var rel = Path.GetRelativePath(src, file);
                var nf = Path.Combine(dst, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(nf)!);
                File.Copy(file, nf, overwrite: true);
                journal.TryAddFile(nf);
                await Task.Yield();
            }
        }

        public void Undo(UndoJournal j)
        {
            if (j == null) return;
            // удаляем файлы
            foreach (var f in j.CreatedFiles.Reverse<string>())
            {
                try { if (File.Exists(f)) File.Delete(f); } catch { }
            }
            // и папки (снизу вверх)
            foreach (var d in j.CreatedDirs.OrderByDescending(s => s.Length))
            {
                try { if (Directory.Exists(d) && !Directory.EnumerateFileSystemEntries(d).Any()) Directory.Delete(d, false); } catch { }
            }
        }
    }

    public class UndoJournal
    {
        public HashSet<string> CreatedFiles { get; } = new();
        public HashSet<string> CreatedDirs { get; } = new();
        public void TryAddFile(string path) { if (!string.IsNullOrWhiteSpace(path)) CreatedFiles.Add(path); }
        public void TryAddDir(string path) { if (!string.IsNullOrWhiteSpace(path)) CreatedDirs.Add(path); }
    }
}
