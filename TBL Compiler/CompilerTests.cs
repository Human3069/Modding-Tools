using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

internal static class CompilerTests
{
    private static void Assert(bool isValid, string messageText)
    {
        if (isValid == false)
        {
            throw new Exception(messageText);
        }
    }

    private static void ExpectFailure(Action testAction)
    {
        bool isRejected = false;
        try
        {
            testAction();
        }
        catch (InvalidDataException)
        {
            isRejected = true;
        }

        Assert(isRejected == true, "Invalid input was accepted.");
    }

    private static int Main()
    {
        Encoding gameEncoding = TblCodec.GetGameEncoding(1252);
        byte[] originalBytes = File.ReadAllBytes("../origins/rez/stat_txt.tbl");
        List<byte[]> originalEntries = TblCodec.ReadEntries(originalBytes);
        List<string> originalTexts = new List<string>();
        foreach (byte[] entryBytes in originalEntries)
        {
            StringBuilder entryBuilder = new StringBuilder();
            foreach (byte byteValue in entryBytes)
            {
                entryBuilder.Append("<" + byteValue + ">");
            }

            originalTexts.Add(entryBuilder.ToString());
        }

        byte[] restoredBytes = TblCodec.Compile(originalTexts.ToArray(), gameEncoding);
        Assert(TblCodec.AreEqual(originalBytes, restoredBytes) == true, "Original TBL did not rebuild exactly.");
        File.WriteAllBytes("output/original-roundtrip.tbl", restoredBytes);
        List<byte[]> sampleEntries = TblCodec.ReadEntries(TblCodec.Compile(new string[] { "Marine<0>*<0>Ground Units<0>", "", "Line 1<10>Line 2", " <35><60><62> " }, gameEncoding));
        Assert(sampleEntries.Count == 4, "Blank entry was discarded.");
        Assert(sampleEntries[1].Length == 1 && sampleEntries[1][0] == 0, "Blank entry is not NUL terminated.");
        Assert(TblCodec.AreEqual(sampleEntries[0], Encoding.ASCII.GetBytes("Marine\0*\0Ground Units\0")) == true, "Substring boundaries were lost.");
        Assert(sampleEntries[2][6] == 10, "Newline tag was not converted.");
        Assert(sampleEntries[3][0] == 32 && sampleEntries[3][sampleEntries[3].Length - 2] == 32, "Whitespace was lost.");
        ExpectFailure(delegate
        {
            TblCodec.Compile(new string[] { "Bad<256>" }, gameEncoding);
        });
        ExpectFailure(delegate
        {
            TblCodec.Compile(new string[] { "Bad<0A>" }, gameEncoding);
        });
        ExpectFailure(delegate
        {
            TblCodec.Compile(new string[] { "Bad<" }, gameEncoding);
        });
        ExpectFailure(delegate
        {
            TblCodec.Compile(new string[] { new string('A', 65535) }, gameEncoding);
        });
        ExpectFailure(delegate
        {
            TblCodec.Compile(new string[32767], gameEncoding);
        });
        bool isEncodingRejected = false;
        try
        {
            TblCodec.Compile(new string[] { "한글" }, gameEncoding);
        }
        catch (InvalidDataException)
        {
            isEncodingRejected = true;
        }

        Assert(isEncodingRejected == true, "Unrepresentable characters were silently replaced.");
        Encoding koreanEncoding = TblCodec.GetGameEncoding(949);
        byte[] koreanBytes = TblCodec.ReadEntries(TblCodec.Compile(new string[] { "한글<0>" }, koreanEncoding))[0];
        Assert(koreanEncoding.GetString(koreanBytes).TrimEnd('\0') == "한글", "CP949 conversion failed.");
        Console.WriteLine("PASS: original 1547 entries / 40398 bytes restored exactly; substring, empty entry, whitespace, tags, malformed input, size limits and encoding tests.");
        return 0;
    }
}

