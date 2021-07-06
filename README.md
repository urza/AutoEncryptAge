# AutoEncryptAge

Watch for files in input directory, encrypt them by [Age](https://github.com/FiloSottile/age) and move to output directory.

This is simple self contained CLI application that runs on Windows, Linux and other .net core targets. It has no dependencies other than .net core (.net 6) and [System.CommandLine](https://github.com/dotnet/command-line-api) for parsing args. The code is very simple, just one file, all the heavy lifting is done by Age.

```
Usage:
  AutoEncryptAge [options]

Options:
  --init-dir <init-dir>                By default this is current directory where you started the app. You can provide absolute path. [default: ]
  --input-dir <input-dir>              Directory to watch for files to encrypt. Default is "init-dir/2encrypt", but you can also specify absolute path. [default: ]
  --output-dir <output-dir>            Encrypted files are moved here. Default is initDir/encypted. When ecrypting from input dir, relative subdirectory structure is preserved. Filenames have .age extenion (original extension is kept) [default: ]
  --pubkeys <pubkeys>                  File with Age pubkeys, is used as -R arg with age. [default: ]
  --age-binary-path <age-binary-path>  Where to look for Age binary. Will be downloaded if necessery. [default: ]
  --delete-files-after-encryption      Default is true. [default: True]
  --delete-dirs-after-encryption       Default is true. [default: True]
  --version                            Show version information
  -?, -h, --help                       Show help and usage information
```

When you run it without arguments, it will create this folder sructure in the current directory, download Age if necessery and generate new key pair for encryption.
 
 ![image](https://user-images.githubusercontent.com/189804/124627605-fc4c3e00-de7f-11eb-8648-1169ba84f0d0.png)

 And will start watching the `input-dir` ("2encrypt" by default) folder. When you put any file in it, it will ecnrypt it with all pubkeys in `pubkeys` and move it to `output-dir` ("encrypted" by default) folder.
 
 If everything already exists, it will just start watching the input directory. You can keep it running (watching) or just run it once and it will encrypt whatever is waiting in `input-dir`.
 
