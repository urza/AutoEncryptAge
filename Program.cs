using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace AutoEncryptAge
{
    class Program
    {
        static DirectoryInfo input_dir;
        static DirectoryInfo output_dir;
        static FileInfo age_pub_keys;
        static DirectoryInfo age_binary_path;
        static DirectoryInfo init_dir;
        static bool delete_files_after_enc;
        static bool delete_dirs_after_enc;

        /// <summary>
        /// Watch for files created in input directory, encrypt them by age (https://github.com/FiloSottile/age) and move to output directory.
        /// </summary>
        /// <param name="initDir">By default this is current directory where you started the app. You can instead provide absolute path to somewhere else. In such case that will be the default starting point for other directories.</param>
        /// <param name="inputDir">Directory to watch for files you want to encrypt. Default is "init-dir/2encrypt", but you can also specify absolute path.</param>
        /// <param name="outputDir">Encrypted files are moved here. Default is initDir/encrypted. When encrypting from input dir, relative subdirectory structure is preserved. Encrypted files have additional .age extenion</param>
        /// <param name="pubkeys">File with age pubkeys, is used as -R arg with age, may contain multiple public keys (each on new line).</param>
        /// <param name="ageBinaryPath">Where to look for age binary. Will be downloaded from https://github.com/FiloSottile/age/releases if necessary.</param>
        /// <param name="deleteFilesAfterEncryption">Default is true.</param>
        /// <param name="deleteDirsAfterEncryption">Default is true. Only delete if empty.</param>
        /// <returns></returns>
        static async Task Main(DirectoryInfo initDir = null, DirectoryInfo inputDir = null, 
                                DirectoryInfo outputDir = null, FileInfo pubkeys = null, DirectoryInfo ageBinaryPath = null,
                                bool deleteFilesAfterEncryption = true, bool deleteDirsAfterEncryption = true)
        {
            init_dir = initDir ?? new DirectoryInfo(Directory.GetCurrentDirectory());
            input_dir = inputDir ?? new DirectoryInfo(Path.Combine(init_dir.FullName, "2encrypt"));
            output_dir = outputDir ?? new DirectoryInfo(Path.Combine(init_dir.FullName, "encrypted"));
            age_pub_keys = pubkeys ?? new FileInfo(Path.Combine(init_dir.FullName, "age_pubkeys.txt"));
            age_binary_path = ageBinaryPath ?? new DirectoryInfo(Path.Combine(init_dir.FullName, "age_bin"));
            delete_files_after_enc = deleteFilesAfterEncryption;
            delete_dirs_after_enc = deleteDirsAfterEncryption;

            await Init();

            await WatchDir(input_dir);
        }

        private static async Task WatchDir(DirectoryInfo dir)
        {
            // tried to use FileWatcher, but it has errors when more files is copied at once, and ms recommends polling..
            // https://stackoverflow.com/questions/15519089/avoid-error-too-many-changes-at-once-in-directory

            Console.WriteLine();
            Console.WriteLine("watching directory " + input_dir);

            while (true)
            {
                var files = input_dir.EnumerateFiles("*", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    var encrypted = EncryptFile(file);

                    /// Delete orig file ONLY IF:
                    /// 1. delete_files_after_enc == true
                    /// 2. encrypted file was created
                    /// 3. encrypted file has some realistic size = at least 1/2 of input file size
                    if (delete_files_after_enc && encrypted.Exists && encrypted.Length > ((float)file.Length / 2f))
                        File.Delete(file.FullName);
                    else
                        PrintError($"File not encrypted {file.FullName}");
                    
                }

                if (delete_dirs_after_enc)
                    DeleteEmptySubdirectories(dir.FullName);

                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }

        /// <summary>
        /// if necessary create directories, download age binary, generate new key pair 
        /// </summary>
        private static async Task Init()
        {
            if (!input_dir.Exists)
            {
                Console.WriteLine($"Creating input directory at {input_dir}");
                input_dir.Create();
            }

            if (!output_dir.Exists)
            {
                Console.WriteLine($"Creating output directory at {output_dir}");
                output_dir.Create();
            }

            if (!age_binary_path.Exists)
            {
                Console.WriteLine($"Creating age_binary_path directory at {age_binary_path}");
                age_binary_path.Create();
            }

            /// download from github and unzip to age_binary_path
            if (!File.Exists(Path.Combine(age_binary_path.FullName, "age.exe")))
            {
                Console.WriteLine();
                Console.WriteLine($"Downloading binary release of age from https://github.com/FiloSottile/age/releases");
                string releaseUrl = "https://github.com/FiloSottile/age/releases/download/v1.0.0-rc.3/age-v1.0.0-rc.3-windows-amd64.zip";
                
                age_binary_path.Delete(recursive:true);
                age_binary_path.Create();

                var downloadzip = Path.Combine(age_binary_path.FullName, "download.zip");
                using var c = new WebClient();
                c.DownloadFile(releaseUrl, downloadzip);
                ZipFile.ExtractToDirectory(downloadzip, age_binary_path.FullName);

                //zip contains /age, lets copy .exe files it one level up to age_bin and clean
                DirectoryCopy(Path.Combine(age_binary_path.FullName, "age"), age_binary_path.FullName, false);
                Directory.Delete(Path.Combine(age_binary_path.FullName, "age"), true);
                File.Delete(downloadzip);
            }

            /// pubkey not detected, generate ney key pair
            if (!age_pub_keys.Exists)
            {
                Console.WriteLine("generating new age key pair:");
                var privkey_path = Path.Combine(init_dir.FullName, "age_private.key");
                Process.Start(Path.Combine(age_binary_path.FullName, "age-keygen.exe"), $"-o {privkey_path}").WaitForExit();
                await Task.Delay(500);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("**************************************************");
                Console.WriteLine($"  new PRIVATE KEY generated in: {Environment.NewLine}  {privkey_path} {Environment.NewLine}  MOVE IT TO SECURE LOCATION ");
                Console.WriteLine("**************************************************");
                Console.ResetColor();
                var pubkey = File.ReadAllLines(privkey_path).Where(line => line.StartsWith("# public key:")).First().Split(":")[1].Trim();
                File.WriteAllText(age_pub_keys.FullName, pubkey);
                Console.WriteLine($"Public key is in {age_pub_keys.FullName}, this will be used for encryption, no need to have private key around if you don't need to decrypt here.");
            }
        }

        /// <summary>
        /// Call age binary to encrypt file to destination
        /// </summary>
        private static FileInfo EncryptFile(FileInfo file)
        {
            var out_file = new FileInfo(file.FullName.Replace(input_dir.FullName, output_dir.FullName) + ".age");

            var dir = out_file.Directory;
            if (!dir.Exists) dir.Create();

            Console.WriteLine("Encrypting " + file);
            Process.Start(fileName: Path.Combine(age_binary_path.FullName, "age.exe"),
                          arguments: $"-e -o \"{out_file.FullName}\" -R {age_pub_keys} \"{file.FullName}\"")
            .WaitForExit(); //need to wait for age to finish, otherwise the file gets deleted too soon

            return out_file;
        }

        private static void PrintError(string error)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(error);
            Console.ResetColor();
        }

        /// <summary>
        /// Recursively delete all empty subdirectories
        /// </summary>
        public static void DeleteEmptySubdirectories(string parentDirectory)
        {
            foreach (string directory in Directory.GetDirectories(parentDirectory))
            {
                DeleteEmptySubdirectories(directory);
                if (!Directory.EnumerateFileSystemEntries(directory).Any()) System.IO.Directory.Delete(directory, false);
            }
        }

        /// <summary>
        /// Copy content of directory
        /// </summary>
        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();

            // If the destination directory doesn't exist, create it.       
            Directory.CreateDirectory(destDirName);

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string tempPath = Path.Combine(destDirName, file.Name);
                file.CopyTo(tempPath, false);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string tempPath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, tempPath, copySubDirs);
                }
            }
        }
    }
}
