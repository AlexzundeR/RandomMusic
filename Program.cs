using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.IO;
using SettingsModule;

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
                settings.SetSetting("MusicFromDirectory", currentInput);

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
                    settings.SetSetting("MusicToDirectory", currentInput);

                    if (!Directory.Exists(musicCopyDirectory))
                    {
                        Console.WriteLine("Папка отсутствовала. Создаем.");
                        Directory.CreateDirectory(musicCopyDirectory);
                    }
                    Console.WriteLine("Считываем файлы из папки на мобиле...");
                    var currentFiles =
                        Directory.GetFiles(musicCopyDirectory, "*.*", SearchOption.AllDirectories).Select(
                            Path.GetFileName).ToList();

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
                    var maxSize = Convert.ToDouble(Console.ReadLine()) * (1024 * 1024);

                    var wantToHold = maxSize - size * (1024 * 1024);

                    size = GetDirectorySize(musicCopyDirectory);
                    Int32 counter = 1;
                    while (size < maxSize)
                    {
                        var index = rnd.Next(1, files.Count()) - 1;
                        var file = files[index];
                        try
                        {
                            var fileName = Path.GetFileName(file);
                            if (rename)
                                fileName = counter + "-" + fileName;
                            var fileSize = GetFileSize(file);
                            if (size + fileSize < maxSize)
                            {
                                File.Copy(file, musicCopyDirectory + @"\" + fileName, true);
                                Console.WriteLine("{1}. {0} скопирован.", fileName, counter);
                            }
                            else
                            {
                                var lastFile = files.FirstOrDefault(e => size + GetFileSize(e) < maxSize);
                                if (lastFile != null)
                                {
                                    var lastFileName = Path.GetFileName(lastFile);
                                    File.Copy(file, musicCopyDirectory + @"\" + lastFileName, true);
                                    Console.WriteLine("{1}. {0} скопирован.", lastFileName, counter);
                                }
                                else
                                {
                                    break;
                                }
                            }
                            size += fileSize;
                            Console.WriteLine("Прогресс:{0:P0}.", (1 - (maxSize - size) / wantToHold));
                            counter++;
                            files.Remove(file);
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("{0} не скопирован", file);
                        }
                        finally
                        {
                            files.RemoveAt(index);
                        }
                    }
                    Console.WriteLine("Закончено");
                }
            }
            Console.ReadLine();
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
