using System.Buffers.Binary;

namespace Atmosphere.Services;

/// <summary>Checks ZIP metadata budgets before ZipArchive materializes its entry list.</summary>
internal static class ZipMetadataBudget
{
    private const uint EndSignature = 0x06054b50;
    private const uint Zip64LocatorSignature = 0x07064b50;
    private const uint Zip64EndSignature = 0x06064b50;
    private const uint EntrySignature = 0x02014b50;
    private const uint DigitalSignature = 0x05054b50;
    private const int EndSize = 22;
    private const int MaxCentralDirectoryBytes = 64 * 1024 * 1024;

    public static void Validate(Stream file, int maxEntries)
    {
        try
        {
            ValidateCore(file, maxEntries);
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidDataException("ZIP central directory is truncated.", ex);
        }
        finally
        {
            file.Position = 0;
        }
    }

    private static void ValidateCore(Stream file, int maxEntries)
    {
        // The last EOCD signature in the standard search window is the one ZipArchive uses.
        var tail = new byte[(int)Math.Min(file.Length, EndSize + ushort.MaxValue)];
        file.Position = file.Length - tail.Length;
        file.ReadExactly(tail);
        var endIndex = tail.Length - EndSize;
        while (endIndex >= 0 && U32(tail, endIndex) != EndSignature)
            endIndex--;
        if (endIndex < 0) throw InvalidDirectory();

        var end = tail.AsSpan(endIndex);
        if (end.Length != EndSize + U16(end, 20) ||
            U16(end, 4) != 0 || U16(end, 6) != 0 || U16(end, 8) != U16(end, 10))
            throw InvalidDirectory();

        var endOffset = file.Length - end.Length;
        ulong entries = U16(end, 10);
        ulong bytes = U32(end, 12);
        ulong offset = U32(end, 16);
        var centralDirectoryEnd = endOffset;
        Span<byte> locator = stackalloc byte[20];
        var usesZip64 = entries == ushort.MaxValue || offset == uint.MaxValue;
        var hasGap = offset <= (ulong)endOffset && bytes < (ulong)endOffset - offset;
        if (endOffset >= locator.Length && (usesZip64 || hasGap))
        {
            file.Position = endOffset - locator.Length;
            file.ReadExactly(locator);
            if (U32(locator, 0) == Zip64LocatorSignature)
            {
                var recordOffset = U64(locator, 8);
                var locatorOffset = endOffset - locator.Length;
                Span<byte> record = stackalloc byte[56];
                if (U32(locator, 4) != 0 || U32(locator, 16) != 1 || locatorOffset < record.Length ||
                    recordOffset > (ulong)(locatorOffset - record.Length))
                    throw InvalidDirectory();

                centralDirectoryEnd = (long)recordOffset;
                file.Position = centralDirectoryEnd;
                file.ReadExactly(record);
                if (U32(record, 0) != Zip64EndSignature ||
                    U64(record, 4) != (ulong)(locatorOffset - centralDirectoryEnd - 12) ||
                    U32(record, 16) != 0 || U32(record, 20) != 0 ||
                    U64(record, 24) != U64(record, 32))
                    throw InvalidDirectory();

                var zip64Entries = U64(record, 32);
                var zip64Bytes = U64(record, 40);
                var zip64Offset = U64(record, 48);
                // ZipArchive ignores optional ZIP64 records if no required legacy field is saturated.
                if (!usesZip64 && (zip64Entries != entries || zip64Offset != offset) ||
                    bytes != uint.MaxValue && bytes != zip64Bytes)
                    throw InvalidDirectory();
                entries = zip64Entries;
                bytes = zip64Bytes;
                offset = zip64Offset;
            }
        }

        if (entries > (ulong)maxEntries)
            throw new InvalidDataException($"Archive contains too many entries (maximum {maxEntries:N0}).");
        if (bytes > MaxCentralDirectoryBytes)
            throw new InvalidDataException("ZIP central directory exceeds the 64 MiB metadata limit.");
        if (offset > (ulong)centralDirectoryEnd || bytes != (ulong)centralDirectoryEnd - offset)
            throw InvalidDirectory();

        // Verify actual on-disk records too. No allocation depends on untrusted entry sizes or count.
        file.Position = (long)offset;
        Span<byte> header = stackalloc byte[46];
        for (ulong i = 0; i < entries; i++)
        {
            if (centralDirectoryEnd - file.Position < header.Length) throw InvalidDirectory();
            file.ReadExactly(header);
            if (U32(header, 0) != EntrySignature) throw InvalidDirectory();
            var variableLength = U16(header, 28) + U16(header, 30) + U16(header, 32);
            if (centralDirectoryEnd - file.Position < variableLength) throw InvalidDirectory();
            file.Position += variableLength;
        }

        // The optional directory digital-signature record is not a file entry.
        if (file.Position != centralDirectoryEnd)
        {
            var signature = header[..6];
            if (centralDirectoryEnd - file.Position < signature.Length) throw InvalidDirectory();
            file.ReadExactly(signature);
            if (U32(signature, 0) != DigitalSignature ||
                centralDirectoryEnd - file.Position != U16(signature, 4))
                throw InvalidDirectory();
        }
    }

    private static InvalidDataException InvalidDirectory() =>
        new("ZIP central directory is invalid or spans multiple disks.");

    private static ushort U16(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);

    private static uint U32(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);

    private static ulong U64(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt64LittleEndian(bytes[offset..]);
}
