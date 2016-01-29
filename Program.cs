using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.IO;
using SettingsModule;
using System.Threading.Tasks;

namespace RandomMusic
{
    class Program
    {
        static void Main(string[] args)
        {
            //I want to refactor that 
            SettingsStorage settings = new SettingsStorage(new XmlSettingsProvider(true));

            var musicDirectory = (settings.GetSetting("MusicFromDirectory").GetValue() ?? @"F:\Музыка").ToString();
            var musicCopyDirectory = (settings.GetSetting("MusicToDirectory").GetValue() ?? @"E:\Music").ToString();

            Console.WriteLine("Путь до папки с музыкой на компьютере:({0})", musicDirectory);
            var currentInput = Console.ReadLine();
            if (!String.IsNullOrWhiteSpace(currentInput))
            {
                musicDirectory = currentInput;
            }

            var currentDrive = DriveInfo.GetDrives().FirstOrDefault(e => e.Name == musicDirectory.Substring(0, 3));
            if (currentDrive == null)
            {
                Console.WriteLine("Не существует диска");
            }
            else
            {
                settings.SetSetting("MusicFromDirectory", musicDirectory);

                if (!Directory.Exists(musicDirectory))
                {
                    Console.WriteLine("Папка отсутствовала. Создаем.");
                    Directory.CreateDirectory(musicDirectory);
                }

                Console.WriteLine("Путь до папки с музыкой на мобиле:({0})", musicCopyDirectory);
                currentInput = Console.ReadLine();
                if (!String.IsNullOrWhiteSpace(currentInput))
                {
                    musicCopyDirectory = currentInput;
                }

                currentDrive = DriveInfo.GetDrives().FirstOrDefault(e => e.Name == musicCopyDirectory.Substring(0, 3));
                if (currentDrive == null)
                {
                    Console.WriteLine("Не существует диска");
                }
                else
                {
                    settings.SetSetting("MusicToDirectory", musicCopyDirectory);

                    if (!Directory.Exists(musicCopyDirectory))
                    {
                        Console.WriteLine("Папка отсутствовала. Создаем.");
                        Directory.CreateDirectory(musicCopyDirectory);
                    }
                    Console.WriteLine("Считываем файлы из папки на мобиле...");
                    var currentFiles =
                        Directory.EnumerateFiles(musicCopyDirectory, "*.*", SearchOption.AllDirectories)
                        .Where(e => !e.Contains("System Volume Information")).Select(Path.GetFileName).ToList();

                    if (currentFiles.Count > 0)
                    {
                        Console.WriteLine("Папка не пуста. Очистить? (0-нет;1-да)");
                        var clean = Convert.ToInt32(Console.ReadLine()) == 1;
                        if (clean)
                        {
                            foreach (var currentFile in currentFiles)
                            {
                                File.Delete(musicCopyDirectory + @"\" + currentFile);
                            }
                            Console.WriteLine("Папка очищена.");
                        }
                    }

                    Random rnd = new Random((int)DateTime.Now.Ticks);
                    Console.WriteLine("Считываем файлы из папки на компьютере...");

                    var files = Directory.GetFiles(musicDirectory, "*.*", SearchOption.AllDirectories).Where(e =>
                                                                                                                 {
                                                                                                                     var ext = Path.GetExtension(e) ?? "".ToLower();
                                                                                                                     var fileName = Path.GetFileName(e);
                                                                                                                     return !currentFiles.Contains(fileName) &&
                                                                                                                     (ext == ".mp3" || ext == ".flac" || ext == ".ogg" || ext == ".wav");
                                                                                                                 }).ToList();

                    Console.WriteLine("Проиндексировать названия песен?(0-нет;1-да)");
                    var rename = Convert.ToInt32(Console.ReadLine()) == 1;

                    var size = GetDirectorySize(musicCopyDirectory) / (1024 * 1024);
                    Console.WriteLine("Текущий размер папки:{0:F2} Мб", size);

                    var freeSpace = currentDrive.TotalFreeSpace / (1024 * 1024);
                    Console.WriteLine("Максимальный размер папки:{0:F2} Мб", freeSpace + size);

                    Console.WriteLine("Введите желаемый размер папки (Мб)");
                    Int64 maxSize = Convert.ToInt64(Console.ReadLine()) * (1024 * 1024);

                    var wantToHold = maxSize - size * (1024 * 1024);

                    size = GetDirectorySize(musicCopyDirectory);
                    var filesList = files.ToList();

                    FileCopyState state = new FileCopyState()
                    {
                        Count = 1,
                        CurrentSize = size,
                        Random = rnd,
                        Files = filesList,
                        MaxLength = maxSize,
                        Rename = rename,
                        CopyDirectory = musicCopyDirectory
                    };

                    var selectedFile = SelectFile(state);

                    while (state.CurrentSize < state.MaxLength && state.Files.Count > 0)
                    {
                        try
                        {
                            var selectTask = Task.Run(() =>
                            {
                                return SelectFile(state);
                            });

                            File.Copy(selectedFile.Item1, selectedFile.Item2, true);

                            Console.WriteLine("{1}. {0} скопирован.", Path.GetFileName(selectedFile.Item2), state.Count++);

                            Console.WriteLine("Прогресс:{0:P1}.", (1 - (state.MaxLength - state.CurrentSize) / (Double)wantToHold));
                            state.Count++;
                            state.Files.Remove(selectedFile.Item1);
                            selectedFile = selectTask.Result;
                            if (selectedFile==null) break;
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("{0} не скопирован", Path.GetFileName(selectedFile.Item2));
                            state.Files.Remove(selectedFile.Item1);
                        }
                    }
                    Console.WriteLine("Закончено");
                }
            }
            Console.ReadLine();
        }

        public class FileCopyState
        {
            public Random Random;
            public List<String> Files;
            public Boolean Rename;
            public Int64 CurrentSize;
            public Int64 MaxLength;
            public Int32 Count;
            public string CopyDirectory;
        }

        private static Tuple<String, String> SelectFile(FileCopyState state)
        {
            if (state.Files.Count == 0) return null;
            var index = state.Random.Next(0, state.Files.Count-1);
            var file = state.Files.ElementAt(index);

            var fileName = Path.GetFileName(file);
            if (fileName.Length >= 250)
            {
                state.Files.Remove(file);
                return SelectFile(state);
            }

            var fileSize = GetFileSize(file);
            if (state.Rename)
                fileName = state.Count + "-" + fileName;
            if (state.CurrentSize + fileSize > state.MaxLength)
            {
                state.Files.Remove(file);
                return SelectFile(state);
            }

            state.CurrentSize += fileSize;
            return new Tuple<string, string>(file, state.CopyDirectory + @"\" + fileName);
        }

        private static long GetDirectorySize(string folderPath)
        {
            DirectoryInfo di = new DirectoryInfo(folderPath);
            return di.EnumerateFiles("*.*", SearchOption.AllDirectories).Sum(fi => fi.Length);
        }

        private static long GetFileSize(string filePath)
        {
            FileInfo fi = new FileInfo(filePath);
            return fi.Length;
        }


    }
}
