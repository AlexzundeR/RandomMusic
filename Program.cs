using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
using SettingsModule;
using System.Threading.Tasks;
using System.Windows.Forms;

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
            SendKeys.SendWait(musicDirectory);

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
                SendKeys.SendWait(musicCopyDirectory);

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
                        SendKeys.SendWait("1");

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
                        var maxLength = e.Length > 249 ? 249 : e.Length;
                        var shortedPath = e.Substring(0, maxLength);
                        var ext = Path.GetExtension(shortedPath) ?? "".ToLower();
                        var fileName = Path.GetFileName(shortedPath);
                        return !currentFiles.Contains(fileName) && (ext == ".mp3" || ext == ".flac" || ext == ".ogg" || ext == ".wav");
                    }).OrderByDescending(e => new FileInfo(e).LastWriteTimeUtc).ToList();

                    Console.WriteLine("Проиндексировать названия песен?(0-нет;1-да)");
                    SendKeys.SendWait("1");
                    var rename = Convert.ToInt32(Console.ReadLine()) == 1;

                    var size = GetDirectorySize(musicCopyDirectory) / (1024 * 1024);
                    //Console.WriteLine("Текущий размер папки:{0:F2} Мб", size);

                    var freeSpace = currentDrive.TotalFreeSpace / (1024 * 1024);
                    Console.WriteLine("Доступно места:{0:F2} Мб", freeSpace + size);

                    Console.WriteLine("Введите желаемый размер папки (Мб)");
                    Int64 maxSize = Convert.ToInt64(Console.ReadLine()) * (1024 * 1024);

                    Console.WriteLine("Выбирать в первую очередь из ранее добавленных? (0-нет;1-да)");
                    SendKeys.SendWait("1");
                    var asNormal = Convert.ToInt32(Console.ReadLine())==1;


                    var wantToHold = maxSize - size * (1024 * 1024);

                    size = GetDirectorySize(musicCopyDirectory);
                    var filesList = files.ToList();

                    FileCopyState state = new FileCopyState()
                    {
                        Count = 0,
                        CurrentSize = size,
                        AsNormal = asNormal,
                        Random = rnd,
                        Files = filesList,
                        MaxLength = maxSize,
                        Rename = rename,
                        CopyDirectory = musicCopyDirectory
                    };

                    
                    Queue<Tuple<String,String,long,int>> filesQueue = new Queue<Tuple<string, string,long,int>>();
                    Boolean fillEnds = false;
                    Task.Run(() =>
                    {
                        while (state.Files.Count > 0)
                        {
                            var file = SelectFile(state);
                            if (file != null&&file.Item2!=null)
                            {
                                filesQueue.Enqueue(file);
                            }

                        }
                        fillEnds = true;
                    });
                    long summSize = 0;
                    while (fillEnds&&filesQueue.Count > 0||!fillEnds)
                    {
                        if (filesQueue.Count > 0)
                        {
                            var selectedFile = filesQueue.Dequeue();

                            try
                            {
                                File.Copy(selectedFile.Item1, selectedFile.Item2, true);

                                summSize += selectedFile.Item3;
                                Console.WriteLine("{1}. {0} скопирован.", Path.GetFileName(selectedFile.Item2),
                                    selectedFile.Item4);

                                Console.WriteLine("Прогресс:{0:P1}.",
                                    (1 - (state.MaxLength - summSize)/(Double)wantToHold));

                            }
                            catch (Exception)
                            {
                                Console.WriteLine("{0} не скопирован", Path.GetFileName(selectedFile.Item2));
                            }
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
            public bool AsNormal { get; set; }
        }

        private static Tuple<String, String,long,int> SelectFile(FileCopyState state)
        {
            if (state.Files.Count == 0) return null;
            
            int index;
            if (state.AsNormal)
            {
                double u1 = state.Random.NextDouble(); //these are uniform(0,1) random doubles
                double u2 = state.Random.NextDouble();
                double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2); //random normal(0,1)
                double randNormal = Math.Abs(randStdNormal*0.1)/5;
                if (randNormal > 1)
                    randNormal = 1;

                index = (int) (randNormal*(state.Files.Count - 1));
                Debug.WriteLine(index);
            }
            else
            {
                index = state.Random.Next(0, state.Files.Count - 1);
            }
            var file = state.Files.ElementAt(index);

            var fileName = Path.GetFileName(file);
            if (fileName.Length >= 250)
            {
                state.Files.Remove(file);
                return new Tuple<string, string, long,int>(file, null,0,0);
            }

            var fileSize = GetFileSize(file);
            if (state.Rename)
                fileName = (state.Count+1) + "-" + fileName;
            if (state.CurrentSize + fileSize > state.MaxLength)
            {
                state.Files.Remove(file);
                return new Tuple<string, string, long, int>(file, null, 0,0);
            }
            state.CurrentSize += fileSize;
            state.Count++;
            state.Files.Remove(file);
            return new Tuple<string, string, long, int>(file, state.CopyDirectory + @"\" + fileName, fileSize, state.Count);
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
