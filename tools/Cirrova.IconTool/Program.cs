using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection.PortableExecutable;

namespace Cirrova.IconTool;

internal static class Program
{
    // Used exclusively on a Windows Actions runner. No Python or Pillow required.
    private static readonly int[] IconSizes = [16, 24, 32, 48, 64, 128, 256];

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || args.Length > 2)
                throw new ArgumentException("Usage: dotnet run --project tools/Cirrova.IconTool -- create|verify [exe]");

            if (args[0] == "create" && args.Length == 1) Create();
            else if (args[0] == "verify") Verify(args.Length == 2
                ? args[1] : "out/cirrova-win-x64/Cirrova.exe");
            else throw new ArgumentException("Expected create or verify [exe].");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
    }

    private static void Create()
    {
        const string source = "icon.png";
        const string destination = "src/Cirrova/Assets/Cirrova.ico";
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        using var original = Image.FromFile(source);
        var images = new List<byte[]>();
        foreach (var size in IconSizes)
        {
            using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.CompositingMode = CompositingMode.SourceOver;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                var scale = Math.Min((double)size / original.Width, (double)size / original.Height);
                int width = Math.Max(1, (int)Math.Round(original.Width * scale));
                int height = Math.Max(1, (int)Math.Round(original.Height * scale));
                graphics.DrawImage(original, (size - width) / 2, (size - height) / 2, width, height);
            }

            using var data = new MemoryStream();
            bitmap.Save(data, ImageFormat.Png);
            images.Add(data.ToArray());
        }

        using var stream = File.Create(destination);
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0); // ICO reserved
        writer.Write((ushort)1); // 1 = Windows icon
        writer.Write((ushort)images.Count);
        var offset = 6 + images.Count * 16;
        for (var i = 0; i < images.Count; i++)
        {
            var size = IconSizes[i];
            writer.Write((byte)(size == 256 ? 0 : size));
            writer.Write((byte)(size == 256 ? 0 : size));
            writer.Write((byte)0); // color count
            writer.Write((byte)0); // reserved
            writer.Write((ushort)1); // color planes
            writer.Write((ushort)32); // bits per pixel
            writer.Write((uint)images[i].Length);
            writer.Write((uint)offset);
            offset += images[i].Length;
        }

        foreach (var png in images) writer.Write(png);
        Console.WriteLine($"Created {destination} with {images.Count} original-artwork resolutions.");
    }

    private static void Verify(string exe)
    {
        if (!File.Exists(exe)) throw new FileNotFoundException("Missing Windows executable", exe);
        using var stream = File.OpenRead(exe);
        using var reader = new PEReader(stream);
        var header = reader.PEHeaders.PEHeader ??
            throw new InvalidDataException("No valid PE header found.");
        var directory = header.ResourceTableDirectory;
        if (directory.RelativeVirtualAddress == 0)
            throw new InvalidDataException("Executable has no native resource directory.");

        var sections = reader.PEHeaders.SectionHeaders;
        var section = sections.FirstOrDefault(x =>
            directory.RelativeVirtualAddress >= x.VirtualAddress &&
            directory.RelativeVirtualAddress < x.VirtualAddress + Math.Max(x.VirtualSize, x.SizeOfRawData));
        if (section.SizeOfRawData <= 0)
            throw new InvalidDataException("PE resource directory is not in a file section.");

        long resourceOffset = section.PointerToRawData + directory.RelativeVirtualAddress - section.VirtualAddress;
        stream.Position = resourceOffset + 12; // IMAGE_RESOURCE_DIRECTORY header
        using var binary = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        ushort named = binary.ReadUInt16();
        ushort ids = binary.ReadUInt16();
        var types = new HashSet<uint>();
        for (var i = 0; i < named + ids; i++)
        {
            uint id = binary.ReadUInt32();
            _ = binary.ReadUInt32(); // child directory location
            if ((id & 0x80000000) == 0) types.Add(id);
        }

        // Win32 RT_ICON=3 and RT_GROUP_ICON=14: both are necessary for Explorer icons.
        if (!types.Contains(3) || !types.Contains(14))
            throw new InvalidDataException($"Missing Win32 icon resources. Types: {string.Join(", ", types)}");
        Console.WriteLine($"Verified embedded Windows icon resources in {exe}.");
    }
}
