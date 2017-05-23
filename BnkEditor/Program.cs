using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BnkEditor
{
    class Program
    {
        public static string filePath;
        public static string wemPath;
        public static uint indexNumber;
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                try
                {
                    Console.WriteLine("Enter your .bnk file path (i.e. C:\\Users\\Spodes\\Desktop\\Lobby.bnk)");
                    filePath = Console.ReadLine();
                    Console.WriteLine("Enter your .wem file path (i.e. C:\\Users\\Spodes\\Desktop\\TryMe.wem)");
                    wemPath = Console.ReadLine();
                    Console.WriteLine("Enter the index of the file you want to have changed (i.e. 123) Make sure the index exists");
                    indexNumber = UInt32.Parse(Console.ReadLine());
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.ReadKey();
                    Environment.Exit(1);
                }

                if(!File.Exists(filePath))
                {
                    Console.WriteLine(".bnk file not found in path");
                    Console.ReadKey();
                    Environment.Exit(1);
                }

                if (!File.Exists(wemPath))
                {
                    Console.WriteLine(".wem file not found in path");
                    Console.ReadKey();
                    Environment.Exit(1);
                }

                if (indexNumber < 1)
                {
                    Console.WriteLine("Invalid index number");
                    Console.ReadKey();
                    Environment.Exit(1);
                }


                //Read data
                byte[] data;
                using (Stream fs = File.OpenRead(filePath))
                {
                    data = new byte[4];
                    fs.Read(data, 0, data.Length);
                    if (data.SequenceEqual(new byte[] { 0x42, 0x4B, 0x48, 0x44 }))
                    {
                        Console.WriteLine("ignoring BKHD Section");
                    }
                    else
                    {
                        Console.WriteLine("Invalid .bnk file");
                        Console.ReadKey();
                        Environment.Exit(1);
                    }

                    while (true)
                    {
                        data = new byte[4];
                        fs.Read(data, 0, data.Length);
                        

                        if (data.SequenceEqual(new byte[] { 0x44, 0x49, 0x44, 0x58 }))
                        {
                            Console.WriteLine("DIDX section reading...");
                            DIDXSection _DIDXSection = ReadDIDXSection(fs.Position);
                            Console.WriteLine("Replacing file...");
                            ReplaceWEMFile(_DIDXSection, "");
                            Environment.Exit(1);
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("Invalid Syntax. Syntax => bnk path\\yourfile.bnk wem file\\yourfile.wem IndexNumber");
                Console.ReadKey();
            }
        }

        public static void ReplaceWEMFile(DIDXSection didx, string newFilePath)
        {
            WEMFile originWEM = null;
            //Get correct wem file
            foreach (WEMFile wem in didx.DIDXFiles)
            {
                if (wem.Num == indexNumber)
                {
                    originWEM = wem;
                    break;
                }
            }

            if (originWEM == null)
            {
                Console.WriteLine("Invalid index value");
                Console.ReadKey();
                Environment.Exit(1);
            }

            Console.WriteLine("Replacing " + "[" + originWEM.Num + "]" + " Position:" + originWEM.Position + " Size:" + originWEM.Size + " ID:" + originWEM.ID);

            //add 12 because 4 bytes => didxSize 8 bytes DataObject and Size
            long replacementOffset = didx.DIDXSectionPosition + 12 + didx.DIDXSectionSize + originWEM.Position;
            Console.WriteLine("Wem replacement offset:" + replacementOffset);
            
            byte[] data;
            using (Stream writeStream = File.OpenWrite(Path.GetFileNameWithoutExtension(filePath)))
            {
                using (Stream readStream = File.OpenRead(filePath))
                {
                    Console.WriteLine("Starting replacement...");
                    //Write BKHD and start of DIDX Section
                    while (readStream.Position != didx.DIDXSectionPosition + 4)
                    {
                        data = new byte[4];
                        readStream.Read(data, 0, data.Length);
                        writeStream.Write(data, 0, data.Length);
                    }

                    Console.WriteLine("Skipping...");
                    //Skip all unchanged files
                    uint fileCount = 1;
                    while(fileCount != originWEM.Num)
                    {
                        data = new byte[12];
                        readStream.Read(data, 0, data.Length);
                        writeStream.Write(data, 0, data.Length);
                        fileCount++;
                    }

                    Console.WriteLine("Creating entries...");
                    //Write our file size
                    FileInfo newWemInfo = new FileInfo(wemPath);
                    bool fileIsSmaller = false;
                    long fileDifference = 0;
                    if (newWemInfo.Length < originWEM.Size)
                    {
                        fileIsSmaller = true;
                        fileDifference = originWEM.Size - newWemInfo.Length;
                    }
                    else
                    {
                        fileDifference = newWemInfo.Length - originWEM.Size;
                    }

                    //Copy ID
                    data = new byte[4];
                    readStream.Read(data, 0, data.Length);
                    writeStream.Write(data, 0, data.Length);

                    //Copy Position
                    data = new byte[4];
                    readStream.Read(data, 0, data.Length);
                    writeStream.Write(data, 0, data.Length);

                    //Write Size
                    data = BitConverter.GetBytes((uint)newWemInfo.Length);
                    writeStream.Write(data, 0, data.Length);

                    //read stream to stay in sync
                    readStream.Read(data, 0, data.Length);

                    //Loop through DIDX files and arrange according files

                    Console.WriteLine("Looping through DIDX");
                    long EndOfDIDXSection = readStream.Position + didx.DIDXSectionSize;
                    while (readStream.Position < EndOfDIDXSection)
                    {
                        //Copy ID
                        data = new byte[4];
                        readStream.Read(data, 0, data.Length);
                        writeStream.Write(data, 0, data.Length);

                        //Write Position
                        data = new byte[4];
                        readStream.Read(data, 0, data.Length);
                        uint oldPosition = BitConverter.ToUInt32(data,0);
                        
                        //Calculate new position
                        if (fileIsSmaller)
                        {
                            data = BitConverter.GetBytes(oldPosition - (uint)fileDifference);
                        }
                        else
                        {
                            data = BitConverter.GetBytes(oldPosition + (uint)fileDifference);
                        }

                        writeStream.Write(data, 0, data.Length);
                        

                        //Copy Size
                        data = new byte[4];
                        readStream.Read(data, 0, data.Length);
                        writeStream.Write(data, 0, data.Length);
                    }

                    Console.WriteLine("Writing DATA header...");
                    //Write DATA header
                    data = new byte[4];
                    readStream.Read(data, 0, data.Length);
                    writeStream.Write(data, 0, data.Length);

                    //Change DATA Size
                    data = new byte[4];
                    readStream.Read(data, 0, data.Length);
                    uint oldSize = BitConverter.ToUInt32(data, 0);

                    if (fileIsSmaller)
                    {
                        data = BitConverter.GetBytes(oldSize - (uint)fileDifference);
                    }
                    else
                    {
                        data = BitConverter.GetBytes(oldSize + (uint)fileDifference);
                    }

                    writeStream.Write(data, 0, data.Length);

                    Console.WriteLine("Writing content...");
                    //Write content
                    while (readStream.Position < replacementOffset)
                    {
                        data = new byte[1];
                        readStream.Read(data, 0, data.Length);
                        writeStream.Write(data, 0, data.Length);
                    }

                    Console.WriteLine("Replacing .wem ...");
                    //Write our wem
                    using (Stream readWEMStream = File.OpenRead(wemPath))
                    {
                        while(readWEMStream.Position != newWemInfo.Length)
                        {
                            data = new byte[1];
                            readWEMStream.Read(data, 0, data.Length);
                            writeStream.Write(data, 0, data.Length);
                        }
                        readWEMStream.Close();
                    }

                    //Skip wem in readstream
                    readStream.Position = readStream.Position + originWEM.Size;

                    Console.WriteLine("Finnishing...");
                    //Write leftOver Content
                    while (readStream.Position < readStream.Length)
                    {
                        data = new byte[1000];
                        int amount = readStream.Read(data, 0, data.Length);
                        writeStream.Write(data, 0, amount);
                    }
                    readStream.Close();
                }
                writeStream.Close();
                Console.WriteLine("Done.");
                Console.ReadKey();
            }
        }

        public static DIDXSection ReadDIDXSection(long position)
        {
            DIDXSection retVar = new DIDXSection();
            
            using (Stream fs = File.OpenRead(filePath))
            {
                retVar.DIDXSectionPosition = position;
                Console.WriteLine("DIDXSection Position:" + retVar.DIDXSectionPosition);
                fs.Seek(position, SeekOrigin.Begin);

                byte[] data = new byte[4];
                fs.Read(data, 0, data.Length);

                uint DIDXSectionSize = BitConverter.ToUInt32(data, 0);
                retVar.DIDXSectionSize = DIDXSectionSize;
                Console.WriteLine("DIDXSectionSize:" + DIDXSectionSize);

                long EndOfDIDXSection = fs.Position + DIDXSectionSize;
                Console.WriteLine("End of DIDXSection:" + EndOfDIDXSection);

                List<WEMFile> WEMFileList = new List<WEMFile>();
                while (fs.Position != EndOfDIDXSection)
                {
                    data = new byte[4];
                    fs.Read(data, 0, data.Length);
                    uint ID = BitConverter.ToUInt32(data, 0);

                    data = new byte[4];
                    fs.Read(data, 0, data.Length);
                    uint Position = BitConverter.ToUInt32(data, 0);

                    data = new byte[4];
                    fs.Read(data, 0, data.Length);
                    uint Size = BitConverter.ToUInt32(data, 0);

                    retVar.DIDXFileCount++;

                    WEMFile tempWEM = new WEMFile();

                    tempWEM.Num = retVar.DIDXFileCount;
                    tempWEM.ID = ID;
                    tempWEM.Position = Position;
                    tempWEM.Size = Size;

                    Console.WriteLine("[" + tempWEM.Num + "]" + " Position:" + Position + " Size:" + Size + " ID:"+tempWEM.ID );

                    WEMFileList.Add(tempWEM);
                }
                retVar.DIDXFiles = WEMFileList.ToArray();
                fs.Close();
            }
            Console.WriteLine(retVar.DIDXFileCount + " files parsed.");
            return retVar;
        }
    }
}

public class DIDXSection
{
    public long DIDXSectionPosition = 0;
    public uint DIDXSectionSize = 0;
    public uint DIDXFileCount = 0;
    public WEMFile[] DIDXFiles = { }; 
}

public class WEMFile
{
    public uint Num = 0;
    public uint Size = 0;
    public uint Position = 0;
    public uint ID = 0;
}
